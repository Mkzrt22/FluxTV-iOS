using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace FluxTV.Views
{
    /// <summary>Paramètre d'ouverture d'une fiche de film.</summary>
    public sealed class MovieArgs
    {
        public Movie Movie;
        public string Group;
    }

    /// <summary>Paramètre d'ouverture d'une fiche de série.</summary>
    public sealed class SeriesArgs
    {
        public Series Series;
        public string Group;
    }

    /// <summary>
    /// Coque de l'app : menu latéral, pages, lecteur par-dessus, messages. Ⓑ revient en arrière, puis au menu.
    /// </summary>
    public sealed partial class ShellPage : Page
    {
        public static ShellPage Current { get; private set; }

        readonly AppState app = AppState.Current;
        readonly DispatcherTimer toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.6) };
        string currentKey = "home";
        bool setupMode;
        bool railOpen;
        Control lastContentFocus;
        DateTime railQuietUntil;
        PlayerView player;
        object focusBeforePlayer;
        IntroView intro;
        bool introBlocking;

        public ShellPage()
        {
            InitializeComponent();
            Current = this;
            toastTimer.Tick += (s, e) =>
            {
                toastTimer.Stop();
                ToastHide.Begin();
            };
            app.ToastRequested += ShowToast;
            app.PlayRequested += OpenPlayer;
            app.SourceChanged += OnSourceChanged;
            FocusManager.GotFocus += OnGlobalGotFocus;
            FocusManager.GettingFocus += OnGlobalGettingFocus;
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            UpdateRail();
        }

        /// <summary>
        /// Animation de lancement pendant que les chaînes se chargent, puis accueil si une source est connue,
        /// sinon choix de la source.
        /// </summary>
        public void Start()
        {
            var ready = app.Start();
            PlayIntro(false);
            if (ready) ShowApp("home");
            else ShowSetup();
        }

        // ───────────────────────────── Intro ─────────────────────────────

        void PlayIntro(bool hold)
        {
            // L'app reste affichée derrière le fond opaque de l'intro : elle est déjà dessinée (images
            // comprises) quand elle apparaît, sans à-coup
            introBlocking = true;
            intro = new IntroView();
            intro.Revealing += OnIntroRevealing;
            intro.Finished += OnIntroFinished;
            IntroHost.Children.Add(intro);
            intro.Play(IntroReady, IntroStatus, hold);
        }

        /// <summary>Chaînes chargées et page prête à recevoir le focus (l'intro attend 6 s au plus).</summary>
        bool IntroReady()
        {
            if (app.Source == null) return true;
            if (app.LiveLoading) return false;
            return !(ContentFrame.Content is TvPage page) || page.CanTakeFocus;
        }

        string IntroStatus()
        {
            if (app.Source == null) return "Bienvenue";
            if (app.LiveLoading) return "Chargement des chaînes…";
            return "Prêt";
        }

        /// <summary>La plongée commence : l'app sort de l'éclair et reçoit le focus.</summary>
        void OnIntroRevealing()
        {
            introBlocking = false;
            if (intro != null) intro.IsHitTestVisible = false;
            RevealMain.Begin();
            FocusContentSoon();
        }

        void OnIntroFinished()
        {
            var v = intro;
            intro = null;
            if (v == null) return;
            v.Revealing -= OnIntroRevealing;
            v.Finished -= OnIntroFinished;
            // Focus encore sur l'intro (page pas prête à temps) : il va sur la page, et si le système le
            // replace sur le menu en retirant l'intro, le menu reste fermé
            var focused = FocusManager.GetFocusedElement() as DependencyObject;
            if (focused == null || Tv.IsInside(focused, v))
            {
                railQuietUntil = DateTime.UtcNow.AddMilliseconds(1500);
                (ContentFrame.Content as TvPage)?.FocusDefault();
            }
            v.Release();
            IntroHost.Children.Remove(v);
        }

        public bool IsPlaying => player != null;

        void OnSourceChanged()
        {
            if (app.Source == null)
            {
                if (!setupMode) ShowSetup();
            }
            else if (setupMode)
            {
                ShowApp("home");
            }
        }

        void ShowSetup()
        {
            setupMode = true;
            SetRailOpen(false);
            Rail.Visibility = Visibility.Collapsed;
            ContentFrame.Margin = new Thickness(0);
            ContentFrame.Navigate(typeof(SetupPage));
            ContentFrame.BackStack.Clear();
            FocusContentSoon();
        }

        void ShowApp(string key)
        {
            setupMode = false;
            Rail.Visibility = Visibility.Visible;
            ContentFrame.Margin = new Thickness(112, 0, 0, 0);
            Navigate(key);
        }

        // ───────────────────────────── Navigation ─────────────────────────────

        static Type PageOf(string key)
        {
            switch (key)
            {
                case "live": return typeof(LivePage);
                case "movies": return typeof(MoviesPage);
                case "series": return typeof(SeriesPage);
                case "search": return typeof(SearchPage);
                case "settings": return typeof(SettingsPage);
                default: return typeof(HomePage);
            }
        }

        /// <summary>Page principale (menu) : l'historique de navigation repart de zéro.</summary>
        public void Navigate(string key, object parameter = null, bool focusContent = true)
        {
            currentKey = key;
            UpdateRail();
            var type = PageOf(key);
            if (ContentFrame.CurrentSourcePageType != type || parameter != null) ContentFrame.Navigate(type, parameter);
            ContentFrame.BackStack.Clear();
            if (focusContent) FocusContentSoon();
        }

        public void OpenMovie(Movie m, string group)
        {
            ContentFrame.Navigate(typeof(MovieDetailPage), new MovieArgs { Movie = m, Group = group });
            FocusContentSoon();
        }

        public void OpenSeries(Series s, string group)
        {
            ContentFrame.Navigate(typeof(SeriesDetailPage), new SeriesArgs { Series = s, Group = group });
            FocusContentSoon();
        }

        void FocusContentSoon()
        {
            // Pendant l'intro, le focus attend la fin de l'animation
            if (introBlocking) return;
            // Pendant le changement de page, le focus peut passer un instant sur le menu : il reste fermé
            railQuietUntil = DateTime.UtcNow.AddMilliseconds(600);
            (ContentFrame.Content as TvPage)?.RequestFocus();
        }

        void OnNavClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.Tag is string key) Navigate(key);
        }

        // ───────────────────────────── Menu latéral ─────────────────────────────

        IEnumerable<Button> NavButtons => new[] { NavHome, NavLive, NavMovies, NavSeries, NavSearch, NavSettings };

        Button CurrentNavButton
        {
            get
            {
                switch (currentKey)
                {
                    case "live": return NavLive;
                    case "movies": return NavMovies;
                    case "series": return NavSeries;
                    case "search": return NavSearch;
                    case "settings": return NavSettings;
                    default: return NavHome;
                }
            }
        }

        void UpdateRail()
        {
            var current = CurrentNavButton;
            foreach (var b in NavButtons) b.Foreground = ReferenceEquals(b, current) ? Palette.AccentBright : Palette.TextDim;
        }

        public void FocusRail()
        {
            if (setupMode) return;
            railQuietUntil = DateTime.MinValue;
            CurrentNavButton.Focus(FocusState.Keyboard);
            SetRailOpen(true);
        }

        void SetRailOpen(bool open)
        {
            if (open == railOpen) return;
            railOpen = open;
            foreach (var b in NavButtons)
                if (b.Content is StackPanel sp && sp.Children.Count > 1)
                    sp.Children[1].Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            if (open) RailOpenAnim.Begin();
            else RailCloseAnim.Begin();
        }

        void OnGlobalGotFocus(object sender, FocusManagerGotFocusEventArgs e)
        {
            if (introBlocking) return;
            var el = e.NewFocusedElement;
            var inRail = Tv.IsInside(el, Rail);
            if (!setupMode && player == null)
            {
                // Focus déplacé par l'app (page qui change) : le menu ne s'ouvre pas pour autant
                if (!inRail) SetRailOpen(false);
                else if (DateTime.UtcNow >= railQuietUntil) SetRailOpen(true);
            }
            if (!inRail && el is Control c && Tv.IsInside(el, ContentFrame)) lastContentFocus = c;
        }

        /// <summary>En entrant dans le menu à la manette, le focus va sur la page en cours.</summary>
        void OnGlobalGettingFocus(object sender, GettingFocusEventArgs e)
        {
            // Pendant l'intro, l'app (encore invisible) ne prend pas le focus
            if (introBlocking)
            {
                if (e.NewFocusedElement != null && !Tv.IsInside(e.NewFocusedElement, IntroHost)) e.TryCancel();
                return;
            }
            if (setupMode || player != null || e.Direction == FocusNavigationDirection.None) return;
            var target = e.NewFocusedElement;
            if (target == null || !Tv.IsInside(target, Rail) || Tv.IsInside(e.OldFocusedElement, Rail)) return;
            var current = CurrentNavButton;
            if (!ReferenceEquals(target, current)) e.TrySetNewFocusedElement(current);
        }

        void OnRailKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var focused = FocusManager.GetFocusedElement();
            if (Keys.IsRight(e.Key))
            {
                e.Handled = true;
                if (!Tv.FocusControl(lastContentFocus)) (ContentFrame.Content as TvPage)?.FocusDefault();
            }
            else if (Keys.IsLeft(e.Key))
            {
                e.Handled = true;
            }
            else if (Keys.IsUp(e.Key) && ReferenceEquals(focused, NavHome))
            {
                e.Handled = true;
            }
            else if (Keys.IsDown(e.Key) && ReferenceEquals(focused, NavSettings))
            {
                e.Handled = true;
            }
        }

        // ───────────────────────────── Retour ─────────────────────────────

        void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (!e.Handled) e.Handled = Back();
        }

        void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Clavier (PC) : Échap fait comme Ⓑ
            if (e.Key == Windows.System.VirtualKey.Escape && !e.Handled) e.Handled = Back();
        }

        public bool Back()
        {
            if (introBlocking) return true;
            if (player != null)
            {
                player.HandleBack();
                return true;
            }
            if (ContentFrame.Content is TvPage page && page.HandleBack()) return true;
            if (setupMode) return false;
            if (ContentFrame.CanGoBack)
            {
                railQuietUntil = DateTime.UtcNow.AddMilliseconds(600);
                ContentFrame.GoBack();
                return true;
            }
            if (!railOpen)
            {
                FocusRail();
                return true;
            }
            if (currentKey != "home")
            {
                Navigate("home", focusContent: false);
                NavHome.Focus(FocusState.Keyboard);
                return true;
            }
            return false;
        }

        // ───────────────────────────── Lecteur ─────────────────────────────

        void OpenPlayer(PlayerRequest request)
        {
            if (player != null)
            {
                player.Load(request);
                return;
            }
            focusBeforePlayer = FocusManager.GetFocusedElement();
            SetRailOpen(false);
            player = new PlayerView();
            player.Closed += OnPlayerClosed;
            PlayerHost.Children.Add(player);
            PlayerHost.Visibility = Visibility.Visible;
            ContentFrame.Visibility = Visibility.Collapsed;
            Rail.Visibility = Visibility.Collapsed;
            player.Load(request);
        }

        void OnPlayerClosed()
        {
            var p = player;
            player = null;
            if (p != null)
            {
                p.Closed -= OnPlayerClosed;
                PlayerHost.Children.Remove(p);
            }
            PlayerHost.Visibility = Visibility.Collapsed;
            ContentFrame.Visibility = Visibility.Visible;
            Rail.Visibility = setupMode ? Visibility.Collapsed : Visibility.Visible;
            app.Library.NotifyChanged();
            var target = focusBeforePlayer;
            focusBeforePlayer = null;
            Tv.Later(() =>
            {
                if (!Tv.FocusControl(target)) (ContentFrame.Content as TvPage)?.FocusDefault();
            });
        }

        /// <summary>Retour de veille : le direct en cours est relancé.</summary>
        public void OnResuming() => player?.OnResuming();

        public void OnSuspending() => player?.SaveProgress();

        // ───────────────────────────── Messages ─────────────────────────────

        void ShowToast(string text)
        {
            ToastText.Text = text ?? "";
            ToastHide.Stop();
            ToastShow.Begin();
            toastTimer.Stop();
            toastTimer.Start();
        }

        // ───────────────────────────── Essais automatiques ─────────────────────────────

        /// <summary>
        /// Captures d'écran automatiques (serveur de compilation) : l'app s'ouvre sur la démonstration, sans
        /// rien enregistrer, puis affiche l'écran demandé.
        /// </summary>
        public async void StartTest(string screen)
        {
            if (screen == "intro" || screen == "intro-sortie")
            {
                // « intro-sortie » : animation complète jusqu'à l'accueil (photographiée en rafale)
                app.UseForTest(Source.Demo);
                PlayIntro(screen == "intro");
                ShowApp("home");
                return;
            }
            if (screen == "setup")
            {
                app.TestMode = true;
                ShowSetup();
                return;
            }
            app.UseForTest(Source.Demo);
            ShowApp("home");
            await Task.Delay(1500);
            switch (screen)
            {
                case "live":
                case "movies":
                case "series":
                case "settings":
                    Navigate(screen);
                    break;
                case "search":
                    Navigate("search", "bunny");
                    break;
                case "rail":
                    FocusRail();
                    break;
                case "movie":
                    OpenMovie(Demo.Movies[0], Demo.VodCategory.Name);
                    break;
                case "seriesdetail":
                    OpenSeries(Demo.SeriesList[0], Demo.SeriesCategory.Name);
                    break;
                case "credits":
                    Navigate("settings");
                    await Task.Delay(800);
                    (ContentFrame.Content as SettingsPage)?.ShowCreditsForTest();
                    break;
                case "play-live":
                case "play-channels":
                    app.Play(Demo.Channels[screen == "play-live" ? 0 : 1], Demo.Channels);
                    await Task.Delay(6000);
                    player?.ShowForTest(screen == "play-channels" ? "channels" : "controls");
                    break;
                case "play-movie":
                case "play-options":
                    app.Play(Demo.Movies[1], Demo.VodCategory.Name, true);
                    await Task.Delay(6000);
                    player?.ShowForTest(screen == "play-options" ? "options" : "controls");
                    break;
                case "row":
                    if (ContentFrame.Content is HomePage home) home.FocusFirstRow();
                    break;
            }
        }
    }
}
