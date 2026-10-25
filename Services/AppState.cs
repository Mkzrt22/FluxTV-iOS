using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluxTV.Core;
using Windows.Storage;

namespace FluxTV.Services
{
    /// <summary>Ce que le lecteur doit ouvrir, avec son contexte (zapping, épisode suivant).</summary>
    public sealed class PlayerRequest
    {
        public PlayItem Item;
        /// <summary>Direct : chaînes de la liste en cours, pour zapper.</summary>
        public List<LiveChannel> Channels = new List<LiveChannel>();
        public int ChannelIndex = -1;
        /// <summary>Série : épisodes dans l'ordre, pour enchaîner.</summary>
        public List<Episode> Episodes = new List<Episode>();
        public Series Series;
        public string Cover;
    }

    /// <summary>
    /// État de l'app : source, catalogue du direct, guide TV, bibliothèque, lecture. Tout se passe sur le fil
    /// de l'interface ; les pages s'abonnent aux événements pour se mettre à jour.
    /// </summary>
    public sealed class AppState
    {
        public static AppState Current { get; } = new AppState();

        public SettingsStore Settings { get; } = new SettingsStore();
        public LibraryStore Library { get; } = new LibraryStore();

        public Source Source { get; private set; }
        public Repository Repo { get; private set; }
        public AccountInfo Account { get; private set; }
        public LiveCatalog Live { get; private set; }
        public bool LiveLoading { get; private set; }
        public string LiveError { get; private set; }
        /// <summary>Compte intégré à une copie personnelle de l'app (null sinon).</summary>
        public Source Embedded { get; private set; }
        /// <summary>Essais automatiques (captures d'écran) : rien n'est enregistré.</summary>
        public bool TestMode { get; set; }

        /// <summary>Nouvelle source, ou listes actualisées : les pages rechargent leurs contenus.</summary>
        public event Action SourceChanged;
        public event Action LiveChanged;
        public event Action AccountChanged;
        /// <summary>Programmes d'une chaîne reçus (identifiant de la chaîne).</summary>
        public event Action<string> ProgrammesLoaded;
        public event Action<string> ToastRequested;
        public event Action<PlayerRequest> PlayRequested;

        readonly Dictionary<string, List<Programme>> programmes = new Dictionary<string, List<Programme>>();
        readonly Dictionary<string, DateTimeOffset> programmesAt = new Dictionary<string, DateTimeOffset>();
        readonly HashSet<string> epgPending = new HashSet<string>();
        readonly List<LiveChannel> epgQueue = new List<LiveChannel>();
        int epgRunning;
        Dictionary<string, LiveChannel> channelIndex;

        static string CacheBase => ApplicationData.Current.LocalCacheFolder.Path;

        AppState() { }

        // ───────────────────────────── Démarrage, compte ─────────────────────────────

        /// <summary>
        /// Source enregistrée, sinon compte intégré (copie personnelle). La démonstration essayée avant ne
        /// remplace pas le compte intégré. Renvoie false s'il faut choisir une source.
        /// </summary>
        public bool Start()
        {
            Embedded = SourceStore.LoadEmbedded();
            var s = SourceStore.Load();
            if ((s == null || s.Kind == SourceKind.Demo) && Embedded != null) s = Embedded;
            if (s == null) return false;
            Use(s);
            _ = LoadLiveAsync();
            _ = RefreshAccountAsync();
            return true;
        }

        public void UseForTest(Source s)
        {
            TestMode = true;
            Use(s);
            _ = LoadLiveAsync();
        }

        void Use(Source s)
        {
            Source = s;
            Repo = s == null ? null : new Repository(s, CacheBase);
            Account = null;
            Live = null;
            LiveError = null;
            channelIndex = null;
            ResetProgrammes();
            SourceChanged?.Invoke();
        }

        void ResetProgrammes()
        {
            programmes.Clear();
            programmesAt.Clear();
            epgPending.Clear();
            epgQueue.Clear();
        }

