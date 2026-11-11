import Foundation

/// Source des contenus choisie par l'utilisateur.
enum Source: Codable, Equatable, Hashable, Sendable {
    case xtream(XtreamAccount)
    case playlist(url: String, epg: String?)
    case demo

    var displayName: String {
        switch self {
        case .xtream(let a): return URLComponents(string: a.server)?.host ?? a.server
        case .playlist(let url, _): return URLComponents(string: url)?.host ?? "Playlist"
        case .demo: return T("Démonstration", "Demo")
        }
    }

    /// Dossier de cache propre à cette source (jamais d'identifiants en clair dans le nom).
    var cacheKey: String {
        switch self {
        case .xtream(let a): return "x-" + Hash.fnv1a64Hex(a.server + "\u{1F}" + a.username)
        case .playlist(let url, _): return "p-" + Hash.fnv1a64Hex(url)
        case .demo: return "demo"
        }
    }

    var xtream: XtreamAccount? { if case .xtream(let a) = self { return a } else { return nil } }
}

/// Chaînes en direct d'une source.
struct LiveCatalog: Sendable {
    var categories: [Category] = []
    var channels: [LiveChannel] = []

    /// Chaînes par catégorie, dans l'ordre du fournisseur.
    func channels(in categoryId: String) -> [LiveChannel] { channels.filter { $0.categoryId == categoryId } }

    func category(_ id: String) -> Category? { categories.first { $0.id == id } }
}

/// Ce que le lecteur doit lire.
struct PlayItem: Identifiable, Sendable {
    var id: String { ref.id }
    let ref: ContentRef
    let url: URL
    let title: String
    let subtitle: String?
    let artwork: String?
    let isLive: Bool
    let userAgent: String?
    /// Position de départ (reprise), en secondes.
    var startAt: Double?

    /// Le même contenu à une autre adresse (direct dans l'autre format).
    func with(url: URL) -> PlayItem {
        PlayItem(ref: ref, url: url, title: title, subtitle: subtitle, artwork: artwork, isLive: isLive,
                 userAgent: userAgent, startAt: startAt)
    }
}

enum PlaybackResolver {
    /// Adresse de lecture d'un contenu enregistré, reconstruite avec le compte courant.
    static func url(for ref: ContentRef, source: Source, liveExtension: String = "ts") -> URL? {
        if let u = ref.url { return URL(string: u) }
        guard let account = source.xtream, let num = ref.numericId else { return nil }
        switch ref.kind {
        case .live: return account.liveURL(num, ext: liveExtension)
        case .movie: return account.movieURL(num, ext: ref.ext ?? "mp4")
        case .episode: return account.episodeURL(num, ext: ref.ext ?? "mkv")
        case .series, .other: return nil
        }
    }

    /// Format d'un direct Xtream (…/live/compte/code/123.ts ou .m3u8) : « ts », « m3u8 », ou nil
    /// si l'adresse n'en est pas un.
    static func liveExtension(of url: URL) -> String? {
        let parts = url.path.split(separator: "/")
        guard parts.count >= 4, parts[parts.count - 4] == "live", let file = parts.last,
              let dot = file.lastIndex(of: "."), dot != file.startIndex, file[..<dot].allSatisfy(\.isASCIIDigit) else { return nil }
        let ext = file[file.index(after: dot)...].lowercased()
        return ext == "ts" || ext == "m3u8" ? ext : nil
    }

    /**
     La même chaîne dans l'autre format (MPEG-TS ↔ HLS). Certains serveurs n'en servent qu'un :
     quand une chaîne ne démarre pas, le lecteur essaie l'autre avant d'afficher une erreur.
     */
    static func alternateLive(_ url: URL) -> URL? {
        guard let ext = liveExtension(of: url), var parts = URLComponents(url: url, resolvingAgainstBaseURL: false) else { return nil }
        let path = parts.percentEncodedPath
        guard path.count > ext.count else { return nil }
        parts.percentEncodedPath = String(path.dropLast(ext.count)) + (ext == "ts" ? "m3u8" : "ts")
        return parts.url
    }
}

