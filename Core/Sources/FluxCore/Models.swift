import Foundation

// ───────────────────────────── Contenus ─────────────────────────────

enum ContentKind: String, Codable, Hashable, Sendable {
    case live, movie, episode, series, other
}

/**
 Référence STABLE d'un contenu : c'est ce qui est enregistré dans les favoris, l'historique
 et les reprises. Jamais d'identifiants de compte dedans : l'adresse de lecture est
 reconstruite avec le compte courant (un changement de serveur ou de mot de passe chez le
 fournisseur ne casse rien).

 Identifiants : "live:123", "vod:456", "ep:789" (Xtream) ou "u:<empreinte>" (playlist M3U).
 */
struct ContentRef: Codable, Hashable, Identifiable, Sendable {
    var id: String
    var kind: ContentKind
    var name: String
    var image: String?
    var group: String?
    /// Extension du fichier (films et épisodes Xtream) : mp4, mkv…
    var ext: String?
    var seriesId: String?
    var seriesName: String?
    var season: Int?
    var episode: Int?
    /// Playlists M3U uniquement : adresse complète du flux.
    var url: String?
    var userAgent: String?
    var tvgId: String?
    var num: Int?

    init(id: String, kind: ContentKind, name: String, image: String? = nil, group: String? = nil,
         ext: String? = nil, seriesId: String? = nil, seriesName: String? = nil, season: Int? = nil,
         episode: Int? = nil, url: String? = nil, userAgent: String? = nil, tvgId: String? = nil, num: Int? = nil) {
        self.id = id; self.kind = kind; self.name = name; self.image = image; self.group = group
        self.ext = ext; self.seriesId = seriesId; self.seriesName = seriesName; self.season = season
        self.episode = episode; self.url = url; self.userAgent = userAgent; self.tvgId = tvgId; self.num = num
    }

    /// Partie numérique d'un identifiant Xtream ("vod:456" → "456").
    var numericId: String? { Ids.numericPart(id) }
}

/// Catégorie (TV, films ou séries), dans l'ordre du fournisseur.
struct Category: Identifiable, Hashable, Sendable {
    let id: String
    /// Nom nettoyé, prêt à afficher (« FR | SPORTS » → « Sports »).
    let name: String
    let rawName: String
    let isAdult: Bool

    init(id: String, rawName: String) {
        self.id = id
        self.rawName = rawName
        self.name = TextTools.cleanCategory(rawName)
        self.isAdult = TextTools.isAdult(rawName)
    }
}

/// Chaîne en direct.
struct LiveChannel: Identifiable, Hashable, Sendable {
    let id: String
    /// stream_id Xtream (nil en mode playlist M3U).
    let streamId: String?
    /// Nom nettoyé (« FR| TF1 FHD » → « TF1 »), badges à part.
    let name: String
    let rawName: String
    let badges: [String]
    let logo: String?
    let categoryId: String
    let tvgId: String?
    let num: Int
    /// Jours de replay disponibles (0 = pas de replay).
    let archiveDays: Int
    /// Playlist M3U : adresse complète.
    let url: String?
    let userAgent: String?

    init(id: String, streamId: String?, rawName: String, logo: String?, categoryId: String,
         tvgId: String?, num: Int, archiveDays: Int = 0, url: String? = nil, userAgent: String? = nil) {
        self.id = id
        self.streamId = streamId
        self.rawName = rawName
        let cleaned = TextTools.cleanChannel(rawName)
        self.name = cleaned.name
        self.badges = cleaned.badges
        self.logo = logo
        self.categoryId = categoryId
        self.tvgId = tvgId
        self.num = num
        self.archiveDays = archiveDays
        self.url = url
        self.userAgent = userAgent
    }

    func ref(group: String?) -> ContentRef {
        ContentRef(id: id, kind: .live, name: name, image: logo, group: group, url: url,
                   userAgent: userAgent, tvgId: tvgId, num: num)
    }
}

/// Film.
struct Movie: Identifiable, Hashable, Sendable {
    let id: String
    let streamId: String?
    let name: String
    let rawName: String
    let year: Int?
    let badges: [String]
    let poster: String?
    let rating: Double?
    let added: Date?
    let ext: String
    let categoryId: String
    let url: String?

