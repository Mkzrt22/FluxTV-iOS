using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>
    /// Bienvenue : choix de la source (mon compte, depuis mon téléphone, compte Xtream, playlist M3U,
    /// démonstration).
    /// </summary>
    public sealed partial class SetupPage : TvPage
    {
        /// <summary>Adresse montrée sur les captures (aucune page ouverte en essai).</summary>
        const string TestUrl = "http://192.168.1.20:8765/K7P2QX";

        enum PhoneStep { Starting, Waiting, Connecting, Failed, NoNetwork, CannotOpen, TooManyAttempts, Expired }

        readonly AppState app = AppState.Current;
        bool busy;
        PhoneLinkServer phone;
        int phoneGeneration;

        public SetupPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            EmbeddedChoice.Visibility = app.Embedded != null ? Visibility.Visible : Visibility.Collapsed;
            ShowChoices();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            StopPhone();
        }

        public override void FocusDefault()
        {
            if (XtreamForm.Visibility == Visibility.Visible) ServerBox.Focus(FocusState.Keyboard);
            else if (PlaylistForm.Visibility == Visibility.Visible) PlaylistBox.Focus(FocusState.Keyboard);
            else if (PhoneForm.Visibility == Visibility.Visible) (PhoneRetry.Visibility == Visibility.Visible ? PhoneRetry : PhoneBack).Focus(FocusState.Keyboard);
            else if (EmbeddedChoice.Visibility == Visibility.Visible) EmbeddedChoice.Focus(FocusState.Keyboard);
            else PhoneChoice.Focus(FocusState.Keyboard);
        }

        public override bool HandleBack()
        {
            if (busy) return true;
            if (Choices.Visibility == Visibility.Visible) return false;
            ShowChoices();
            FocusDefault();
            return true;
        }

        void ShowChoices()
        {
            StopPhone();
            Presentation.Visibility = Visibility.Visible;
            Choices.Visibility = Visibility.Visible;
            XtreamForm.Visibility = Visibility.Collapsed;
            PlaylistForm.Visibility = Visibility.Collapsed;
            PhoneForm.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Collapsed;
        }

        void ShowForm(FrameworkElement form)
        {
            Choices.Visibility = Visibility.Collapsed;
            Presentation.Visibility = form == PhoneForm ? Visibility.Collapsed : Visibility.Visible;
            form.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
            Tv.Later(FocusDefault);
        }

        void OnXtream(object sender, RoutedEventArgs e) => ShowForm(XtreamForm);

        void OnPlaylist(object sender, RoutedEventArgs e) => ShowForm(PlaylistForm);

        void OnBackToChoices(object sender, RoutedEventArgs e) => HandleBack();

        async void OnEmbedded(object sender, RoutedEventArgs e)
        {
            if (app.Embedded != null) await ConnectAsync(app.Embedded, EmbeddedChoice);
        }

        async void OnDemo(object sender, RoutedEventArgs e) => await ConnectAsync(Source.Demo, DemoChoice);

        async void OnConnectXtream(object sender, RoutedEventArgs e)
        {
            // Lien complet de playlist Xtream collé dans « Serveur » : identifiants repris du lien
            var account = XtreamAccount.FromPlaylistUrl(ServerBox.Text)
                          ?? new XtreamAccount(ServerBox.Text, UserBox.Text, PassBox.Password);
            if (!account.IsComplete)
            {
                ShowError(Tr.T("Remplis le serveur, l'identifiant et le mot de passe.", "Fill in the server, username and password."));
                return;
            }
            await ConnectAsync(Source.Xtream(account), XtreamConnect);
        }

        async void OnConnectPlaylist(object sender, RoutedEventArgs e)
        {
            var url = (PlaylistBox.Text ?? "").Trim();
            if (url.Length == 0)
            {
                ShowError(Tr.T("Indique l'adresse de la playlist.", "Enter the playlist URL."));
                return;
            }
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;
            // Playlist d'un serveur Xtream : on passe par l'API (plus rapide, guide TV inclus)
            var xtream = XtreamAccount.FromPlaylistUrl(url);
            var source = xtream != null ? Source.Xtream(xtream) : Source.Playlist(url, EpgBox.Text);
            await ConnectAsync(source, PlaylistConnect);
        }

        async Task ConnectAsync(Source source, Control from)
        {
            if (busy) return;
            busy = true;
            ErrorText.Visibility = Visibility.Collapsed;
            BusyPanel.Visibility = Visibility.Visible;
            BusyRing.IsActive = true;
            BusyText.Text = source.Kind == SourceKind.Playlist ? Tr.T("Chargement de la playlist…", "Loading playlist…") : Tr.T("Connexion…", "Connecting…");
            try
            {
                await app.ConnectAsync(source);
            }
            catch (Exception ex)
            {
                ShowError(FluxException.MessageOf(ex));
                from?.Focus(FocusState.Keyboard);
            }
            finally
            {
                busy = false;
                BusyRing.IsActive = false;
                BusyPanel.Visibility = Visibility.Collapsed;
            }
        }

        void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }

        // ───────────────────────────── Depuis mon téléphone ─────────────────────────────

        void OnPhone(object sender, RoutedEventArgs e)
        {
            ShowForm(PhoneForm);
            StartPhone();
        }

        void OnPhoneRetry(object sender, RoutedEventArgs e)
        {
            StartPhone();
            PhoneBack.Focus(FocusState.Keyboard);
        }

        /// <summary>Captures d'écran : le QR code d'une adresse type, sans ouvrir de page.</summary>
        public void ShowPhoneForTest()
        {
            ShowForm(PhoneForm);
            DrawQr(TestUrl);
            SetPhoneStep(PhoneStep.Waiting);
        }

        /// <summary>Ouvre la page du téléphone (un nouveau code à chaque fois) et affiche son QR code.</summary>
        async void StartPhone()
        {
            StopPhone();
            int generation = phoneGeneration;
            SetPhoneStep(PhoneStep.Starting);
            PhoneLinkServer server = null;
            bool failed = false;
            try
            {
                server = await PhoneLinkServer.StartAsync();
            }
            catch (Exception ex)
            {
                failed = true;
                Journal.Write("Téléphone\u00A0: page impossible à ouvrir\u00A0: " + ex.Message);
            }
            // Retour, ou autre essai, pendant l'ouverture : cette page-là ne sert plus
            if (generation != phoneGeneration || PhoneForm.Visibility != Visibility.Visible)
            {
                server?.Stop();
                return;
            }
            if (server == null)
            {
                SetPhoneStep(failed ? PhoneStep.CannotOpen : PhoneStep.NoNetwork);
                return;
            }
            phone = server;
            var dispatcher = Dispatcher;
            server.SourceReceived += s => _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => OnPhoneSource(server, s));
            server.Ended += reason => _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => OnPhoneEnded(server, reason));
            DrawQr(server.Url);
            SetPhoneStep(PhoneStep.Waiting);
        }

        /// <summary>
        /// Ferme la page du téléphone. Si la source vient d'être ajoutée depuis le téléphone, elle reste
        /// ouverte quelques secondes : le téléphone a le temps d'afficher « C'est fait ».
        /// </summary>
        void StopPhone()
        {
            phoneGeneration++;
            var server = phone;
            phone = null;
            if (server == null) return;
            var state = server.Link.State;
            if (state == PhoneLinkState.Connecting || state == PhoneLinkState.Connected) server.StopAfter(TimeSpan.FromSeconds(12));
            else server.Stop();
        }

        async void OnPhoneSource(PhoneLinkServer server, Source source)
        {
            if (server != phone)
            {
                server.Link.Report(PhoneLinkState.Failed, Tr.T("La télé a quitté cet écran\u00A0: scanne à nouveau le QR code.", "The TV left this screen: scan the QR code again."));
                return;
            }
            if (busy)
            {
                server.Link.Report(PhoneLinkState.Failed, Tr.T("La console est occupée\u00A0: réessaie dans un instant.", "The console is busy: try again in a moment."));
                return;
            }
            busy = true;
            SetPhoneStep(PhoneStep.Connecting);
            try
            {
                // Source acceptée : le téléphone affiche « C'est fait », la console passe à l'accueil
                await app.ConnectAsync(source, () => server.Link.Report(PhoneLinkState.Connected));
                server.StopAfter(TimeSpan.FromSeconds(12));
            }
            catch (Exception ex)
            {
                var message = FluxException.MessageOf(ex);
                server.Link.Report(PhoneLinkState.Failed, message);
                if (server == phone) SetPhoneStep(PhoneStep.Failed, message);
            }
            finally
            {
                busy = false;
            }
        }

        void OnPhoneEnded(PhoneLinkServer server, PhoneLinkEnd reason)
        {
            if (server != phone) return;
            phone = null;
            bool focusInside = FocusManager.GetFocusedElement() == PhoneBack;
            SetPhoneStep(reason == PhoneLinkEnd.TooManyAttempts ? PhoneStep.TooManyAttempts : PhoneStep.Expired);
            if (focusInside) PhoneRetry.Focus(FocusState.Keyboard);
        }

        void SetPhoneStep(PhoneStep step, string message = null)
        {
            bool open = step == PhoneStep.Waiting || step == PhoneStep.Connecting || step == PhoneStep.Failed;
            bool working = step == PhoneStep.Starting || step == PhoneStep.Waiting || step == PhoneStep.Connecting;
            PhoneRing.IsActive = working;
            PhoneRing.Visibility = working ? Visibility.Visible : Visibility.Collapsed;
            PhoneRetry.Visibility = open || step == PhoneStep.Starting ? Visibility.Collapsed : Visibility.Visible;
            PhoneRetry.Content = step == PhoneStep.NoNetwork || step == PhoneStep.CannotOpen ? Tr.T("Réessayer", "Try again") : Tr.T("Nouveau QR code", "New QR code");
            // Page fermée : QR code estompé (plus personne ne le scanne), adresse masquée
            QrCard.Visibility = step == PhoneStep.NoNetwork || step == PhoneStep.CannotOpen ? Visibility.Collapsed : Visibility.Visible;
            QrCard.Opacity = open ? 1 : step == PhoneStep.Starting ? 0 : 0.18;
            PhoneAddress.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            PhoneStatus.Foreground = (Brush)Application.Current.Resources[step == PhoneStep.Failed || !open && !working ? "ErrorBrush" : "TextDimBrush"];
            switch (step)
            {
                case PhoneStep.Starting:
                    PhoneStatus.Text = Tr.T("Préparation du QR code…", "Preparing the QR code…");
                    break;
                case PhoneStep.Waiting:
                    PhoneStatus.Text = Tr.T("En attente de ton téléphone…", "Waiting for your phone…");
                    break;
                case PhoneStep.Connecting:
                    PhoneStatus.Text = Tr.T("Infos reçues\u00A0: connexion à ton abonnement…", "Details received: connecting to your subscription…");
                    break;
                case PhoneStep.Failed:
                    PhoneStatus.Text = message + " " + Tr.T("Tu peux corriger sur ton téléphone.", "You can fix it on your phone.");
                    break;
                case PhoneStep.NoNetwork:
                    PhoneStatus.Text = Tr.T("Ta console n'est connectée à aucun réseau local. Branche-la en Wi-Fi ou par câble, puis réessaie.",
                                            "Your console isn't connected to a local network. Connect it with Wi-Fi or a cable, then try again.");
                    break;
                case PhoneStep.CannotOpen:
                    PhoneStatus.Text = Tr.T("La console n'a pas pu ouvrir la page pour le téléphone. Réessaie, ou ajoute ta source avec la manette.",
                                            "The console couldn't open the page for your phone. Try again, or add your source with the controller.");
                    break;
                case PhoneStep.TooManyAttempts:
                    PhoneStatus.Text = Tr.T("Page fermée\u00A0: trop d'essais avec un mauvais code. Affiche un nouveau QR code pour recommencer.",
                                            "Page closed: too many tries with a wrong code. Show a new QR code to start again.");
                    break;
                case PhoneStep.Expired:
                    PhoneStatus.Text = Tr.T("Page fermée au bout de 15 minutes. Affiche un nouveau QR code pour recommencer.",
                                            "Page closed after 15 minutes. Show a new QR code to start again.");
                    break;
            }
        }

        /// <summary>
        /// QR code net à l'écran : chaque module fait un nombre entier de pixels réels, et l'image est
        /// affichée à sa taille exacte (aucun lissage qui gênerait la lecture).
        /// </summary>
        void DrawQr(string url)
        {
            PhoneUrl.Text = url;
            var qr = QrCode.Encode(url, QrCode.Correction.M);
            double scale = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            if (scale <= 0) scale = 1;
            if (!Device.IsXbox)
            {
                // Sur PC, l'interface télé (960 × 540) est mise à l'échelle de la fenêtre
                var window = Window.Current.Bounds;
                if (window.Width > 0 && window.Height > 0) scale *= Math.Min(window.Width / 960, window.Height / 540);
            }
            double box = QrCard.Width - QrCard.Padding.Left - QrCard.Padding.Right;
            const int quiet = 2;   // + la marge blanche de la carte : zone claire d'au moins 4 modules
            int modules = qr.Size + 2 * quiet;
            int px = Math.Max(1, (int)Math.Floor(box * scale / modules));
            int size = px * modules;
            var pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
            {
                int my = y / px - quiet;
                for (int x = 0; x < size; x++)
                {
                    int mx = x / px - quiet;
                    byte v = mx >= 0 && my >= 0 && mx < qr.Size && my < qr.Size && qr[mx, my] ? (byte)0 : (byte)255;
                    int i = (y * size + x) * 4;
                    pixels[i] = v;
                    pixels[i + 1] = v;
                    pixels[i + 2] = v;
                    pixels[i + 3] = 255;
                }
            }
            var bitmap = new WriteableBitmap(size, size);
            using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(pixels, 0, pixels.Length);
            bitmap.Invalidate();
            QrImage.Source = bitmap;
            QrImage.Width = size / scale;
            QrImage.Height = size / scale;
        }
    }
}
