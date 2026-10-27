import Foundation

/// Une entrée brute de playlist M3U.
struct M3UEntry: Hashable, Sendable {
    var name: String
    var url: String
    var logo: String?
    var group: String
    var tvgId: String?
    var num: Int
    var userAgent: String?
}

/// Contenu d'une playlist, réparti comme avec un compte Xtream.
struct M3UCatalog: Sendable {
    var epgURL: String?
    var liveCategories: [Category] = []
    var live: [LiveChannel] = []
    var vodCategories: [Category] = []
    var movies: [Movie] = []
    var seriesCategories: [Category] = []
    var series: [Series] = []

    var isEmpty: Bool { live.isEmpty && movies.isEmpty && series.isEmpty }
}

enum M3U {

    // ───────────────────────────── Lecture ─────────────────────────────

    static func parse(_ text: String) -> (entries: [M3UEntry], epgURL: String?) {
        var entries: [M3UEntry] = []
        var epg: String?
        var name = "", logo: String?, group = T("Autres", "Other"), tvgId: String?, ua: String?, num = 0
        var pending = false

        text.enumerateLines { raw, _ in
            let line = raw.trimmingCharacters(in: .whitespaces)
            if line.isEmpty { return }
            if line.hasPrefix("#") {
                let upper = line.prefix(12).uppercased()
                if upper.hasPrefix("#EXTM3U") {
                    let a = attributes(line)
                    if let u = (a["x-tvg-url"] ?? a["url-tvg"])?.split(separator: ",").first.map({ String($0).trimmingCharacters(in: .whitespaces) }),
                       u.hasPrefix("http") { epg = u }
                } else if upper.hasPrefix("#EXTINF") {
                    pending = true
                    let a = attributes(line)
                    tvgId = a["tvg-id"].flatMap { $0.isEmpty ? nil : $0 }
                    logo = a["tvg-logo"].flatMap { $0.isEmpty ? nil : $0 }
                    group = a["group-title"].flatMap { $0.isEmpty ? nil : $0 } ?? T("Autres", "Other")
                    num = a["tvg-chno"].flatMap { Int($0.trimmingCharacters(in: .whitespaces)) } ?? 0
                    let n = extinfName(line)
                    name = n.isEmpty ? (a["tvg-name"] ?? T("Sans nom", "Untitled")) : n
                } else if upper.hasPrefix("#EXTGRP") {
                    if let colon = line.firstIndex(of: ":") {
                        let g = line[line.index(after: colon)...].trimmingCharacters(in: .whitespaces)
                        if !g.isEmpty { group = g }
                    }
                } else if line.lowercased().hasPrefix("#extvlcopt:http-user-agent") {
                    if let eq = line.firstIndex(of: "=") {
                        let v = line[line.index(after: eq)...].trimmingCharacters(in: .whitespaces)
                        ua = v.isEmpty ? nil : v
                    }
                }
                return
            }
            if pending {
                entries.append(M3UEntry(name: name.isEmpty ? T("Sans nom", "Untitled") : name, url: line, logo: logo, group: group,
                                        tvgId: tvgId, num: num, userAgent: ua))
            }
            pending = false; name = ""; logo = nil; tvgId = nil; ua = nil; group = T("Autres", "Other"); num = 0
        }
        return (entries, epg)
    }

    /// Attributs clé="valeur" d'une ligne #EXTINF / #EXTM3U (clés en minuscules).
    static func attributes(_ line: String) -> [String: String] {
        var out: [String: String] = [:]
        let chars = Array(line)
        var i = 0
        while i < chars.count {
            // Clé : lettres, chiffres, tirets, juste avant ="
            guard chars[i] == "=", i + 1 < chars.count, chars[i + 1] == "\"" else { i += 1; continue }
            var k = i - 1
            while k >= 0, chars[k].isLetter || chars[k].isNumber || chars[k] == "-" || chars[k] == "_" { k -= 1 }
            let key = String(chars[(k + 1)..<i]).lowercased()
            var j = i + 2
            while j < chars.count, chars[j] != "\"" { j += 1 }
            if !key.isEmpty, j <= chars.count {
                out[key] = String(chars[(i + 2)..<min(j, chars.count)])
            }
            i = j + 1
        }
        return out
    }

    /// Nom : texte après la PREMIÈRE virgule hors guillemets (« Canal+ Sport, HD » reste entier).
    static func extinfName(_ line: String) -> String {
        var inQuotes = false
        for (idx, ch) in zip(line.indices, line) {
            if ch == "\"" { inQuotes.toggle() }
            else if ch == ",", !inQuotes { return line[line.index(after: idx)...].trimmingCharacters(in: .whitespaces) }
        }
        return ""
    }

    // ───────────────────────────── Classement ─────────────────────────────

    private static let vodExtensions = [".mp4", ".mkv", ".avi", ".mov", ".m4v", ".wmv", ".flv", ".webm"]

    enum Kind { case live, movie, episode }

    static func kind(of e: M3UEntry) -> Kind {
        let lower = e.url.lowercased()
        let path = lower.split(separator: "?", maxSplits: 1).first.map(String.init) ?? lower
        if path.contains("/series/") { return .episode }
        if path.contains("/movie/") { return .movie }
        if vodExtensions.contains(where: { path.hasSuffix($0) }) {
            return episodeMarker(in: e.name) != nil ? .episode : .movie
        }
        return .live
    }