    init(id: String, streamId: String?, rawName: String, poster: String?, rating: Double?, added: Date?,
         ext: String, categoryId: String, year: Int? = nil, url: String? = nil) {
        self.id = id
        self.streamId = streamId
        self.rawName = rawName
        let cleaned = TextTools.cleanTitle(rawName)
        self.name = cleaned.name
        self.year = year ?? cleaned.year
        self.badges = cleaned.badges
        self.poster = poster
        self.rating = rating
        self.added = added
        self.ext = ext
        self.categoryId = categoryId
        self.url = url
    }

    func ref(group: String?) -> ContentRef {
        ContentRef(id: id, kind: .movie, name: name, image: poster, group: group, ext: ext, url: url)
    }
}

/// Série (fiche résumée, telle qu'elle apparaît dans les listes).
struct Series: Identifiable, Hashable, Sendable {
    let id: String
    let seriesId: String
    let name: String
    let rawName: String
    let year: Int?
    let cover: String?
    let plot: String?
    let genre: String?
    let rating: Double?
    let categoryId: String
    let lastModified: Date?
    /// Playlist M3U : épisodes déjà connus (regroupés d'après leur nom « S01 E02 »).
    let episodes: [Episode]?

    /// `id` : identifiant déjà connu (série reconstruite depuis un favori), sinon déduit de la source.
    init(seriesId: String, rawName: String, cover: String?, plot: String?, genre: String?, rating: Double?,
         categoryId: String, lastModified: Date?, year: Int? = nil, episodes: [Episode]? = nil, id: String? = nil) {
        self.id = id ?? ((episodes == nil ? "ser:" : "m3us:") + seriesId)
        self.seriesId = seriesId
        self.rawName = rawName
        let cleaned = TextTools.cleanTitle(rawName)
        self.name = cleaned.name
        self.year = year ?? cleaned.year
        self.cover = cover
        self.plot = plot
        self.genre = genre
        self.rating = rating
        self.categoryId = categoryId
        self.lastModified = lastModified
        self.episodes = episodes
    }
}

extension Series {
    func ref(group: String?) -> ContentRef {
        ContentRef(id: id, kind: .series, name: name, image: cover, group: group, seriesId: seriesId, seriesName: name)
    }

    /**
     Série reconstruite depuis un favori (la fiche complète est rechargée à l'ouverture). Elle
     garde l'identifiant du favori : « m3us:… » pour une playlist (épisodes retrouvés dans la
     playlist), « ser:… » pour un compte Xtream.
     */
    init(ref: ContentRef) {
        self.init(seriesId: ref.seriesId ?? Ids.numericPart(ref.id) ?? ref.id, rawName: ref.name, cover: ref.image, plot: nil,
                  genre: nil, rating: nil, categoryId: ref.group ?? "", lastModified: nil, id: ref.id)
    }
}

extension Movie {
    /// Film reconstruit depuis un favori ou l'historique.
    init(ref: ContentRef) {
        self.init(id: ref.id, streamId: ref.numericId, rawName: ref.name, poster: ref.image, rating: nil, added: nil,
                  ext: ref.ext ?? "mp4", categoryId: ref.group ?? "", url: ref.url)
    }
}

/// Épisode d'une série.
struct Episode: Identifiable, Hashable, Sendable {
    let id: String
    let episodeId: String
    let title: String
    let season: Int
    let number: Int
    let ext: String
    let plot: String?
    let image: String?
    let durationSecs: Int?
    let url: String?

    var label: String { String(format: "S%02dE%02d", season, number) }

    func ref(series: String, seriesName: String, image fallback: String?) -> ContentRef {
        ContentRef(id: id, kind: .episode, name: "\(label) · \(title)", image: image ?? fallback, group: seriesName,
                   ext: ext, seriesId: series, seriesName: seriesName, season: season, episode: number, url: url)
    }
}

/// Fiche complète d'une série : saisons et épisodes.
struct SeriesDetails: Sendable {
    var seasons: [Int: [Episode]]
    var plot: String?
    var cover: String?
    var backdrop: String?
    var genre: String?
    var rating: Double?
    var year: Int?
    var cast: String?
    var director: String?

    var seasonNumbers: [Int] { seasons.keys.sorted() }
    /// Tous les épisodes dans l'ordre (saison puis numéro).
    var allEpisodes: [Episode] { seasonNumbers.flatMap { seasons[$0] ?? [] } }
}

