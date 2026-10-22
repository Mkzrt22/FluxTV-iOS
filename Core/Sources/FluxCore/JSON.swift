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
}

extension Dictionary where Key == String, Value == Any {
    /// Texte non vide (les nombres sont convertis, "null" et "" donnent nil).
    func str(_ key: String) -> String? {
        guard let v = self[key] else { return nil }
        let s: String?
        switch v {
        case let x as String: s = x
        case let x as Bool where type(of: v) == Bool.self: s = x ? "1" : "0"
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