        /// <summary>Vérifie la source puis l'enregistre. Lève une erreur lisible si elle ne fonctionne pas.</summary>
        public async Task ConnectAsync(Source s)
        {
            var candidate = new Repository(s, CacheBase);
            AccountInfo info = null;
            if (s.Kind == SourceKind.Xtream)
            {
                info = await candidate.CheckAccountAsync();
            }
            else
            {
                var l = await candidate.LiveAsync(true);
                var vod = await candidate.VodCategoriesAsync();
                var series = await candidate.SeriesCategoriesAsync();
                if (l.Channels.Count == 0 && vod.Count == 0 && series.Count == 0) throw new FluxException(FluxError.EmptyPlaylist);
            }
            if (!TestMode) SourceStore.Save(s);
            Source = s;
            Repo = candidate;
            Account = info;
            Live = null;
            LiveError = null;
            channelIndex = null;
            ResetProgrammes();
            SourceChanged?.Invoke();
            AccountChanged?.Invoke();
            await LoadLiveAsync();
        }

        public void SignOut()
        {
            var old = Repo;
            if (old != null) _ = Task.Run(() => old.ClearCache());
            SourceStore.Delete();
            Use(null);
        }

        public async Task RefreshAccountAsync()
        {
            var repo = Repo;
            if (repo == null || Source?.Kind != SourceKind.Xtream) return;
            try
            {
                var info = await repo.CheckAccountAsync();
                if (!ReferenceEquals(repo, Repo)) return;
                Account = info;
                AccountChanged?.Invoke();
            }
            catch (Exception e)
            {
                Journal.Write("Vérification du compte : " + FluxException.MessageOf(e));
            }
        }

        /// <summary>Recharge toutes les listes (chaînes, films, séries, guide).</summary>
        public async Task RefreshAllAsync()
        {
            var repo = Repo;
            if (repo == null) return;
            repo.ClearCache();
            ResetProgrammes();
            Live = null;
            channelIndex = null;
            SourceChanged?.Invoke();
            await LoadLiveAsync(true);
            Toast("Listes actualisées");
        }

        // ───────────────────────────── Direct ─────────────────────────────

        public async Task LoadLiveAsync(bool force = false)
        {
            var repo = Repo;
            if (repo == null || LiveLoading) return;
            if (Live != null && !force) return;
            LiveLoading = true;
            LiveError = null;
            LiveChanged?.Invoke();
            try
            {
                var l = await repo.LiveAsync(force);
                if (ReferenceEquals(repo, Repo))
                {
                    Live = l;
                    channelIndex = null;
                }
            }
            catch (Exception e)
            {
                if (ReferenceEquals(repo, Repo)) LiveError = FluxException.MessageOf(e);
                Journal.Write("Chaînes : " + FluxException.MessageOf(e));
            }
            LiveLoading = false;
            LiveChanged?.Invoke();
        }

        /// <summary>Catégories visibles (contenus adultes masqués si demandé).</summary>
        public List<Category> Visible(IEnumerable<Category> categories) =>
            (categories ?? Enumerable.Empty<Category>()).Where(c => !Settings.HideAdult || !c.IsAdult).ToList();

        public List<LiveChannel> VisibleChannels(string categoryId = null)
        {
            var live = Live;
            if (live == null) return new List<LiveChannel>();
            var hidden = Settings.HideAdult
                ? new HashSet<string>(live.Categories.Where(c => c.IsAdult).Select(c => c.Id))
                : new HashSet<string>();
            return live.Channels.Where(c => (categoryId == null || c.CategoryId == categoryId) && !hidden.Contains(c.CategoryId)).ToList();
        }

        public string CategoryName(string id) => Live?.Category(id)?.Name;

        public LiveChannel Channel(string id)
        {
            var live = Live;
            if (live == null || id == null) return null;
            if (channelIndex == null)
            {
                channelIndex = new Dictionary<string, LiveChannel>();
                foreach (var c in live.Channels)
                    if (!channelIndex.ContainsKey(c.Id)) channelIndex[c.Id] = c;
            }
            return channelIndex.TryGetValue(id, out var ch) ? ch : null;
        }

