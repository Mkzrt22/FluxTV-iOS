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

namespace FluxTV.Views
{
    /// <summary>Catalogue de films ou de séries : catégories à gauche, affiches à droite, tri.</summary>
    public sealed partial class CatalogView : UserControl
    {
        const string FavoritesId = "\u0001fav";
        const string AllId = "\u0001all";
        static readonly string[] SortNames = { "Ajouts récents", "A → Z", "Mieux notés" };

        readonly AppState app = AppState.Current;
        readonly DispatcherTimer categoryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        List<CategoryItem> categories = new List<CategoryItem>();
        List<CardItem> cards = new List<CardItem>();
        HashSet<string> hiddenCategories = new HashSet<string>();
        CategoryItem current;
        CategoryItem pending;
        string loadedFor;
        int sortMode;
        int loadToken;
        bool shown;
        bool wantFocus;

        /// <summary>Séries (sinon films).</summary>
        public bool IsSeries { get; set; }

        public CatalogView()
        {
            InitializeComponent();
            SortText.Text = SortNames[0];
            categoryTimer.Tick += (s, e) =>
            {
                categoryTimer.Stop();
                if (pending != null && pending != current) _ = ShowCategoryAsync(pending);
            };
            app.SourceChanged += () =>
            {
                loadedFor = null;
                if (shown) _ = LoadAsync();
            };
            app.Library.Changed += OnLibraryChanged;
            Loaded += (s, e) =>
            {
                shown = true;
                TitleText.Text = IsSeries ? "Séries" : "Films";
                if (loadedFor != app.Source?.CacheKey) _ = LoadAsync();
            };
            Unloaded += (s, e) => shown = false;
        }

        string Noun => IsSeries ? "séries" : "films";

        string Count(int n) => n + " " + (n > 1 ? Noun : IsSeries ? "série" : "film");

        public void FocusDefault()
        {
            if (cards.Count > 0)
            {
                if (!Tv.IsInside(FocusManager.GetFocusedElement() as DependencyObject, PosterGrid)) Tv.FocusIndex(PosterGrid, 0);
            }
            else if (categories.Count > 0)
            {
                _ = Tv.FocusItemAsync(Categories, current ?? categories[0]);
            }
            else if (RetryButton.Visibility == Visibility.Visible)
            {
                RetryButton.Focus(FocusState.Keyboard);
            }
            else
            {
                wantFocus = true;
            }
        }

        // ───────────────────────────── Chargement ─────────────────────────────

        async Task LoadAsync()
        {
            var repo = app.Repo;
            if (repo == null) return;
            loadedFor = app.Source.CacheKey;
            categories = new List<CategoryItem>();
            cards = new List<CardItem>();
            Categories.ItemsSource = null;
            PosterGrid.ItemsSource = null;
            CountText.Text = "";
            ShowLoading("Chargement des catégories…");
            try
            {
                var all = IsSeries ? await repo.SeriesCategoriesAsync() : await repo.VodCategoriesAsync();
                if (!ReferenceEquals(repo, app.Repo)) return;
                hiddenCategories = new HashSet<string>(all.Where(c => app.Settings.HideAdult && c.IsAdult).Select(c => c.Id));
                categories = MakeCategories(app.Visible(all));
                Categories.ItemsSource = categories;
                // Catégorie « nouveautés » si le fournisseur en a une, sinon la première
                var start = categories.FirstOrDefault(c =>
                {
                    if (c.Id == FavoritesId || c.Id == AllId) return false;
                    var f = TextTools.Fold(c.Name);
                    return f.Contains("nouveau") || f.Contains("new") || f.Contains("recent") || f.Contains("dernier");
                }) ?? categories.FirstOrDefault(c => c.Id != FavoritesId && c.Id != AllId) ?? categories.FirstOrDefault();
                if (start == null)
                {
                    ShowMessage("Aucun contenu", IsSeries ? "Ta source ne propose pas de séries." : "Ta source ne propose pas de films.", false);
                    return;
                }
                await ShowCategoryAsync(start);
            }
            catch (Exception e)
            {
                loadedFor = null;
                ShowMessage("Impossible de charger les " + Noun, FluxException.MessageOf(e), true);
            }
        }

