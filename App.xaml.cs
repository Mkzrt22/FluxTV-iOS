using System;
using System.Text.RegularExpressions;
using FluxTV.Core;
using FluxTV.Services;
using FluxTV.UI;
using FluxTV.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace FluxTV
{
    sealed partial class App : Application
    {
        bool started;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += (s, e) => ShellPage.Current?.OnResuming();
            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!started)
            {
                started = true;
                // Tout l'écran de la télé : les marges de sécurité sont gérées par les pages
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                var args = e.Arguments ?? "";
                if (args.StartsWith("test:", StringComparison.Ordinal))
                {
                    // Captures d'écran automatiques du serveur de compilation : « test:home@en » en anglais
                    ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
                    var screen = args.Substring(5).Trim();
                    var at = screen.LastIndexOf('@');
                    Lang.Use(at >= 0 && screen.Substring(at + 1) == Lang.En ? Lang.En : Lang.Fr);
                    if (at >= 0) screen = screen.Substring(0, at);
                    if (screen == "language") Show(new LanguagePage());
                    else NewShell().StartTest(screen);
                }
                else if (Lang.Chosen == null)
                {
                    // Premier lancement : la langue d'abord (celle de l'appareil proposée), puis l'app dans cette langue
                    Lang.Apply();
                    var page = new LanguagePage();
                    page.Chosen += () => NewShell().Start();
                    Show(page);
                }
                else
                {
                    Lang.Apply();
                    NewShell().Start();
                }
            }
            Window.Current.Activate();
        }

        static ShellPage NewShell()
        {
            var shell = new ShellPage();
            Show(shell);
            return shell;
        }

        /// <summary>Écran de l'app : toute la télé sur Xbox ; sur PC, l'interface télé (960 × 540) mise à l'échelle de la fenêtre.</summary>
        static void Show(UIElement content)
        {
            if (Device.IsXbox)
            {
                Window.Current.Content = content;
                return;
            }
            var stage = new Grid { Width = 960, Height = 540 };
            stage.Children.Add(content);
            var root = new Grid { Background = new SolidColorBrush(Windows.UI.Colors.Black) };
            root.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = stage });
            Window.Current.Content = root;
        }

        /// <summary>
        /// Langue changée dans les réglages : toute l'interface est refaite dans la nouvelle langue, sans
        /// redémarrer, et revient sur les réglages.
        /// </summary>
        public static void ChangeLanguage(string code)
        {
            if (code == Lang.Current) return;
            Lang.Save(code);
            ShellPage.Current?.Detach();
            AppState.Current.Relocalize();
            NewShell().Resume("settings", "language");
        }

        void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            try
            {
                ShellPage.Current?.OnSuspending();
                AppState.Current.Library.FlushNow();
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>Erreur imprévue : notée dans le journal (sans adresse), l'app continue.</summary>
        void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            var text = Regex.Replace(e.Exception?.ToString() ?? e.Message ?? "", @"\w+://\S+", "<adresse>");
            Journal.Write("Erreur imprévue : " + text);
            AppState.Current.Toast(Tr.T("Une erreur est survenue", "Something went wrong"));
        }
    }
}