        /// <summary>Chaînes favorites, dans l'ordre où elles ont été ajoutées.</summary>
        public List<LiveChannel> FavoriteChannels =>
            Library.Data.FavoritesOf(ContentKind.Live).Select(r => Channel(r.Id)).Where(c => c != null).ToList();

        public List<LiveChannel> RecentChannels =>
            Library.Data.RecentLive().Select(r => Channel(r.Id)).Where(c => c != null).ToList();

        // ───────────────────────────── Guide TV ─────────────────────────────

        /// <summary>Programmes d'une chaîne : demandés à l'affichage, trois à la fois, les plus récents d'abord.</summary>
        public void RequestProgrammes(LiveChannel ch)
        {
            if (ch == null || Repo == null || IsFresh(ch.Id) || epgPending.Contains(ch.Id)) return;
            epgPending.Add(ch.Id);
            epgQueue.Add(ch);
            if (epgQueue.Count > 40)
            {
                epgPending.Remove(epgQueue[0].Id);
                epgQueue.RemoveAt(0);
            }
            Pump();
        }

        bool IsFresh(string id)
        {
            if (!programmes.TryGetValue(id, out var list) || !programmesAt.TryGetValue(id, out var at)) return false;
            var now = DateTimeOffset.UtcNow;
            if (now - at > TimeSpan.FromMinutes(20)) return false;
            return list.Count == 0 || list[list.Count - 1].Stop > now;
        }

        void Pump()
        {
            while (epgRunning < 3 && epgQueue.Count > 0)
            {
                var ch = epgQueue[epgQueue.Count - 1];
                epgQueue.RemoveAt(epgQueue.Count - 1);
                epgRunning++;
                _ = FetchProgrammesAsync(ch);
            }
        }

        async Task FetchProgrammesAsync(LiveChannel ch)
        {
            var repo = Repo;
            try
            {
                var list = await repo.ProgrammesAsync(ch);
                if (ReferenceEquals(repo, Repo))
                {
                    programmes[ch.Id] = list;
                    programmesAt[ch.Id] = DateTimeOffset.UtcNow;
                    ProgrammesLoaded?.Invoke(ch.Id);
                }
            }
            catch (Exception) { }
            finally
            {
                epgRunning--;
                epgPending.Remove(ch.Id);
                Pump();
            }
        }

        public List<Programme> ProgrammesOf(string channelId) =>
            channelId != null && programmes.TryGetValue(channelId, out var l) ? l : null;

        public (Programme Now, Programme Next) NowNext(LiveChannel ch) =>
            ch == null ? (null, null) : Programme.NowNext(ProgrammesOf(ch.Id), DateTimeOffset.UtcNow);

        // ───────────────────────────── Lecture ─────────────────────────────

        /// <summary>Format du direct : HLS de préférence (le plus sûr pour le lecteur de la Xbox).</summary>
        public string LiveExtension => Settings.LiveFormat == "auto" ? (Account?.LiveExtension ?? "m3u8") : Settings.LiveFormat;

        string UrlOf(ContentRef r) => Source == null ? null : PlaybackResolver.Url(r, Source, LiveExtension);

        public PlayItem ItemFor(LiveChannel ch)
        {
            var r = ch.Ref(CategoryName(ch.CategoryId));
            var url = UrlOf(r);
            if (url == null) return null;
            var now = NowNext(ch).Now;
            return new PlayItem
            {
                Ref = r, Url = url, Title = ch.Name, Subtitle = now?.Title ?? r.Group, Artwork = ch.Logo, IsLive = true,
                UserAgent = ch.UserAgent,
            };
        }

        public void Play(LiveChannel ch, List<LiveChannel> list)
        {
            var item = ItemFor(ch);
            if (item == null) { Toast("Adresse de lecture introuvable"); return; }
            Library.AddHistory(item.Ref);
            list = list != null && list.Count > 0 ? list : new List<LiveChannel> { ch };
            PlayRequested?.Invoke(new PlayerRequest
            {
                Item = item, Channels = list, ChannelIndex = list.FindIndex(c => c.Id == ch.Id),
            });
        }

