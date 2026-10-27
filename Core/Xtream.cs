using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FluxTV.Core
{
    /// <summary>Compte Xtream Codes (serveur, nom d'utilisateur, mot de passe).</summary>
    public sealed class XtreamAccount : IEquatable<XtreamAccount>
    {
        public string Server { get; }
        public string Username { get; }
        public string Password { get; }

        public XtreamAccount(string server, string username, string password)
        {
            Server = NormalizeServer(server);
            Username = (username ?? "").Trim();
            Password = (password ?? "").Trim();
        }

        public bool IsComplete => Server.Length > 0 && Username.Length > 0 && Password.Length > 0;

        public bool Equals(XtreamAccount o) => o != null && o.Server == Server && o.Username == Username && o.Password == Password;
        public override bool Equals(object obj) => Equals(obj as XtreamAccount);
        public override int GetHashCode() => (Server + "\u001F" + Username).GetHashCode();

        /// <summary>
        /// Adresse du serveur telle que tapée → « http(s)://hote:port ». Accepte « hote:port », une adresse
        /// avec « / » final, ou une adresse complète collée (…/player_api.php, …/get.php?…).
        /// </summary>
        public static string NormalizeServer(string raw)
        {
            var s = (raw ?? "").Trim();
            if (s.Length == 0) return "";
            if (!s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                s = "http://" + s;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Host)) return s.TrimEnd('/');
            var path = u.AbsolutePath;
            // Pages connues retirées (le serveur peut être installé dans un sous-dossier)
            foreach (var page in new[] { "/player_api.php", "/get.php", "/xmltv.php", "/panel_api.php" })
                if (path.EndsWith(page, StringComparison.OrdinalIgnoreCase)) path = path.Substring(0, path.Length - page.Length);
            path = path.TrimEnd('/');
            // Port gardé s'il était écrit (même « :80 ») : Uri.Authority omet les ports par défaut
            var hostPort = s.Substring(s.IndexOf("://", StringComparison.Ordinal) + 3).Split('/', '?', '#')[0];
            var hasPort = hostPort.LastIndexOf(':') > hostPort.LastIndexOf(']');
            return u.Scheme + "://" + u.Host + (hasPort ? ":" + u.Port.ToString(CultureInfo.InvariantCulture) : "") + path;
        }

        /// <summary>
        /// Lien de playlist Xtream collé à la place d'un compte
        /// (http://hote:port/get.php?username=…&amp;password=…&amp;type=m3u_plus) → compte Xtream.
        /// </summary>
        public static XtreamAccount FromPlaylistUrl(string raw)
        {
            var s = (raw ?? "").Trim();
            if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || string.IsNullOrEmpty(u.Query)) return null;
            var q = QueryItems(u.Query);
            q.TryGetValue("username", out var user);
            q.TryGetValue("password", out var pass);
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return null;
            var path = u.AbsolutePath.ToLowerInvariant();
            if (!path.EndsWith("get.php", StringComparison.Ordinal) && !path.EndsWith("player_api.php", StringComparison.Ordinal)
                && !path.EndsWith("xmltv.php", StringComparison.Ordinal))
                return null;
            return new XtreamAccount(s, user, pass);
        }

        static Dictionary<string, string> QueryItems(string query)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in query.TrimStart('?').Split('&'))
            {
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                var k = Uri.UnescapeDataString(eq >= 0 ? part.Substring(0, eq) : part);
                var v = eq >= 0 ? Uri.UnescapeDataString(part.Substring(eq + 1)) : "";
                if (!d.ContainsKey(k)) d[k] = v;
            }
            return d;
        }

        // ───────────────────────────── Adresses ─────────────────────────────

        static string Enc(string s) => Uri.EscapeDataString(s ?? "");

        /// <summary>player_api.php?username=…&amp;password=…&amp;action=…</summary>
        public string ApiUrl(string action, params (string Key, string Value)[] parameters)
        {
            var sb = new StringBuilder(Server).Append("/player_api.php?username=").Append(Enc(Username)).Append("&password=").Append(Enc(Password));
            if (!string.IsNullOrEmpty(action)) sb.Append("&action=").Append(Enc(action));
            foreach (var (k, v) in parameters) sb.Append('&').Append(Enc(k)).Append('=').Append(Enc(v));
            return sb.ToString();
        }

        public string LiveUrl(string streamId, string ext = "ts") =>
            $"{Server}/live/{Enc(Username)}/{Enc(Password)}/{streamId}.{(string.IsNullOrEmpty(ext) ? "ts" : ext)}";

        public string MovieUrl(string streamId, string ext) =>
            $"{Server}/movie/{Enc(Username)}/{Enc(Password)}/{streamId}.{(string.IsNullOrEmpty(ext) ? "mp4" : ext)}";

        public string EpisodeUrl(string episodeId, string ext) =>
            $"{Server}/series/{Enc(Username)}/{Enc(Password)}/{episodeId}.{(string.IsNullOrEmpty(ext) ? "mkv" : ext)}";

        /// <summary>Replay d'un programme passé. L'heure s'exprime dans le fuseau DU SERVEUR.</summary>
        public string TimeshiftUrl(string streamId, DateTimeOffset start, int minutes, TimeSpan serverOffset) =>
            $"{Server}/timeshift/{Enc(Username)}/{Enc(Password)}/{Math.Max(1, minutes)}/" +
            $"{start.ToOffset(serverOffset).ToString("yyyy-MM-dd:HH-mm", CultureInfo.InvariantCulture)}/{streamId}.ts";

        /// <summary>Lien EPG complet (XMLTV) du serveur.</summary>
        public string XmltvUrl => $"{Server}/xmltv.php?username={Enc(Username)}&password={Enc(Password)}";
    }

    /// <summary>État du compte renvoyé par le serveur.</summary>
    public sealed class AccountInfo
    {
        public bool Authenticated;
        public string Status;
        public DateTimeOffset? Expiry;
        public bool IsTrial;
        public int? MaxConnections;
        public int? ActiveConnections;
        public List<string> AllowedFormats = new List<string>();
        public string ServerTimeZone;

        public bool IsActive => Authenticated && (Status == null || Status.Equals("active", StringComparison.OrdinalIgnoreCase));

        /// <summary>Jours restants (null = illimité).</summary>
        public int? DaysLeft(DateTimeOffset now)
        {
            if (Expiry == null) return null;
            return Math.Max(0, (int)Math.Ceiling((Expiry.Value - now).TotalDays));
        }

        /// <summary>Format du direct : HLS (m3u8) si le serveur l'autorise — le plus sûr pour le lecteur de la Xbox —, sinon TS.</summary>
        public string LiveExtension
        {
            get
            {
                var f = AllowedFormats.Select(x => x.ToLowerInvariant()).ToList();
                if (f.Count == 0 || f.Contains("m3u8")) return "m3u8";
                return "ts";
            }
        }
    }

    /// <summary>Analyse des réponses player_api.php (fonctions pures : testées sans réseau).</summary>
    public static class XtreamParse
    {
        public static AccountInfo Account(string json)
        {
            var root = Json.ParseObject(json);
            var ui = root?.Obj("user_info");
            if (ui == null) throw new FluxException(FluxError.UnexpectedResponse);
            return new AccountInfo
            {
                Authenticated = ui.Flag("auth"),
                Status = ui.Str("status"),
                Expiry = ui.UnixDate("exp_date"),
                IsTrial = ui.Flag("is_trial"),
                MaxConnections = ui.Int("max_connections"),
                ActiveConnections = ui.Int("active_cons"),
                AllowedFormats = (ui.Arr("allowed_output_formats") ?? new List<object>()).OfType<string>().ToList(),
                ServerTimeZone = root.Obj("server_info")?.Str("timezone"),
            };
        }

        public static List<Category> Categories(string json)
        {
            var output = new List<Category>();
            var seen = new HashSet<string>();
            Json.EachObject(json, o =>
            {
                var id = o.Str("category_id");
                var name = o.Str("category_name");
                if (id == null || name == null || !seen.Add(id)) return;
                output.Add(new Category(id, name));
            });
            return output;
        }

        public static List<LiveChannel> LiveStreams(string json)
        {
            var output = new List<LiveChannel>();
            Json.EachObject(json, o =>
            {
                var sid = o.Str("stream_id");
                if (sid == null) return;
                var archive = o.Flag("tv_archive");
                output.Add(new LiveChannel(
                    Ids.Live(sid), sid,
                    o.Str("name") ?? Tr.T("Sans nom", "Untitled"),
                    ValidImage(o.Str("stream_icon")),
                    o.Str("category_id") ?? "",
                    o.Str("epg_channel_id"),
                    o.Int("num") ?? 0,
                    archive ? Math.Max(1, o.Int("tv_archive_duration") ?? 1) : 0));
            });
            return output;
        }

        public static List<Movie> VodStreams(string json)
        {
            var output = new List<Movie>();
            Json.EachObject(json, o =>
            {
                var sid = o.Str("stream_id");
                if (sid == null) return;
                var year = o.Int("year");
                output.Add(new Movie(
                    Ids.Vod(sid), sid,
                    o.Str("name") ?? o.Str("title") ?? Tr.T("Sans titre", "Untitled"),
                    ValidImage(o.Str("stream_icon")) ?? ValidImage(o.Str("cover")),
                    o.Rating("rating"),
                    o.UnixDate("added"),
                    o.Str("container_extension") ?? "mp4",
                    o.Str("category_id") ?? "",
                    year >= 1900 && year <= 2100 ? year : null));
            });
            return output;
        }

        public static List<Series> SeriesList(string json)
        {
            var output = new List<Series>();
            Json.EachObject(json, o =>
            {
                var sid = o.Str("series_id");
                if (sid == null) return;
                output.Add(new Series(
                    sid,
                    o.Str("name") ?? o.Str("title") ?? Tr.T("Sans titre", "Untitled"),
                    ValidImage(o.Str("cover")),
                    o.Str("plot"),
                    o.Str("genre"),
                    o.Rating("rating"),
                    o.Str("category_id") ?? "",
                    o.UnixDate("last_modified"),
                    YearOf(o.Str("releaseDate") ?? o.Str("release_date") ?? o.Str("year"))));
            });
            return output;
        }

        static int? YearOf(string s)
        {
            if (s == null || s.Length < 4) return null;
            if (!int.TryParse(s.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y)) return null;
            return y >= 1900 && y <= 2100 ? y : (int?)null;
        }

        /// <summary>
        /// Saisons et épisodes. Selon les panels, "episodes" est un objet {"1":[…]} OU un tableau
        /// [[…],[…]] (voire [] vide) : les deux sont gérés.
        /// </summary>
        public static SeriesDetails SeriesInfo(string json)
        {
            var root = Json.ParseObject(json) ?? new Dictionary<string, object>();
            var details = new SeriesDetails();

            void Add(Dictionary<string, object> o, int fallbackSeason, int index)
            {
                var epId = o.Str("id");
                if (epId == null) return;
                var info = o.Obj("info") ?? new Dictionary<string, object>();
                var season = o.Int("season") ?? fallbackSeason;
                var ep = new Episode(
                    Ids.Episode(epId), epId,
                    CleanEpisodeTitle(o.Str("title") ?? Tr.T("Épisode ", "Episode ") + (index + 1)),
                    season,
                    o.Int("episode_num") ?? index + 1,
                    o.Str("container_extension") ?? "mkv",
                    info.Str("plot") ?? info.Str("description"),
                    ValidImage(info.Str("movie_image")) ?? ValidImage(info.Str("cover_big")),
                    info.Int("duration_secs") ?? ParseDuration(info.Str("duration")),
                    null);
                if (!details.Seasons.TryGetValue(season, out var list)) details.Seasons[season] = list = new List<Episode>();
                list.Add(ep);
            }

            root.TryGetValue("episodes", out var episodes);
            if (episodes is Dictionary<string, object> dict)
            {
                foreach (var kv in dict)
                {
                    if (!int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) || !(kv.Value is List<object> list)) continue;
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] is Dictionary<string, object> o) Add(o, num, i);
                }
            }
            else if (episodes is List<object> arr)
            {
                for (int s = 0; s < arr.Count; s++)
                {
                    if (arr[s] is List<object> list)
                    {
                        for (int i = 0; i < list.Count; i++)
                            if (list[i] is Dictionary<string, object> o) Add(o, s + 1, i);
                    }
                    else if (arr[s] is Dictionary<string, object> o)
                    {
                        Add(o, 1, s);
                    }
                }
            }
            // Même numéro d'épisode en double (fichiers de secours) : on garde le premier
            foreach (var k in details.Seasons.Keys.ToList())
            {
                var seen = new HashSet<int>();
                details.Seasons[k] = details.Seasons[k].OrderBy(e => e.Number).Where(e => seen.Add(e.Number)).ToList();
            }

            var infoRoot = root.Obj("info") ?? new Dictionary<string, object>();
            details.Plot = infoRoot.Str("plot");
            details.Cover = ValidImage(infoRoot.Str("cover"));
            details.Backdrop = infoRoot.FirstUrl("backdrop_path");
            details.Genre = infoRoot.Str("genre");
            details.Rating = infoRoot.Rating("rating");
            details.Year = YearOf(infoRoot.Str("releaseDate") ?? infoRoot.Str("release_date"));
            details.Cast = infoRoot.Str("cast");
            details.Director = infoRoot.Str("director");
            return details;
        }

        public static MovieInfo VodInfo(string json)
        {
            var info = Json.ParseObject(json)?.Obj("info");
            if (info == null) return null;
            var trailer = info.Str("youtube_trailer") ?? info.Str("trailer");
            return new MovieInfo
            {
                Plot = info.Str("plot") ?? info.Str("description"),
                Cast = info.Str("cast") ?? info.Str("actors"),
                Director = info.Str("director"),
                Genre = info.Str("genre"),
                ReleaseDate = info.Str("releasedate") ?? info.Str("release_date"),
                DurationSecs = info.Int("duration_secs") ?? ParseDuration(info.Str("duration")),
                Rating = info.Rating("rating"),
                Cover = ValidImage(info.Str("movie_image")) ?? ValidImage(info.Str("cover_big")),
                Backdrop = info.FirstUrl("backdrop_path"),
                Trailer = trailer == null ? null : YoutubeId(trailer),
            };
        }

        /// <summary>get_short_epg / get_simple_data_table : titres et descriptions en base64.</summary>
        public static List<Programme> EpgListings(string json)
        {
            var root = Json.ParseObject(json);
            var arr = root?.Arr("epg_listings");
            var list = new List<Programme>();
            if (arr == null) return list;
            foreach (var item in arr)
            {
                if (!(item is Dictionary<string, object> o)) continue;
                var st = o.Long("start_timestamp");
                var en = o.Long("stop_timestamp");
                if (st == null || en == null || en <= st) continue;
                var title = Base64(o.Str("title")).Trim();
                if (title.Length == 0) continue;
                var desc = Base64(o.Str("description")).Trim();
                list.Add(new Programme(
                    DateTimeOffset.FromUnixTimeSeconds(st.Value), DateTimeOffset.FromUnixTimeSeconds(en.Value),
                    title, desc.Length == 0 ? null : desc, o.Flag("has_archive")));
            }
            var seen = new HashSet<DateTimeOffset>();
            return list.OrderBy(p => p.Start).Where(p => seen.Add(p.Start)).ToList();
        }

        // ───────────────────────────── Outils ─────────────────────────────

        public static string Base64(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try
            {
                var clean = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
                return Encoding.UTF8.GetString(Convert.FromBase64String(clean));
            }
            catch (FormatException)
            {
                return s;
            }
        }

        /// <summary>"01:52:10", "112 min", "6730" → secondes.</summary>
        public static int? ParseDuration(string s)
        {
            if (s == null) return null;
            var t = s.Trim();
            if (t.Contains(":"))
            {
                int secs = 0;
                bool any = false;
                foreach (var part in t.Split(':'))
                {
                    if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) continue;
                    secs = secs * 60 + n;
                    any = true;
                }
                return any && secs > 0 ? secs : (int?)null;
            }
            var digits = new string(t.TakeWhile(Ids.IsAsciiDigit).ToArray());
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v <= 0) return null;
            return t.ToLowerInvariant().Contains("min") || v < 400 ? v * 60 : v;
        }

        /// <summary>Identifiant YouTube depuis un identifiant seul ou une adresse.</summary>
        public static string YoutubeId(string s)
        {
            var t = (s ?? "").Trim();
            if (t.Length == 11 && !t.Contains("/") && !t.Contains("=")) return t;
            if (Uri.TryCreate(t, UriKind.Absolute, out var u))
            {
                foreach (var part in u.Query.TrimStart('?').Split('&'))
                    if (part.StartsWith("v=", StringComparison.Ordinal) && part.Length == 13) return part.Substring(2);
                var last = u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
                if (last.Length == 11) return last;
            }
            return null;
        }

        /// <summary>Images « vides » fréquentes chez les fournisseurs.</summary>
        public static string ValidImage(string s)
        {
            var t = (s ?? "").Trim();
            return t.StartsWith("http", StringComparison.OrdinalIgnoreCase) && t.Length > 10 ? t : null;
        }

        static readonly Regex EpisodeMarker = new Regex(@"[Ss]\d{1,2}\s?[Ee]\d{1,3}");

        /// <summary>« Brumes - S01E03 - Les Disparus » → « Les Disparus ».</summary>
        public static string CleanEpisodeTitle(string raw)
        {
            var t = (raw ?? "").Trim();
            var m = EpisodeMarker.Match(t);
            if (m.Success)
            {
                var after = t.Substring(m.Index + m.Length).Trim(' ', '-', '–', ':', '·', '|');
                if (after.Length > 0) t = after;
            }
            return t;
        }
    }
}
