using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxTV.Core
{
    public enum SourceKind { Xtream, Playlist, Demo }

    /// <summary>Source des contenus choisie par l'utilisateur.</summary>
    public sealed class Source : IEquatable<Source>
    {
        public SourceKind Kind { get; }
        public XtreamAccount Account { get; }
        public string PlaylistUrl { get; }
        public string EpgUrl { get; }

        Source(SourceKind kind, XtreamAccount account, string playlistUrl, string epgUrl)
        {
            Kind = kind;
            Account = account;
            PlaylistUrl = playlistUrl;
            EpgUrl = epgUrl;
        }

        public static Source Xtream(XtreamAccount a) => new Source(SourceKind.Xtream, a, null, null);
        public static Source Playlist(string url, string epg) => new Source(SourceKind.Playlist, null, url, string.IsNullOrWhiteSpace(epg) ? null : epg.Trim());
        public static readonly Source Demo = new Source(SourceKind.Demo, null, null, null);

        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case SourceKind.Xtream: return Uri.TryCreate(Account.Server, UriKind.Absolute, out var u) ? u.Host : Account.Server;
                    case SourceKind.Playlist: return Uri.TryCreate(PlaylistUrl, UriKind.Absolute, out var p) ? p.Host : "Playlist";
                    default: return Tr.T("Démonstration", "Demo");
                }
            }
        }

        /// <summary>Dossier de cache propre à cette source (jamais d'identifiants en clair dans le nom).</summary>
        public string CacheKey
        {
            get
            {
                switch (Kind)
                {
                    case SourceKind.Xtream: return "x-" + Hash.Fnv1a64Hex(Account.Server + "\u001F" + Account.Username);
                    case SourceKind.Playlist: return "p-" + Hash.Fnv1a64Hex(PlaylistUrl);
                    default: return "demo";
                }
            }
        }

        public bool Equals(Source o) =>
            o != null && o.Kind == Kind && Equals(o.Account, Account) && o.PlaylistUrl == PlaylistUrl && o.EpgUrl == EpgUrl;
        public override bool Equals(object obj) => Equals(obj as Source);
        public override int GetHashCode() => CacheKey.GetHashCode();

        public string ToJson()
        {
            switch (Kind)
            {
                case SourceKind.Xtream:
                    return "{\"kind\":\"xtream\",\"server\":" + Json.Quote(Account.Server) + ",\"username\":" + Json.Quote(Account.Username)
                           + ",\"password\":" + Json.Quote(Account.Password) + "}";
                case SourceKind.Playlist:
                    return "{\"kind\":\"playlist\",\"url\":" + Json.Quote(PlaylistUrl) + ",\"epg\":" + Json.Quote(EpgUrl) + "}";
                default:
                    return "{\"kind\":\"demo\"}";
            }
        }

        public static Source FromJson(string json)
        {
            var o = Json.ParseObject(json);
            switch (o?.Str("kind"))
            {
                case "xtream":
                    var a = new XtreamAccount(o.Str("server"), o.Str("username"), o.Str("password"));
                    return a.IsComplete ? Xtream(a) : null;
                case "playlist":
                    return o.Str("url") == null ? null : Playlist(o.Str("url"), o.Str("epg"));
                case "demo":
                    return Demo;
                default:
                    return null;
            }
        }
    }

    /// <summary>Chaînes en direct d'une source.</summary>
    public sealed class LiveCatalog
    {
        public List<Category> Categories = new List<Category>();
        public List<LiveChannel> Channels = new List<LiveChannel>();

        public Category Category(string id) => Categories.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>Ce que le lecteur doit lire.</summary>
    public sealed class PlayItem
    {
        public ContentRef Ref;
        public string Url;
        public string Title;
        public string Subtitle;
        public string Artwork;
        public bool IsLive;
        public string UserAgent;
        /// <summary>Position de départ (reprise), en secondes.</summary>
        public double? StartAt;

        public PlayItem With(double? startAt)
        {
            var copy = (PlayItem)MemberwiseClone();
            copy.StartAt = startAt;
            return copy;
        }
    }

    public static class PlaybackResolver
    {
        /// <summary>Adresse de lecture d'un contenu enregistré, reconstruite avec le compte courant.</summary>
        public static string Url(ContentRef r, Source source, string liveExtension = "m3u8")
        {
            if (!string.IsNullOrEmpty(r.Url)) return r.Url;
            var account = source?.Account;
            var num = r.NumericId;
            if (account == null || num == null) return null;
            switch (r.Kind)
            {
                case ContentKind.Live: return account.LiveUrl(num, liveExtension);
                case ContentKind.Movie: return account.MovieUrl(num, r.Ext ?? "mp4");
                case ContentKind.Episode: return account.EpisodeUrl(num, r.Ext ?? "mkv");
                default: return null;
            }
        }
    }

    /// <summary>Petit cache disque (réponses du serveur, playlists) par source.</summary>
    public sealed class DiskCache
    {
        readonly string directory;

        public DiskCache(string baseDirectory, string ns)
        {
            directory = Path.Combine(baseDirectory, "FluxTV", ns);
        }

        string FileOf(string name) => Path.Combine(directory, name.Replace('/', '_').Replace('\\', '_'));

        /// <summary>Données encore fraîches (plus récentes que maxAge).</summary>
        public byte[] Read(string name, TimeSpan maxAge)
        {
            var f = FileOf(name);
            try
            {
                if (!File.Exists(f) || DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > maxAge) return null;
                return File.ReadAllBytes(f);
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>Données, même périmées (secours hors ligne).</summary>
        public byte[] ReadAny(string name)
        {
            var f = FileOf(name);
            try { return File.Exists(f) ? File.ReadAllBytes(f) : null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void Write(string name, byte[] data)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(FileOf(name), data);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public void Clear()
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Accès aux contenus d'une source, avec cache : la liste des chaînes (souvent plusieurs dizaines
    /// de milliers d'entrées) n'est rechargée qu'au plus toutes les 12 h, ou à la demande. Les appels
    /// viennent du fil de l'interface ; l'analyse des grandes listes se fait en arrière-plan.
    /// </summary>
    public sealed class Repository
    {
        public Source Source { get; }
        readonly DiskCache cache;

        static readonly TimeSpan CatalogMaxAge = TimeSpan.FromHours(12);
        static readonly TimeSpan EpgMaxAge = TimeSpan.FromMinutes(20);

        public AccountInfo Account { get; private set; }
        M3UCatalog playlist;
        LiveCatalog liveCache;
        List<Category> vodCategoriesCache, seriesCategoriesCache;
        readonly Dictionary<string, List<Movie>> moviesByCategory = new Dictionary<string, List<Movie>>();
        readonly Dictionary<string, List<Series>> seriesByCategory = new Dictionary<string, List<Series>>();
        List<Movie> allMoviesCache;
        List<Series> allSeriesCache;
        readonly Dictionary<string, (DateTimeOffset At, List<Programme> List)> epg = new Dictionary<string, (DateTimeOffset, List<Programme>)>();
        readonly Dictionary<string, Task<List<Programme>>> epgInFlight = new Dictionary<string, Task<List<Programme>>>();
        Dictionary<string, List<Programme>> xmltv;
        Task xmltvTask;
        Task<M3UCatalog> playlistTask;

        public Repository(Source source, string cacheBase)
        {
            Source = source;
            cache = new DiskCache(cacheBase, source.CacheKey);
        }

        public void ClearCache()
        {
            cache.Clear();
            playlist = null;
            playlistTask = null;
            liveCache = null;
            vodCategoriesCache = null;
            seriesCategoriesCache = null;
            moviesByCategory.Clear();
            seriesByCategory.Clear();
            allMoviesCache = null;
            allSeriesCache = null;
            epg.Clear();
            xmltv = null;
        }

        // ───────────────────────────── Compte ─────────────────────────────

        /// <summary>Vérifie le compte Xtream (échéance, connexions…). Sans effet pour une playlist.</summary>
        public async Task<AccountInfo> CheckAccountAsync()
        {
            if (Source.Kind != SourceKind.Xtream) return null;
            var info = XtreamParse.Account(await RawAsync(null, TimeSpan.FromSeconds(20)));
            if (!info.Authenticated) throw new FluxException(FluxError.AuthRefused);
            Account = info;
            if (!info.IsActive) throw new FluxException(FluxError.AccountInactive, info.Status ?? "inactif");
            return info;
        }

        // ───────────────────────────── Playlist ─────────────────────────────

        Task<M3UCatalog> LoadPlaylistAsync(bool force)
        {
            if (Source.Kind == SourceKind.Demo) return Task.FromResult(Demo.Catalog);
            if (!force && playlist != null) return Task.FromResult(playlist);
            if (!force && playlistTask != null) return playlistTask;
            playlistTask = FetchPlaylistAsync(force);
            return playlistTask;
        }

        async Task<M3UCatalog> FetchPlaylistAsync(bool force)
        {
            try
            {
                var data = force ? null : cache.Read("playlist.m3u", CatalogMaxAge);
                if (data == null)
                {
                    try
                    {
                        data = await HttpLoader.BytesAsync(Source.PlaylistUrl, TimeSpan.FromSeconds(120));
                        cache.Write("playlist.m3u", data);
                    }
                    catch (FluxException)
                    {
                        // Hors ligne : la dernière playlist connue fait l'affaire
                        data = cache.ReadAny("playlist.m3u");
                        if (data == null) throw;
                    }
                }
                var bytes = data;
                var cat = await Task.Run(() => M3U.Catalog(HttpLoader.Decode(bytes)));
                if (!string.IsNullOrEmpty(Source.EpgUrl)) cat.EpgUrl = Source.EpgUrl;
                if (cat.IsEmpty) throw new FluxException(FluxError.EmptyPlaylist);
                playlist = cat;
                return cat;
            }
            finally
            {
                playlistTask = null;
            }
        }

        // ───────────────────────────── Direct ─────────────────────────────

        public async Task<LiveCatalog> LiveAsync(bool force = false)
        {
            if (!force && liveCache != null) return liveCache;
            LiveCatalog l;
            if (Source.Kind == SourceKind.Xtream)
            {
                var catsText = await CachedAsync("live_categories.json", force, () => RawAsync("get_live_categories", TimeSpan.FromSeconds(45)));
                var streamsText = await CachedAsync("live_streams.json", force, () => RawAsync("get_live_streams", TimeSpan.FromSeconds(90)));
                l = await Task.Run(() =>
                {
                    var cats = XtreamParse.Categories(catsText);
                    var channels = XtreamParse.LiveStreams(streamsText);
                    // Chaînes sans catégorie connue : regroupées dans « Autres »
                    var known = new HashSet<string>(cats.Select(c => c.Id));
                    if (channels.Any(c => !known.Contains(c.CategoryId))) cats.Add(new Category("", Tr.T("Autres", "Other")));
                    return new LiveCatalog { Categories = cats, Channels = channels };
                });
            }
            else
            {
                var p = await LoadPlaylistAsync(force);
                l = new LiveCatalog { Categories = p.LiveCategories, Channels = p.Live };
            }
            liveCache = l;
            return l;
        }

        // ───────────────────────────── Films ─────────────────────────────

        public async Task<List<Category>> VodCategoriesAsync(bool force = false)
        {
            if (!force && vodCategoriesCache != null) return vodCategoriesCache;
            List<Category> cats;
            if (Source.Kind == SourceKind.Xtream)
                cats = XtreamParse.Categories(await CachedAsync("vod_categories.json", force, () => RawAsync("get_vod_categories", TimeSpan.FromSeconds(45))));
            else
                cats = (await LoadPlaylistAsync(force)).VodCategories;
            vodCategoriesCache = cats;
            return cats;
        }

        public async Task<List<Movie>> MoviesAsync(string category, bool force = false)
        {
            if (!force && moviesByCategory.TryGetValue(category, out var m)) return m;
            List<Movie> list;
            if (Source.Kind == SourceKind.Xtream)
            {
                var text = await CachedAsync("vod_" + category + ".json", force,
                    () => RawAsync("get_vod_streams", TimeSpan.FromSeconds(90), ("category_id", category)));
                list = await Task.Run(() => XtreamParse.VodStreams(text));
            }
            else
            {
                list = (await LoadPlaylistAsync(false)).Movies.Where(x => x.CategoryId == category).ToList();
            }
            moviesByCategory[category] = list;
            return list;
        }

        /// <summary>Tous les films (recherche, nouveautés). Une seule requête, mise en cache.</summary>
        public async Task<List<Movie>> AllMoviesAsync(bool force = false)
        {
            if (!force && allMoviesCache != null) return allMoviesCache;
            List<Movie> list;
            if (Source.Kind == SourceKind.Xtream)
            {
                var text = await CachedAsync("vod_all.json", force, () => RawAsync("get_vod_streams", TimeSpan.FromSeconds(180)));
                list = await Task.Run(() => XtreamParse.VodStreams(text));
            }
            else
            {
                list = (await LoadPlaylistAsync(false)).Movies;
            }
            allMoviesCache = list;
            return list;
        }

        public async Task<MovieInfo> MovieInfoAsync(Movie movie)
        {
            if (Source.Kind == SourceKind.Demo) return Demo.MovieInfos.TryGetValue(movie.Id, out var d) ? d : null;
            if (Source.Kind != SourceKind.Xtream || movie.StreamId == null || movie.Url != null) return null;
            var name = "vodinfo_" + movie.StreamId + ".json";
            var cached = cache.Read(name, TimeSpan.FromDays(7));
            if (cached != null)
            {
                var info = XtreamParse.VodInfo(HttpLoader.Decode(cached));
                if (info != null) return info;
            }
            var text = await RawAsync("get_vod_info", TimeSpan.FromSeconds(45), ("vod_id", movie.StreamId));
            cache.Write(name, Encoding.UTF8.GetBytes(text));
            return XtreamParse.VodInfo(text);
        }

        // ───────────────────────────── Séries ─────────────────────────────

        public async Task<List<Category>> SeriesCategoriesAsync(bool force = false)
        {
            if (!force && seriesCategoriesCache != null) return seriesCategoriesCache;
            List<Category> cats;
            if (Source.Kind == SourceKind.Xtream)
                cats = XtreamParse.Categories(await CachedAsync("series_categories.json", force, () => RawAsync("get_series_categories", TimeSpan.FromSeconds(45))));
            else
                cats = (await LoadPlaylistAsync(force)).SeriesCategories;
            seriesCategoriesCache = cats;
            return cats;
        }

        public async Task<List<Series>> SeriesAsync(string category, bool force = false)
        {
            if (!force && seriesByCategory.TryGetValue(category, out var s)) return s;
            List<Series> list;
            if (Source.Kind == SourceKind.Xtream)
            {
                var text = await CachedAsync("series_" + category + ".json", force,
                    () => RawAsync("get_series", TimeSpan.FromSeconds(90), ("category_id", category)));
                list = await Task.Run(() => XtreamParse.SeriesList(text));
            }
            else
            {
                list = (await LoadPlaylistAsync(false)).Series.Where(x => x.CategoryId == category).ToList();
            }
            seriesByCategory[category] = list;
            return list;
        }

        public async Task<List<Series>> AllSeriesAsync(bool force = false)
        {
            if (!force && allSeriesCache != null) return allSeriesCache;
            List<Series> list;
            if (Source.Kind == SourceKind.Xtream)
            {
                var text = await CachedAsync("series_all.json", force, () => RawAsync("get_series", TimeSpan.FromSeconds(180)));
                list = await Task.Run(() => XtreamParse.SeriesList(text));
            }
            else
            {
                list = (await LoadPlaylistAsync(false)).Series;
            }
            allSeriesCache = list;
            return list;
        }

        public async Task<SeriesDetails> SeriesDetailsAsync(Series s)
        {
            if (s.Episodes != null) return M3U.Details(s);
            var name = "seriesinfo_" + s.SeriesId + ".json";
            var cached = cache.Read(name, TimeSpan.FromHours(6));
            string text;
            if (cached != null)
            {
                text = HttpLoader.Decode(cached);
            }
            else
            {
                text = await RawAsync("get_series_info", TimeSpan.FromSeconds(45), ("series_id", s.SeriesId));
                cache.Write(name, Encoding.UTF8.GetBytes(text));
            }
            var d = XtreamParse.SeriesInfo(text);
            d.Cover ??= s.Cover;
            d.Plot ??= s.Plot;
            return d;
        }

        // ───────────────────────────── Guide TV ─────────────────────────────

        /// <summary>Programmes d'une chaîne (en cours et à venir). Rechargés au plus toutes les 20 min.</summary>
        public async Task<List<Programme>> ProgrammesAsync(LiveChannel channel)
        {
            var now = DateTimeOffset.UtcNow;
            if (Source.Kind == SourceKind.Demo) return Demo.Programmes(channel.Id, now);
            if (channel.StreamId != null && Source.Kind == SourceKind.Xtream)
            {
                var sid = channel.StreamId;
                if (epg.TryGetValue(sid, out var e) && now - e.At < EpgMaxAge
                    && (e.List.Count == 0 || e.List[e.List.Count - 1].Stop > now))
                    return e.List;
                if (epgInFlight.TryGetValue(sid, out var running)) return await running;
                var task = FetchShortEpgAsync(sid);
                epgInFlight[sid] = task;
                var list = await task;
                epgInFlight.Remove(sid);
                epg[sid] = (now, list);
                return list;
            }
            // Playlist : guide XMLTV
            if (channel.TvgId == null) return new List<Programme>();
            await LoadXmltvIfNeededAsync();
            return xmltv != null && xmltv.TryGetValue(channel.TvgId, out var l) ? l : new List<Programme>();
        }

        async Task<List<Programme>> FetchShortEpgAsync(string sid)
        {
            try
            {
                return XtreamParse.EpgListings(await RawAsync("get_short_epg", TimeSpan.FromSeconds(15), ("stream_id", sid), ("limit", "10")));
            }
            catch (Exception)
            {
                return new List<Programme>();
            }
        }

        /// <summary>Programme complet (replay inclus) pour le guide.</summary>
        public async Task<List<Programme>> FullProgrammesAsync(LiveChannel channel)
        {
            if (channel.StreamId != null && Source.Kind == SourceKind.Xtream)
            {
                try { return XtreamParse.EpgListings(await RawAsync("get_simple_data_table", TimeSpan.FromSeconds(30), ("stream_id", channel.StreamId))); }
                catch (Exception) { return new List<Programme>(); }
            }
            return await ProgrammesAsync(channel);
        }

        async Task LoadXmltvIfNeededAsync()
        {
            if (xmltv != null) return;
            if (xmltvTask == null) xmltvTask = FetchXmltvAsync();
            await xmltvTask;
        }

        async Task FetchXmltvAsync()
        {
            M3UCatalog p;
            try { p = await LoadPlaylistAsync(false); }
            catch (Exception) { xmltv = new Dictionary<string, List<Programme>>(); return; }
            if (string.IsNullOrEmpty(p.EpgUrl)) { xmltv = new Dictionary<string, List<Programme>>(); return; }
            var wanted = new HashSet<string>(p.Live.Where(c => c.TvgId != null).Select(c => c.TvgId), StringComparer.Ordinal);
            var data = cache.Read("epg.xml", TimeSpan.FromHours(6));
            if (data == null)
            {
                try
                {
                    data = await HttpLoader.BytesAsync(p.EpgUrl, TimeSpan.FromSeconds(180));
                    cache.Write("epg.xml", data);
                }
                catch (FluxException)
                {
                    data = cache.ReadAny("epg.xml");
                }
            }
            if (data == null) { xmltv = new Dictionary<string, List<Programme>>(); return; }
            var bytes = data;
            xmltv = await Task.Run(() => XmlTv.Parse(bytes, wanted, DateTimeOffset.UtcNow));
        }

        // ───────────────────────────── Outils ─────────────────────────────

        Task<string> RawAsync(string action, TimeSpan timeout, params (string, string)[] parameters)
        {
            var account = Source.Account;
            if (account == null) throw new FluxException(FluxError.BadAddress);
            return HttpLoader.TextAsync(account.ApiUrl(action, parameters), timeout);
        }

        /// <summary>Réponse en cache si fraîche ; sinon téléchargée (et l'ancienne en secours hors ligne).</summary>
        async Task<string> CachedAsync(string name, bool force, Func<Task<string>> fetch)
        {
            if (!force)
            {
                var fresh = cache.Read(name, CatalogMaxAge);
                if (fresh != null) return HttpLoader.Decode(fresh);
            }
            try
            {
                var text = await fetch();
                var old = cache.ReadAny(name);
                // Réponse vide ou d'erreur : on ne remplace pas une liste valide
                if (text.Length > 2 || old == null) cache.Write(name, Encoding.UTF8.GetBytes(text));
                return text.Length > 2 || old == null ? text : HttpLoader.Decode(old);
            }
            catch (FluxException)
            {
                var old = cache.ReadAny(name);
                if (old != null) return HttpLoader.Decode(old);
                throw;
            }
        }
    }
}
