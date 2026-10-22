import Foundation

/**
 Outils texte : recherche insensible aux accents, tri à la française, et surtout nettoyage
 des noms envoyés par les fournisseurs IPTV.

    « FR| TF1 FHD »                → « TF1 » + badge FHD
    « |FR| CANAL+ SPORT ᵁᴴᴰ »      → « CANAL+ SPORT » + badge UHD
    « FR - Dune (2021) [MULTI] »   → « Dune », 2021, badge MULTI
    « FR | SPORTS » (catégorie)    → « Sports »
 */
enum TextTools {

    // ───────────────────────────── Recherche, tri ─────────────────────────────

    /// "Les Misérables" → "les miserables"
    static func fold(_ s: String) -> String {
        s.folding(options: [.diacriticInsensitive, .caseInsensitive, .widthInsensitive], locale: Locale(identifier: "fr_FR"))
    }

    /// Tri A→Z à la française (« Été » entre « Eau » et « Fargo »), nombres dans l'ordre naturel.
    static func frenchLess(_ a: String, _ b: String) -> Bool {
        let fa = fold(a), fb = fold(b)
        if fa != fb { return fa.compare(fb, options: [.numeric]) == .orderedAscending }
        return a < b
    }

    /// Tous les mots de la recherche figurent dans le texte (ordre libre, accents ignorés).
    static func matches(_ text: String, query words: [String]) -> Bool {
        guard !words.isEmpty else { return true }
        let t = fold(text)
        return words.allSatisfy { t.contains($0) }
    }

    static func queryWords(_ q: String) -> [String] {
        fold(q).split(whereSeparator: { $0 == " " || $0 == "\t" }).map(String.init)
    }

    // ───────────────────────────── Contenu adulte ─────────────────────────────

    private static let adultRegex = try! NSRegularExpression(
        pattern: #"(\badult|\bxxx\b|\bporn|\bsex\b|\bx-rated|18\+|\bnsfw\b|hentai|\bxx\b|\+18\b)"#,
        options: [.caseInsensitive])

    static func isAdult(_ label: String) -> Bool {
        adultRegex.firstMatch(in: label, range: NSRange(label.startIndex..., in: label)) != nil
    }

    // ───────────────────────────── Nettoyage des noms ─────────────────────────────

    /// Préfixes de pays / de langue / de bouquet placés devant les noms par les fournisseurs.
    private static let prefixCodes: Set<String> = [
        "FR", "FRA", "FRANCE", "BE", "BEL", "CH", "SUI", "CA", "QC", "LU", "MC", "UK", "GB", "US", "USA", "EN", "ENG",
        "DE", "GER", "ES", "ESP", "IT", "ITA", "PT", "NL", "AR", "ARA", "TR", "AF", "AFR", "MA", "DZ", "TN", "PL",
        "RO", "RU", "IN", "VIP", "VOD", "TV", "LIVE", "FILM", "FILMS", "SERIE", "SERIES", "SÉRIES", "MULTI", "NF", "AMZ", "DSNP",
    ]

    private static let separators = CharacterSet(charactersIn: "|:-–—▎┃⎮│»•✦★⭐︱｜/")

    /// Petits caractères « exposants » utilisés comme étiquettes de qualité.
    private static let superscripts: [(String, String)] = [
        ("ᵁᴴᴰ", "UHD"), ("ᶠᴴᴰ", "FHD"), ("ᴴᴰ", "HD"), ("ˢᴰ", "SD"), ("⁴ᴷ", "4K"), ("ᴴᴱⱽᶜ", "HEVC"), ("ᴿᴬᵂ", "RAW"),
        ("³⁸⁴⁰ᴾ", "4K"), ("¹⁰⁸⁰ᴾ", "FHD"), ("⁷²⁰ᴾ", "HD"), ("ᴴᴰᴿ", "HDR"), ("⁶⁰ᶠᵖˢ", "60FPS"), ("⁵⁰ᶠᵖˢ", "50FPS"),
    ]

    /// Étiquettes de qualité reconnues comme mots entiers → badge affiché.
    private static let qualityTags: [String: String] = [
        "UHD": "4K", "4K": "4K", "2160P": "4K", "4K+": "4K", "FHD": "FHD", "FULLHD": "FHD", "1080P": "FHD", "1080": "FHD",
        "HD": "HD", "720P": "HD", "HD+": "HD", "SD": "SD", "HEVC": "HEVC", "H265": "HEVC", "H.265": "HEVC", "X265": "HEVC",
        "HDR": "HDR", "HDR10": "HDR", "DV": "HDR", "50FPS": "50 i/s", "60FPS": "60 i/s", "RAW": "RAW", "LQ": "SD", "HQ": "HD",
        "BACKUP": "", "ALT": "", "(BACKUP)": "",
    ]

