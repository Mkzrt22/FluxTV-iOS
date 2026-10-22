import Foundation

// ───────────────────────────── Compte ─────────────────────────────

/// Compte Xtream Codes (serveur, nom d'utilisateur, mot de passe).
struct XtreamAccount: Codable, Equatable, Hashable, Sendable {
    var server: String
    var username: String
    var password: String

    init(server: String, username: String, password: String) {
        self.server = XtreamAccount.normalizeServer(server)
        self.username = username.trimmingCharacters(in: .whitespacesAndNewlines)
        self.password = password.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var isComplete: Bool { !server.isEmpty && !username.isEmpty && !password.isEmpty }

    /**
     Adresse du serveur telle que tapée → « http(s)://hote:port ».
     Accepte « hote:port », une adresse avec « / » final, ou une adresse complète collée
     (…/player_api.php, …/get.php?…).
     */
    static func normalizeServer(_ raw: String) -> String {
        var s = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !s.isEmpty else { return "" }
        if !s.lowercased().hasPrefix("http://") && !s.lowercased().hasPrefix("https://") { s = "http://" + s }
        guard var comps = URLComponents(string: s), let host = comps.host, !host.isEmpty else {
            return s.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        }
        comps.query = nil
        comps.fragment = nil
        // Chemin : on enlève les pages connues (le serveur peut être installé dans un sous-dossier)
        var path = comps.path
        for page in ["/player_api.php", "/get.php", "/xmltv.php", "/panel_api.php"] where path.lowercased().hasSuffix(page) {
            path = String(path.dropLast(page.count))
        }
        while path.hasSuffix("/") { path.removeLast() }
        comps.path = path
        return comps.string ?? s
    }

    /**
     Lien de playlist Xtream collé à la place d'un compte
     (http://hote:port/get.php?username=…&password=…&type=m3u_plus) → compte Xtream.
     */
    static func fromPlaylistURL(_ raw: String) -> XtreamAccount? {
        let s = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let comps = URLComponents(string: s), let items = comps.queryItems,
              let user = items.first(where: { $0.name.lowercased() == "username" })?.value, !user.isEmpty,
              let pass = items.first(where: { $0.name.lowercased() == "password" })?.value, !pass.isEmpty
        else { return nil }
        let path = comps.path.lowercased()
        guard path.hasSuffix("get.php") || path.hasSuffix("player_api.php") || path.hasSuffix("xmltv.php") else { return nil }
        return XtreamAccount(server: s, username: user, password: pass)
    }

    // ───────────────────────────── Adresses ─────────────────────────────

    private static let segmentAllowed: CharacterSet = {
        var c = CharacterSet.alphanumerics
        c.insert(charactersIn: "-._~")
        return c
    }()

    private func enc(_ s: String) -> String {
        s.addingPercentEncoding(withAllowedCharacters: XtreamAccount.segmentAllowed) ?? s
    }

    /// player_api.php?username=…&password=…&action=…
    func apiURL(_ action: String?, _ params: [(String, String)] = []) -> URL? {
        guard var comps = URLComponents(string: server + "/player_api.php") else { return nil }
        var items = [URLQueryItem(name: "username", value: username), URLQueryItem(name: "password", value: password)]
        if let action { items.append(URLQueryItem(name: "action", value: action)) }
        items += params.map { URLQueryItem(name: $0.0, value: $0.1) }
        comps.queryItems = items
        // « + » n'est pas encodé par défaut dans les requêtes : certains serveurs le lisent comme une espace
        comps.percentEncodedQuery = comps.percentEncodedQuery?.replacingOccurrences(of: "+", with: "%2B")
        return comps.url
    }

    func liveURL(_ streamId: String, ext: String = "ts") -> URL? {
        URL(string: "\(server)/live/\(enc(username))/\(enc(password))/\(streamId).\(ext)")
    }

    func movieURL(_ streamId: String, ext: String) -> URL? {
        URL(string: "\(server)/movie/\(enc(username))/\(enc(password))/\(streamId).\(ext.isEmpty ? "mp4" : ext)")
    }

    func episodeURL(_ episodeId: String, ext: String) -> URL? {
        URL(string: "\(server)/series/\(enc(username))/\(enc(password))/\(episodeId).\(ext.isEmpty ? "mkv" : ext)")
    }

    /// Replay d'un programme passé. L'heure s'exprime dans le fuseau DU SERVEUR.
    func timeshiftURL(_ streamId: String, start: Date, minutes: Int, serverTimeZone: TimeZone?) -> URL? {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.timeZone = serverTimeZone ?? .current
        f.dateFormat = "yyyy-MM-dd:HH-mm"
        return URL(string: "\(server)/timeshift/\(enc(username))/\(enc(password))/\(max(1, minutes))/\(f.string(from: start))/\(streamId).ts")
    }

    /// Lien EPG complet (XMLTV) du serveur.
    var xmltvURL: URL? {
        guard var comps = URLComponents(string: server + "/xmltv.php") else { return nil }
        comps.queryItems = [URLQueryItem(name: "username", value: username), URLQueryItem(name: "password", value: password)]
        return comps.url
    }
}

/// État du compte renvoyé par le serveur.
struct AccountInfo: Sendable, Equatable {
    var authenticated: Bool
    var status: String?
    var expiry: Date?
    var isTrial: Bool
    var maxConnections: Int?
    var activeConnections: Int?
    var allowedFormats: [String]
    var serverTimeZone: String?