/// Petit cache disque (réponses du serveur, playlists) par source.
struct DiskCache: Sendable {
    let directory: URL

    init(namespace: String, base: URL? = nil) {
        let root = base ?? (try? FileManager.default.url(for: .cachesDirectory, in: .userDomainMask, appropriateFor: nil, create: true))
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        directory = root.appendingPathComponent("FluxTV", isDirectory: true).appendingPathComponent(namespace, isDirectory: true)
    }

    private func file(_ name: String) -> URL {
        directory.appendingPathComponent(name.replacingOccurrences(of: "/", with: "_"))
    }

    /// Données encore fraîches (plus récentes que maxAge secondes).
    func read(_ name: String, maxAge: TimeInterval) -> Data? {
        let f = file(name)
        guard let attrs = try? FileManager.default.attributesOfItem(atPath: f.path),
              let date = attrs[.modificationDate] as? Date, Date().timeIntervalSince(date) < maxAge else { return nil }
        return try? Data(contentsOf: f)
    }

    /// Données, même périmées (secours hors ligne).
    func readAny(_ name: String) -> Data? { try? Data(contentsOf: file(name)) }

    func exists(_ name: String) -> Bool { FileManager.default.fileExists(atPath: file(name).path) }

    /// Change la date d'une entrée (elle sera redemandée plus tôt ou plus tard).
    func touch(_ name: String, _ date: Date) {
        try? FileManager.default.setAttributes([.modificationDate: date], ofItemAtPath: file(name).path)
    }

    func write(_ name: String, _ data: Data) {
        try? FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try? data.write(to: file(name), options: [.atomic])
    }

    func clear() { try? FileManager.default.removeItem(at: directory) }
}

/**
 Accès aux contenus d'une source, avec cache : la liste des chaînes (souvent plusieurs
 dizaines de milliers d'entrées) n'est rechargée qu'au plus toutes les 12 h, ou à la demande.
 */