    /// Étiquettes de langue des films / séries.
    private static let languageTags: [String: String] = [
        "MULTI": "MULTI", "VOSTFR": "VOSTFR", "VOST": "VOSTFR", "VF": "VF", "VFF": "VF", "VFQ": "VFQ", "VO": "VO",
        "TRUEFRENCH": "VF", "FRENCH": "VF", "SUBFRENCH": "VOSTFR",
    ]

    private static let qualityOrder = ["4K", "FHD", "HD", "SD"]

    /// Nom de chaîne nettoyé + badges (qualité, codec).
    static func cleanChannel(_ raw: String) -> (name: String, badges: [String]) {
        var badges: [String] = []
        var s = expandSuperscripts(raw, into: &badges)
        s = stripPrefixes(s)
        var words = splitWords(s)
        // Étiquettes de qualité, en fin de nom (« TF1 FHD », « M6 HD 50FPS »)
        while let last = words.last {
            let key = last.uppercased().trimmingCharacters(in: CharacterSet(charactersIn: "()[]|"))
            guard let tag = qualityTags[key] else { break }
            if !tag.isEmpty { badges.append(tag) }
            words.removeLast()
        }
        let name = words.joined(separator: " ").trimmingCharacters(in: separators.union(.whitespaces))
        return (name.isEmpty ? raw.trimmingCharacters(in: .whitespaces) : name, normalizeBadges(badges))
    }

    /// Titre de film ou de série nettoyé : année et étiquettes à part.
    static func cleanTitle(_ raw: String) -> (name: String, year: Int?, badges: [String]) {
        var badges: [String] = []
        var s = expandSuperscripts(raw, into: &badges)
        s = stripPrefixes(s)
        var year: Int?

        // Étiquettes entre crochets ou parenthèses : [MULTI], (VOSTFR), (2021), [4K]
        s = replaceGroups(in: s) { inner in
            let key = inner.uppercased().trimmingCharacters(in: .whitespaces)
            if key.count == 4, let y = Int(key), (1900...2100).contains(y) { year = year ?? y; return "" }
            if let q = qualityTags[key] { if !q.isEmpty { badges.append(q) }; return "" }
            if let l = languageTags[key] { badges.append(l); return "" }
            return nil
        }

        var words = splitWords(s)
        // Étiquettes en fin de titre : « Oppenheimer 4K », « Dune MULTI », « Titre - 2021 »
        while let last = words.last {
            let key = last.uppercased()
            if let q = qualityTags[key] { if !q.isEmpty { badges.append(q) }; words.removeLast(); continue }
            if let l = languageTags[key] { badges.append(l); words.removeLast(); continue }
            if words.count >= 3, key.count == 4, let y = Int(key), (1900...2100).contains(y),
               words[words.count - 2] == "-" || words[words.count - 2] == "–" {
                year = year ?? y
                words.removeLast(2)
                continue
            }
            if last == "-" || last == "–" || last == "|" { words.removeLast(); continue }
            break
        }
        let name = words.joined(separator: " ").trimmingCharacters(in: separators.union(.whitespaces))
        return (name.isEmpty ? raw.trimmingCharacters(in: .whitespaces) : name, year, normalizeBadges(badges))
    }

    /// Nom de catégorie : « FR | SPORTS » → « Sports », « |FR| CINÉMA 4K » → « Cinéma 4K ».
    static func cleanCategory(_ raw: String) -> String {
        var ignored: [String] = []
        let s = stripPrefixes(expandSuperscripts(raw, into: &ignored))
            .trimmingCharacters(in: separators.union(.whitespaces))
        guard !s.isEmpty else { return raw.trimmingCharacters(in: .whitespaces) }
        return titleCaseIfShouting(s)
    }

    // ───────────────────────────── Outils internes ─────────────────────────────

    private static func expandSuperscripts(_ s: String, into badges: inout [String]) -> String {
        var out = s
        for (sup, tag) in superscripts where out.contains(sup) {
            out = out.replacingOccurrences(of: sup, with: " ")
            badges.append(tag)
        }
        return out
    }