        List<CategoryItem> MakeCategories(List<Category> visible)
        {
            var list = new List<CategoryItem>();
            var favorites = app.Library.Data.FavoritesOf(IsSeries ? ContentKind.Series : ContentKind.Movie).Count;
            if (favorites > 0) list.Add(new CategoryItem { Id = FavoritesId, Name = "Favoris", Count = favorites.ToString() });
            list.Add(new CategoryItem { Id = AllId, Name = IsSeries ? "Toutes les séries" : "Tous les films" });
            list.AddRange(visible.Select(c => new CategoryItem { Id = c.Id, Name = c.Name }));
            return list;
        }

        async Task ShowCategoryAsync(CategoryItem c)
        {
            if (current != null) current.IsCurrent = false;
            current = c;
            c.IsCurrent = true;
            var token = ++loadToken;
            var repo = app.Repo;
            var keepFocus = Tv.IsInside(FocusManager.GetFocusedElement() as DependencyObject, PosterGrid);
            ShowLoading("Chargement…");
            PosterGrid.ItemsSource = null;
            try
            {
                List<CardItem> list;
                if (IsSeries)
                {
                    List<Series> series;
                    if (c.Id == FavoritesId)
                        series = app.Library.Data.FavoritesOf(ContentKind.Series).Select(Series.FromRef).ToList();
                    else if (c.Id == AllId)
                        series = (await repo.AllSeriesAsync()).Where(s => !hiddenCategories.Contains(s.CategoryId)).ToList();
                    else
                        series = await repo.SeriesAsync(c.Id);
                    list = series.Select(s => SeriesCard(s, c)).ToList();
                }
                else
                {
                    List<Movie> movies;
                    if (c.Id == FavoritesId)
                        movies = app.Library.Data.FavoritesOf(ContentKind.Movie).Select(Movie.FromRef).ToList();
                    else if (c.Id == AllId)
                        movies = (await repo.AllMoviesAsync()).Where(m => !hiddenCategories.Contains(m.CategoryId)).ToList();
                    else
                        movies = await repo.MoviesAsync(c.Id);
                    list = movies.Select(m => MovieCard(m, c)).ToList();
                }
                if (token != loadToken || !ReferenceEquals(repo, app.Repo)) return;
                cards = c.Id == FavoritesId ? list : Sorted(list);
                PosterGrid.ItemsSource = cards;
                if (c.Count == null) c.Count = cards.Count.ToString();
                CountText.Text = c.Name + " · " + Count(cards.Count);
                if (cards.Count == 0) ShowMessage("Rien ici", "Cette catégorie est vide.", false);
                else HideState();
                if (wantFocus || keepFocus)
                {
                    wantFocus = false;
                    Tv.Later(() => Tv.FocusIndex(PosterGrid, 0));
                }
            }
            catch (Exception e)
            {
                if (token != loadToken) return;
                ShowMessage("Impossible de charger cette catégorie", FluxException.MessageOf(e), true);
            }
        }

        List<CardItem> Sorted(List<CardItem> list)
        {
            switch (sortMode)
            {
                case 1:
                    // Clés calculées une fois (listes de plusieurs milliers de titres)
                    var keyed = list.Select(x => (Key: TextTools.Fold(x.Title), Item: x)).ToList();
                    keyed.Sort((a, b) => TextTools.CompareFolded(a.Key, b.Key));
                    return keyed.Select(k => k.Item).ToList();
                case 2:
                    return list.OrderByDescending(x => RatingOf(x)).ToList();
                default:
                    return list.OrderByDescending(x => AddedOf(x)).ToList();
            }
        }

        static double RatingOf(CardItem c) =>
            c.Payload is Movie m ? m.Rating ?? 0 : c.Payload is Series s ? s.Rating ?? 0 : 0;

        static DateTimeOffset AddedOf(CardItem c) =>
            c.Payload is Movie m ? m.Added ?? DateTimeOffset.MinValue
            : c.Payload is Series s ? s.LastModified ?? DateTimeOffset.MinValue : DateTimeOffset.MinValue;

        CardItem MovieCard(Movie m, CategoryItem c)
        {
            var parts = new List<string>();
            if (m.Year != null) parts.Add(m.Year.Value.ToString());
            if (m.Rating is double r && r > 0) parts.Add(Format.Rating(r));
            return new CardItem
            {
                Kind = CardKind.Movie,
                Payload = m,
                Id = m.Id,
                Title = m.Name,
                Subtitle = string.Join(" · ", parts),
                Image = m.Poster,
                Badge = m.Badges.FirstOrDefault(),
                Progress = app.Library.Resume(m.Id)?.Fraction ?? 0,
                Watched = app.Library.IsWatched(m.Id),
            };
        }