/// Fiche complète d'un film.
struct MovieInfo: Sendable {
    var plot: String?
    var cast: String?
    var director: String?
    var genre: String?
    var releaseDate: String?
    var durationSecs: Int?
    var rating: Double?
    var cover: String?
    var backdrop: String?
    /// Identifiant YouTube de la bande-annonce.
    var trailer: String?

    var year: Int? {
        guard let r = releaseDate, r.count >= 4, let y = Int(r.prefix(4)), (1900...2100).contains(y) else { return nil }
        return y
    }
}

/// Programme du guide TV.
struct Programme: Hashable, Sendable {
    let start: Date
    let stop: Date
    let title: String
    let desc: String?
    let hasArchive: Bool

    init(start: Date, stop: Date, title: String, desc: String? = nil, hasArchive: Bool = false) {
        self.start = start; self.stop = stop; self.title = title; self.desc = desc; self.hasArchive = hasArchive
    }

    func isNow(_ now: Date = Date()) -> Bool { start <= now && now < stop }
    func isPast(_ now: Date = Date()) -> Bool { stop <= now }

    /// Avancement 0…1 du programme en cours.
    func progress(_ now: Date = Date()) -> Double {
        let total = stop.timeIntervalSince(start)
        guard total > 0 else { return 0 }
        return min(1, max(0, now.timeIntervalSince(start) / total))
    }

    /// Programme en cours et programme suivant d'une liste triée.
    static func nowNext(_ list: [Programme], at now: Date = Date()) -> (now: Programme?, next: Programme?) {
        guard let i = list.firstIndex(where: { now < $0.stop }) else { return (nil, nil) }
        if list[i].start <= now {
            return (list[i], i + 1 < list.count ? list[i + 1] : nil)
        }
        return (nil, list[i])
    }
}

// ───────────────────────────── Identifiants ─────────────────────────────

enum Ids {
    static func live(_ streamId: String) -> String { "live:\(streamId)" }
    static func vod(_ streamId: String) -> String { "vod:\(streamId)" }
    static func episode(_ episodeId: String) -> String { "ep:\(episodeId)" }

    /// Partie numérique d'un identifiant typé ("vod:456" → "456"), sinon nil.
    static func numericPart(_ id: String) -> String? {
        guard let colon = id.firstIndex(of: ":") else { return nil }
        let tail = id[id.index(after: colon)...]
        return !tail.isEmpty && tail.allSatisfy(\.isASCIIDigit) ? String(tail) : nil
    }

    /**
     Identifiant déduit d'une adresse (playlists M3U) : les adresses de type Xtream
     (…/live/user/pass/123.ts) donnent "live:123" — les mêmes favoris qu'avec le compte Xtream.
     */
    static func fromURL(_ url: String) -> String {
        let path = url.split(separator: "?", maxSplits: 1).first.map(String.init) ?? url
        let parts = path.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        // …/(live|movie|series)/user/pass/123(.ext)
        if parts.count >= 4 {
            let kind = parts[parts.count - 4]
            let last = parts[parts.count - 1]
            let num = last.split(separator: ".", maxSplits: 1).first.map(String.init) ?? last
            if !num.isEmpty, num.allSatisfy(\.isASCIIDigit) {
                switch kind {
                case "live": return live(num)
                case "movie": return vod(num)
                case "series": return episode(num)
                default:
                    // Forme courte des playlists Xtream : http://hote:port/user/pass/123
                    if parts.count == 6, parts[0].hasPrefix("http"), parts[1].isEmpty,
                       last.hasSuffix(".ts") || last.hasSuffix(".m3u8") || !last.contains(".") {
                        return live(num)
                    }
                }
            }
        }
        return "u:" + Hash.fnv1a64Hex(url)
    }
}

enum Hash {
    /// Empreinte courte et stable (FNV-1a 64 bits) — identifiants des entrées de playlist.
    static func fnv1a64Hex(_ s: String) -> String {
        var h: UInt64 = 0xcbf29ce484222325
        for b in s.utf8 {
            h ^= UInt64(b)
            h = h &* 0x100000001b3
        }
        let hex = String(h, radix: 16)
        return String(repeating: "0", count: max(0, 16 - hex.count)) + hex
    }
}

extension Character {
    var isASCIIDigit: Bool { ("0"..."9").contains(self) }
}