    /// Retire « FR| », « |FR| », « [FR] », « (FR) », « FR: », « FR - », « FR ▎ » en tête (plusieurs fois).
    static func stripPrefixes(_ input: String) -> String {
        var s = input.trimmingCharacters(in: .whitespaces)
        for _ in 0..<3 {
            let before = s
            // Formes encadrées : |FR|  [FR]  (FR)  ┃FR┃
            if let first = s.first, "|[(┃│".contains(first) {
                let closing: Character = first == "[" ? "]" : (first == "(" ? ")" : first)
                let rest = s.dropFirst()
                if let end = rest.firstIndex(of: closing) {
                    let code = rest[rest.startIndex..<end].trimmingCharacters(in: .whitespaces).uppercased()
                    if prefixCodes.contains(code) || (code.count == 2 && code.allSatisfy(\.isLetter)) {
                        s = String(rest[rest.index(after: end)...]).trimmingCharacters(in: separators.union(.whitespaces))
                    }
                }
            }
            // Forme libre : FR| …  FR: …  FR - …  FR ▎…
            if let sepIdx = s.unicodeScalars.firstIndex(where: { separators.contains($0) }) {
                let head = String(s.unicodeScalars[s.unicodeScalars.startIndex..<sepIdx]).trimmingCharacters(in: .whitespaces)
                if !head.isEmpty, head.count <= 8, prefixCodes.contains(head.uppercased()) {
                    let tail = String(s.unicodeScalars[sepIdx...])
                    s = tail.trimmingCharacters(in: separators.union(.whitespaces))
                }
            }
            if s == before { break }
        }
        return s.isEmpty ? input.trimmingCharacters(in: .whitespaces) : s
    }

    private static func splitWords(_ s: String) -> [String] {
        s.split(whereSeparator: { $0 == " " || $0 == "\t" || $0 == "\u{00A0}" }).map(String.init)
    }

    /// Remplace chaque groupe [..] ou (..) par le résultat de `transform` (nil = laisser tel quel).
    private static func replaceGroups(in s: String, _ transform: (String) -> String?) -> String {
        var out = ""
        var i = s.startIndex
        while i < s.endIndex {
            let c = s[i]
            if c == "[" || c == "(" {
                let closing: Character = c == "[" ? "]" : ")"
                if let end = s[s.index(after: i)...].firstIndex(of: closing) {
                    let inner = String(s[s.index(after: i)..<end])
                    if let replacement = transform(inner) {
                        out += replacement.isEmpty ? " " : replacement
                    } else {
                        out += String(s[i...end])
                    }
                    i = s.index(after: end)
                    continue
                }
            }
            out.append(c)
            i = s.index(after: i)
        }
        return out
    }

    private static func normalizeBadges(_ badges: [String]) -> [String] {
        var seen = Set<String>()
        var out: [String] = []
        // Une seule qualité principale (la meilleure), puis les autres étiquettes
        if let best = qualityOrder.first(where: { badges.contains($0) }) { out.append(best); seen.formUnion(qualityOrder) }
        for b in badges where !seen.contains(b) {
            seen.insert(b)
            out.append(b)
        }
        return out
    }

    /// « SPORTS » → « Sports », « CINÉMA ET SÉRIES » → « Cinéma et séries » ; les noms en
    /// casse mixte (« Canal+ », « Disney+ ») et les sigles courts (« TNT ») restent tels quels.
    private static func titleCaseIfShouting(_ s: String) -> String {
        let letters = s.unicodeScalars.filter { CharacterSet.letters.contains($0) }
        guard letters.count > 3, s == s.uppercased() else { return s }
        let words = splitWords(s)
        let small: Set<String> = ["ET", "DE", "DU", "DES", "LA", "LE", "LES", "EN", "À", "A", "AU", "AUX", "&"]
        return words.enumerated().map { i, w -> String in
            let up = w.uppercased()
            if w.count <= 3, w.unicodeScalars.contains(where: { CharacterSet.decimalDigits.contains($0) }) { return w }
            if small.contains(up) { return i == 0 ? w.prefix(1).uppercased() + w.dropFirst().lowercased() : w.lowercased() }
            // Mot court tout en capitales : probablement un sigle (TNT, RMC, XXX)
            if w.count <= 3, w.allSatisfy(\.isLetter) { return w }
            return w.prefix(1).uppercased() + w.dropFirst().lowercased()
        }.joined(separator: " ")
    }
}