        CardItem SeriesCard(Series s, CategoryItem c)
        {
            var parts = new List<string>();
            if (s.Year != null) parts.Add(s.Year.Value.ToString());
            if (s.Rating is double r && r > 0) parts.Add(Format.Rating(r));
            return new CardItem
            {
                Kind = CardKind.Series,
                Payload = s,
                Id = s.Id,
                Title = s.Name,
                Subtitle = string.Join(" · ", parts),
                Image = s.Cover,
            };
        }

        void OnLibraryChanged()
        {
            if (IsSeries) return;
            foreach (var c in cards)
            {
                c.Progress = app.Library.Resume(c.Id)?.Fraction ?? 0;
                c.Watched = app.Library.IsWatched(c.Id);
            }
        }

        // ───────────────────────────── États ─────────────────────────────

        void ShowLoading(string text)
        {
            StatePanel.Visibility = Visibility.Visible;
            StateRing.Visibility = Visibility.Visible;
            StateRing.IsActive = true;
            StateTitle.Text = text;
            StateText.Text = "";
            RetryButton.Visibility = Visibility.Collapsed;
        }

        void ShowMessage(string title, string text, bool retry)
        {
            StatePanel.Visibility = Visibility.Visible;
            StateRing.IsActive = false;
            StateRing.Visibility = Visibility.Collapsed;
            StateTitle.Text = title;
            StateText.Text = text ?? "";
            RetryButton.Visibility = retry ? Visibility.Visible : Visibility.Collapsed;
        }

        void HideState()
        {
            StatePanel.Visibility = Visibility.Collapsed;
            StateRing.IsActive = false;
        }

        void OnRetry(object sender, RoutedEventArgs e)
        {
            if (current != null && categories.Count > 0) _ = ShowCategoryAsync(current);
            else _ = LoadAsync();
        }

        // ───────────────────────────── Manette ─────────────────────────────

        void OnCategoryFocus(object sender, RoutedEventArgs e)
        {
            if (!(Tv.ItemOf(Categories, e.OriginalSource) is CategoryItem c)) return;
            pending = c;
            categoryTimer.Stop();
            if (c != current) categoryTimer.Start();
        }

        async void OnCategoryClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is CategoryItem c)) return;
            categoryTimer.Stop();
            if (c != current) await ShowCategoryAsync(c);
            if (cards.Count > 0) Tv.FocusIndex(PosterGrid, 0);
        }

        void OnSort(object sender, RoutedEventArgs e)
        {
            sortMode = (sortMode + 1) % SortNames.Length;
            SortText.Text = SortNames[sortMode];
            if (current == null || current.Id == FavoritesId || cards.Count == 0) return;
            cards = Sorted(cards);
            PosterGrid.ItemsSource = cards;
        }

        void OnCardClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is CardItem card)) return;
            var group = current?.Id == FavoritesId || current?.Id == AllId ? null : current?.Name;
            if (card.Payload is Movie m) ShellPage.Current?.OpenMovie(m, group);
            else if (card.Payload is Series s) ShellPage.Current?.OpenSeries(s, group);
        }

        void OnGridKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Keys.IsFavorite(e.Key) && !Keys.IsMenu(e.Key)) return;
            if (!(Tv.ItemOf(PosterGrid, e.OriginalSource) is CardItem card)) return;
            e.Handled = true;
            var group = current?.Id == FavoritesId || current?.Id == AllId ? null : current?.Name;
            if (card.Payload is Movie m) app.ToggleFavorite(m.Ref(group));
            else if (card.Payload is Series s) app.ToggleFavorite(s.Ref(group));
            RefreshFavoritesCategory();
        }

        void RefreshFavoritesCategory()
        {
            var favorites = app.Library.Data.FavoritesOf(IsSeries ? ContentKind.Series : ContentKind.Movie).Count;
            var favItem = categories.FirstOrDefault(c => c.Id == FavoritesId);
            if (favItem != null && favorites > 0)
            {
                favItem.Count = favorites.ToString();
                return;
            }
            if ((favItem != null) == (favorites > 0)) return;
            var keep = current;
            var visible = categories.Where(c => c.Id != FavoritesId && c.Id != AllId)
                .Select(c => new Category(c.Id, c.Name)).ToList();
            categories = MakeCategories(visible);
            foreach (var c in categories)
                if (keep != null && c.Id == keep.Id)
                {
                    c.Count = keep.Count;
                    c.IsCurrent = true;
                    current = c;
                }
            Categories.ItemsSource = categories;
        }
    }
}
