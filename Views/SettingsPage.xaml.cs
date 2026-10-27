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
    /// <summary>Réglages : source, langue, lecture, contenus, aide de la manette, version.</summary>
    public sealed partial class SettingsPage : TvPage
    {
        static readonly string[] Formats = { "auto", "m3u8", "ts" };

        readonly AppState app = AppState.Current;
        bool loading;
        bool confirmChange;
        bool focusLanguage;

        public SettingsPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            VersionText.Text = Tr.T("FluxTV Player pour Xbox · version ", "FluxTV Player for Xbox · version ") + AppInfo.Version;
            var english = Lang.Current == Lang.En;
            FrenchMark.Visibility = english ? Visibility.Collapsed : Visibility.Visible;
            EnglishMark.Visibility = english ? Visibility.Visible : Visibility.Collapsed;
            app.AccountChanged += () => { if (IsLoaded) Fill(); };
            app.SourceChanged += () => { if (IsLoaded) Fill(); };
            Loaded += (s, e) =>
            {
                confirmChange = false;
                ConfirmText.Visibility = Visibility.Collapsed;
                Fill();
            };
            ChangeButton.LostFocus += (s, e) =>
            {
                confirmChange = false;
                ConfirmText.Visibility = Visibility.Collapsed;
            };
        }

        public override void FocusDefault()
        {
            if (focusLanguage)
            {
                focusLanguage = false;
                (Lang.Current == Lang.En ? EnglishButton : FrenchButton).Focus(FocusState.Keyboard);
                return;
            }
            RefreshButton.Focus(FocusState.Keyboard);
        }

        /// <summary>Retour sur les réglages après un changement de langue : le focus revient sur le choix de langue.</summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            focusLanguage = e.Parameter as string == "language";
        }

        void OnFrench(object sender, RoutedEventArgs e) => App.ChangeLanguage(Lang.Fr);

        void OnEnglish(object sender, RoutedEventArgs e) => App.ChangeLanguage(Lang.En);

        void OnTopFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, 0, null);

        void OnBottomFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, Scroller.ScrollableHeight, null);

        async void OnCredits(object sender, RoutedEventArgs e)
        {
            var show = Credits.Visibility != Visibility.Visible;
            Credits.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CreditsLabel.Text = show ? Tr.T("Masquer les crédits", "Hide credits") : Tr.T("Crédits et licences", "Credits and licenses");
            if (!show) return;
            // Après le défilement dû au focus : les crédits remontent en entier dans l'écran
            await Task.Delay(200);
            Credits.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 1, AnimationDesired = true });
        }

        /// <summary>Captures automatiques : crédits affichés.</summary>
        public void ShowCreditsForTest()
        {
            CreditsButton.Focus(FocusState.Keyboard);
            OnCredits(null, null);
        }

        void Fill()
        {
            loading = true;
            var s = app.Source;
            if (s == null)
            {
                SourceTitle.Text = Tr.T("Aucune source", "No source");
                SourceDetail.Text = "";
                AccountDetail.Text = "";
            }
            else
            {
                switch (s.Kind)
                {
                    case SourceKind.Xtream:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = ReferenceEquals(s, app.Embedded) || s.Equals(app.Embedded) ? Tr.T("Mon compte", "My account") : Tr.T("Compte Xtream", "Xtream account");
                        SourceDetail.Text = Tr.T("Serveur : ", "Server: ") + s.DisplayName;
                        break;
                    case SourceKind.Playlist:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = Tr.T("Playlist M3U", "M3U playlist");
                        SourceDetail.Text = Tr.T("Adresse : ", "Address: ") + s.DisplayName + (s.EpgUrl != null ? Tr.T(" · guide TV ajouté", " · TV guide added") : "");
                        break;
                    default:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = Tr.T("Démonstration", "Demo");
                        SourceDetail.Text = Tr.T("Chaînes et films libres de droits, pour essayer l'app.", "Free-licensed channels and movies, to try the app.");
                        break;
                }
                AccountDetail.Text = AccountLine();
                AccountDetail.Visibility = AccountDetail.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            EmbeddedButton.Visibility = app.Embedded != null && !app.Embedded.Equals(s) ? Visibility.Visible : Visibility.Collapsed;

            AutoplaySwitch.IsOn = app.Settings.AutoplayNext;
            RefreshRateSwitch.IsOn = app.Settings.MatchFrameRate;
            AdultSwitch.IsOn = app.Settings.HideAdult;
            FillFormat();
            loading = false;
        }

        string AccountLine()
        {
            var a = app.Account;
            if (a == null) return "";
            var parts = new System.Collections.Generic.List<string>();
            if (a.Expiry is DateTimeOffset exp)
            {
                var days = a.DaysLeft(DateTimeOffset.UtcNow) ?? 0;
                parts.Add(Tr.T("Abonnement jusqu'au " + Format.Date(exp) + " (" + days + " j)",
                    "Subscription until " + Format.Date(exp) + " (" + days + (days == 1 ? " day" : " days") + " left)"));
            }
            else
            {
                parts.Add(Tr.T("Abonnement sans date de fin", "Subscription with no end date"));
            }
            if (a.MaxConnections is int max) parts.Add(Format.Count(max, "écran", "écrans", "screen", "screens"));
            if (a.IsTrial) parts.Add(Tr.T("essai", "trial"));
            return string.Join(" · ", parts);
        }

        void FillFormat()
        {
            switch (app.Settings.LiveFormat)
            {
                case "m3u8":
                    FormatText.Text = "HLS (m3u8)";
                    FormatDetail.Text = Tr.T("Le plus compatible avec le lecteur de la Xbox.", "The most compatible with the Xbox player.");
                    break;
                case "ts":
                    FormatText.Text = "MPEG-TS (ts)";
                    FormatDetail.Text = Tr.T("À essayer si une chaîne ne démarre pas en HLS.", "Try this if a channel doesn't start in HLS.");
                    break;
                default:
                    FormatText.Text = Tr.T("Automatique (", "Automatic (") + (app.LiveExtension == "ts" ? "TS" : "HLS") + ")";
                    FormatDetail.Text = Tr.T("HLS quand le serveur le permet, sinon TS. En cas d'échec, l'autre format est essayé.",
                        "HLS when the server allows it, otherwise TS. If it fails, the other format is tried.");
                    break;
            }
        }

        void OnFormat(object sender, RoutedEventArgs e)
        {
            var i = Array.IndexOf(Formats, app.Settings.LiveFormat);
            app.Settings.LiveFormat = Formats[(i + 1 + Formats.Length) % Formats.Length];
            FillFormat();
        }

        void OnToggled(object sender, RoutedEventArgs e)
        {
            if (loading) return;
            var hideAdultChanged = app.Settings.HideAdult != AdultSwitch.IsOn;
            app.Settings.AutoplayNext = AutoplaySwitch.IsOn;
            app.Settings.MatchFrameRate = RefreshRateSwitch.IsOn;
            app.Settings.HideAdult = AdultSwitch.IsOn;
            // Listes refaites sans (ou avec) les catégories pour adultes
            if (hideAdultChanged) _ = app.RefreshAllAsync();
        }

        void OnRefresh(object sender, RoutedEventArgs e) => _ = app.RefreshAllAsync();

        async void OnUseEmbedded(object sender, RoutedEventArgs e)
        {
            if (app.Embedded == null) return;
            EmbeddedButton.IsEnabled = false;
            try
            {
                await app.ConnectAsync(app.Embedded);
                app.Toast(Tr.T("Ton compte est utilisé", "Your account is now in use"));
            }
            catch (Exception ex)
            {
                app.Toast(FluxException.MessageOf(ex));
            }
            EmbeddedButton.IsEnabled = true;
            Fill();
        }

        /// <summary>Deux appuis : la source enregistrée est oubliée, retour à l'écran de bienvenue.</summary>
        void OnChangeSource(object sender, RoutedEventArgs e)
        {
            if (!confirmChange)
            {
                confirmChange = true;
                ConfirmText.Visibility = Visibility.Visible;
                return;
            }
            confirmChange = false;
            ConfirmText.Visibility = Visibility.Collapsed;
            app.SignOut();
        }
    }
}
