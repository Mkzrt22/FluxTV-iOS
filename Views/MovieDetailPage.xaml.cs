using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Fiche d'un film : résumé, distribution, lecture ou reprise, favori, vu.</summary>
    public sealed partial class MovieDetailPage : TvPage
    {
        readonly AppState app = AppState.Current;
        Movie movie;
        string group;
        MovieInfo info;

        public MovieDetailPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                app.Library.Changed += Refresh;
                Refresh();
            };
            Unloaded += (s, e) => app.Library.Changed -= Refresh;
            PlayButton.GotFocus += (s, e) => ActionHint.Text = "";
            RestartButton.GotFocus += (s, e) => ActionHint.Text = "";
            FavoriteButton.GotFocus += (s, e) => UpdateHint();
            WatchedButton.GotFocus += (s, e) => UpdateHint();
            FavoriteButton.LostFocus += (s, e) => ActionHint.Text = "";
            WatchedButton.LostFocus += (s, e) => ActionHint.Text = "";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (movie != null || !(e.Parameter is MovieArgs args)) return;
            movie = args.Movie;
            group = args.Group;
            Fill();
            _ = LoadInfoAsync();
        }

        public override void FocusDefault() => PlayButton.Focus(FocusState.Keyboard);

        async Task LoadInfoAsync()
        {
            InfoRing.IsActive = true;
            try
            {
                var repo = app.Repo;
                if (repo != null) info = await repo.MovieInfoAsync(movie);
            }
            catch (Exception e)
            {
                Journal.Write("Fiche du film : " + FluxException.MessageOf(e));
            }
            InfoRing.IsActive = false;
            InfoRing.Visibility = Visibility.Collapsed;
            Fill();
        }

        void Fill()
        {
            TitleText.Text = movie.Name;
            var parts = new List<string>();
            var year = info?.Year ?? movie.Year;
            if (year != null) parts.Add(year.Value.ToString());
            if (info?.DurationSecs is int d && d > 0) parts.Add(Format.Duration(d));
            var rating = info?.Rating ?? movie.Rating;
            if (rating is double r && r > 0) parts.Add(Format.Rating(r));
            if (!string.IsNullOrWhiteSpace(info?.Genre)) parts.Add(info.Genre);
            MetaText.Text = string.Join(" · ", parts);
            MetaText.Visibility = parts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            BadgePanel.Children.Clear();
            foreach (var b in movie.Badges) BadgePanel.Children.Add(MakeBadge(b));
            BadgePanel.Visibility = movie.Badges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            PlotText.Text = info?.Plot ?? "";
            PlotText.Visibility = string.IsNullOrWhiteSpace(info?.Plot) ? Visibility.Collapsed : Visibility.Visible;
            var credits = new List<string>();
            if (!string.IsNullOrWhiteSpace(info?.Director)) credits.Add("Réalisation : " + info.Director);
            if (!string.IsNullOrWhiteSpace(info?.Cast)) credits.Add("Avec : " + info.Cast);
            CreditsText.Text = string.Join("\n", credits);
            CreditsText.Visibility = credits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            var poster = info?.Cover ?? movie.Poster;
            Poster.Url = poster;
            Backdrop.Url = info?.Backdrop ?? poster;
            Refresh();
        }

        internal static Border MakeBadge(string text) => new Border
        {
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 6, 0),
            CornerRadius = new CornerRadius(6),
            Background = Tv.Brush(0x14FFFFFF),
            BorderBrush = Tv.Brush(0x1FFFFFFF),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = text.ToUpperInvariant(),
                Style = (Style)Application.Current.Resources["FaintText"],
                FontSize = 10,
                CharacterSpacing = 80,
                Foreground = Palette.TextDim,
            },
        };

        void Refresh()
        {
            if (movie == null) return;
            var p = app.Library.Resume(movie.Id);
            if (p != null)
            {
                PlayText.Text = Format.Resume(p.Position);
                RestartButton.Visibility = Visibility.Visible;
                ResumePanel.Visibility = p.Duration > 0 ? Visibility.Visible : Visibility.Collapsed;
                ResumeBar.Value = p.Fraction;
                ResumeText.Text = p.Duration > 0 ? "Reste " + Format.Duration(Math.Max(0, p.Duration - p.Position)) : "";
            }
            else
            {
                PlayText.Text = "Lecture";
                RestartButton.Visibility = Visibility.Collapsed;
                ResumePanel.Visibility = Visibility.Collapsed;
            }
            var fav = app.Library.IsFavorite(movie.Id);
            FavoriteGlyph.Text = fav ? "" : "";
            FavoriteGlyph.Foreground = fav ? Palette.Gold : Palette.Text;
            var watched = app.Library.IsWatched(movie.Id);
            WatchedGlyph.Foreground = watched ? Tv.Brush(0xFF34D399) : Palette.Text;
            UpdateHint();
        }

        void UpdateHint()
        {
            var focused = Windows.UI.Xaml.Input.FocusManager.GetFocusedElement();
            if (ReferenceEquals(focused, FavoriteButton))
                ActionHint.Text = app.Library.IsFavorite(movie.Id) ? "Retirer des favoris" : "Ajouter aux favoris";
            else if (ReferenceEquals(focused, WatchedButton))
                ActionHint.Text = app.Library.IsWatched(movie.Id) ? "Marquer comme non vu" : "Marquer comme vu";
        }

        void OnPlay(object sender, RoutedEventArgs e) => app.Play(movie, group);

        void OnRestart(object sender, RoutedEventArgs e) => app.Play(movie, group, fromStart: true);

        void OnFavorite(object sender, RoutedEventArgs e) => app.ToggleFavorite(movie.Ref(group));

        void OnWatched(object sender, RoutedEventArgs e)
        {
            var watched = !app.Library.IsWatched(movie.Id);
            app.Library.SetWatched(movie.Id, watched);
            app.Toast(watched ? "Marqué comme vu" : "Marqué comme non vu");
        }
    }
}
