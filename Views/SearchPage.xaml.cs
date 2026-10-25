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
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Recherche dans les chaînes, les films et les séries (accents et majuscules ignorés).</summary>
    public sealed partial class SearchPage : TvPage
    {
        const int MaxResults = 60;

        readonly AppState app = AppState.Current;
        readonly DispatcherTimer debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        List<(string Key, Movie Item)> movies;
        List<(string Key, Series Item)> series;
        List<(string Key, LiveChannel Item)> channels;
        LiveCatalog channelsFor;
        string indexedFor;
        int token;

        public SearchPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            debounce.Tick += (s, e) =>
            {
                debounce.Stop();
                _ = RunAsync(Query.Text);
            };
            foreach (var row in new[] { ChannelsRow, MoviesRow, SeriesRow })
            {
                row.Invoked += OnInvoked;
                row.FavoriteRequested += OnFavorite;
                row.Realized += c =>
                {
                    if (c.Payload is LiveChannel ch) app.RequestProgrammes(ch);
                };
            }
            app.SourceChanged += () =>
            {
                movies = null;
                series = null;
                channels = null;
                indexedFor = null;
                Query.Text = "";
                ShowRows(new List<CardItem>(), new List<CardItem>(), new List<CardItem>());
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string q && q.Length > 0)
            {
                Query.Text = q;
                debounce.Stop();
                _ = RunAsync(q);
            }
        }

        public override void FocusDefault() => Query.Focus(FocusState.Keyboard);

        void OnQueryFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, 0, null);

        void OnQueryChanged(object sender, TextChangedEventArgs e)
        {
            debounce.Stop();
            debounce.Start();
        }

        void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Bas depuis le champ : premier résultat
            if (Keys.IsDown(e.Key))
            {
                foreach (var row in new[] { ChannelsRow, MoviesRow, SeriesRow })
                    if (row.Visibility == Visibility.Visible && row.FocusFirst())
                    {
                        e.Handled = true;
                        return;
                    }
            }
        }

        // ───────────────────────────── Recherche ─────────────────────────────

        async Task RunAsync(string text)
        {
            var mine = ++token;
            var words = TextTools.QueryWords(text ?? "");
            if (words.Count == 0 || (text ?? "").Trim().Length < 2)
            {
                ShowRows(new List<CardItem>(), new List<CardItem>(), new List<CardItem>());
                SetState(false, "Appuie sur Ⓐ pour écrire avec le clavier de la console.");
                return;
            }
            var repo = app.Repo;
            if (repo == null) return;
            try
            {
                if (indexedFor != app.Source.CacheKey)
                {
                    movies = null;
                    series = null;
                    channels = null;
                    indexedFor = app.Source.CacheKey;
                }
                // Chaînes : déjà chargées
                if (channels == null || !ReferenceEquals(channelsFor, app.Live))
                {
                    var visible = app.VisibleChannels();
                    channelsFor = app.Live;
                    channels = await Task.Run(() => visible.Select(c => (TextTools.Fold(c.Name), c)).ToList());
                }
                var foundChannels = await Task.Run(() => channels.Where(c => TextTools.MatchesFolded(c.Key, words)).Take(MaxResults).Select(c => c.Item).ToList());
                if (mine != token) return;
                ChannelsRow.SetItems(foundChannels.Select(c => ChannelCard(c, foundChannels)).ToList());

                // Films et séries : listes complètes chargées une fois
                if (movies == null)
                {
                    SetState(true, "Recherche dans les films…");
                    var hidden = HiddenIds(await repo.VodCategoriesAsync());
                    var all = await repo.AllMoviesAsync();
                    movies = await Task.Run(() => all.Where(m => !hidden.Contains(m.CategoryId)).Select(m => (TextTools.Fold(m.Name), m)).ToList());
                }
                var foundMovies = await Task.Run(() => movies.Where(m => TextTools.MatchesFolded(m.Key, words)).Take(MaxResults).Select(m => m.Item).ToList());
                if (mine != token) return;
                MoviesRow.SetItems(foundMovies.Select(MovieCard).ToList());

                if (series == null)
                {
                    SetState(true, "Recherche dans les séries…");
                    var hidden = HiddenIds(await repo.SeriesCategoriesAsync());
                    var all = await repo.AllSeriesAsync();
                    series = await Task.Run(() => all.Where(s => !hidden.Contains(s.CategoryId)).Select(s => (TextTools.Fold(s.Name), s)).ToList());
                }
                var foundSeries = await Task.Run(() => series.Where(s => TextTools.MatchesFolded(s.Key, words)).Take(MaxResults).Select(s => s.Item).ToList());
                if (mine != token) return;
                SeriesRow.SetItems(foundSeries.Select(SeriesCard).ToList());

                var total = foundChannels.Count + foundMovies.Count + foundSeries.Count;
                SetState(false, total == 0 ? "Aucun résultat pour « " + text.Trim() + " »." : "");
            }
            catch (Exception e)
            {
                if (mine == token) SetState(false, FluxException.MessageOf(e));
            }
        }

        HashSet<string> HiddenIds(List<Category> cats) =>
            new HashSet<string>(cats.Where(c => app.Settings.HideAdult && c.IsAdult).Select(c => c.Id));

        void ShowRows(List<CardItem> ch, List<CardItem> mv, List<CardItem> sr)
        {
            ChannelsRow.SetItems(ch);
            MoviesRow.SetItems(mv);
            SeriesRow.SetItems(sr);
        }

        void SetState(bool busy, string text)
        {
            StateRing.IsActive = busy;
            StateRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            StateText.Text = text ?? "";
            StatePanel.Visibility = busy || !string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ───────────────────────────── Cartes ─────────────────────────────

        CardItem ChannelCard(LiveChannel ch, List<LiveChannel> list)
        {
            var now = app.NowNext(ch).Now;
            return new CardItem
            {
                Kind = CardKind.Channel,
                Payload = ch,
                Id = ch.Id,
                Title = ch.Name,
                Subtitle = now?.Title ?? app.CategoryName(ch.CategoryId),
                Image = ch.Logo,
                IsLogo = true,
                LiveProgress = true,
                Number = ch.Num > 0 ? ch.Num.ToString() : null,
                Progress = now?.Progress(DateTimeOffset.UtcNow) ?? 0,
            };
        }

        CardItem MovieCard(Movie m) => new CardItem
        {
            Kind = CardKind.Movie,
            Payload = m,
            Id = m.Id,
            Title = m.Name,
            Subtitle = m.Year?.ToString() ?? "",
            Image = m.Poster,
            Badge = m.Badges.FirstOrDefault(),
            Progress = app.Library.Resume(m.Id)?.Fraction ?? 0,
            Watched = app.Library.IsWatched(m.Id),
        };

        CardItem SeriesCard(Series s) => new CardItem
        {
            Kind = CardKind.Series,
            Payload = s,
            Id = s.Id,
            Title = s.Name,
            Subtitle = s.Year?.ToString() ?? "",
            Image = s.Cover,
        };

        void OnInvoked(CardItem card)
        {
            switch (card.Payload)
            {
                case LiveChannel ch:
                    var list = ChannelsRow.Items.Select(c => c.Payload).OfType<LiveChannel>().ToList();
                    app.Play(ch, list);
                    break;
                case Movie m:
                    ShellPage.Current?.OpenMovie(m, null);
                    break;
                case Series s:
                    ShellPage.Current?.OpenSeries(s, null);
                    break;
            }
        }

        void OnFavorite(CardItem card)
        {
            switch (card.Payload)
            {
                case LiveChannel ch:
                    app.ToggleFavorite(ch.Ref(app.CategoryName(ch.CategoryId)));
                    break;
                case Movie m:
                    app.ToggleFavorite(m.Ref(null));
                    break;
                case Series s:
                    app.ToggleFavorite(s.Ref(null));
                    break;
            }
        }
    }
}
