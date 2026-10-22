import Foundation
#if canImport(FoundationXML)
import FoundationXML
#endif

/**
 Guide TV au format XMLTV (playlists M3U). On ne garde que les chaînes de la playlist et
 une fenêtre utile (3 h avant → 36 h après maintenant) : un guide complet pèse vite des
 centaines de Mo.
 */
enum XMLTV {

    static func parse(_ data: Data, wanted: Set<String>, now: Date = Date()) -> [String: [Programme]] {
        let body = Gzip.isGzip(data) ? (Gzip.decompress(data) ?? Data()) : data
        let delegate = Collector(wanted: wanted, from: now.addingTimeInterval(-3 * 3600), to: now.addingTimeInterval(36 * 3600))
        let parser = XMLParser(data: body)
        parser.delegate = delegate
        parser.shouldProcessNamespaces = false
        parser.shouldResolveExternalEntities = false
        _ = parser.parse()
        var out = delegate.result
        for k in out.keys { out[k]!.sort { $0.start < $1.start } }
        return out
    }

    /// « 20261002203000 +0200 » (ou sans décalage : heure UTC).
    static func date(_ raw: String?) -> Date? {
        guard let raw = raw?.trimmingCharacters(in: .whitespaces) else { return nil }
        let digits = Array(raw.prefix { $0.isASCIIDigit })
        guard digits.count >= 12 else { return nil }
        func num(_ from: Int, _ len: Int) -> Int? {
            guard from + len <= digits.count else { return nil }
            return Int(String(digits[from..<(from + len)]))
        }
        var c = DateComponents()
        c.year = num(0, 4); c.month = num(4, 2); c.day = num(6, 2)
        c.hour = num(8, 2); c.minute = num(10, 2); c.second = num(12, 2) ?? 0
        var offset = 0
        let rest = raw.dropFirst(digits.count).trimmingCharacters(in: .whitespaces)
        if rest.count >= 5, let sign = rest.first, sign == "+" || sign == "-",
           let hh = Int(rest.dropFirst().prefix(2)), let mm = Int(rest.dropFirst(3).prefix(2)) {
            offset = (hh * 3600 + mm * 60) * (sign == "-" ? -1 : 1)
        }
        guard let tz = TimeZone(secondsFromGMT: offset) else { return nil }
        c.timeZone = tz
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = tz
        return cal.date(from: c)
    }

    private final class Collector: NSObject, XMLParserDelegate {
        let wanted: Set<String>
        let from: Date
        let to: Date
        var result: [String: [Programme]] = [:]

        private var channel: String?
        private var start: Date?
        private var stop: Date?
        private var keep = false
        private var title = ""
        private var desc = ""
        private var titleCount = 0
        private var descCount = 0
        private var element = ""

        init(wanted: Set<String>, from: Date, to: Date) {
            self.wanted = wanted
            self.from = from
            self.to = to
        }

        func parser(_ parser: XMLParser, didStartElement elementName: String, namespaceURI: String?,
                    qualifiedName qName: String?, attributes attributeDict: [String: String] = [:]) {
            element = elementName
            switch elementName {
            case "programme":
                channel = attributeDict["channel"]
                start = XMLTV.date(attributeDict["start"])
                stop = XMLTV.date(attributeDict["stop"])
                title = ""; desc = ""; titleCount = 0; descCount = 0
                if let ch = channel, wanted.contains(ch), let s = start, let e = stop, e > s, e >= from, s <= to {
                    keep = true
                } else {
                    keep = false
                }
            case "title": titleCount += 1
            case "desc": descCount += 1
            default: break
            }
        }

        func parser(_ parser: XMLParser, foundCharacters string: String) {
            guard keep else { return }
            // Plusieurs <title lang="…"> par programme : on ne garde que le premier
            if element == "title", titleCount == 1 { title += string }
            else if element == "desc", descCount == 1 { desc += string }
        }

        func parser(_ parser: XMLParser, didEndElement elementName: String, namespaceURI: String?, qualifiedName qName: String?) {
            if elementName == "programme" {
                let t = title.trimmingCharacters(in: .whitespacesAndNewlines)
                if keep, !t.isEmpty, let ch = channel, let s = start, let e = stop {
                    let d = desc.trimmingCharacters(in: .whitespacesAndNewlines)
                    result[ch, default: []].append(Programme(start: s, stop: e, title: t, desc: d.isEmpty ? nil : d))
                }
                keep = false
            }
            element = ""
        }
    }
}
