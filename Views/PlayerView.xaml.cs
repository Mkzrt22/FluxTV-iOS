using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace FluxTV.Views
{
    /// <summary>
    /// Lecteur plein écran (lecteur vidéo de la console : HLS, MP4, MKV, Dolby).
    /// Direct : ⬆⬇ ou LB/RB zappent, Ⓧ ouvre la liste des chaînes. Films et séries : ⬅➡ reculent ou
    /// avancent, Ⓐ met en pause. Ⓨ : audio, sous-titres et format d'image. Ⓑ : masque, puis ferme.
    /// </summary>
    public sealed partial class PlayerView : UserControl
    {
        enum Overlay { Hidden, Banner, Full }

        /// <summary>Format du direct qui a marché pendant cette session (m3u8 ou ts), après un échec de l'autre.</summary>
        static string workingLiveExt;

        public event Action Closed;

        readonly AppState app = AppState.Current;
        readonly MediaPlayer player;
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        readonly DispatcherTimer upNextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        readonly DisplayRequest display = new DisplayRequest();
        bool displayActive;

        PlayerRequest request;
        PlayItem current;
        MediaPlaybackItem item;
        AdaptiveMediaSource adaptive;
        string currentUrl;
        int generation;
        Overlay overlay = Overlay.Hidden;
        DateTime lastInteraction = DateTime.UtcNow;
        DateTime lastSave = DateTime.UtcNow;
        DateTime lastSeekKey;
        DateTime openedAt;
        int seekRepeat;
        bool closed;
        bool opened;
        bool failedShown;
        bool endedHandled;
        bool triedAlternate;
        int failures;
        Episode upNextEpisode;
        int upNextLeft;
        List<ChannelItem> panelChannels = new List<ChannelItem>();
        bool holdOverlay;

        public PlayerView()
        {
            InitializeComponent();
            player = new MediaPlayer { AutoPlay = true, AudioCategory = MediaPlayerAudioCategory.Movie };
            player.MediaOpened += (s, e) =>
            {
                var source = s.Source;
                Ui(() => { if (ReferenceEquals(source, item)) OnOpened(); });
            };
            player.MediaFailed += (s, e) =>
            {
                var source = s.Source;
                var error = e.Error;
                var code = e.ExtendedErrorCode?.HResult ?? 0;
                Ui(() => { if (ReferenceEquals(source, item)) OnFailed(error, code, 0); });
            };
            player.MediaEnded += (s, e) =>
            {
                var source = s.Source;
                Ui(() => { if (ReferenceEquals(source, item)) OnEnded(); });
            };
            player.PlaybackSession.PlaybackStateChanged += (s, e) => Ui(UpdateState);
            player.PlaybackSession.BufferingStarted += (s, e) => Ui(UpdateState);
            player.PlaybackSession.BufferingEnded += (s, e) => Ui(UpdateState);
            // Télécommande multimédia : chaîne ou épisode précédent / suivant
            player.CommandManager.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
            player.CommandManager.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Always;
            player.CommandManager.NextReceived += (s, e) =>
            {
                e.Handled = true;
                Ui(() => Skip(+1));
            };
            player.CommandManager.PreviousReceived += (s, e) =>
            {
                e.Handled = true;
                Ui(() => Skip(-1));
            };
            Video.SetMediaPlayer(player);
            timer.Tick += (s, e) => OnTick();
            upNextTimer.Tick += (s, e) => OnUpNextTick();
            ControlsOut.Completed += (s, e) =>
            {
                if (overlay == Overlay.Hidden) Controls.Visibility = Visibility.Collapsed;
            };
            app.ProgrammesLoaded += OnProgrammes;
            Loaded += (s, e) => Focus(FocusState.Programmatic);
        }

        void Ui(Action a)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!closed) a();
            });
        }

        // ───────────────────────────── Ouverture ─────────────────────────────

        public void Load(PlayerRequest req)
        {
            request = req;
            var live = req.Item.IsLive;
            LiveInfo.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
            VodInfo.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
            Back10Button.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
            Forward30Button.Visibility = live ? Visibility.Collapsed : Visibility.Visible;
            ChannelsButton.Visibility = live && req.Channels.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            ErrorChannels.Visibility = ChannelsButton.Visibility;
            FavoriteButton.Visibility = live ? Visibility.Visible : Visibility.Collapsed;
            timer.Start();
            Start(req.Item);
            ShowOverlay(Overlay.Banner);
        }

        void Start(PlayItem pi)
        {
            var gen = ++generation;
            current = pi;
            opened = false;
            endedHandled = false;
            failedShown = false;
            failures = 0;
            triedAlternate = false;
            HideError();
            HideUpNext();
            currentUrl = pi.Url;
            if (pi.IsLive && workingLiveExt != null && !pi.Url.EndsWith("." + workingLiveExt, StringComparison.OrdinalIgnoreCase))
            {
                var alt = AlternateUrl(pi.Url);
                if (alt != null && alt.EndsWith("." + workingLiveExt, StringComparison.OrdinalIgnoreCase)) currentUrl = alt;
            }
            TitleText.Text = pi.Title ?? "";
            SubtitleText.Text = pi.Subtitle ?? "";
            LoadingArt.Url = pi.Artwork;
            LoadingArt.Visibility = Visibility.Visible;
            LoadingText.Text = pi.IsLive ? "Connexion à la chaîne…" : "Chargement…";
            SetLoading(true);
            NextButton.Visibility = NextEpisode() != null ? Visibility.Visible : Visibility.Collapsed;
            UpdateInfo();
            _ = OpenAsync(gen, currentUrl);
        }

        static bool IsHls(string url)
        {
            var path = url;
            var q = path.IndexOf('?');
            if (q >= 0) path = path.Substring(0, q);
            return path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase);
        }

        async Task OpenAsync(int gen, string url)
        {
            MediaSource source;
            try
            {
                var uri = new Uri(url);
                if (IsHls(url))
                {
                    var http = new Windows.Web.Http.HttpClient();
                    http.DefaultRequestHeaders.UserAgent.TryParseAdd(current.UserAgent ?? HttpLoader.UserAgent);
                    var result = await AdaptiveMediaSource.CreateFromUriAsync(uri, http);
                    if (gen != generation || closed) return;
                    if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
                    {
                        adaptive = result.MediaSource;
                        // Meilleure qualité dès le départ (télé sur une bonne connexion)
                        if (adaptive.AvailableBitrates.Count > 0) adaptive.InitialBitrate = adaptive.AvailableBitrates.Max();
                        source = MediaSource.CreateFromAdaptiveMediaSource(adaptive);
                    }
                    else if (result.Status == AdaptiveMediaSourceCreationStatus.ManifestDownloadFailure)
                    {
                        var status = result.HttpResponseMessage != null ? (int)result.HttpResponseMessage.StatusCode : 0;
                        OnFailed(MediaPlayerError.NetworkError, result.ExtendedError?.HResult ?? 0, status);
                        return;
                    }
                    else
                    {
                        // Pas une vraie playlist HLS : lecture directe
                        source = MediaSource.CreateFromUri(uri);
                    }
                }
                else
                {
                    source = MediaSource.CreateFromUri(uri);
                }
            }
            catch (Exception e)
            {
                if (gen == generation) OnFailed(MediaPlayerError.SourceNotSupported, e.HResult, 0);
                return;
            }
            if (gen != generation || closed) return;
            var playback = new MediaPlaybackItem(source);
            ApplyDisplayProperties(playback, current);
            playback.VideoTracksChanged += (s, e) => Ui(() => { if (ReferenceEquals(s, item)) MatchFrameRate(); });
            item = playback;
            player.Source = playback;
            player.Play();
        }

        void ApplyDisplayProperties(MediaPlaybackItem playback, PlayItem pi)
        {
            try
            {
                var props = playback.GetDisplayProperties();
                props.Type = MediaPlaybackType.Video;
                props.VideoProperties.Title = pi.Title ?? "";
                props.VideoProperties.Subtitle = pi.Subtitle ?? "";
                if (Uri.TryCreate(pi.Artwork ?? "", UriKind.Absolute, out var art) && (art.Scheme == "http" || art.Scheme == "https"))
                    props.Thumbnail = RandomAccessStreamReference.CreateFromUri(art);
                playback.ApplyDisplayProperties(props);
            }
            catch (Exception) { }
        }

        void OnOpened()
        {
            opened = true;
            openedAt = DateTime.UtcNow;
            LoadingArt.Visibility = Visibility.Collapsed;
            if (current.IsLive && triedAlternate)
            {
                // L'autre format a marché : il servira pour les chaînes suivantes
                workingLiveExt = currentUrl.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ? "ts" : "m3u8";
            }
            var session = player.PlaybackSession;
            var duration = session.NaturalDuration.TotalSeconds;
            if (!current.IsLive && current.StartAt is double start && start > 0 && (duration <= 0 || start < duration - 15))
            {
                try { session.Position = TimeSpan.FromSeconds(start); }
                catch (Exception) { }
            }
            UpdateState();
            MatchFrameRate();
            UpdateInfo();
        }

        async void MatchFrameRate()
        {
            if (!app.Settings.MatchFrameRate || !Device.IsXbox || item == null) return;
            try
            {
                if (item.VideoTracks.Count == 0) return;
                var props = item.VideoTracks[0].GetEncodingProperties();
                var rate = props?.FrameRate;
                if (rate == null || rate.Numerator == 0 || rate.Denominator == 0) return;
                await DisplayRate.MatchAsync(rate.Numerator / (double)rate.Denominator);
            }
            catch (Exception) { }
        }

        // ───────────────────────────── États ─────────────────────────────

        void UpdateState()
        {
            var state = player.PlaybackSession.PlaybackState;
            var busy = !failedShown && (state == MediaPlaybackState.Opening || state == MediaPlaybackState.Buffering || (!opened && state != MediaPlaybackState.Paused));
            SetLoading(busy);
            PlayPauseGlyph.Text = state == MediaPlaybackState.Paused ? "" : "";
            KeepDisplayOn(state == MediaPlaybackState.Playing || state == MediaPlaybackState.Buffering || state == MediaPlaybackState.Opening);
        }

        void SetLoading(bool busy)
        {
            LoadingPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BufferRing.IsActive = busy;
        }

        void KeepDisplayOn(bool on)
        {
            if (on == displayActive) return;
            try
            {
                if (on) display.RequestActive();
                else display.RequestRelease();
                displayActive = on;
            }
            catch (Exception) { }
        }

        void OnFailed(MediaPlayerError error, int code, int httpStatus)
        {
            if (closed || failedShown) return;
            // Un direct qui a bien marché un moment a de nouveau droit à trois reconnexions
            if (current.IsLive)
            {
                if (opened && DateTime.UtcNow - openedAt > TimeSpan.FromSeconds(30)) failures = 0;
                opened = false;
            }
            Journal.Write("Lecture impossible : " + error + " (0x" + code.ToString("X8") + (httpStatus > 0 ? ", HTTP " + httpStatus : "") + ")"
                          + (current.IsLive ? ", direct" : ""));
            // Direct : on réessaie, et on essaie l'autre format (HLS ou TS)
            if (current.IsLive && failures < 3 && httpStatus != 401 && httpStatus != 403)
            {
                failures++;
                var alt = AlternateUrl(currentUrl);
                if (!triedAlternate && alt != null)
                {
                    triedAlternate = true;
                    currentUrl = alt;
                }
                LoadingText.Text = "Reconnexion…";
                SetLoading(true);
                _ = RetryLaterAsync(generation, 1200 * failures);
                return;
            }
            ShowError(error, code, httpStatus);
        }

        async Task RetryLaterAsync(int gen, int ms)
        {
            await Task.Delay(ms);
            if (gen != generation || closed) return;
            await OpenAsync(gen, currentUrl);
        }

        /// <summary>Même chaîne Xtream dans l'autre format (…/123.m3u8 ↔ …/123.ts).</summary>
        static string AlternateUrl(string url)
        {
            if (AppState.Current.Source?.Kind != SourceKind.Xtream || url == null || url.Contains("?")) return null;
            if (url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) return url.Substring(0, url.Length - 5) + ".ts";
            if (url.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)) return url.Substring(0, url.Length - 3) + ".m3u8";
            return null;
        }

        void ShowError(MediaPlayerError error, int code, int httpStatus)
        {
            failedShown = true;
            SetLoading(false);
            HideOverlay();
            ErrorTitle.Text = current.IsLive ? "Impossible de lire cette chaîne" : "Impossible de lire cette vidéo";
            ErrorText.Text = Describe(error, code, httpStatus, current.IsLive);
            ErrorPanel.Visibility = Visibility.Visible;
            RetryButton.Focus(FocusState.Keyboard);
        }

        static string Describe(MediaPlayerError error, int code, int httpStatus, bool live)
        {
            if (httpStatus == 401 || httpStatus == 403)
                return "Le serveur refuse l'accès (code " + httpStatus + ") : abonnement expiré, ou trop d'écrans connectés en même temps.";
            if (httpStatus == 404) return "Ce flux est introuvable sur le serveur (code 404).";
            if (httpStatus >= 500) return "Le serveur du fournisseur ne répond pas correctement (code " + httpStatus + "). Réessaie dans un instant.";
            switch (error)
            {
                case MediaPlayerError.NetworkError:
                    return "La connexion au flux a échoué. Vérifie la connexion de la console, ou réessaie dans un instant.";
                case MediaPlayerError.DecodingError:
                    return "Le format de cette vidéo n'est pas pris en charge par le lecteur de la Xbox.";
                case MediaPlayerError.SourceNotSupported:
                    return live
                        ? "Ce flux n'est pas lisible par le lecteur de la Xbox. Dans les réglages, essaie l'autre « Format du direct »."
                        : "Ce fichier n'est pas lisible par le lecteur de la Xbox (format ou codec non pris en charge).";
                case MediaPlayerError.Aborted:
                    return "La lecture a été interrompue.";
                default:
                    return "Erreur de lecture (code 0x" + code.ToString("X8") + ").";
            }
        }

        void HideError() => ErrorPanel.Visibility = Visibility.Collapsed;

        void OnRetry(object sender, RoutedEventArgs e)
        {
            var resume = opened && !current.IsLive ? player.PlaybackSession.Position.TotalSeconds : (double?)null;
            Start(resume != null && resume > 0 ? current.With(resume) : current);
            Focus(FocusState.Programmatic);
        }

        void OnClose(object sender, RoutedEventArgs e) => Close();

        void OnEnded()
        {
            if (endedHandled) return;
            if (current.IsLive)
            {
                // Flux coupé : reconnexion
                OnFailed(MediaPlayerError.NetworkError, 0, 0);
                return;
            }
            endedHandled = true;
            SaveProgress(true);
            var next = NextEpisode();
            if (next != null && app.Settings.AutoplayNext) StartUpNext(next);
            else Close();
        }

        // ───────────────────────────── Fermeture ─────────────────────────────

        /// <summary>Position enregistrée (reprise), ou contenu marqué vu à la fin.</summary>
        public void SaveProgress(bool finished = false)
        {
            if (current == null || current.IsLive || !opened || closed) return;
            var session = player.PlaybackSession;
            var duration = session.NaturalDuration.TotalSeconds;
            if (duration <= 0 || double.IsInfinity(duration) || double.IsNaN(duration)) return;
            var position = finished ? duration : session.Position.TotalSeconds;
            app.Library.SavePosition(current.Ref, position, duration);
            lastSave = DateTime.UtcNow;
        }

        public void Close()
        {
            if (closed) return;
            SaveProgress();
            closed = true;
            timer.Stop();
            upNextTimer.Stop();
            app.ProgrammesLoaded -= OnProgrammes;
            KeepDisplayOn(false);
            try
            {
                player.Pause();
                player.Source = null;
            }
            catch (Exception) { }
            Video.SetMediaPlayer(null);
            player.Dispose();
            item = null;
            adaptive = null;
            _ = DisplayRate.RestoreAsync();
            Closed?.Invoke();
        }

        /// <summary>Ⓑ : panneau, puis commandes, puis lecteur.</summary>
        public void HandleBack()
        {
            if (UpNext.Visibility == Visibility.Visible)
            {
                HideUpNext();
                Close();
            }
            else if (SidePanel.Visibility == Visibility.Visible) ClosePanel();
            else if (ErrorPanel.Visibility == Visibility.Visible) Close();
            else if (overlay != Overlay.Hidden) HideOverlay();
            else Close();
        }

        /// <summary>Essais automatiques : commandes, liste des chaînes ou options affichées et gardées à l'écran.</summary>
        public void ShowForTest(string what)
        {
            holdOverlay = true;
            if (what == "channels") OpenChannels();
            else if (what == "options") OpenOptions();
            else ShowOverlay(Overlay.Full);
        }

        /// <summary>Retour de veille : le direct reprend en temps réel.</summary>
        public void OnResuming()
        {
            if (closed || current == null || !current.IsLive) return;
            Start(current);
        }

        // ───────────────────────────── Commandes ─────────────────────────────

        void ShowOverlay(Overlay o)
        {
            lastInteraction = DateTime.UtcNow;
            if (o == Overlay.Hidden)
            {
                HideOverlay();
                return;
            }
            if (overlay == Overlay.Full && o == Overlay.Banner) o = Overlay.Full;
            Buttons.Visibility = o == Overlay.Full ? Visibility.Visible : Visibility.Collapsed;
            if (overlay == Overlay.Hidden)
            {
                Controls.Visibility = Visibility.Visible;
                ControlsIn.Begin();
            }
            overlay = o;
            UpdateInfo();
            if (o == Overlay.Full)
            {
                var focused = FocusManager.GetFocusedElement() as DependencyObject;
                if (!Tv.IsInside(focused, Controls))
                {
                    if (current.IsLive && ChannelsButton.Visibility == Visibility.Visible) ChannelsButton.Focus(FocusState.Keyboard);
                    else PlayPauseButton.Focus(FocusState.Keyboard);
                }
            }
        }

        void HideOverlay()
        {
            if (overlay == Overlay.Hidden) return;
            overlay = Overlay.Hidden;
            ControlsOut.Begin();
            Focus(FocusState.Programmatic);
        }

        void OnTick()
        {
            if (closed || current == null) return;
            var now = DateTime.UtcNow;
            ClockText.Text = DateTime.Now.ToString("HH:mm");
            if (overlay != Overlay.Hidden) UpdateInfo();
            var playing = player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            if (overlay != Overlay.Hidden && playing && SidePanel.Visibility != Visibility.Visible && !holdOverlay)
            {
                var idle = (now - lastInteraction).TotalSeconds;
                if (idle > (overlay == Overlay.Banner ? 4.5 : 7)) HideOverlay();
            }
            if (!current.IsLive && opened && playing && (now - lastSave).TotalSeconds > 15) SaveProgress();
        }

        void UpdateInfo()
        {
            if (current == null) return;
            if (current.IsLive) UpdateLiveInfo();
            else UpdateVodInfo();
        }

        LiveChannel CurrentChannel =>
            request != null && request.ChannelIndex >= 0 && request.ChannelIndex < request.Channels.Count
                ? request.Channels[request.ChannelIndex]
                : app.Channel(current?.Ref.Id);

        void UpdateLiveInfo()
        {
            var ch = CurrentChannel;
            LiveLogo.Url = ch?.Logo ?? current.Artwork;
            LiveNumber.Text = ch != null && ch.Num > 0 ? "N° " + ch.Num : "";
            TitleText.Text = ch?.Name ?? current.Title;
            SubtitleText.Text = ch != null ? app.CategoryName(ch.CategoryId) ?? "" : current.Subtitle ?? "";
            if (ch != null) app.RequestProgrammes(ch);
            var (now, next) = app.NowNext(ch);
            LiveNow.Text = now?.Title ?? "Programme non communiqué";
            LiveProgress.Visibility = now != null ? Visibility.Visible : Visibility.Collapsed;
            LiveProgress.Value = now?.Progress(DateTimeOffset.UtcNow) ?? 0;
            LiveTimes.Text = now != null ? Format.Hour(now.Start) + " – " + Format.Hour(now.Stop) : "";
            LiveNext.Text = next != null ? "À suivre · " + Format.Hour(next.Start) + "  " + next.Title : "";
            var fav = app.Library.IsFavorite(current.Ref.Id);
            FavoriteGlyph.Text = fav ? "" : "";
            FavoriteGlyph.Foreground = fav ? Palette.Gold : Palette.Text;
        }

        void OnProgrammes(string channelId)
        {
            if (!closed && current != null && current.IsLive && current.Ref.Id == channelId && overlay != Overlay.Hidden) UpdateLiveInfo();
        }

        void UpdateVodInfo()
        {
            var session = player.PlaybackSession;
            var position = session.Position.TotalSeconds;
            var duration = session.NaturalDuration.TotalSeconds;
            PositionText.Text = Format.Clock(Math.Max(0, position));
            if (duration > 0 && !double.IsInfinity(duration))
            {
                var left = Math.Max(0, duration - position);
                RemainText.Text = "−" + Format.Clock(left) + " · fin à " + DateTime.Now.AddSeconds(left).ToString("HH:mm");
                var width = SeekTrack.ActualWidth;
                var fill = Math.Max(0, Math.Min(1, position / duration)) * width;
                SeekFill.Width = fill;
                SeekKnob.Margin = new Thickness(Math.Max(0, fill - 8), 0, 0, 0);
            }
            else
            {
                RemainText.Text = "";
                SeekFill.Width = 0;
            }
        }

        void OnSeekFocus(object sender, RoutedEventArgs e)
        {
            var focused = ReferenceEquals(FocusManager.GetFocusedElement(), SeekBar);
            SeekKnob.Opacity = focused ? 1 : 0;
            SeekTrack.Height = focused ? 7 : 5;
            SeekFill.Height = focused ? 7 : 5;
        }

        void ShowFlash(string text)
        {
            FlashText.Text = text;
            FlashAnim.Stop();
            FlashAnim.Begin();
        }

        void TogglePause()
        {
            var state = player.PlaybackSession.PlaybackState;
            if (state == MediaPlaybackState.Paused)
            {
                player.Play();
            }
            else if (player.PlaybackSession.CanPause)
            {
                player.Pause();
                SaveProgress();
            }
        }

        void Seek(double delta)
        {
            var session = player.PlaybackSession;
            if (current.IsLive || !opened || !session.CanSeek) return;
            var duration = session.NaturalDuration.TotalSeconds;
            var target = Math.Max(0, session.Position.TotalSeconds + delta);
            if (duration > 0) target = Math.Min(target, Math.Max(0, duration - 2));
            session.Position = TimeSpan.FromSeconds(target);
            var amount = Math.Abs(delta) >= 60 ? (int)(Math.Abs(delta) / 60) + " min" : (int)Math.Abs(delta) + " s";
            ShowFlash((delta < 0 ? "−" : "+") + amount);
            ShowOverlay(Overlay.Banner);
            UpdateVodInfo();
        }

        /// <summary>Pas d'avance : 10 s, puis 30 s et 60 s si la touche reste enfoncée.</summary>
        double SeekStep(KeyRoutedEventArgs e)
        {
            var now = DateTime.UtcNow;
            seekRepeat = e.KeyStatus.WasKeyDown || (now - lastSeekKey).TotalMilliseconds < 450 ? seekRepeat + 1 : 0;
            lastSeekKey = now;
            return seekRepeat > 14 ? 60 : seekRepeat > 5 ? 30 : 10;
        }

        void OnPlayPause(object sender, RoutedEventArgs e)
        {
            TogglePause();
            lastInteraction = DateTime.UtcNow;
        }

        void OnBack10(object sender, RoutedEventArgs e) => Seek(-10);

        void OnForward30(object sender, RoutedEventArgs e) => Seek(30);

        void OnFavorite(object sender, RoutedEventArgs e)
        {
            var ch = CurrentChannel;
            if (ch == null) return;
            app.ToggleFavorite(ch.Ref(app.CategoryName(ch.CategoryId)));
            UpdateLiveInfo();
        }

        void Skip(int delta)
        {
            if (current == null) return;
            if (current.IsLive) Zap(delta);
            else if (delta > 0 && NextEpisode() is Episode next) PlayEpisode(next);
        }

        // ───────────────────────────── Direct : zapping ─────────────────────────────

        void Zap(int delta)
        {
            if (request == null || !current.IsLive || request.Channels.Count < 2) return;
            var n = request.Channels.Count;
            var i = request.ChannelIndex < 0 ? 0 : request.ChannelIndex;
            ZapTo(((i + delta) % n + n) % n);
        }

        void ZapTo(int index)
        {
            if (index < 0 || index >= request.Channels.Count) return;
            var ch = request.Channels[index];
            var pi = app.ItemFor(ch);
            if (pi == null) return;
            request.ChannelIndex = index;
            app.Library.AddHistory(pi.Ref);
            Start(pi);
            ShowOverlay(Overlay.Banner);
        }

        // ───────────────────────────── Séries : épisode suivant ─────────────────────────────

        Episode NextEpisode()
        {
            if (request?.Episodes == null || current?.Ref.Kind != ContentKind.Episode) return null;
            var i = request.Episodes.FindIndex(e => e.Id == current.Ref.Id);
            return i >= 0 && i + 1 < request.Episodes.Count ? request.Episodes[i + 1] : null;
        }

        void PlayEpisode(Episode e)
        {
            var s = request?.Series;
            if (s == null) return;
            SaveProgress();
            var cover = request.Cover ?? s.Cover;
            var r = e.Ref(s.SeriesId, s.Name, cover);
            var url = PlaybackResolver.Url(r, app.Source, app.LiveExtension);
            if (url == null) return;
            app.Library.AddHistory(r);
            Start(new PlayItem
            {
                Ref = r, Url = url, Title = s.Name, Subtitle = e.Label + " · " + e.Title, Artwork = e.Image ?? cover,
                StartAt = app.Library.Resume(r.Id)?.Position,
            });
            ShowOverlay(Overlay.Banner);
        }

        void OnNextEpisode(object sender, RoutedEventArgs e)
        {
            if (NextEpisode() is Episode next) PlayEpisode(next);
        }

        void StartUpNext(Episode e)
        {
            upNextEpisode = e;
            upNextLeft = 8;
            UpNextTitle.Text = e.Label + " · " + e.Title;
            UpNextCountdown.Text = "Dans " + upNextLeft + " s";
            UpNext.Visibility = Visibility.Visible;
            HideOverlay();
            UpNextPlay.Focus(FocusState.Keyboard);
            upNextTimer.Start();
        }

        void HideUpNext()
        {
            upNextTimer.Stop();
            UpNext.Visibility = Visibility.Collapsed;
        }

        void OnUpNextTick()
        {
            upNextLeft--;
            if (upNextLeft > 0)
            {
                UpNextCountdown.Text = "Dans " + upNextLeft + " s";
                return;
            }
            PlayUpNext();
        }

        void PlayUpNext()
        {
            var e = upNextEpisode;
            HideUpNext();
            if (e == null) return;
            Focus(FocusState.Programmatic);
            PlayEpisode(e);
        }

        void OnUpNextPlay(object sender, RoutedEventArgs e) => PlayUpNext();

        void OnUpNextCancel(object sender, RoutedEventArgs e)
        {
            HideUpNext();
            Close();
        }

        // ───────────────────────────── Panneaux ─────────────────────────────

        void OnChannels(object sender, RoutedEventArgs e) => OpenChannels();

        void OnTracks(object sender, RoutedEventArgs e) => OpenOptions();

        void OpenChannels()
        {
            if (request == null || !current.IsLive || request.Channels.Count < 2) return;
            HideOverlay();
            HideError();
            PanelTitle.Text = "Chaînes";
            panelChannels = request.Channels.Select(ch =>
            {
                var now = app.NowNext(ch).Now;
                return new ChannelItem(ch)
                {
                    IsFavorite = app.Library.IsFavorite(ch.Id),
                    NowTitle = now?.Title,
                    Progress = now?.Progress(DateTimeOffset.UtcNow) ?? 0,
                };
            }).ToList();
            PanelChannels.ItemsSource = panelChannels;
            PanelChannels.Visibility = Visibility.Visible;
            PanelOptions.Visibility = Visibility.Collapsed;
            SidePanel.Visibility = Visibility.Visible;
            var target = request.ChannelIndex >= 0 && request.ChannelIndex < panelChannels.Count ? panelChannels[request.ChannelIndex] : panelChannels.FirstOrDefault();
            _ = Tv.FocusItemAsync(PanelChannels, target);
        }

        void OnPanelChannelRealized(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || !(args.Item is ChannelItem c)) return;
            app.RequestProgrammes(c.Channel);
            var now = app.NowNext(c.Channel).Now;
            c.NowTitle = now?.Title;
            c.Progress = now?.Progress(DateTimeOffset.UtcNow) ?? 0;
        }

        void OnPanelChannel(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is ChannelItem c)) return;
            var index = request.Channels.IndexOf(c.Channel);
            ClosePanel();
            if (index >= 0 && index != request.ChannelIndex) ZapTo(index);
            else if (failedShown) Start(current);
        }

        void OpenOptions()
        {
            HideOverlay();
            PanelTitle.Text = "Audio, sous-titres, image";
            AudioList.ItemsSource = AudioOptions();
            SubtitleList.ItemsSource = SubtitleOptions();
            ImageList.ItemsSource = ImageOptions();
            PanelChannels.Visibility = Visibility.Collapsed;
            PanelOptions.Visibility = Visibility.Visible;
            SidePanel.Visibility = Visibility.Visible;
            PanelOptions.ChangeView(null, 0, null, true);
            var audio = (List<OptionItem>)AudioList.ItemsSource;
            _ = Tv.FocusItemAsync(AudioList, audio.FirstOrDefault(o => o.IsCurrent) ?? audio.FirstOrDefault());
        }

        void ClosePanel()
        {
            SidePanel.Visibility = Visibility.Collapsed;
            PanelChannels.ItemsSource = null;
            panelChannels = new List<ChannelItem>();
            if (failedShown) RetryButton.Focus(FocusState.Keyboard);
            else Focus(FocusState.Programmatic);
        }

        List<OptionItem> AudioOptions()
        {
            var list = new List<OptionItem>();
            var tracks = item?.AudioTracks;
            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    var t = tracks[i];
                    var language = LanguageName(t.Language);
                    var title = !string.IsNullOrWhiteSpace(t.Label) ? t.Label : language ?? "Piste " + (i + 1);
                    list.Add(new OptionItem
                    {
                        Title = title,
                        Detail = language != null && language != title ? language : null,
                        Index = i,
                        IsCurrent = i == tracks.SelectedIndex,
                    });
                }
            }
            if (list.Count == 0) list.Add(new OptionItem { Title = "Piste par défaut", Index = -1, IsCurrent = true });
            return list;
        }

        List<OptionItem> SubtitleOptions()
        {
            var list = new List<OptionItem>();
            var tracks = item?.TimedMetadataTracks;
            var anyShown = false;
            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    var t = tracks[i];
                    if (t.TimedMetadataKind != TimedMetadataKind.Subtitle && t.TimedMetadataKind != TimedMetadataKind.Caption) continue;
                    var shown = tracks.GetPresentationMode((uint)i) == TimedMetadataTrackPresentationMode.PlatformPresented;
                    anyShown |= shown;
                    var language = LanguageName(t.Language);
                    var title = !string.IsNullOrWhiteSpace(t.Label) ? t.Label : language ?? "Sous-titres " + (list.Count + 1);
                    list.Add(new OptionItem
                    {
                        Title = title,
                        Detail = language != null && language != title ? language : null,
                        Index = i,
                        IsCurrent = shown,
                    });
                }
            }
            list.Insert(0, new OptionItem { Title = "Désactivés", Index = -1, IsCurrent = !anyShown });
            return list;
        }

        List<OptionItem> ImageOptions()
        {
            var s = Video.Stretch;
            return new List<OptionItem>
            {
                new OptionItem { Title = "Adapter à l'écran", Detail = "Image entière", Index = (int)Stretch.Uniform, IsCurrent = s == Stretch.Uniform },
                new OptionItem { Title = "Remplir l'écran", Detail = "Sans bandes noires, bords coupés", Index = (int)Stretch.UniformToFill, IsCurrent = s == Stretch.UniformToFill },
                new OptionItem { Title = "Étirer", Detail = "Image déformée", Index = (int)Stretch.Fill, IsCurrent = s == Stretch.Fill },
            };
        }

        void OnAudioClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is OptionItem o) || o.Index < 0 || item == null) return;
            try { item.AudioTracks.SelectedIndex = o.Index; }
            catch (Exception) { }
            foreach (var x in (List<OptionItem>)AudioList.ItemsSource) x.IsCurrent = x == o;
        }

        void OnSubtitleClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is OptionItem o) || item == null) return;
            var tracks = item.TimedMetadataTracks;
            try
            {
                for (int i = 0; i < tracks.Count; i++)
                    tracks.SetPresentationMode((uint)i, i == o.Index ? TimedMetadataTrackPresentationMode.PlatformPresented : TimedMetadataTrackPresentationMode.Disabled);
            }
            catch (Exception) { }
            foreach (var x in (List<OptionItem>)SubtitleList.ItemsSource) x.IsCurrent = x == o;
        }

        void OnImageClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is OptionItem o)) return;
            Video.Stretch = (Stretch)o.Index;
            foreach (var x in (List<OptionItem>)ImageList.ItemsSource) x.IsCurrent = x == o;
        }

        static readonly Dictionary<string, string> Languages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fr"] = "Français", ["fre"] = "Français", ["fra"] = "Français",
            ["en"] = "Anglais", ["eng"] = "Anglais",
            ["de"] = "Allemand", ["ger"] = "Allemand", ["deu"] = "Allemand",
            ["es"] = "Espagnol", ["spa"] = "Espagnol",
            ["it"] = "Italien", ["ita"] = "Italien",
            ["pt"] = "Portugais", ["por"] = "Portugais",
            ["ar"] = "Arabe", ["ara"] = "Arabe",
            ["tr"] = "Turc", ["tur"] = "Turc",
            ["nl"] = "Néerlandais", ["dut"] = "Néerlandais", ["nld"] = "Néerlandais",
            ["pl"] = "Polonais", ["pol"] = "Polonais",
            ["ru"] = "Russe", ["rus"] = "Russe",
            ["ja"] = "Japonais", ["jpn"] = "Japonais",
            ["zh"] = "Chinois", ["chi"] = "Chinois", ["zho"] = "Chinois",
            ["ko"] = "Coréen", ["kor"] = "Coréen",
            ["qaa"] = "Version originale", ["mul"] = "Plusieurs langues", ["und"] = null,
        };

        static string LanguageName(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var key = code.Trim();
            var dash = key.IndexOf('-');
            if (dash > 0) key = key.Substring(0, dash);
            return Languages.TryGetValue(key, out var name) ? name : code.Trim();
        }

        // ───────────────────────────── Manette ─────────────────────────────

        void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var k = e.Key;
            lastInteraction = DateTime.UtcNow;
            if (k == Windows.System.VirtualKey.GamepadB || k == Windows.System.VirtualKey.Escape) return;
            if (SidePanel.Visibility == Visibility.Visible || ErrorPanel.Visibility == Visibility.Visible
                || UpNext.Visibility == Visibility.Visible || current == null)
                return;
            var live = current.IsLive;

            // Raccourcis valables partout
            if (Keys.IsPreviousPage(k))
            {
                if (live) Zap(-1);
                else Seek(-30);
                e.Handled = true;
                return;
            }
            if (Keys.IsNextPage(k))
            {
                if (live) Zap(+1);
                else Seek(+30);
                e.Handled = true;
                return;
            }
            if (Keys.IsLeftTrigger(k) || Keys.IsRightTrigger(k))
            {
                if (!live) Seek(Keys.IsLeftTrigger(k) ? -300 : 300);
                e.Handled = true;
                return;
            }
            if (Keys.IsX(k))
            {
                if (live) OpenChannels();
                else OpenOptions();
                e.Handled = true;
                return;
            }
            if (Keys.IsFavorite(k) || Keys.IsMenu(k))
            {
                OpenOptions();
                e.Handled = true;
                return;
            }
            if (Keys.IsView(k))
            {
                ShowOverlay(overlay == Overlay.Hidden ? Overlay.Banner : overlay);
                e.Handled = true;
                return;
            }

            var focused = FocusManager.GetFocusedElement() as DependencyObject;
            if (overlay == Overlay.Full && ReferenceEquals(focused, SeekBar))
            {
                if (Keys.IsLeft(k)) { Seek(-SeekStep(e)); e.Handled = true; }
                else if (Keys.IsRight(k)) { Seek(SeekStep(e)); e.Handled = true; }
                return;
            }
            // Commandes affichées : la manette circule entre les boutons
            if (overlay == Overlay.Full && Tv.IsInside(focused, Controls)) return;

            if (live)
            {
                if (Keys.IsUp(k)) { Zap(-1); e.Handled = true; }
                else if (Keys.IsDown(k)) { Zap(+1); e.Handled = true; }
                else if (Keys.IsLeft(k) || Keys.IsRight(k) || Keys.IsAccept(k)) { ShowOverlay(Overlay.Full); e.Handled = true; }
            }
            else
            {
                if (Keys.IsLeft(k)) { Seek(-SeekStep(e)); e.Handled = true; }
                else if (Keys.IsRight(k)) { Seek(SeekStep(e)); e.Handled = true; }
                else if (Keys.IsAccept(k))
                {
                    TogglePause();
                    ShowOverlay(Overlay.Full);
                    e.Handled = true;
                }
                else if (Keys.IsUp(k) || Keys.IsDown(k)) { ShowOverlay(Overlay.Full); e.Handled = true; }
            }
        }
    }
}