actor Repository {
    let source: Source
    private let http: HTTPClient
    private let cache: DiskCache
    private var xtream: XtreamClient?

    static let catalogMaxAge: TimeInterval = 12 * 3600
    static let epgMaxAge: TimeInterval = 20 * 60

    private(set) var account: AccountInfo?
    private var playlist: M3UCatalog?
    private var liveCache: LiveCatalog?
    private var vodCategoriesCache: [Category]?
    private var seriesCategoriesCache: [Category]?
    private var moviesByCategory: [String: [Movie]] = [:]
    private var seriesByCategory: [String: [Series]] = [:]
    private var allMoviesCache: [Movie]?
    private var allSeriesCache: [Series]?
    private var epg: [String: (at: Date, list: [Programme])] = [:]
    private var epgInFlight: [String: Task<[Programme], Never>] = [:]
    private var xmltv: [String: [Programme]]?
    private var xmltvTask: Task<Void, Never>?

    init(source: Source, http: HTTPClient = .shared, cacheBase: URL? = nil) {
        self.source = source
        self.http = http
        self.cache = DiskCache(namespace: source.cacheKey, base: cacheBase)
        if let a = source.xtream { xtream = XtreamClient(account: a, http: http) }
    }

    /// Format du direct (« ts » ou « m3u8 ») d'après le compte.
    var liveExtension: String { account?.liveExtension ?? "ts" }

    func clearCache() {
        cache.clear()
        playlist = nil; liveCache = nil; vodCategoriesCache = nil; seriesCategoriesCache = nil
        moviesByCategory = [:]; seriesByCategory = [:]; allMoviesCache = nil; allSeriesCache = nil
        epg = [:]; xmltv = nil
    }

    // ───────────────────────────── Compte ─────────────────────────────

    /// Vérifie le compte Xtream (échéance, connexions…). Sans effet pour une playlist.
    @discardableResult
    func checkAccount() async throws -> AccountInfo? {
        guard let xtream else { return nil }
        let info = try await xtream.authenticate()
        account = info
        if !info.isActive { throw FluxError.accountInactive(info.status ?? "inactif") }
        return info
    }

    // ───────────────────────────── Playlist ─────────────────────────────

    private func loadPlaylist(force: Bool) async throws -> M3UCatalog {
        if case .demo = source { return Demo.catalog }
        if !force, let p = playlist { return p }
        guard case .playlist(let urlString, let epgOverride) = source else { return M3UCatalog() }
        var data = force ? nil : cache.read("playlist.m3u", maxAge: Repository.catalogMaxAge)
        var read: M3UCatalog?
        if data == nil {
            guard let url = URL(string: urlString) else { throw FluxError.badAddress }
            do {
                let fresh = try await http.data(url, timeout: 120)
                let parsed = Repository.catalog(from: fresh)
                // Vérifiée avant d'être gardée : une page d'erreur du serveur ne remplace jamais
                // une playlist qui marche (elle restait 12 h en cache)
                if !parsed.isEmpty {
                    cache.write("playlist.m3u", fresh)
                    read = parsed
                } else if !cache.exists("playlist.m3u") {
                    throw FluxError.emptyPlaylist
                }
            } catch {
                // Hors ligne : la dernière playlist connue fait l'affaire
                guard cache.exists("playlist.m3u") else { throw error }
            }
            if read == nil { data = cache.readAny("playlist.m3u") }
        }
        var cat = read ?? Repository.catalog(from: data ?? Data())
        if let epgOverride, !epgOverride.isEmpty { cat.epgURL = epgOverride }
        if cat.isEmpty { throw FluxError.emptyPlaylist }
        playlist = cat
        return cat
    }

    /// Playlist telle que reçue (gzip compris) → chaînes, films et séries.
    private static func catalog(from data: Data) -> M3UCatalog {
        let body = Gzip.isGzip(data) ? (Gzip.decompress(data) ?? Data()) : data
        return M3U.catalog(from: String(decoding: body, as: UTF8.self))
    }

    // ───────────────────────────── Direct ─────────────────────────────

    func live(force: Bool = false) async throws -> LiveCatalog {
        if !force, let l = liveCache { return l }
        switch source {
        case .demo, .playlist:
            let p = try await loadPlaylist(force: force)
            let l = LiveCatalog(categories: p.liveCategories, channels: p.live)
            liveCache = l
            return l
        case .xtream:
            let catsData = try await cached("live_categories.json", force: force) { try await self.raw("get_live_categories") }
            let streamsData = try await cached("live_streams.json", force: force) { try await self.raw("get_live_streams", timeout: 90) }
            var cats = XtreamParse.categories(catsData)
            let channels = XtreamParse.liveStreams(streamsData)
            // Chaînes sans catégorie connue : regroupées dans « Autres »
            let known = Set(cats.map(\.id))
            if channels.contains(where: { !known.contains($0.categoryId) }) {
                cats.append(Category(id: "", rawName: T("Autres", "Other")))
            }
            let l = LiveCatalog(categories: cats, channels: channels)
            liveCache = l
            return l
        }
    }

    // ───────────────────────────── Films ─────────────────────────────

    func vodCategories(force: Bool = false) async throws -> [Category] {
        if !force, let c = vodCategoriesCache { return c }
        let cats: [Category]
        switch source {
        case .demo, .playlist: cats = try await loadPlaylist(force: force).vodCategories
        case .xtream:
            cats = XtreamParse.categories(try await cached("vod_categories.json", force: force) { try await self.raw("get_vod_categories") })
        }
        vodCategoriesCache = cats
        return cats
    }

    func movies(category: String, force: Bool = false) async throws -> [Movie] {
        if !force, let m = moviesByCategory[category] { return m }
        let list: [Movie]
        switch source {
        case .demo, .playlist: list = try await loadPlaylist(force: false).movies.filter { $0.categoryId == category }
        case .xtream:
            list = XtreamParse.vodStreams(try await cached("vod_\(category).json", force: force) {
                try await self.raw("get_vod_streams", [("category_id", category)], timeout: 90)
            })
        }
        moviesByCategory[category] = list
        return list
    }

    /// Tous les films (recherche, nouveautés). Une seule requête, mise en cache.
    func allMovies(force: Bool = false) async throws -> [Movie] {
        if !force, let m = allMoviesCache { return m }
        let list: [Movie]
        switch source {
        case .demo, .playlist: list = try await loadPlaylist(force: false).movies
        case .xtream:
            list = XtreamParse.vodStreams(try await cached("vod_all.json", force: force) {
                try await self.raw("get_vod_streams", timeout: 180)
            })
        }
        allMoviesCache = list
        return list
    }

    func movieInfo(_ movie: Movie) async throws -> MovieInfo? {
        if case .demo = source { return Demo.movieInfo[movie.id] }
        guard source.xtream != nil, let sid = movie.streamId, movie.url == nil else { return nil }
        if let d = cache.read("vodinfo_\(sid).json", maxAge: 7 * 86_400), let i = XtreamParse.vodInfo(d) { return i }
        let data = try await raw("get_vod_info", [("vod_id", sid)])
        cache.write("vodinfo_\(sid).json", data)
        return XtreamParse.vodInfo(data)
    }

    // ───────────────────────────── Séries ─────────────────────────────

    func seriesCategories(force: Bool = false) async throws -> [Category] {
        if !force, let c = seriesCategoriesCache { return c }
        let cats: [Category]
        switch source {
        case .demo, .playlist: cats = try await loadPlaylist(force: force).seriesCategories
        case .xtream:
            cats = XtreamParse.categories(try await cached("series_categories.json", force: force) { try await self.raw("get_series_categories") })
        }
        seriesCategoriesCache = cats
        return cats
    }

    func series(category: String, force: Bool = false) async throws -> [Series] {
        if !force, let s = seriesByCategory[category] { return s }
        let list: [Series]
        switch source {
        case .demo, .playlist: list = try await loadPlaylist(force: false).series.filter { $0.categoryId == category }
        case .xtream:
            list = XtreamParse.series(try await cached("series_\(category).json", force: force) {
                try await self.raw("get_series", [("category_id", category)], timeout: 90)
            })
        }
        seriesByCategory[category] = list
        return list
    }

    func allSeries(force: Bool = false) async throws -> [Series] {
        if !force, let s = allSeriesCache { return s }
        let list: [Series]
        switch source {
        case .demo, .playlist: list = try await loadPlaylist(force: false).series
        case .xtream:
            list = XtreamParse.series(try await cached("series_all.json", force: force) { try await self.raw("get_series", timeout: 180) })
        }
        allSeriesCache = list
        return list
    }

    func seriesDetails(_ s: Series) async throws -> SeriesDetails {
        if s.episodes != nil { return M3U.details(of: s) }
        let data: Data
        if let d = cache.read("seriesinfo_\(s.seriesId).json", maxAge: 6 * 3600) {
            data = d
        } else {
            data = try await raw("get_series_info", [("series_id", s.seriesId)])
            cache.write("seriesinfo_\(s.seriesId).json", data)
        }
        var d = XtreamParse.seriesInfo(data)
        if d.cover == nil { d.cover = s.cover }
        if d.plot == nil { d.plot = s.plot }
        return d
    }

    // ───────────────────────────── Guide TV ─────────────────────────────

    /// Programmes d'une chaîne (en cours et à venir). Rechargés au plus toutes les 20 min.
    func programmes(for channel: LiveChannel) async -> [Programme] {
        if case .demo = source { return Demo.programmes(for: channel.id) }
        if let sid = channel.streamId, let xtream {
            if let e = epg[sid], Date().timeIntervalSince(e.at) < Repository.epgMaxAge,
               (e.list.last?.stop ?? .distantPast) > Date() || e.list.isEmpty {
                return e.list
            }
            if let running = epgInFlight[sid] { return await running.value }
            let task = Task<[Programme], Never> { (try? await xtream.shortEPG(sid, limit: 10)) ?? [] }
            epgInFlight[sid] = task
            let list = await task.value
            epgInFlight[sid] = nil
            epg[sid] = (Date(), list)
            return list
        }
        // Playlist : guide XMLTV
        guard let tvg = channel.tvgId else { return [] }
        await loadXMLTVIfNeeded()
        return xmltv?[XMLTV.key(tvg)] ?? []
    }

    /// Programme complet (replay inclus) pour le guide.
    func fullProgrammes(for channel: LiveChannel) async -> [Programme] {
        if let sid = channel.streamId, let xtream {
            return (try? await xtream.fullEPG(sid)) ?? []
        }
        return await programmes(for: channel)
    }

    private func loadXMLTVIfNeeded() async {
        if xmltv != nil { return }
        if let t = xmltvTask { await t.value; return }
        let task = Task { await self.fetchXMLTV() }
        xmltvTask = task
        await task.value
        xmltvTask = nil
    }

    private func fetchXMLTV() async {
        guard let p = try? await loadPlaylist(force: false), let s = p.epgURL, let url = URL(string: s) else {
            xmltv = [:]
            return
        }
        let wanted = Set(p.live.compactMap(\.tvgId))
        let maxAge: TimeInterval = 6 * 3600
        if let fresh = cache.read("epg.xml", maxAge: maxAge) {
            xmltv = XMLTV.parse(fresh, wanted: wanted)
            return
        }
        if let downloaded = try? await http.data(url, timeout: 180) {
            let read = XMLTV.parse(downloaded, wanted: wanted)
            // Une page d'erreur reçue à la place du guide ne remplace pas celui qui marche
            if !read.isEmpty || !cache.exists("epg.xml") {
                cache.write("epg.xml", downloaded)
                xmltv = read
                return
            }
            // Rien d'utile dans la réponse : nouvel essai dans une demi-heure
            cache.touch("epg.xml", Date().addingTimeInterval(-maxAge + 30 * 60))
        }
        // Téléchargement raté ou réponse inutilisable : le guide précédent, même ancien
        // (avant, un échec laissait le guide vide alors qu'il y en avait un sur l'appareil)
        xmltv = cache.readAny("epg.xml").map { XMLTV.parse($0, wanted: wanted) } ?? [:]
    }

    // ───────────────────────────── Outils ─────────────────────────────

    private func raw(_ action: String, _ params: [(String, String)] = [], timeout: TimeInterval = 45) async throws -> Data {
        guard let account = source.xtream, let url = account.apiURL(action, params) else { throw FluxError.badAddress }
        return try await http.data(url, timeout: timeout)
    }

    /// Réponse en cache si fraîche ; sinon téléchargée (et l'ancienne en secours hors ligne).
    private func cached(_ name: String, force: Bool, fetch: () async throws -> Data) async throws -> Data {
        if !force, let d = cache.read(name, maxAge: Repository.catalogMaxAge) { return d }
        do {
            let d = try await fetch()
            // Réponse vide ou d'erreur : on ne remplace pas une liste valide
            // (existence vérifiée sans relire l'ancienne liste, qui peut peser plus de 100 Mo)
            if d.count > 2 || !cache.exists(name) { cache.write(name, d) }
            return d.count > 2 ? d : (cache.readAny(name) ?? d)
        } catch {
            if let old = cache.readAny(name) { return old }
            throw error
        }
    }
}