    /// « Brumes S01 E03 », « Brumes S1E3 - Titre », « Brumes 1x03 » → (« Brumes », 1, 3, « Titre »).
    private static let episodePatterns: [NSRegularExpression] = [
        #"^(.*?)[\s\-–._]*[Ss](\d{1,2})[\s._]*[Ee](\d{1,3})(.*)$"#,
        #"^(.*?)[\s\-–._]+(\d{1,2})x(\d{1,3})(.*)$"#,
    ].compactMap { try? NSRegularExpression(pattern: $0) }

    static func episodeMarker(in name: String) -> (show: String, season: Int, episode: Int, title: String?)? {
        for re in episodePatterns {
            guard let m = re.firstMatch(in: name, range: NSRange(name.startIndex..., in: name)), m.numberOfRanges == 5,
                  let r1 = Range(m.range(at: 1), in: name), let r2 = Range(m.range(at: 2), in: name),
                  let r3 = Range(m.range(at: 3), in: name), let r4 = Range(m.range(at: 4), in: name),
                  let s = Int(name[r2]), let e = Int(name[r3]) else { continue }
            let show = name[r1].trimmingCharacters(in: CharacterSet(charactersIn: " -–._|:"))
            let rest = name[r4].trimmingCharacters(in: CharacterSet(charactersIn: " -–._|:"))
            guard !show.isEmpty else { continue }
            return (show, s, e, rest.isEmpty ? nil : rest)
        }
        return nil
    }

    /// Répartit une playlist en chaînes, films et séries (épisodes regroupés par série).
    static func catalog(from text: String) -> M3UCatalog {
        let parsed = parse(text)
        var cat = M3UCatalog(epgURL: parsed.epgURL)
        var liveGroups: [String] = [], vodGroups: [String] = [], serGroups: [String] = []
        var liveSeen = Set<String>(), vodSeen = Set<String>(), serSeen = Set<String>()
        var seenIds = Set<String>()
        // Séries : clé = groupe + nom de la série
        var showOrder: [String] = []
        var shows: [String: (group: String, name: String, logo: String?, episodes: [Episode])] = [:]

        for e in parsed.entries {
            let id = Ids.fromURL(e.url)
            switch kind(of: e) {
            case .live:
                guard seenIds.insert(id).inserted else { continue }
                if liveSeen.insert(e.group).inserted { liveGroups.append(e.group) }
                cat.live.append(LiveChannel(id: id, streamId: nil, rawName: e.name, logo: e.logo, categoryId: e.group,
                                            tvgId: e.tvgId, num: e.num, url: e.url, userAgent: e.userAgent))
            case .movie:
                guard seenIds.insert(id).inserted else { continue }
                if vodSeen.insert(e.group).inserted { vodGroups.append(e.group) }
                let ext = (e.url.split(separator: "?").first.map(String.init) ?? e.url).split(separator: ".").last.map(String.init) ?? "mp4"
                cat.movies.append(Movie(id: id, streamId: Ids.numericPart(id), rawName: e.name, poster: e.logo, rating: nil,
                                        added: nil, ext: ext.count <= 4 ? ext : "mp4", categoryId: e.group, url: e.url))
            case .episode:
                guard seenIds.insert(id).inserted else { continue }
                let marker = episodeMarker(in: e.name)
                let showName = marker?.show ?? e.group
                let key = e.group + "\u{1F}" + TextTools.fold(showName)
                if shows[key] == nil {
                    showOrder.append(key)
                    shows[key] = (e.group, showName, e.logo, [])
                    if serSeen.insert(e.group).inserted { serGroups.append(e.group) }
                }
                let number = marker?.episode ?? (shows[key]!.episodes.count + 1)
                let season = marker?.season ?? 1
                let ext = (e.url.split(separator: "?").first.map(String.init) ?? e.url).split(separator: ".").last.map(String.init) ?? "mkv"
                shows[key]!.episodes.append(Episode(
                    id: id, episodeId: Ids.numericPart(id) ?? id,
                    title: marker?.title ?? T("Épisode \(number)", "Episode \(number)"),
                    season: season, number: number, ext: ext.count <= 4 ? ext : "mkv",
                    plot: nil, image: e.logo, durationSecs: nil, url: e.url))
            }
        }
        for key in showOrder {
            guard let s = shows[key] else { continue }
            cat.series.append(Series(seriesId: Hash.fnv1a64Hex(key), rawName: s.name, cover: s.logo, plot: nil, genre: nil,
                                     rating: nil, categoryId: s.group, lastModified: nil, episodes: s.episodes))
        }
        cat.liveCategories = liveGroups.map { Category(id: $0, rawName: $0) }
        cat.vodCategories = vodGroups.map { Category(id: $0, rawName: $0) }
        cat.seriesCategories = serGroups.map { Category(id: $0, rawName: $0) }
        return cat
    }

    /// Saisons d'une série de playlist (épisodes déjà connus).
    static func details(of s: Series) -> SeriesDetails {
        var seasons: [Int: [Episode]] = [:]
        for e in s.episodes ?? [] { seasons[e.season, default: []].append(e) }
        for k in seasons.keys { seasons[k]!.sort { $0.number < $1.number } }
        return SeriesDetails(seasons: seasons, plot: s.plot, cover: s.cover, backdrop: nil, genre: s.genre,
                             rating: s.rating, year: s.year, cast: nil, director: nil)
    }
}
