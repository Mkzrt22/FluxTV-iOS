using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace FluxTV.Core
{
    /// <summary>
    /// Guide TV au format XMLTV (playlists M3U). On ne garde que les chaînes de la playlist et une
    /// fenêtre utile (3 h avant → 36 h après maintenant) : un guide complet pèse vite des centaines de Mo.
    /// </summary>
    public static class XmlTv
    {
        /// <summary>
        /// Identifiant de chaîne tel qu'il est comparé : « TF1.fr » dans la playlist et « tf1.fr » dans le
        /// guide désignent la même chaîne. Sans cela, le guide restait vide pour ces chaînes.
        /// </summary>
        public static string Key(string id) => (id ?? "").Trim().ToLowerInvariant();

        /// <summary>Le résultat est indexé par <see cref="Key"/>.</summary>
        public static Dictionary<string, List<Programme>> Parse(byte[] data, ISet<string> wanted, DateTimeOffset now)
        {
            using (var memory = new MemoryStream(data ?? new byte[0]))
            using (var stream = Gzip.IsGzip(data) ? Gzip.Reading(memory) : memory)
                return Parse(stream, wanted, now);
        }

        /// <summary>
        /// Guide lu en flux depuis le disque, décompressé au fil de la lecture : un guide de plusieurs
        /// centaines de Mo ne passe jamais en entier par la mémoire.
        /// </summary>
        public static Dictionary<string, List<Programme>> ParseFile(string path, ISet<string> wanted, DateTimeOffset now)
        {
            try
            {
                using (var stream = Gzip.OpenFile(path))
                    return Parse(stream, wanted, now);
            }
            catch (IOException) { return new Dictionary<string, List<Programme>>(StringComparer.Ordinal); }
            catch (UnauthorizedAccessException) { return new Dictionary<string, List<Programme>>(StringComparer.Ordinal); }
        }

        public static Dictionary<string, List<Programme>> Parse(Stream stream, ISet<string> wanted, DateTimeOffset now)
        {
            var from = now.AddHours(-3);
            var to = now.AddHours(36);
            var keys = new HashSet<string>(wanted.Select(Key), StringComparer.Ordinal);
            var result = new Dictionary<string, List<Programme>>(StringComparer.Ordinal);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                XmlResolver = null,
                CheckCharacters = false,
            };
            try
            {
                using (var r = XmlReader.Create(stream, settings))
                {
                    r.MoveToContent();
                    while (!r.EOF)
                    {
                        if (r.NodeType != XmlNodeType.Element || r.Name != "programme")
                        {
                            r.Read();
                            continue;
                        }
                        var channel = Key(r.GetAttribute("channel"));
                        var start = Date(r.GetAttribute("start"));
                        var stop = Date(r.GetAttribute("stop"));
                        bool keep = channel.Length > 0 && keys.Contains(channel) && start.HasValue && stop.HasValue
                                    && stop > start && stop >= from && start <= to;
                        if (!keep)
                        {
                            r.Skip();
                            continue;
                        }
                        string title = null, desc = null;
                        using (var sub = r.ReadSubtree())
                        {
                            sub.Read();
                            while (!sub.EOF)
                            {
                                if (sub.NodeType == XmlNodeType.Element && (sub.Name == "title" || sub.Name == "desc"))
                                {
                                    var element = sub.Name;
                                    string text;
                                    try { text = sub.ReadElementContentAsString(); }
                                    catch (XmlException) { text = null; sub.Skip(); }
                                    // Plusieurs <title lang="…"> par programme : on garde le premier
                                    if (element == "title" && title == null) title = text;
                                    else if (element == "desc" && desc == null) desc = text;
                                    continue;
                                }
                                sub.Read();
                            }
                        }
                        r.Read();
                        title = title?.Trim();
                        if (string.IsNullOrEmpty(title)) continue;
                        desc = desc?.Trim();
                        if (!result.TryGetValue(channel, out var list)) result[channel] = list = new List<Programme>();
                        list.Add(new Programme(start.Value, stop.Value, title, string.IsNullOrEmpty(desc) ? null : desc));
                    }
                }
            }
            catch (XmlException)
            {
                // Guide tronqué ou mal formé : on garde ce qui a été lu
            }
            catch (IOException)
            {
                // Fichier coupé pendant la lecture : idem
            }
            catch (InvalidDataException)
            {
                // Compression abîmée : idem
            }
            foreach (var k in result.Keys.ToList()) result[k] = result[k].OrderBy(p => p.Start).ToList();
            return result;
        }

        /// <summary>« 20261002203000 +0200 » (ou sans décalage : heure UTC).</summary>
        public static DateTimeOffset? Date(string raw)
        {
            raw = raw?.Trim();
            if (string.IsNullOrEmpty(raw)) return null;
            int n = 0;
            while (n < raw.Length && Ids.IsAsciiDigit(raw[n])) n++;
            if (n < 12) return null;
            int Num(int from, int len) => from + len <= n ? int.Parse(raw.Substring(from, len), CultureInfo.InvariantCulture) : 0;
            var offset = TimeSpan.Zero;
            var rest = raw.Substring(n).Trim();
            if (rest.Length >= 5 && (rest[0] == '+' || rest[0] == '-')
                && int.TryParse(rest.Substring(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hh)
                && int.TryParse(rest.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var mm))
            {
                offset = new TimeSpan(hh, mm, 0);
                if (rest[0] == '-') offset = offset.Negate();
            }
            try
            {
                return new DateTimeOffset(Num(0, 4), Num(4, 2), Num(6, 2), Num(8, 2), Num(10, 2), n >= 14 ? Num(12, 2) : 0, offset);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
