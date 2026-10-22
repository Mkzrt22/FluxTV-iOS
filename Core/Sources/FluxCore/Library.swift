import Foundation

/// Position de lecture d'un film ou d'un épisode.
struct PlaybackPosition: Codable, Hashable, Sendable {
    var position: Double
    var duration: Double
    var updated: Date

    var fraction: Double { duration > 0 ? min(1, max(0, position / duration)) : 0 }
}

/**
 Données personnelles : favoris, historique, reprises, contenus vus. Fonctions pures
 (testées sans appareil) ; l'enregistrement sur disque est fait par [LibraryFile].
 */
struct LibraryData: Codable, Sendable, Equatable {
    static let historyMax = 60
    static let watchedMax = 5000
    /// En dessous, on ne propose pas de reprise (début du film).
    static let resumeMinSeconds: Double = 15
    /// Au-delà de 95 %, le contenu est considéré comme vu (générique de fin).
    static let finishedRatio = 0.95

    var favorites: [ContentRef] = []
    /// Plus récent en premier.
    var history: [ContentRef] = []
    var positions: [String: PlaybackPosition] = [:]
    var watched: [String] = []
    var lastLiveId: String?

    // ───────────────────────────── Favoris ─────────────────────────────

    func isFavorite(_ id: String) -> Bool { favorites.contains { $0.id == id } }

    /// Ajoute ou retire ; renvoie true si le contenu est maintenant en favori.
    @discardableResult
    mutating func toggleFavorite(_ ref: ContentRef) -> Bool {
        if let i = favorites.firstIndex(where: { $0.id == ref.id }) {
            favorites.remove(at: i)
            return false
        }
        favorites.append(ref)
        return true
    }

    mutating func moveFavorite(from: Int, to: Int) {
        guard favorites.indices.contains(from), (0...favorites.count).contains(to), from != to else { return }
        let item = favorites.remove(at: from)
        favorites.insert(item, at: min(to > from ? to - 1 : to, favorites.count))
    }

    func favorites(of kind: ContentKind) -> [ContentRef] { favorites.filter { $0.kind == kind } }

    // ───────────────────────────── Historique ─────────────────────────────

    mutating func addHistory(_ ref: ContentRef) {
        history.removeAll { $0.id == ref.id }
        // Séries : un seul épisode par série dans l'historique (le plus récent)
        if ref.kind == .episode, let sid = ref.seriesId {
            history.removeAll { $0.kind == .episode && $0.seriesId == sid }
        }
        history.insert(ref, at: 0)
        if history.count > LibraryData.historyMax { history.removeLast(history.count - LibraryData.historyMax) }
        if ref.kind == .live { lastLiveId = ref.id }
    }

    mutating func removeHistory(_ id: String) {
        history.removeAll { $0.id == id }
        positions[id] = nil
    }

    func recentLive(limit: Int = 12) -> [ContentRef] { Array(history.filter { $0.kind == .live }.prefix(limit)) }

    // ───────────────────────────── Reprises ─────────────────────────────

    /**
     Enregistre la position. Au-delà de 95 %, le contenu passe en « vu » et la reprise est
     effacée. Renvoie true si le contenu vient d'être terminé.
     */
    @discardableResult
    mutating func savePosition(_ ref: ContentRef, position: Double, duration: Double, now: Date = Date()) -> Bool {
        guard ref.kind != .live, position.isFinite, duration.isFinite else { return false }
        if duration > 0, position / duration >= LibraryData.finishedRatio {
            positions[ref.id] = nil
            markWatched(ref.id)
            return true
        }
        if position >= LibraryData.resumeMinSeconds {
            positions[ref.id] = PlaybackPosition(position: position, duration: max(0, duration), updated: now)
        }
        return false
    }

    /// Position de reprise (nil si rien à reprendre).
    func resume(_ id: String) -> PlaybackPosition? {
        guard let p = positions[id], p.position >= LibraryData.resumeMinSeconds else { return nil }
        return p
    }

    /// « Continuer à regarder » : films et épisodes commencés, du plus récent au plus ancien.
    func continueWatching(limit: Int = 20) -> [ContentRef] {
        Array(history.filter { $0.kind != .live && resume($0.id) != nil }.prefix(limit))
    }

    // ───────────────────────────── Vus ─────────────────────────────

    func isWatched(_ id: String) -> Bool { watched.contains(id) }

    mutating func markWatched(_ id: String) {
        guard !watched.contains(id) else { return }
        watched.append(id)
        positions[id] = nil
        if watched.count > LibraryData.watchedMax { watched.removeFirst(watched.count - LibraryData.watchedMax) }
    }

    mutating func markUnwatched(_ id: String) {
        watched.removeAll { $0 == id }
    }

    /**
     Prochain épisode à regarder dans une série : l'épisode en cours s'il est commencé,
     sinon le premier épisode non vu après le dernier épisode vu.
     */
    func nextEpisode(in episodes: [Episode]) -> (episode: Episode, resume: PlaybackPosition?)? {
        guard !episodes.isEmpty else { return nil }
        // Épisode commencé le plus récemment
        let started = episodes.compactMap { e in resume(e.id).map { (e, $0) } }.max { $0.1.updated < $1.1.updated }
        if let s = started { return (s.0, s.1) }
        if let lastWatched = episodes.lastIndex(where: { isWatched($0.id) }) {
            let next = episodes.index(after: lastWatched)
            if next < episodes.endIndex { return (episodes[next], nil) }
            return nil
        }
        return (episodes[0], nil)
    }
}

/// Enregistrement de [LibraryData] dans un fichier JSON (écriture atomique).
struct LibraryFile: Sendable {
    let url: URL

    static var defaultURL: URL {
        let base = (try? FileManager.default.url(for: .applicationSupportDirectory, in: .userDomainMask,
                                                  appropriateFor: nil, create: true))
            ?? URL(fileURLWithPath: NSTemporaryDirectory())
        return base.appendingPathComponent("FluxTV", isDirectory: true).appendingPathComponent("library.json")
    }

    func load() -> LibraryData {
        guard let data = try? Data(contentsOf: url) else { return LibraryData() }
        let dec = JSONDecoder()
        dec.dateDecodingStrategy = .secondsSince1970
        return (try? dec.decode(LibraryData.self, from: data)) ?? LibraryData()
    }

    func save(_ lib: LibraryData) throws {
        let enc = JSONEncoder()
        enc.dateEncodingStrategy = .secondsSince1970
        let data = try enc.encode(lib)
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: url, options: [.atomic])
    }
}