    var isActive: Bool { authenticated && (status == nil || status?.lowercased() == "active") }

    /// Jours restants (nil = illimité).
    func daysLeft(now: Date = Date()) -> Int? {
        guard let e = expiry else { return nil }
        return max(0, Int(ceil(e.timeIntervalSince(now) / 86_400)))
    }

    /// Format du direct : « ts » si le serveur l'autorise (le plus fiable), sinon HLS.
    var liveExtension: String {
        let f = allowedFormats.map { $0.lowercased() }
        if f.isEmpty || f.contains("ts") { return "ts" }
        if f.contains("m3u8") { return "m3u8" }
        return "ts"
    }
}

// ───────────────────────────── Lecture des réponses ─────────────────────────────

/// Analyse des réponses player_api.php (fonctions pures : testées sans réseau).
enum XtreamParse {

    static func account(_ data: Data) throws -> AccountInfo {
        guard let root = JSON.object(data), let ui = root.obj("user_info") else {
            throw FluxError.unexpectedResponse
        }
        let formats = (ui.arr("allowed_output_formats") ?? []).compactMap { $0 as? String }
        return AccountInfo(
            authenticated: ui.flag("auth"),
            status: ui.str("status"),
            expiry: ui.unixDate("exp_date"),
            isTrial: ui.flag("is_trial"),
            maxConnections: ui.int("max_connections"),
            activeConnections: ui.int("active_cons"),
            allowedFormats: formats,
            serverTimeZone: root.obj("server_info")?.str("timezone")
        )
    }

    static func categories(_ data: Data) -> [Category] {
        guard let arr = JSON.array(data) else { return [] }
        var seen = Set<String>()
        return arr.compactMap { item in
            guard let o = item as? JSONObject, let id = o.str("category_id"), let name = o.str("category_name"),
                  seen.insert(id).inserted else { return nil }
            return Category(id: id, rawName: name)
        }
    }

