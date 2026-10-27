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
    /// <summary>Fiche d'une série : reprise de l'épisode en cours, saisons, épisodes (vus, commencés).</summary>
    public sealed partial class SeriesDetailPage : TvPage
    {
        readonly AppState app = AppState.Current;
        readonly DispatcherTimer seasonTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        Series series;
        string group;
        SeriesDetails details;
        List<CategoryItem> seasons = new List<CategoryItem>();
        List<EpisodeItem> episodes = new List<EpisodeItem>();
        CategoryItem currentSeason;
        CategoryItem pendingSeason;
        (Episode Episode, PlaybackPosition Resume)? next;

        string Cover => details?.Cover ?? series?.Cover;

        public SeriesDetailPage()
        {
            InitializeComponent();
            Backdrop.Opened += (img, w, h) => Backdrop.Opacity = h > w * 1.15 ? 0.45 : 0.9;
            seasonTimer.Tick += (s, e) =>
            {
                seasonTimer.Stop();
                if (pendingSeason != null && pendingSeason != currentSeason) ShowSeason(pendingSeason, false);
            };
            Loaded += (s, e) =>
            {
                app.Library.Changed += Refresh;
                Refresh();
            };
            Unloaded += (s, e) => app.Library.Changed -= Refresh;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (series != null || !(e.Parameter is SeriesArgs args)) return;
            series = args.Series;
            group = args.Group;
            FillHeader();
            _ = LoadAsync();
        }

        public override void FocusDefault()
        {
            if (PlayButton.IsEnabled) PlayButton.Focus(FocusState.Keyboard);
            else FavoriteButton.Focus(FocusState.Keyboard);
        }

        void OnTopFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, 0, null);

        // ───────────────────────────── Chargement ─────────────────────────────

        async Task LoadAsync()
        {
            var repo = app.Repo;
            if (repo == null) return;
            try
            {
                details = await repo.SeriesDetailsAsync(series);
            }
            catch (Exception e)
            {
                StateRing.IsActive = false;
                StateRing.Visibility = Visibility.Collapsed;
                StateText.Text = FluxException.MessageOf(e);
                return;
            }
            FillHeader();
            var all = details.AllEpisodes;
            if (all.Count == 0)
            {
                StateRing.IsActive = false;
                StateRing.Visibility = Visibility.Collapsed;
                StateText.Text = Tr.T("Aucun épisode disponible pour le moment.", "No episodes available yet.");
                return;
            }
            StatePanel.Visibility = Visibility.Collapsed;
            StateRing.IsActive = false;
            seasons = details.SeasonNumbers.Select(n => new CategoryItem
            {
                Id = n.ToString(),
                Season = n,
                Name = n == 0 ? Tr.T("Épisodes spéciaux", "Specials") : Tr.T("Saison ", "Season ") + n,
            }).ToList();
            Seasons.ItemsSource = seasons;
            Seasons.Visibility = seasons.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            EpisodesHeader.Visibility = Visibility.Visible;
            Episodes.Visibility = Visibility.Visible;
            next = app.Library.Data.NextEpisode(all);
            var startSeason = next?.Episode.Season ?? seasons[0].Season;
            ShowSeason(seasons.FirstOrDefault(s => s.Season == startSeason) ?? seasons[0], false);
            PlayButton.IsEnabled = true;
            Refresh();
            // Focus sur « Lecture » s'il n'était encore nulle part sur la page
            if (FocusManager.GetFocusedElement() is DependencyObject f && !Tv.IsInside(f, this) || ReferenceEquals(FocusManager.GetFocusedElement(), FavoriteButton))
                PlayButton.Focus(FocusState.Keyboard);
        }

        void FillHeader()
        {
            TitleText.Text = series.Name;
            var parts = new List<string>();
            var year = details?.Year ?? series.Year;
            if (year != null) parts.Add(year.Value.ToString());
            if (details != null && details.Seasons.Count > 0)
                parts.Add(Format.Count(details.Seasons.Count, "saison", "saisons", "season", "seasons"));
            var rating = details?.Rating ?? series.Rating;
            if (rating is double r && r > 0) parts.Add(Format.Rating(r));
            var genre = details?.Genre ?? series.Genre;
            if (!string.IsNullOrWhiteSpace(genre)) parts.Add(genre);
            MetaText.Text = string.Join(" · ", parts);
            var plot = details?.Plot ?? series.Plot;
            PlotText.Text = plot ?? "";
            PlotText.Visibility = string.IsNullOrWhiteSpace(plot) ? Visibility.Collapsed : Visibility.Visible;
            var credits = new List<string>();
            if (!string.IsNullOrWhiteSpace(details?.Director)) credits.Add(Tr.T("Création : ", "Created by: ") + details.Director);
            if (!string.IsNullOrWhiteSpace(details?.Cast)) credits.Add(Tr.T("Avec : ", "Starring: ") + details.Cast);
            CreditsText.Text = string.Join("\n", credits);
            CreditsText.Visibility = credits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Poster.Url = Cover;
            Backdrop.Url = details?.Backdrop ?? Cover;
        }

        void ShowSeason(CategoryItem s, bool focusFirst)
        {
            if (details == null || !details.Seasons.TryGetValue(s.Season, out var list)) return;
            if (currentSeason != null) currentSeason.IsCurrent = false;
            currentSeason = s;
            s.IsCurrent = true;
            episodes = list.Select(e => new EpisodeItem(e, Cover)).ToList();
            ApplyMarks();
            Episodes.ItemsSource = episodes;
            SeasonTitle.Text = s.Name;
            SeasonCount.Text = Format.Count(episodes.Count, "épisode", "épisodes", "episode", "episodes");
            var target = next != null ? episodes.FirstOrDefault(x => x.Episode.Id == next.Value.Episode.Id) : null;
            if (target != null) Episodes.ScrollIntoView(target);
            if (focusFirst && episodes.Count > 0) _ = Tv.FocusItemAsync(Episodes, target ?? episodes[0]);
        }

        void ApplyMarks()
        {
            foreach (var item in episodes)
            {
                item.Progress = app.Library.Resume(item.Episode.Id)?.Fraction ?? 0;
                item.Watched = app.Library.IsWatched(item.Episode.Id);
            }
        }

        void Refresh()
        {
            if (series == null) return;
            ApplyMarks();
            if (details != null)
            {
                next = app.Library.Data.NextEpisode(details.AllEpisodes);
                if (next != null)
                {
                    var (ep, resume) = next.Value;
                    PlayText.Text = (resume != null ? Tr.T("Reprendre ", "Resume ") : Tr.T("Lecture ", "Play ")) + ep.Label;
                }
                else
                {
                    PlayText.Text = Tr.T("Revoir depuis le début", "Watch again from the start");
                }
            }
            var fav = app.Library.IsFavorite(series.Id);
            FavoriteGlyph.Text = fav ? "" : "";
            FavoriteGlyph.Foreground = fav ? Palette.Gold : Palette.Text;
        }

        // ───────────────────────────── Manette ─────────────────────────────

        void OnPlay(object sender, RoutedEventArgs e)
        {
            if (details == null) return;
            var all = details.AllEpisodes;
            if (all.Count == 0) return;
            var ep = next?.Episode ?? all[0];
            app.Play(ep, series, all, Cover, fromStart: next == null);
        }

        void OnFavorite(object sender, RoutedEventArgs e) => app.ToggleFavorite(series.Ref(group));

        void OnSeasonFocus(object sender, RoutedEventArgs e)
        {
            if (!(Tv.ItemOf(Seasons, e.OriginalSource) is CategoryItem s)) return;
            pendingSeason = s;
            seasonTimer.Stop();
            if (s != currentSeason) seasonTimer.Start();
        }

        void OnSeasonClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is CategoryItem s)) return;
            seasonTimer.Stop();
            ShowSeason(s, true);
        }

        void OnEpisodeClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EpisodeItem item && details != null)
                app.Play(item.Episode, series, details.AllEpisodes, Cover);
        }

        /// <summary>Ⓨ : épisode vu ou non vu.</summary>
        void OnEpisodeKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Keys.IsFavorite(e.Key) && !Keys.IsMenu(e.Key)) return;
            if (!(Tv.ItemOf(Episodes, e.OriginalSource) is EpisodeItem item)) return;
            e.Handled = true;
            var watched = !app.Library.IsWatched(item.Episode.Id);
            app.Library.SetWatched(item.Episode.Id, watched);
            app.Toast((watched ? Tr.T("Vu : ", "Watched: ") : Tr.T("Non vu : ", "Unwatched: ")) + item.Label);
        }
    }
}
