import Foundation

/**
 Lecture tolérante des réponses Xtream : selon les panels, un même champ arrive en texte,
 en nombre, à null, voire en tableau. On lit donc tout « à la main », sans jamais faire
 échouer toute une liste pour un élément mal formé.
 */
typealias JSONObject = [String: Any]

enum JSON {
    static func parse(_ data: Data) -> Any? {
        try? JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed])
    }

    static func object(_ data: Data) -> JSONObject? { parse(data) as? JSONObject }
    static func array(_ data: Data) -> [Any]? { parse(data) as? [Any] }

    /**
     Objets d'une grande liste, lus un par un. La liste de tous les films d'un gros fournisseur
     dépasse 100 Mo : transformée d'un bloc en arbre d'objets, elle en occupait dix fois plus et
     l'app pouvait être fermée par le système. Ici, seul l'objet en cours est en mémoire.
     Accepte un tableau, ou un objet dont les valeurs sont les éléments (certains panels).
     Une liste coupée en route donne ce qui a été lu.
     */
    static func eachObject(in data: Data, _ body: (JSONObject) -> Void) {
        data.withUnsafeBytes { (raw: UnsafeRawBufferPointer) in
            guard let base = raw.baseAddress else { return }
            let bytes = raw.bindMemory(to: UInt8.self)
            var depth = 0                      // accolades et crochets ouverts
            var inString = false, escaped = false
            var start = -1                     // début de l'élément en cours (niveau 1)
            for i in 0..<bytes.count {
                let c = bytes[i]
                if inString {
                    if escaped { escaped = false }
                    else if c == 0x5C { escaped = true }            // \
                    else if c == 0x22 { inString = false }           // "
                    continue
                }
                switch c {
                case 0x22: inString = true                           // "
                case 0x7B:                                           // {
                    if depth == 1 { start = i }
                    depth += 1
                case 0x5B: depth += 1                                // [
                case 0x7D:                                           // }
                    depth -= 1
                    if depth == 1, start >= 0 {
                        let item = Data(bytes: base.advanced(by: start), count: i - start + 1)
                        if let o = (try? JSONSerialization.jsonObject(with: item)) as? JSONObject { body(o) }
                        start = -1
                    }
                case 0x5D: depth -= 1                                // ]
                default: break
                }
                if depth < 0 { return }
            }
        }
    }

    /// true ou false du JSON (et non un nombre 0 ou 1).
    static func bool(_ value: Any) -> Bool? {
        #if canImport(Darwin)
        guard let n = value as? NSNumber, CFGetTypeID(n) == CFBooleanGetTypeID() else { return nil }
        return n.boolValue
        #else
        if type(of: value) == Bool.self { return value as? Bool }
        guard let n = value as? NSNumber, String(cString: n.objCType) == "c" || String(cString: n.objCType) == "B" else { return nil }
        return n.boolValue
        #endif
    }
}

extension Dictionary where Key == String, Value == Any {
    /// Texte non vide (les nombres sont convertis, "null" et "" donnent nil).
    func str(_ key: String) -> String? {
        guard let v = self[key] else { return nil }
        let s: String?
        // « false » là où un texte est attendu (logo, nom…) veut dire « rien », pas « 0 »
        if let b = JSON.bool(v) { return b ? "1" : nil }
        switch v {
        case let x as String: s = x
        case let x as Int: s = String(x)
        case let x as Double: s = x == x.rounded() && abs(x) < 1e15 ? String(Int(x)) : String(x)
        case let x as NSNumber: s = x.stringValue
        default: s = nil
        }
        guard let t = s?.trimmingCharacters(in: .whitespacesAndNewlines), !t.isEmpty, t != "null" else { return nil }
        return t
    }

    func int(_ key: String) -> Int? {
        guard let s = str(key) else { return nil }
        if let i = Int(s) { return i }
        if let d = Double(s), d.isFinite { return Int(d) }
        return nil
    }

    func double(_ key: String) -> Double? {
        guard let s = str(key)?.replacingOccurrences(of: ",", with: "."), let d = Double(s), d.isFinite else { return nil }
        return d
    }

    /// "1", 1 ou true.
    func flag(_ key: String) -> Bool {
        guard let s = str(key)?.lowercased() else { return false }
        return s == "1" || s == "true" || s == "yes"
    }

    func obj(_ key: String) -> JSONObject? { self[key] as? JSONObject }
    func arr(_ key: String) -> [Any]? { self[key] as? [Any] }

    /// Date en secondes Unix (texte ou nombre).
    func unixDate(_ key: String) -> Date? {
        guard let t = int(key), t > 0 else { return nil }
        return Date(timeIntervalSince1970: TimeInterval(t))
    }

    /// Première adresse http d'un champ « texte ou tableau de textes » (backdrop_path…).
    func firstURL(_ key: String) -> String? {
        switch self[key] {
        case let s as String:
            return s.hasPrefix("http") ? s : nil
        case let a as [Any]:
            return a.compactMap { $0 as? String }.first { $0.hasPrefix("http") }
        default:
            return nil
        }
    }

    /// Note sur 10 (« 7.5 », 7.5, « 0 » → nil).
    func rating(_ key: String) -> Double? {
        guard let d = double(key), d > 0 else { return nil }
        return d > 10 ? d / 10 : d
    }
}