    static func liveStreams(_ data: Data) -> [LiveChannel] {
        guard let arr = JSON.array(data) else { return [] }
        var out: [LiveChannel] = []
        out.reserveCapacity(arr.count)
        for item in arr {
            guard let o = item as? JSONObject, let sid = o.str("stream_id") else { continue }
            let archive = o.flag("tv_archive")
            out.append(LiveChannel(
                id: Ids.live(sid), streamId: sid,
                rawName: o.str("name") ?? "Sans nom",
                logo: o.str("stream_icon").flatMap(validImage),
                categoryId: o.str("category_id") ?? "",
                tvgId: o.str("epg_channel_id"),
                num: o.int("num") ?? 0,
                archiveDays: archive ? max(1, o.int("tv_archive_duration") ?? 1) : 0
            ))
        }
        return out
    }

    static func vodStreams(_ data: Data) -> [Movie] {
        guard let arr = JSON.array(data) else { return [] }
        var out: [Movie] = []
        out.reserveCapacity(arr.count)
        for item in arr {
            guard let o = item as? JSONObject, let sid = o.str("stream_id") else { continue }
            out.append(Movie(
                id: Ids.vod(sid), streamId: sid,
                rawName: o.str("name") ?? o.str("title") ?? "Sans titre",
                poster: o.str("stream_icon").flatMap(validImage) ?? o.str("cover").flatMap(validImage),
                rating: o.rating("rating"),
                added: o.unixDate("added"),
                ext: o.str("container_extension") ?? "mp4",
                categoryId: o.str("category_id") ?? "",
                year: o.int("year").flatMap { (1900...2100).contains($0) ? $0 : nil }
            ))
        }
        return out
    }

    static func series(_ data: Data) -> [Series] {
        guard let arr = JSON.array(data) else { return [] }
        var out: [Series] = []
        out.reserveCapacity(arr.count)
        for item in arr {
            guard let o = item as? JSONObject, let sid = o.str("series_id") else { continue }
            let release = o.str("releaseDate") ?? o.str("release_date") ?? o.str("year")
            out.append(Series(
                seriesId: sid,
                rawName: o.str("name") ?? o.str("title") ?? "Sans titre",
                cover: o.str("cover").flatMap(validImage),
                plot: o.str("plot"),
                genre: o.str("genre"),
                rating: o.rating("rating"),
                categoryId: o.str("category_id") ?? "",
                lastModified: o.unixDate("last_modified"),
                year: release.flatMap { Int($0.prefix(4)) }.flatMap { (1900...2100).contains($0) ? $0 : nil }
            ))
        }
        return out
    }

    /**
     Saisons et épisodes. Selon les panels, "episodes" est un objet {"1":[…]} OU un
     tableau [[…],[…]] (voire [] vide) : les deux sont gérés.
     */
    static func seriesInfo(_ data: Data) -> SeriesDetails {
        let root = JSON.object(data) ?? [:]
        var seasons: [Int: [Episode]] = [:]

        func add(_ o: JSONObject, fallbackSeason: Int, index: Int) {
            guard let epId = o.str("id") else { return }
            let info = o.obj("info") ?? [:]
            let season = o.int("season") ?? fallbackSeason
            let ep = Episode(
                id: Ids.episode(epId), episodeId: epId,
                title: cleanEpisodeTitle(o.str("title") ?? "Épisode \(index + 1)"),
                season: season,
                number: o.int("episode_num") ?? (index + 1),
                ext: o.str("container_extension") ?? "mkv",
                plot: info.str("plot") ?? info.str("description"),
                image: info.str("movie_image").flatMap(validImage) ?? info.str("cover_big").flatMap(validImage),
                durationSecs: info.int("duration_secs") ?? info.str("duration").flatMap(parseDuration),
                url: nil
            )
            seasons[season, default: []].append(ep)
        }

        switch root["episodes"] {
        case let dict as JSONObject:
            for (key, value) in dict {
                guard let num = Int(key), let list = value as? [Any] else { continue }
                for (i, e) in list.enumerated() { if let o = e as? JSONObject { add(o, fallbackSeason: num, index: i) } }
            }
        case let arr as [Any]:
            for (s, item) in arr.enumerated() {
                if let list = item as? [Any] {
                    for (i, e) in list.enumerated() { if let o = e as? JSONObject { add(o, fallbackSeason: s + 1, index: i) } }
                } else if let o = item as? JSONObject {
                    add(o, fallbackSeason: 1, index: s)
                }
            }
        default:
            break
        }
        for k in seasons.keys {
            // Même numéro d'épisode en double (fichiers de secours) : on garde le premier
            var seen = Set<Int>()
            seasons[k] = seasons[k]!.sorted { $0.number < $1.number }.filter { seen.insert($0.number).inserted }
        }

        let info = root.obj("info") ?? [:]
        let release = info.str("releaseDate") ?? info.str("release_date")
        return SeriesDetails(
            seasons: seasons,
            plot: info.str("plot"),
            cover: info.str("cover").flatMap(validImage),
            backdrop: info.firstURL("backdrop_path"),
            genre: info.str("genre"),
            rating: info.rating("rating"),
            year: release.flatMap { Int($0.prefix(4)) }.flatMap { (1900...2100).contains($0) ? $0 : nil },
            cast: info.str("cast"),
            director: info.str("director")
        )
    }

