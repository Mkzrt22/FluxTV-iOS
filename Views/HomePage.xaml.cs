using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Accueil : reprise en grand, puis rangées (reprises, chaînes, films, séries, favoris).</summary>
    public sealed partial class HomePage : TvPage
    {
        readonly AppState app = AppState.Current;
        readonly DispatcherTimer clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        readonly Dictionary<string, List<CardItem>> channelCards = new Dictionary<string, List<CardItem>>();
        List<Movie> latestMovies = new List<Movie>();
        List<Series> featuredSeries = new List<Series>();
        string moviesGroup;
        string seriesGroup;
        string rowsLoadedFor;
        object heroTarget;
        bool shown;
        (CardRow Row, int Index)? focusMemory;

        CardRow[] Rows => new[] { ResumeRow, FavChannelsRow, RecentChannelsRow, DiscoverRow, MoviesRow, SeriesRow, FavMoviesRow, FavSeriesRow };

        public HomePage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            clock.Tick += (s, e) => Tick();
            foreach (var row in Rows)
            {
                row.Invoked += OnCardInvoked;
                row.FavoriteRequested += OnCardFavorite;
                row.Realized += OnCardRealized;
            }
            app.LiveChanged += OnDataChanged;
            app.Library.Changed += OnDataChanged;
            app.AccountChanged += UpdateHeader;
            app.ProgrammesLoaded += OnProgrammes;
            app.SourceChanged += OnSourceChanged;
            HeroImage.Opened += OnHeroImageOpened;
            Loaded += OnLoaded;
            Unloaded += (s, e) =>
            {
                shown = false;
                clock.Stop();
            };
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            shown = true;
            clock.Start();
            Tick();
            UpdateHeader();
            Refresh();
            _ = LoadRowsAsync();
        }

        void OnSourceChanged()
        {
            rowsLoadedFor = null;
            latestMovies = new List<Movie>();
            featuredSeries = new List<Series>();
            if (shown)
            {
                Refresh();
                _ = LoadRowsAsync();
            }
        }

        void OnDataChanged()
        {
            if (shown) Refresh();
        }

        // ───────────────────────────── Focus ─────────────────────────────

        public override void FocusDefault()
        {
            if (focusMemory.HasValue)
            {
                var (row, index) = focusMemory.Value;
                focusMemory = null;
                if (row.Visibility == Visibility.Visible && row.Items.Count > 0
                    && Tv.FocusIndex(row.ListView, Math.Min(index, row.Items.Count - 1))) return;
            }
            if (Hero.Visibility == Visibility.Visible && Hero.Focus(FocusState.Keyboard)) return;
            foreach (var r in Rows)
                if (r.Visibility == Visibility.Visible && r.FocusFirst()) return;
            if (RetryButton.Visibility == Visibility.Visible) RetryButton.Focus(FocusState.Keyboard);
            else ShellPage.Current?.FocusRail();
        }

        /// <summary>Essais automatiques : focus sur la première carte de la première rangée.</summary>
        public void FocusFirstRow()
        {
            foreach (var r in Rows)
                if (r.Visibility == Visibility.Visible && r.FocusFirst()) return;
        }

        void OnHeroFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, 0, null);

        // ───────────────────────────── Contenu ─────────────────────────────

        void Tick()
        {
            Clock.Text = DateTime.Now.ToString("HH:mm");
            var h = DateTime.Now.Hour;
            Greeting.Text = h >= 5 && h < 12 ? "Bonjour" : h >= 12 && h < 18 ? "Bon après-midi" : "Bonsoir";
            // Avancement des programmes en cours
            foreach (var pair in channelCards)
                foreach (var card in pair.Value)
                    ApplyProgramme(card);
        }

        void UpdateHeader()
        {
            SourceName.Text = app.Source == null ? "" : app.Source.DisplayName;
            var days = app.Account?.DaysLeft(DateTimeOffset.UtcNow);
            if (days is int d && d <= 30)
            {
                SubscriptionText.Text = d == 0 ? "Abonnement expiré" : "Abonnement : " + d + " j";
                SubscriptionText.Foreground = d <= 7 ? Palette.Gold : Palette.TextDim;
                SubscriptionChip.Visibility = Visibility.Visible;
            }
            else
            {
                SubscriptionChip.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>Reconstruit les rangées (favoris, reprises, chaînes) et la mise en avant.</summary>
        void Refresh()
        {
            RememberFocus();
            var lib = app.Library.Data;
            channelCards.Clear();

            var resume = lib.ContinueWatching();
            ResumeRow.SetItems(resume.Select(ResumeCard).ToList());

            var favorites = app.FavoriteChannels;
            var favIds = new HashSet<string>(favorites.Select(c => c.Id));
            var recent = app.RecentChannels.Where(c => !favIds.Contains(c.Id)).ToList();
            FavChannelsRow.SetItems(favorites.Select(c => ChannelCard(c, favorites)).ToList());
            RecentChannelsRow.SetItems(recent.Select(c => ChannelCard(c, recent)).ToList());
            var discover = favorites.Count == 0 && recent.Count == 0 ? app.VisibleChannels().Take(16).ToList() : new List<LiveChannel>();
            DiscoverRow.SetItems(discover.Select(c => ChannelCard(c, discover)).ToList());

            MoviesRow.Title = moviesGroup ?? "Films";
            MoviesRow.SetItems(latestMovies.Select(m => MovieCard(m, moviesGroup)).ToList());
            SeriesRow.Title = seriesGroup ?? "Séries";
            SeriesRow.SetItems(featuredSeries.Select(s => SeriesCard(s, seriesGroup)).ToList());
            FavMoviesRow.SetItems(lib.FavoritesOf(ContentKind.Movie).Select(r => MovieCard(Movie.FromRef(r), r.Group)).ToList());
            FavSeriesRow.SetItems(lib.FavoritesOf(ContentKind.Series).Select(r => SeriesCard(Series.FromRef(r), r.Group)).ToList());

            UpdateHero(resume, favorites.Count > 0 ? favorites : app.RecentChannels);
            UpdateLiveState();
            RestoreFocus();
        }

        void RememberFocus()
        {
            var focused = FocusManager.GetFocusedElement() as DependencyObject;
            foreach (var r in Rows)
            {
                if (!Tv.IsInside(focused, r)) continue;
                var item = Tv.ItemOf(r.ListView, focused);
                var index = item == null ? 0 : r.Items.IndexOf(item as CardItem);
                focusMemory = (r, Math.Max(0, index));
                return;
            }
        }

        void RestoreFocus()
        {
            if (focusMemory == null || ShellPage.Current?.IsPlaying == true) return;
            // Focus perdu (rangée reconstruite) : même position qu'avant
            if (FocusManager.GetFocusedElement() is DependencyObject d && Tv.IsInTree(d)) { focusMemory = null; return; }
            Tv.Later(FocusDefault);
        }

        void UpdateLiveState()
        {
            if (app.LiveLoading && app.Live == null)
            {
                LiveState.Visibility = Visibility.Visible;
                LiveRing.IsActive = true;
                LiveRing.Visibility = Visibility.Visible;
                LiveStateTitle.Text = "Chargement des chaînes…";
                LiveStateText.Text = "";
                RetryButton.Visibility = Visibility.Collapsed;
            }
            else if (app.LiveError != null && app.Live == null)
            {
                LiveState.Visibility = Visibility.Visible;
                LiveRing.IsActive = false;
                LiveRing.Visibility = Visibility.Collapsed;
                LiveStateTitle.Text = "Impossible de charger tes chaînes";
                LiveStateText.Text = app.LiveError;
                RetryButton.Visibility = Visibility.Visible;
            }
            else
            {
                LiveState.Visibility = Visibility.Collapsed;
                LiveRing.IsActive = false;
                RetryButton.Visibility = Visibility.Collapsed;
            }
        }

        void OnRetry(object sender, RoutedEventArgs e) => _ = app.LoadLiveAsync(true);

        // ───────────────────────────── Mise en avant ─────────────────────────────

        void OnHeroImageOpened(RemoteImage image, int width, int height)
        {
            if (heroTarget is LiveChannel) return;
            var portrait = height > width * 1.15;
            HeroPosterBox.Visibility = portrait ? Visibility.Visible : Visibility.Collapsed;
            HeroImage.Opacity = portrait ? 0.4 : 1;
            if (portrait) HeroPoster.Url = image.Url;
        }

        void UpdateHero(List<ContentRef> resume, List<LiveChannel> channels)
        {
            var previous = HeroImage.Url;
            HeroImage.ImageStretch = Stretch.UniformToFill;
            HeroImage.ImagePadding = new Thickness(0);
            HeroProgress.Visibility = Visibility.Collapsed;
            HeroProgress.Foreground = Palette.Accent;
            if (resume.Count > 0)
            {
                var r = resume[0];
                heroTarget = r;
                HeroEyebrow.Text = r.Kind == ContentKind.Episode ? "REPRENDRE LA SÉRIE" : "REPRENDRE";
                HeroTitle.Text = r.Kind == ContentKind.Episode ? (r.SeriesName ?? r.Name) : r.Name;
                HeroSubtitle.Text = ResumeSubtitle(r) ?? "";
                HeroImage.Url = r.Image;
                var p = app.Library.Resume(r.Id);
                if (p != null && p.Fraction > 0)
                {
                    HeroProgress.Value = p.Fraction;
                    HeroProgress.Visibility = Visibility.Visible;
                }
                HeroAction.Text = "Reprendre";
                Hero.Visibility = Visibility.Visible;
            }
            else if (channels.Count > 0)
            {
                var ch = channels[0];
                heroTarget = ch;
                app.RequestProgrammes(ch);
                var now = app.NowNext(ch).Now;
                HeroEyebrow.Text = "EN DIRECT";
                HeroTitle.Text = now?.Title ?? ch.Name;
                HeroSubtitle.Text = now == null ? (app.CategoryName(ch.CategoryId) ?? "") : ch.Name + " · " + Format.Hour(now.Start) + " – " + Format.Hour(now.Stop);
                HeroImage.Url = ch.Logo;
                HeroImage.ImageStretch = Stretch.Uniform;
                HeroImage.ImagePadding = new Thickness(520, 44, 44, 44);
                if (now != null)
                {
                    HeroProgress.Value = now.Progress(DateTimeOffset.UtcNow);
                    HeroProgress.Foreground = Palette.OnAir;
                    HeroProgress.Visibility = Visibility.Visible;
                }
                HeroAction.Text = "Regarder";
                Hero.Visibility = Visibility.Visible;
            }
            else if (latestMovies.FirstOrDefault(x => x.Poster != null) is Movie featured)
            {
                heroTarget = featured;
                HeroEyebrow.Text = (moviesGroup ?? "À voir").ToUpperInvariant();
                HeroTitle.Text = featured.Name;
                HeroSubtitle.Text = MovieSubtitle(featured);
                HeroImage.Url = featured.Poster;
                HeroAction.Text = "Voir la fiche";
                Hero.Visibility = Visibility.Visible;
            }
            else
            {
                heroTarget = null;
                Hero.Visibility = Visibility.Collapsed;
            }
            // Nouvelle image : l'affiche verticale éventuelle sera remise en place à son ouverture
            if (HeroImage.Url != previous || heroTarget is LiveChannel)
            {
                HeroPosterBox.Visibility = Visibility.Collapsed;
                HeroImage.Opacity = 1;
            }
        }

        void OnHeroClick(object sender, RoutedEventArgs e)
        {
            switch (heroTarget)
            {
                case ContentRef r:
                    app.Play(r);
                    break;
                case LiveChannel ch:
                    app.Play(ch, app.VisibleChannels(ch.CategoryId));
                    break;
                case Movie m:
                    ShellPage.Current?.OpenMovie(m, moviesGroup);
                    break;
            }
        }

        // ───────────────────────────── Cartes ─────────────────────────────

        CardItem ResumeCard(ContentRef r) => new CardItem
        {
            Kind = CardKind.Resume,
            Payload = r,
            Id = r.Id,
            Title = r.Kind == ContentKind.Episode ? (r.SeriesName ?? r.Name) : r.Name,
            Subtitle = ResumeSubtitle(r),
            Image = r.Image,
            Progress = app.Library.Resume(r.Id)?.Fraction ?? 0,
        };

        string ResumeSubtitle(ContentRef r)
        {
            var p = app.Library.Resume(r.Id);
            if (p == null) return r.Group;
            var remain = p.Duration > 0 ? "Reste " + Format.Duration(Math.Max(0, p.Duration - p.Position)) : Format.Resume(p.Position);
            return r.Kind == ContentKind.Episode ? r.Name + " · " + remain : remain;
        }

        CardItem ChannelCard(LiveChannel ch, List<LiveChannel> list)
        {
            var card = new CardItem
            {
                Kind = CardKind.Channel,
                Payload = (ch, list),
                Id = ch.Id,
                Title = ch.Name,
                Image = ch.Logo,
                IsLogo = true,
                LiveProgress = true,
                Number = ch.Num > 0 ? ch.Num.ToString() : null,
                Subtitle = app.CategoryName(ch.CategoryId),
            };
            ApplyProgramme(card);
            if (!channelCards.TryGetValue(ch.Id, out var cards)) channelCards[ch.Id] = cards = new List<CardItem>();
            cards.Add(card);
            return card;
        }

        void ApplyProgramme(CardItem card)
        {
            if (!(card.Payload is ValueTuple<LiveChannel, List<LiveChannel>> p)) return;
            var now = app.NowNext(p.Item1).Now;
            if (now == null) return;
            card.Subtitle = now.Title;
            card.Progress = now.Progress(DateTimeOffset.UtcNow);
        }

        void OnProgrammes(string channelId)
        {
            if (channelCards.TryGetValue(channelId, out var cards))
                foreach (var c in cards) ApplyProgramme(c);
            if (heroTarget is LiveChannel ch && ch.Id == channelId && shown)
                UpdateHero(app.Library.Data.ContinueWatching(), new List<LiveChannel> { ch });
        }

        void OnCardRealized(CardItem card)
        {
            if (card.Payload is ValueTuple<LiveChannel, List<LiveChannel>> p) app.RequestProgrammes(p.Item1);
        }

        CardItem MovieCard(Movie m, string group) => new CardItem
        {
            Kind = CardKind.Movie,
            Payload = (m, group),
            Id = m.Id,
            Title = m.Name,
            Subtitle = MovieSubtitle(m),
            Image = m.Poster,
            Badge = m.Badges.FirstOrDefault(),
            Progress = app.Library.Resume(m.Id)?.Fraction ?? 0,
            Watched = app.Library.IsWatched(m.Id),
        };

        static string MovieSubtitle(Movie m)
        {
            var parts = new List<string>();
            if (m.Year != null) parts.Add(m.Year.Value.ToString());
            if (m.Rating is double r && r > 0) parts.Add(Format.Rating(r));
            return string.Join(" · ", parts);
        }

        CardItem SeriesCard(Series s, string group) => new CardItem
        {
            Kind = CardKind.Series,
            Payload = (s, group),
            Id = s.Id,
            Title = s.Name,
            Subtitle = SeriesSubtitle(s),
            Image = s.Cover,
        };

        static string SeriesSubtitle(Series s)
        {
            var parts = new List<string>();
            if (s.Year != null) parts.Add(s.Year.Value.ToString());
            if (s.Rating is double r && r > 0) parts.Add(Format.Rating(r));
            return string.Join(" · ", parts);
        }

        void OnCardInvoked(CardItem card)
        {
            switch (card.Payload)
            {
                case ContentRef r:
                    app.Play(r);
                    break;
                case ValueTuple<LiveChannel, List<LiveChannel>> ch:
                    app.Play(ch.Item1, ch.Item2);
                    break;
                case ValueTuple<Movie, string> m:
                    ShellPage.Current?.OpenMovie(m.Item1, m.Item2);
                    break;
                case ValueTuple<Series, string> s:
                    ShellPage.Current?.OpenSeries(s.Item1, s.Item2);
                    break;
            }
        }

        /// <summary>Ⓨ : favori (ou retrait de « Continuer à regarder »).</summary>
        void OnCardFavorite(CardItem card)
        {
            switch (card.Payload)
            {
                case ContentRef r:
                    app.Library.RemoveHistory(r.Id);
                    app.Toast("Retiré de « Continuer à regarder »");
                    break;
                case ValueTuple<LiveChannel, List<LiveChannel>> ch:
                    app.ToggleFavorite(ch.Item1.Ref(app.CategoryName(ch.Item1.CategoryId)));
                    break;
                case ValueTuple<Movie, string> m:
                    app.ToggleFavorite(m.Item1.Ref(m.Item2));
                    break;
                case ValueTuple<Series, string> s:
                    app.ToggleFavorite(s.Item1.Ref(s.Item2));
                    break;
            }
        }

        // ───────────────────────────── Films et séries ─────────────────────────────

        async Task LoadRowsAsync()
        {
            var repo = app.Repo;
            var key = app.Source?.CacheKey;
            if (repo == null || key == null || rowsLoadedFor == key) return;
            rowsLoadedFor = key;
            try
            {
                var cats = app.Visible(await repo.VodCategoriesAsync());
                // Catégorie « nouveautés » si le fournisseur en a une, sinon la première
                var pick = cats.FirstOrDefault(c =>
                {
                    var f = TextTools.Fold(c.Name);
                    return f.Contains("nouveau") || f.Contains("new") || f.Contains("recent") || f.Contains("dernier");
                }) ?? cats.FirstOrDefault();
                if (pick != null)
                {
                    var movies = await repo.MoviesAsync(pick.Id);
                    if (!ReferenceEquals(repo, app.Repo)) return;
                    moviesGroup = pick.Name;
                    latestMovies = movies.OrderByDescending(m => m.Added ?? DateTimeOffset.MinValue).Take(24).ToList();
                }
            }
            catch (Exception e)
            {
                Journal.Write("Accueil, films : " + FluxException.MessageOf(e));
            }
            try
            {
                var cats = app.Visible(await repo.SeriesCategoriesAsync());
                var first = cats.FirstOrDefault();
                if (first != null)
                {
                    var list = await repo.SeriesAsync(first.Id);
                    if (!ReferenceEquals(repo, app.Repo)) return;
                    seriesGroup = first.Name;
                    featuredSeries = list.OrderByDescending(s => s.LastModified ?? DateTimeOffset.MinValue).Take(24).ToList();
                }
            }
            catch (Exception e)
            {
                Journal.Write("Accueil, séries : " + FluxException.MessageOf(e));
            }
            if (shown) Refresh();
        }
    }
}
