using System;
using System.Threading.Tasks;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Bienvenue : choix de la source (mon compte, compte Xtream, playlist M3U, démonstration).</summary>
    public sealed partial class SetupPage : TvPage
    {
        readonly AppState app = AppState.Current;
        bool busy;

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

        public override void FocusDefault()
        {
            if (XtreamForm.Visibility == Visibility.Visible) ServerBox.Focus(FocusState.Keyboard);
            else if (PlaylistForm.Visibility == Visibility.Visible) PlaylistBox.Focus(FocusState.Keyboard);
            else if (EmbeddedChoice.Visibility == Visibility.Visible) EmbeddedChoice.Focus(FocusState.Keyboard);
            else XtreamChoice.Focus(FocusState.Keyboard);
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
            Choices.Visibility = Visibility.Visible;
            XtreamForm.Visibility = Visibility.Collapsed;
            PlaylistForm.Visibility = Visibility.Collapsed;
            ErrorText.Visibility = Visibility.Collapsed;
        }

        void ShowForm(StackPanel form)
        {
            Choices.Visibility = Visibility.Collapsed;
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
    }
}