        public void Play(Movie m, string group, bool fromStart = false)
        {
            var r = m.Ref(group);
            var url = UrlOf(r);
            if (url == null) { Toast("Adresse de lecture introuvable"); return; }
            var start = fromStart ? null : Library.Resume(r.Id)?.Position;
            Library.AddHistory(r);
            PlayRequested?.Invoke(new PlayerRequest
            {
                Item = new PlayItem
                {
                    Ref = r, Url = url, Title = m.Name, Subtitle = m.Year?.ToString() ?? group, Artwork = m.Poster, StartAt = start,
                },
            });
        }

        public void Play(Episode e, Series s, List<Episode> episodes, string cover, bool fromStart = false)
        {
            var r = e.Ref(s.SeriesId, s.Name, cover ?? s.Cover);
            var url = UrlOf(r);
            if (url == null) { Toast("Adresse de lecture introuvable"); return; }
            var start = fromStart ? null : Library.Resume(r.Id)?.Position;
            Library.AddHistory(r);
            PlayRequested?.Invoke(new PlayerRequest
            {
                Item = new PlayItem
                {
                    Ref = r, Url = url, Title = s.Name, Subtitle = e.Label + " · " + e.Title, Artwork = e.Image ?? cover ?? s.Cover,
                    StartAt = start,
                },
                Episodes = episodes ?? new List<Episode>(),
                Series = s,
                Cover = cover ?? s.Cover,
            });
        }

        /// <summary>Reprise depuis l'accueil (historique, favoris).</summary>
        public async void Play(ContentRef r)
        {
            if (r.Kind == ContentKind.Live)
            {
                var ch = Channel(r.Id);
                if (ch == null) { Toast("Cette chaîne n'est plus disponible"); return; }
                Play(ch, VisibleChannels(ch.CategoryId));
                return;
            }
            var url = UrlOf(r);
            if (url == null) { Toast("Ce contenu n'est plus disponible"); return; }
            var request = new PlayerRequest
            {
                Item = new PlayItem
                {
                    Ref = r, Url = url, IsLive = false, UserAgent = r.UserAgent, Artwork = r.Image,
                    Title = r.Kind == ContentKind.Episode ? (r.SeriesName ?? r.Name) : r.Name,
                    Subtitle = r.Kind == ContentKind.Episode ? r.Name : r.Group,
                    StartAt = Library.Resume(r.Id)?.Position,
                },
            };
            if (r.Kind == ContentKind.Episode)
            {
                // Épisodes de la série, pour enchaîner sur le suivant (au plus 4 s d'attente)
                var lookup = SeriesOfAsync(r);
                var done = await Task.WhenAny(lookup, Task.Delay(4000));
                if (done == lookup && lookup.Result.Series != null)
                {
                    request.Series = lookup.Result.Series;
                    request.Episodes = lookup.Result.Episodes;
                    request.Cover = r.Image;
                }
            }
            Library.AddHistory(r);
            PlayRequested?.Invoke(request);
        }

        async Task<(Series Series, List<Episode> Episodes)> SeriesOfAsync(ContentRef r)
        {
            var repo = Repo;
            if (r.SeriesId == null || repo == null) return (null, null);
            try
            {
                Series s;
                if (Source.Kind == SourceKind.Xtream)
                    s = new Series(r.SeriesId, r.SeriesName ?? r.Group, r.Image, null, null, null, null, null);
                else
                    s = (await repo.AllSeriesAsync()).FirstOrDefault(x => x.SeriesId == r.SeriesId);
                if (s == null) return (null, null);
                var d = await repo.SeriesDetailsAsync(s);
                return (s, d.AllEpisodes);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        // ───────────────────────────── Messages ─────────────────────────────

        public void Toast(string text) => ToastRequested?.Invoke(text);

        public void ToggleFavorite(ContentRef r)
        {
            var added = Library.ToggleFavorite(r);
            Toast(added ? "Ajouté aux favoris" : "Retiré des favoris");
        }
    }
}
