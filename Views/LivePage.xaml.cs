using System;
using System.Collections.Generic;
using System.Linq;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>TV en direct : catégories, chaînes avec le programme en cours, détails de la chaîne sous le focus.</summary>
    public sealed partial class LivePage : TvPage
    {
        const string FavoritesId = "\u0001fav";
        const string AllId = "\u0001all";

        readonly AppState app = AppState.Current;
        readonly DispatcherTimer categoryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        List<CategoryItem> categories = new List<CategoryItem>();
        List<ChannelItem> channels = new List<ChannelItem>();
        readonly Dictionary<string, ChannelItem> channelById = new Dictionary<string, ChannelItem>();
        CategoryItem current;
        CategoryItem pending;
        ChannelItem detail;
        LiveCatalog builtFor;
        bool shown;

        public LivePage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            categoryTimer.Tick += (s, e) =>
            {
                categoryTimer.Stop();
                if (pending != null && pending != current) ShowCategory(pending, null);
            };
            tick.Tick += (s, e) =>
            {
                foreach (var c in channels) ApplyProgramme(c);
                ShowDetails(detail);
            };
            app.LiveChanged += () => { if (shown) Build(); };
            app.SourceChanged += () => { builtFor = null; if (shown) Build(); };
            app.ProgrammesLoaded += OnProgrammes;
            app.Library.Changed += OnLibraryChanged;
            Loaded += (s, e) =>
            {
                shown = true;
                tick.Start();
                Build();
            };
            Unloaded += (s, e) =>
            {
                shown = false;
                tick.Stop();
            };
        }

        public override void FocusDefault()
        {
            if (channels.Count > 0)
            {
                var target = detail != null && channels.Contains(detail) ? detail : channels[0];
                _ = Tv.FocusItemAsync(Channels, target);
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
                ShellPage.Current?.FocusRail();
            }
        }

        // ───────────────────────────── Listes ─────────────────────────────

        void Build()
        {
            var live = app.Live;
            if (live == null)
            {
                builtFor = null;
                Categories.ItemsSource = null;
                Channels.ItemsSource = null;
                categories = new List<CategoryItem>();
                channels = new List<ChannelItem>();
                Details.Visibility = Visibility.Collapsed;
                ShowState();
                return;
            }
            StatePanel.Visibility = Visibility.Collapsed;
            StateRing.IsActive = false;
            if (ReferenceEquals(builtFor, live))
            {
                RefreshFavorites();
                return;
            }
            builtFor = live;
            categories = MakeCategories();
            Categories.ItemsSource = categories;

            // Catégorie de la dernière chaîne regardée, sinon les favoris, sinon la première
            var last = app.Channel(app.Library.Data.LastLiveId);
            var start = (last != null ? categories.FirstOrDefault(c => c.Id == last.CategoryId) : null)
                        ?? categories.FirstOrDefault(c => c.Id == FavoritesId)
                        ?? categories.FirstOrDefault(c => c.Id != AllId)
                        ?? categories.FirstOrDefault();
            current = null;
            if (start != null) ShowCategory(start, last?.Id);
        }

        List<CategoryItem> MakeCategories()
        {
            var visible = app.VisibleChannels();
            var counts = new Dictionary<string, int>();
            foreach (var ch in visible) counts[ch.CategoryId] = counts.TryGetValue(ch.CategoryId, out var n) ? n + 1 : 1;
            var list = new List<CategoryItem>();
            var favorites = app.FavoriteChannels.Count;
            if (favorites > 0) list.Add(new CategoryItem { Id = FavoritesId, Name = Tr.T("Favoris", "Favorites"), Count = favorites.ToString() });
            list.Add(new CategoryItem { Id = AllId, Name = Tr.T("Toutes les chaînes", "All channels"), Count = visible.Count.ToString() });
            foreach (var c in app.Visible(app.Live.Categories))
                if (counts.TryGetValue(c.Id, out var count))
                    list.Add(new CategoryItem { Id = c.Id, Name = c.Name, Count = count.ToString() });
            return list;
        }

        void ShowState()
        {
            StatePanel.Visibility = Visibility.Visible;
            if (app.LiveError != null)
            {
                StateRing.IsActive = false;
                StateRing.Visibility = Visibility.Collapsed;
                StateTitle.Text = Tr.T("Impossible de charger tes chaînes", "Couldn't load your channels");
                StateText.Text = app.LiveError;
                RetryButton.Visibility = Visibility.Visible;
            }
            else
            {
                StateRing.Visibility = Visibility.Visible;
                StateRing.IsActive = true;
                StateTitle.Text = Tr.T("Chargement des chaînes…", "Loading channels…");
                StateText.Text = Tr.T("La première fois, cela peut prendre quelques secondes.", "The first time, this can take a few seconds.");
                RetryButton.Visibility = Visibility.Collapsed;
                if (!app.LiveLoading) _ = app.LoadLiveAsync();
            }
        }

        void OnRetry(object sender, RoutedEventArgs e) => _ = app.LoadLiveAsync(true);

        void ShowCategory(CategoryItem c, string focusChannelId)
        {
            if (current != null) current.IsCurrent = false;
            current = c;
            c.IsCurrent = true;
            List<LiveChannel> list;
            if (c.Id == FavoritesId) list = app.FavoriteChannels;
            else if (c.Id == AllId) list = app.VisibleChannels();
            else list = app.VisibleChannels(c.Id);
            channelById.Clear();
            channels = list.Select(ch =>
            {
                var item = new ChannelItem(ch) { IsFavorite = app.Library.IsFavorite(ch.Id) };
                ApplyProgramme(item);
                channelById[ch.Id] = item;
                return item;
            }).ToList();
            Channels.ItemsSource = channels;
            CountText.Text = c.Name + " · " + Format.Count(channels.Count, "chaîne", "chaînes", "channel", "channels");
            var target = focusChannelId != null && channelById.TryGetValue(focusChannelId, out var f) ? f : channels.FirstOrDefault();
            ShowDetails(target);
            if (target != null) Channels.ScrollIntoView(target);
        }

        void RefreshFavorites()
        {
            foreach (var c in channels) c.IsFavorite = app.Library.IsFavorite(c.Channel.Id);
            var favorites = app.FavoriteChannels.Count;
            var favItem = categories.FirstOrDefault(c => c.Id == FavoritesId);
            var expected = favorites > 0 ? favorites.ToString() : null;
            if (favItem?.Count == expected) return;
            RebuildCategories();
            // Liste des favoris affichée : refaite, le focus reste à la même place
            if (current != null && current.Id == FavoritesId)
            {
                var focused = Tv.FocusedItem(Channels) as ChannelItem;
                var index = focused == null ? -1 : channels.IndexOf(focused);
                ShowCategory(current, null);
                if (index >= 0 && channels.Count > 0) Tv.Later(() => Tv.FocusIndex(Channels, Math.Min(index, channels.Count - 1)));
            }
        }

        void RebuildCategories()
        {
            var keep = current?.Id;
            var hadFocus = Tv.IsInside(FocusManager.GetFocusedElement() as DependencyObject, Categories);
            categories = MakeCategories();
            Categories.ItemsSource = categories;
            current = categories.FirstOrDefault(c => c.Id == keep);
            if (current != null) current.IsCurrent = true;
            else if (categories.Count > 0) ShowCategory(categories[0], null);
            if (hadFocus && current != null) _ = Tv.FocusItemAsync(Categories, current);
        }

        void OnLibraryChanged()
        {
            if (shown && builtFor != null) RefreshFavorites();
        }

        // ───────────────────────────── Focus, choix ─────────────────────────────

        void OnCategoryFocus(object sender, RoutedEventArgs e)
        {
            if (!(Tv.ItemOf(Categories, e.OriginalSource) is CategoryItem c)) return;
            pending = c;
            categoryTimer.Stop();
            if (c != current) categoryTimer.Start();
        }

        void OnCategoryClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is CategoryItem c)) return;
            categoryTimer.Stop();
            if (c != current) ShowCategory(c, null);
            if (channels.Count > 0) _ = Tv.FocusItemAsync(Channels, channels[0]);
        }

        void OnChannelFocus(object sender, RoutedEventArgs e)
        {
            if (Tv.ItemOf(Channels, e.OriginalSource) is ChannelItem item) ShowDetails(item);
        }

        void OnChannelClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChannelItem item) app.Play(item.Channel, channels.Select(c => c.Channel).ToList());
        }

        void OnChannelKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Keys.IsFavorite(e.Key) && !Keys.IsMenu(e.Key)) return;
            if (!(Tv.ItemOf(Channels, e.OriginalSource) is ChannelItem item)) return;
            e.Handled = true;
            app.ToggleFavorite(item.Channel.Ref(app.CategoryName(item.Channel.CategoryId)));
        }

        void OnChannelRealized(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.Item is ChannelItem item) app.RequestProgrammes(item.Channel);
        }

        // ───────────────────────────── Guide TV ─────────────────────────────

        void ApplyProgramme(ChannelItem item)
        {
            var now = app.NowNext(item.Channel).Now;
            item.NowTitle = now?.Title;
            item.Progress = now?.Progress(DateTimeOffset.UtcNow) ?? 0;
        }

        void OnProgrammes(string channelId)
        {
            if (channelById.TryGetValue(channelId, out var item)) ApplyProgramme(item);
            if (detail != null && detail.Channel.Id == channelId) ShowDetails(detail);
        }

        void ShowDetails(ChannelItem item)
        {
            detail = item;
            if (item == null)
            {
                Details.Visibility = Visibility.Collapsed;
                return;
            }
            Details.Visibility = Visibility.Visible;
            var ch = item.Channel;
            app.RequestProgrammes(ch);
            DetailLogo.Url = ch.Logo;
            DetailName.Text = ch.Name;
            var badges = ch.Badges.Count > 0 ? " · " + string.Join(" ", ch.Badges) : "";
            DetailCategory.Text = (ch.Num > 0 ? Tr.T("N° ", "Ch. ") + ch.Num + " · " : "") + (app.CategoryName(ch.CategoryId) ?? "") + badges;
            var (now, next) = app.NowNext(ch);
            if (now != null)
            {
                NowPanel.Visibility = Visibility.Visible;
                NowTitle.Text = now.Title;
                NowTime.Text = Format.Hour(now.Start) + " – " + Format.Hour(now.Stop);
                NowProgress.Value = now.Progress(DateTimeOffset.UtcNow);
                NowDesc.Text = now.Desc ?? "";
                NowDesc.Visibility = string.IsNullOrWhiteSpace(now.Desc) ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                NowPanel.Visibility = Visibility.Collapsed;
            }
            if (next != null)
            {
                NextPanel.Visibility = Visibility.Visible;
                NextTitle.Text = next.Title;
                NextTime.Text = Format.Hour(next.Start) + " – " + Format.Hour(next.Stop);
            }
            else
            {
                NextPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
