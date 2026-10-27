using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FluxTV.Core
{
    /// <summary>Une entrée brute de playlist M3U.</summary>
    public sealed class M3UEntry
    {
        public string Name;
        public string Url;
        public string Logo;
        public string Group;
        public string TvgId;
        public int Num;
        public string UserAgent;
    }

    /// <summary>Contenu d'une playlist, réparti comme avec un compte Xtream.</summary>
    public sealed class M3UCatalog
    {
        public string EpgUrl;
        public List<Category> LiveCategories = new List<Category>();
        public List<LiveChannel> Live = new List<LiveChannel>();
        public List<Category> VodCategories = new List<Category>();
        public List<Movie> Movies = new List<Movie>();
        public List<Category> SeriesCategories = new List<Category>();
        public List<Series> Series = new List<Series>();

        public bool IsEmpty => Live.Count == 0 && Movies.Count == 0 && Series.Count == 0;
    }

    public static class M3U
    {
        // ───────────────────────────── Lecture ─────────────────────────────

        public static (List<M3UEntry> Entries, string EpgUrl) Parse(string text)
        {
            var entries = new List<M3UEntry>();
            string epg = null;
            string name = "", logo = null, group = Tr.T("Autres", "Other"), tvgId = null, ua = null;
            int num = 0;
            bool pending = false;

            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    var upper = line.Length > 12 ? line.Substring(0, 12).ToUpperInvariant() : line.ToUpperInvariant();
                    if (upper.StartsWith("#EXTM3U", StringComparison.Ordinal))
                    {
                        var a = Attributes(line);
                        if (!a.TryGetValue("x-tvg-url", out var u)) a.TryGetValue("url-tvg", out u);
                        u = u?.Split(',').FirstOrDefault()?.Trim();
                        if (u != null && u.StartsWith("http", StringComparison.OrdinalIgnoreCase)) epg = u;
                    }
                    else if (upper.StartsWith("#EXTINF", StringComparison.Ordinal))
                    {
                        pending = true;
                        var a = Attributes(line);
                        tvgId = NonEmpty(a, "tvg-id");
                        logo = NonEmpty(a, "tvg-logo");
                        group = NonEmpty(a, "group-title") ?? Tr.T("Autres", "Other");
                        num = a.TryGetValue("tvg-chno", out var chno) && int.TryParse(chno.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
                        var extName = ExtinfName(line);
                        name = extName.Length == 0 ? (a.TryGetValue("tvg-name", out var tn) ? tn : Tr.T("Sans nom", "Untitled")) : extName;
                    }
                    else if (upper.StartsWith("#EXTGRP", StringComparison.Ordinal))
                    {
                        int colon = line.IndexOf(':');
                        if (colon >= 0)
                        {
                            var g = line.Substring(colon + 1).Trim();
                            if (g.Length > 0) group = g;
                        }
                    }
                    else if (line.StartsWith("#extvlcopt:http-user-agent", StringComparison.OrdinalIgnoreCase))
                    {
                        int eq = line.IndexOf('=');
                        if (eq >= 0)
                        {
                            var v = line.Substring(eq + 1).Trim();
                            ua = v.Length == 0 ? null : v;
                        }
                    }
                    continue;
                }
                if (pending)
                    entries.Add(new M3UEntry
                    {
                        Name = name.Length == 0 ? Tr.T("Sans nom", "Untitled") : name, Url = line, Logo = logo, Group = group, TvgId = tvgId, Num = num,
                        UserAgent = ua,
                    });
                pending = false;
                name = "";
                logo = null;
                tvgId = null;
                ua = null;
                group = Tr.T("Autres", "Other");
                num = 0;
            }
            return (entries, epg);
        }

        static string NonEmpty(Dictionary<string, string> a, string key) =>
            a.TryGetValue(key, out var v) && v.Length > 0 ? v : null;

        /// <summary>Attributs clé="valeur" d'une ligne #EXTINF / #EXTM3U (clés en minuscules).</summary>
        public static Dictionary<string, string> Attributes(string line)
        {
            var output = new Dictionary<string, string>(StringComparer.Ordinal);
            int i = 0;
            while (i < line.Length)
            {
                // Clé : lettres, chiffres, tirets, juste avant ="
                if (line[i] != '=' || i + 1 >= line.Length || line[i + 1] != '"') { i++; continue; }
                int k = i - 1;
                while (k >= 0 && (char.IsLetterOrDigit(line[k]) || line[k] == '-' || line[k] == '_')) k--;
                var key = line.Substring(k + 1, i - k - 1).ToLowerInvariant();
                int j = i + 2;
                while (j < line.Length && line[j] != '"') j++;
                if (key.Length > 0) output[key] = line.Substring(i + 2, Math.Min(j, line.Length) - (i + 2));
                i = j + 1;
            }
            return output;
        }

        /// <summary>Nom : texte après la PREMIÈRE virgule hors guillemets (« Canal+ Sport, HD » reste entier).</summary>
        public static string ExtinfName(string line)
        {
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"') inQuotes = !inQuotes;
                else if (line[i] == ',' && !inQuotes) return line.Substring(i + 1).Trim();
            }
            return "";
        }

        // ───────────────────────────── Classement ─────────────────────────────

        static readonly string[] VodExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".m4v", ".wmv", ".flv", ".webm" };

        public enum EntryKind { Live, Movie, Episode }

        public static EntryKind KindOf(M3UEntry e)
        {
            var lower = e.Url.ToLowerInvariant();
            int q = lower.IndexOf('?');
            var path = q >= 0 ? lower.Substring(0, q) : lower;
            if (path.Contains("/series/")) return EntryKind.Episode;
            if (path.Contains("/movie/")) return EntryKind.Movie;
            if (VodExtensions.Any(x => path.EndsWith(x, StringComparison.Ordinal)))
                return EpisodeMarker(e.Name) != null ? EntryKind.Episode : EntryKind.Movie;
            return EntryKind.Live;
        }

        /// <summary>« Brumes S01 E03 », « Brumes S1E3 - Titre », « Brumes 1x03 » → (« Brumes », 1, 3, « Titre »).</summary>
        static readonly Regex[] EpisodePatterns =
        {
            new Regex(@"^(.*?)[\s\-–._]*[Ss](\d{1,2})[\s._]*[Ee](\d{1,3})(.*)$"),
            new Regex(@"^(.*?)[\s\-–._]+(\d{1,2})x(\d{1,3})(.*)$"),
        };

        static readonly char[] MarkerTrim = { ' ', '-', '–', '.', '_', '|', ':' };

        public sealed class Marker
        {
            public string Show;
            public int Season;
            public int Episode;
            public string Title;
        }

        public static Marker EpisodeMarker(string name)
        {
            foreach (var re in EpisodePatterns)
            {
                var m = re.Match(name ?? "");
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var s)
                    || !int.TryParse(m.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var e))
                    continue;
                var show = m.Groups[1].Value.Trim(MarkerTrim);
                var rest = m.Groups[4].Value.Trim(MarkerTrim);
                if (show.Length == 0) continue;
                return new Marker { Show = show, Season = s, Episode = e, Title = rest.Length == 0 ? null : rest };
            }
            return null;
        }

        static string ExtensionOf(string url, string fallback)
        {
            int q = url.IndexOf('?');
            var path = q >= 0 ? url.Substring(0, q) : url;
            int dot = path.LastIndexOf('.');
            int slash = path.LastIndexOf('/');
            if (dot < 0 || dot < slash) return fallback;
            var ext = path.Substring(dot + 1);
            return ext.Length > 0 && ext.Length <= 4 ? ext : fallback;
        }

        sealed class Show
        {
            public string Group;
            public string Name;
            public string Logo;
            public List<Episode> Episodes = new List<Episode>();
        }

        /// <summary>Répartit une playlist en chaînes, films et séries (épisodes regroupés par série).</summary>
        public static M3UCatalog Catalog(string text)
        {
            var parsed = Parse(text);
            var cat = new M3UCatalog { EpgUrl = parsed.EpgUrl };
            var liveGroups = new List<string>();
            var vodGroups = new List<string>();
            var serGroups = new List<string>();
            var liveSeen = new HashSet<string>();
            var vodSeen = new HashSet<string>();
            var serSeen = new HashSet<string>();
            var seenIds = new HashSet<string>();
            var showOrder = new List<string>();
            var shows = new Dictionary<string, Show>();

            foreach (var e in parsed.Entries)
            {
                var id = Ids.FromUrl(e.Url);
                if (!seenIds.Add(id)) continue;
                switch (KindOf(e))
                {
                    case EntryKind.Live:
                        if (liveSeen.Add(e.Group)) liveGroups.Add(e.Group);
                        cat.Live.Add(new LiveChannel(id, null, e.Name, e.Logo, e.Group, e.TvgId, e.Num, url: e.Url, userAgent: e.UserAgent));
                        break;
                    case EntryKind.Movie:
                        if (vodSeen.Add(e.Group)) vodGroups.Add(e.Group);
                        cat.Movies.Add(new Movie(id, Ids.NumericPart(id), e.Name, e.Logo, null, null, ExtensionOf(e.Url, "mp4"), e.Group, url: e.Url));
                        break;
                    case EntryKind.Episode:
                        var marker = EpisodeMarker(e.Name);
                        var showName = marker?.Show ?? e.Group;
                        var key = e.Group + "\u001F" + TextTools.Fold(showName);
                        if (!shows.TryGetValue(key, out var show))
                        {
                            show = new Show { Group = e.Group, Name = showName, Logo = e.Logo };
                            shows[key] = show;
                            showOrder.Add(key);
                            if (serSeen.Add(e.Group)) serGroups.Add(e.Group);
                        }
                        int number = marker?.Episode ?? show.Episodes.Count + 1;
                        show.Episodes.Add(new Episode(
                            id, Ids.NumericPart(id) ?? id, marker?.Title ?? Tr.T("Épisode ", "Episode ") + number, marker?.Season ?? 1, number,
                            ExtensionOf(e.Url, "mkv"), null, e.Logo, null, e.Url));
                        break;
                }
            }
            foreach (var key in showOrder)
            {
                var s = shows[key];
                cat.Series.Add(new Series(Hash.Fnv1a64Hex(key), s.Name, s.Logo, null, null, null, s.Group, null, episodes: s.Episodes));
            }
            cat.LiveCategories = liveGroups.Select(g => new Category(g, g)).ToList();
            cat.VodCategories = vodGroups.Select(g => new Category(g, g)).ToList();
            cat.SeriesCategories = serGroups.Select(g => new Category(g, g)).ToList();
            return cat;
        }

        /// <summary>Saisons d'une série de playlist (épisodes déjà connus).</summary>
        public static SeriesDetails Details(Series s)
        {
            var d = new SeriesDetails { Plot = s.Plot, Cover = s.Cover, Genre = s.Genre, Rating = s.Rating, Year = s.Year };
            foreach (var e in s.Episodes ?? new List<Episode>())
            {
                if (!d.Seasons.TryGetValue(e.Season, out var list)) d.Seasons[e.Season] = list = new List<Episode>();
                list.Add(e);
            }
            foreach (var k in d.Seasons.Keys.ToList()) d.Seasons[k] = d.Seasons[k].OrderBy(e => e.Number).ToList();
            return d;
        }
    }
}
