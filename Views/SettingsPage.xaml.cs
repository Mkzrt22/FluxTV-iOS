using System;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Réglages : source, lecture, contenus, aide de la manette, version.</summary>
    public sealed partial class SettingsPage : TvPage
    {
        static readonly string[] Formats = { "auto", "m3u8", "ts" };

        readonly AppState app = AppState.Current;
        bool loading;
        bool confirmChange;

        public SettingsPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            VersionText.Text = "FluxTV Player pour Xbox · version " + AppInfo.Version;
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

        public override void FocusDefault() => RefreshButton.Focus(FocusState.Keyboard);

        void OnTopFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, 0, null);

        void OnBottomFocus(object sender, RoutedEventArgs e) => Scroller.ChangeView(null, Scroller.ScrollableHeight, null);

        void OnCredits(object sender, RoutedEventArgs e)
        {
            var show = Credits.Visibility != Visibility.Visible;
            Credits.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CreditsLabel.Text = show ? "Masquer les crédits" : "Crédits et licences";
            Tv.Later(() =>
            {
                // Hauteur à jour avant de défiler jusqu'en bas
                Scroller.UpdateLayout();
                Scroller.ChangeView(null, Scroller.ScrollableHeight, null);
            });
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
                SourceTitle.Text = "Aucune source";
                SourceDetail.Text = "";
                AccountDetail.Text = "";
            }
            else
            {
                switch (s.Kind)
                {
                    case SourceKind.Xtream:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = ReferenceEquals(s, app.Embedded) || s.Equals(app.Embedded) ? "Mon compte" : "Compte Xtream";
                        SourceDetail.Text = "Serveur : " + s.DisplayName;
                        break;
                    case SourceKind.Playlist:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = "Playlist M3U";
                        SourceDetail.Text = "Adresse : " + s.DisplayName + (s.EpgUrl != null ? " · guide TV ajouté" : "");
                        break;
                    default:
                        SourceGlyph.Text = "";
                        SourceTitle.Text = "Démonstration";
                        SourceDetail.Text = "Chaînes et films libres de droits, pour essayer l'app.";
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
                parts.Add("Abonnement jusqu'au " + exp.ToLocalTime().ToString("d MMMM yyyy", Format.French) + " (" + days + " j)");
            }
            else
            {
                parts.Add("Abonnement sans date de fin");
            }
            if (a.MaxConnections is int max) parts.Add(max + (max > 1 ? " écrans" : " écran"));
            if (a.IsTrial) parts.Add("essai");
            return string.Join(" · ", parts);
        }

        void FillFormat()
        {
            switch (app.Settings.LiveFormat)
            {
                case "m3u8":
                    FormatText.Text = "HLS (m3u8)";
                    FormatDetail.Text = "Le plus compatible avec le lecteur de la Xbox.";
                    break;
                case "ts":
                    FormatText.Text = "MPEG-TS (ts)";
                    FormatDetail.Text = "À essayer si une chaîne ne démarre pas en HLS.";
                    break;
                default:
                    FormatText.Text = "Automatique (" + (app.LiveExtension == "ts" ? "TS" : "HLS") + ")";
                    FormatDetail.Text = "HLS quand le serveur le permet, sinon TS. En cas d'échec, l'autre format est essayé.";
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
                app.Toast("Ton compte est utilisé");
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