    static func vodInfo(_ data: Data) -> MovieInfo? {
        guard let root = JSON.object(data), let info = root.obj("info") else { return nil }
        let trailer = info.str("youtube_trailer") ?? info.str("trailer")
        return MovieInfo(
            plot: info.str("plot") ?? info.str("description"),
            cast: info.str("cast") ?? info.str("actors"),
            director: info.str("director"),
            genre: info.str("genre"),
            releaseDate: info.str("releasedate") ?? info.str("release_date"),
            durationSecs: info.int("duration_secs") ?? info.str("duration").flatMap(parseDuration),
            rating: info.rating("rating"),
            cover: info.str("movie_image").flatMap(validImage) ?? info.str("cover_big").flatMap(validImage),
            backdrop: info.firstURL("backdrop_path"),
            trailer: trailer.flatMap(youtubeId)
        )
    }

    /// get_short_epg / get_simple_data_table : titres et descriptions en base64.
    static func epgListings(_ data: Data) -> [Programme] {
        guard let root = JSON.object(data), let arr = root.arr("epg_listings") else { return [] }
        var list: [Programme] = []
        for item in arr {
            guard let o = item as? JSONObject,
                  let st = o.int("start_timestamp"), let en = o.int("stop_timestamp"), en > st else { continue }
            let title = base64(o.str("title")).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !title.isEmpty else { continue }
            let desc = base64(o.str("description")).trimmingCharacters(in: .whitespacesAndNewlines)
            list.append(Programme(
                start: Date(timeIntervalSince1970: TimeInterval(st)),
                stop: Date(timeIntervalSince1970: TimeInterval(en)),
                title: title,
                desc: desc.isEmpty ? nil : desc,
                hasArchive: o.flag("has_archive")
            ))
        }
        list.sort { $0.start < $1.start }
        var seen = Set<Date>()
        return list.filter { seen.insert($0.start).inserted }
    }

    // ───────────────────────────── Outils ─────────────────────────────

    static func base64(_ s: String?) -> String {
        guard let s, !s.isEmpty else { return "" }
        guard let d = Data(base64Encoded: s, options: .ignoreUnknownCharacters), let t = String(data: d, encoding: .utf8) else {
            return s
        }
        return t
    }

    /// "01:52:10", "112 min", "6730" → secondes.
    static func parseDuration(_ s: String) -> Int? {
        let t = s.trimmingCharacters(in: .whitespaces)
        if t.contains(":") {
            let parts = t.split(separator: ":").compactMap { Int($0) }
            guard !parts.isEmpty else { return nil }
            let secs = parts.reduce(0) { $0 * 60 + $1 }
            return secs > 0 ? secs : nil
        }
        let digits = t.prefix { $0.isASCIIDigit }
        guard let n = Int(digits), n > 0 else { return nil }
        return t.lowercased().contains("min") || n < 400 ? n * 60 : n
    }

