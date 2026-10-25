using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FluxTV.Core
{
    /// <summary>
    /// Outils texte : recherche insensible aux accents, tri à la française, et surtout nettoyage
    /// des noms envoyés par les fournisseurs IPTV.
    ///
    ///    « FR| TF1 FHD »                → « TF1 » + badge FHD
    ///    « |FR| CANAL+ SPORT ᵁᴴᴰ »      → « CANAL+ SPORT » + badge 4K
    ///    « FR - Dune (2021) [MULTI] »   → « Dune », 2021, badge MULTI
    ///    « FR | SPORTS » (catégorie)    → « Sports »
    /// </summary>
    public static class TextTools
    {
        // ───────────────────────────── Recherche, tri ─────────────────────────────

        /// <summary>"Les Misérables" → "les miserables" (accents, casse et largeur ignorés).</summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var d = s.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder(d.Length);
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Tri A→Z à la française (« Été » entre « Eau » et « Fargo »), nombres dans l'ordre naturel.</summary>
        public static int FrenchCompare(string a, string b)
        {
            string fa = Fold(a), fb = Fold(b);
            int i = 0, j = 0;
            while (i < fa.Length && j < fb.Length)
            {
                if (char.IsDigit(fa[i]) && char.IsDigit(fb[j]))
                {
                    int si = i, sj = j;
                    while (i < fa.Length && char.IsDigit(fa[i])) i++;
                    while (j < fb.Length && char.IsDigit(fb[j])) j++;
                    string na = fa.Substring(si, i - si).TrimStart('0'), nb = fb.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length.CompareTo(nb.Length);
                    int c = string.CompareOrdinal(na, nb);
                    if (c != 0) return c;
                    continue;
                }
                if (fa[i] != fb[j]) return fa[i].CompareTo(fb[j]);
                i++;
                j++;
            }
            int rest = (fa.Length - i).CompareTo(fb.Length - j);
            return rest != 0 ? rest : string.CompareOrdinal(a ?? "", b ?? "");
        }

        /// <summary>Tous les mots de la recherche figurent dans le texte (ordre libre, accents ignorés).</summary>
        public static bool Matches(string text, IList<string> words)
        {
            if (words == null || words.Count == 0) return true;
            var t = Fold(text);
            foreach (var w in words)
                if (t.IndexOf(w, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        public static List<string> QueryWords(string q) =>
            Fold(q).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // ───────────────────────────── Contenu adulte ─────────────────────────────

        static readonly Regex AdultRegex = new Regex(
            @"(\badult|\bxxx\b|\bporn|\bsex\b|\bx-rated|18\+|\bnsfw\b|hentai|\bxx\b|\+18\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool IsAdult(string label) => !string.IsNullOrEmpty(label) && AdultRegex.IsMatch(label);

        // ───────────────────────────── Nettoyage des noms ─────────────────────────────

        /// <summary>Préfixes de pays / de langue / de bouquet placés devant les noms par les fournisseurs.</summary>
        static readonly HashSet<string> PrefixCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "FR", "FRA", "FRANCE", "BE", "BEL", "CH", "SUI", "CA", "QC", "LU", "MC", "UK", "GB", "US", "USA", "EN", "ENG",
            "DE", "GER", "ES", "ESP", "IT", "ITA", "PT", "NL", "AR", "ARA", "TR", "AF", "AFR", "MA", "DZ", "TN", "PL",
            "RO", "RU", "IN", "VIP", "VOD", "TV", "LIVE", "FILM", "FILMS", "SERIE", "SERIES", "SÉRIES", "MULTI", "NF", "AMZ", "DSNP",
        };

        const string Separators = "|:-–—▎┃⎮│»•✦★⭐︱｜/";

        /// <summary>Petits caractères « exposants » utilisés comme étiquettes de qualité.</summary>
        static readonly (string Sup, string Tag)[] Superscripts =
        {
            ("ᵁᴴᴰ", "UHD"), ("ᶠᴴᴰ", "FHD"), ("ᴴᴰᴿ", "HDR"), ("ᴴᴰ", "HD"), ("ˢᴰ", "SD"), ("⁴ᴷ", "4K"), ("ᴴᴱⱽᶜ", "HEVC"), ("ᴿᴬᵂ", "RAW"),
            ("³⁸⁴⁰ᴾ", "4K"), ("¹⁰⁸⁰ᴾ", "FHD"), ("⁷²⁰ᴾ", "HD"), ("⁶⁰ᶠᵖˢ", "60FPS"), ("⁵⁰ᶠᵖˢ", "50FPS"),
        };

        /// <summary>Étiquettes de qualité reconnues comme mots entiers → badge affiché ("" = retirée sans badge).</summary>
        static readonly Dictionary<string, string> QualityTags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UHD"] = "4K", ["4K"] = "4K", ["2160P"] = "4K", ["4K+"] = "4K", ["FHD"] = "FHD", ["FULLHD"] = "FHD", ["1080P"] = "FHD",
            ["1080"] = "FHD", ["HD"] = "HD", ["720P"] = "HD", ["HD+"] = "HD", ["SD"] = "SD", ["HEVC"] = "HEVC", ["H265"] = "HEVC",
            ["H.265"] = "HEVC", ["X265"] = "HEVC", ["HDR"] = "HDR", ["HDR10"] = "HDR", ["DV"] = "HDR", ["50FPS"] = "50 i/s",
            ["60FPS"] = "60 i/s", ["RAW"] = "RAW", ["LQ"] = "SD", ["HQ"] = "HD", ["BACKUP"] = "", ["ALT"] = "", ["(BACKUP)"] = "",
        };

        /// <summary>Étiquettes de langue des films / séries.</summary>
        static readonly Dictionary<string, string> LanguageTags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MULTI"] = "MULTI", ["VOSTFR"] = "VOSTFR", ["VOST"] = "VOSTFR", ["VF"] = "VF", ["VFF"] = "VF", ["VFQ"] = "VFQ",
            ["VO"] = "VO", ["TRUEFRENCH"] = "VF", ["FRENCH"] = "VF", ["SUBFRENCH"] = "VOSTFR",
        };

        static readonly string[] QualityOrder = { "4K", "FHD", "HD", "SD" };

        /// <summary>Nom de chaîne nettoyé + badges (qualité, codec).</summary>
        public static (string Name, List<string> Badges) CleanChannel(string raw)
        {
            raw ??= "";
            var badges = new List<string>();
            var s = StripPrefixes(ExpandSuperscripts(raw, badges));
            var words = SplitWords(s);
            // Étiquettes de qualité en fin de nom (« TF1 FHD », « M6 HD 50FPS »)
            while (words.Count > 0)
            {
                var key = words[words.Count - 1].ToUpperInvariant().Trim('(', ')', '[', ']', '|');
                if (!QualityTags.TryGetValue(key, out var tag)) break;
                if (tag.Length > 0) badges.Add(tag);
                words.RemoveAt(words.Count - 1);
            }
            var name = TrimSeparators(string.Join(" ", words));
            return (name.Length == 0 ? raw.Trim() : name, NormalizeBadges(badges));
        }

        /// <summary>Titre de film ou de série nettoyé : année et étiquettes à part.</summary>
        public static (string Name, int? Year, List<string> Badges) CleanTitle(string raw)
        {
            raw ??= "";
            var badges = new List<string>();
            var s = StripPrefixes(ExpandSuperscripts(raw, badges));
            int? year = null;

            // Étiquettes entre crochets ou parenthèses : [MULTI], (VOSTFR), (2021), [4K]
            s = ReplaceGroups(s, inner =>
            {
                var key = inner.Trim().ToUpperInvariant();
                if (key.Length == 4 && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y >= 1900 && y <= 2100)
                {
                    year ??= y;
                    return "";
                }
                if (QualityTags.TryGetValue(key, out var q)) { if (q.Length > 0) badges.Add(q); return ""; }
                if (LanguageTags.TryGetValue(key, out var l)) { badges.Add(l); return ""; }
                return null;
            });

            var words = SplitWords(s);
            // Étiquettes en fin de titre : « Oppenheimer 4K », « Dune MULTI », « Titre - 2021 »
            while (words.Count > 0)
            {
                var last = words[words.Count - 1];
                var key = last.ToUpperInvariant();
                if (QualityTags.TryGetValue(key, out var q)) { if (q.Length > 0) badges.Add(q); words.RemoveAt(words.Count - 1); continue; }
                if (LanguageTags.TryGetValue(key, out var l)) { badges.Add(l); words.RemoveAt(words.Count - 1); continue; }
                if (words.Count >= 3 && key.Length == 4 && int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var y)
                    && y >= 1900 && y <= 2100 && (words[words.Count - 2] == "-" || words[words.Count - 2] == "–"))
                {
                    year ??= y;
                    words.RemoveRange(words.Count - 2, 2);
                    continue;
                }
                if (last == "-" || last == "–" || last == "|") { words.RemoveAt(words.Count - 1); continue; }
                break;
            }
            var name = TrimSeparators(string.Join(" ", words));
            return (name.Length == 0 ? raw.Trim() : name, year, NormalizeBadges(badges));
        }

        /// <summary>Nom de catégorie : « FR | SPORTS » → « Sports », « |FR| CINÉMA 4K » → « Cinéma 4K ».</summary>
        public static string CleanCategory(string raw)
        {
            raw ??= "";
            var s = TrimSeparators(StripPrefixes(ExpandSuperscripts(raw, new List<string>())));
            if (s.Length == 0) return raw.Trim();
            return TitleCaseIfShouting(s);
        }

        // ───────────────────────────── Outils internes ─────────────────────────────

        static string ExpandSuperscripts(string s, List<string> badges)
        {
            foreach (var (sup, tag) in Superscripts)
            {
                if (s.IndexOf(sup, StringComparison.Ordinal) < 0) continue;
                s = s.Replace(sup, " ");
                badges.Add(tag);
            }
            return s;
        }

        static bool IsSeparator(char c) => Separators.IndexOf(c) >= 0;

        static string TrimSeparators(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int a = 0, b = s.Length - 1;
            while (a <= b && (char.IsWhiteSpace(s[a]) || IsSeparator(s[a]))) a++;
            while (b >= a && (char.IsWhiteSpace(s[b]) || IsSeparator(s[b]))) b--;
            return a > b ? "" : s.Substring(a, b - a + 1);
        }

        /// <summary>Retire « FR| », « |FR| », « [FR] », « (FR) », « FR: », « FR - », « FR ▎ » en tête (plusieurs fois).</summary>
        public static string StripPrefixes(string input)
        {
            input ??= "";
            var s = input.Trim();
            for (int pass = 0; pass < 3; pass++)
            {
                var before = s;
                // Formes encadrées : |FR|  [FR]  (FR)  ┃FR┃
                if (s.Length > 0 && "|[(┃│".IndexOf(s[0]) >= 0)
                {
                    char first = s[0];
                    char closing = first == '[' ? ']' : first == '(' ? ')' : first;
                    int end = s.IndexOf(closing, 1);
                    if (end > 0)
                    {
                        var code = s.Substring(1, end - 1).Trim().ToUpperInvariant();
                        if (PrefixCodes.Contains(code) || (code.Length == 2 && code.All(char.IsLetter)))
                            s = TrimSeparators(s.Substring(end + 1));
                    }
                }
                // Forme libre : FR| …  FR: …  FR - …  FR ▎…
                int sep = -1;
                for (int k = 0; k < s.Length; k++)
                    if (IsSeparator(s[k])) { sep = k; break; }
                if (sep > 0)
                {
                    var head = s.Substring(0, sep).Trim();
                    if (head.Length > 0 && head.Length <= 8 && PrefixCodes.Contains(head.ToUpperInvariant()))
                        s = TrimSeparators(s.Substring(sep));
                }
                if (s == before) break;
            }
            return s.Length == 0 ? input.Trim() : s;
        }

        static List<string> SplitWords(string s) =>
            s.Split(new[] { ' ', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        /// <summary>Remplace chaque groupe [..] ou (..) par le résultat de transform (null = laisser tel quel).</summary>
        static string ReplaceGroups(string s, Func<string, string> transform)
        {
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '[' || c == '(')
                {
                    char closing = c == '[' ? ']' : ')';
                    int end = s.IndexOf(closing, i + 1);
                    if (end > i)
                    {
                        var inner = s.Substring(i + 1, end - i - 1);
                        var replacement = transform(inner);
                        if (replacement != null) sb.Append(replacement.Length == 0 ? " " : replacement);
                        else sb.Append(s, i, end - i + 1);
                        i = end + 1;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        static List<string> NormalizeBadges(List<string> badges)
        {
            var seen = new HashSet<string>();
            var output = new List<string>();
            // Une seule qualité principale (la meilleure), puis les autres étiquettes
            var best = QualityOrder.FirstOrDefault(badges.Contains);
            if (best != null)
            {
                output.Add(best);
                foreach (var q in QualityOrder) seen.Add(q);
            }
            foreach (var b in badges)
                if (seen.Add(b)) output.Add(b);
            return output;
        }

        static readonly HashSet<string> SmallWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "ET", "DE", "DU", "DES", "LA", "LE", "LES", "EN", "À", "A", "AU", "AUX", "&",
        };

        /// <summary>
        /// « SPORTS » → « Sports », « CINÉMA ET SÉRIES » → « Cinéma et Séries » ; les noms en casse
        /// mixte (« Canal+ ») et les sigles courts (« TNT ») restent tels quels.
        /// </summary>
        static string TitleCaseIfShouting(string s)
        {
            if (s.Count(char.IsLetter) <= 3 || s != s.ToUpperInvariant()) return s;
            var words = SplitWords(s);
            for (int i = 0; i < words.Count; i++)
            {
                var w = words[i];
                if (w.Length <= 3 && w.Any(char.IsDigit)) continue;
                if (SmallWords.Contains(w.ToUpperInvariant())) { words[i] = i == 0 ? Capitalize(w) : w.ToLowerInvariant(); continue; }
                // Mot court tout en capitales : probablement un sigle (TNT, RMC, XXX)
                if (w.Length <= 3 && w.All(char.IsLetter)) continue;
                words[i] = Capitalize(w);
            }
            return string.Join(" ", words);
        }

        public static string Capitalize(string w) =>
            string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant();
    }

    /// <summary>Durées, heures et dates lisibles, en français.</summary>
    public static class Format
    {
        public static readonly CultureInfo French = new CultureInfo("fr-FR");

        /// <summary>« 1 h 58 », « 42 min ».</summary>
        public static string Duration(double seconds)
        {
            int s = (int)Math.Max(0, Math.Min(seconds, int.MaxValue));
            int h = s / 3600, m = s % 3600 / 60;
            return h > 0 ? h + " h " + m.ToString("00", CultureInfo.InvariantCulture) : m + " min";
        }

        /// <summary>Bouton de reprise : « Reprendre à 42 min », ou « Reprendre » avant la première minute.</summary>
        public static string Resume(double seconds) => seconds < 60 ? "Reprendre" : "Reprendre à " + Duration(seconds);

        /// <summary>Horloge « 1:02:03 » / « 12:34 ».</summary>
        public static string Clock(double seconds)
        {
            int s = (int)Math.Max(0, double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : Math.Min(seconds, int.MaxValue));
            int h = s / 3600, m = s % 3600 / 60, sec = s % 60;
            return h > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", h, m, sec)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", m, sec);
        }

        public static string Hour(DateTimeOffset d) => d.ToLocalTime().ToString("HH:mm", French);

        /// <summary>« Jeudi 2 octobre ».</summary>
        public static string Day(DateTimeOffset d) => Capitalize(d.ToLocalTime().ToString("dddd d MMMM yyyy", French));

        static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], French) + s.Substring(1);

        public static string Rating(double r) => "★ " + r.ToString("0.0", French);
    }
}