    /// Identifiant YouTube depuis un identifiant seul ou une adresse.
    static func youtubeId(_ s: String) -> String? {
        let t = s.trimmingCharacters(in: .whitespaces)
        if t.count == 11, !t.contains("/"), !t.contains("=") { return t }
        if let comps = URLComponents(string: t) {
            if let v = comps.queryItems?.first(where: { $0.name == "v" })?.value, v.count == 11 { return v }
            let last = comps.path.split(separator: "/").last.map(String.init) ?? ""
            if last.count == 11 { return last }
        }
        return nil
    }

    /// Images « vides » fréquentes chez les fournisseurs.
    static func validImage(_ s: String) -> String? {
        let t = s.trimmingCharacters(in: .whitespacesAndNewlines)
        guard t.hasPrefix("http"), t.count > 10 else { return nil }
        return t
    }

    /// « Brumes - S01E03 - Les Disparus » → « Les Disparus ».
    static func cleanEpisodeTitle(_ raw: String) -> String {
        var t = raw.trimmingCharacters(in: .whitespaces)
        if let r = t.range(of: #"[Ss]\d{1,2}\s?[Ee]\d{1,3}"#, options: .regularExpression) {
            let after = t[r.upperBound...].trimmingCharacters(in: CharacterSet(charactersIn: " -–:·|"))
            if !after.isEmpty { t = after }
        }
        return t
    }
}

// ───────────────────────────── Client ─────────────────────────────

/// Appels player_api.php.
struct XtreamClient: Sendable {
    let account: XtreamAccount
    let http: HTTPClient

    init(account: XtreamAccount, http: HTTPClient = .shared) {
        self.account = account
        self.http = http
    }

    private func get(_ action: String?, _ params: [(String, String)] = [], timeout: TimeInterval = 30) async throws -> Data {
        guard let url = account.apiURL(action, params) else { throw FluxError.badAddress }
        return try await http.data(url, timeout: timeout)
    }

    func authenticate() async throws -> AccountInfo {
        let info = try XtreamParse.account(try await get(nil, timeout: 15))
        guard info.authenticated else { throw FluxError.authRefused }
        return info
    }

    func liveCategories() async throws -> [Category] { XtreamParse.categories(try await get("get_live_categories")) }
    func liveStreams() async throws -> [LiveChannel] { XtreamParse.liveStreams(try await get("get_live_streams", timeout: 60)) }
    func vodCategories() async throws -> [Category] { XtreamParse.categories(try await get("get_vod_categories")) }
    func seriesCategories() async throws -> [Category] { XtreamParse.categories(try await get("get_series_categories")) }

    func vodStreams(category: String?) async throws -> [Movie] {
        XtreamParse.vodStreams(try await get("get_vod_streams", category.map { [("category_id", $0)] } ?? [], timeout: 90))
    }

    func series(category: String?) async throws -> [Series] {
        XtreamParse.series(try await get("get_series", category.map { [("category_id", $0)] } ?? [], timeout: 90))
    }

    func seriesInfo(_ seriesId: String) async throws -> SeriesDetails {
        XtreamParse.seriesInfo(try await get("get_series_info", [("series_id", seriesId)]))
    }

    func vodInfo(_ streamId: String) async throws -> MovieInfo? {
        XtreamParse.vodInfo(try await get("get_vod_info", [("vod_id", streamId)]))
    }

    func shortEPG(_ streamId: String, limit: Int = 8) async throws -> [Programme] {
        XtreamParse.epgListings(try await get("get_short_epg", [("stream_id", streamId), ("limit", String(limit))], timeout: 15))
    }

    func fullEPG(_ streamId: String) async throws -> [Programme] {
        XtreamParse.epgListings(try await get("get_simple_data_table", [("stream_id", streamId)]))
    }
}
