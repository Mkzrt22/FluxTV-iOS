using System;
using System.Text.RegularExpressions;
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
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += (s, e) => ShellPage.Current?.OnResuming();
            UnhandledException += OnUnhandledException;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (ShellPage.Current == null)
            {
                // Tout l'écran de la télé : les marges de sécurité sont gérées par les pages
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                var shell = new ShellPage();
                if (Device.IsXbox)
                {
                    Window.Current.Content = shell;
                }
                else
                {
                    // Sur PC : l'interface télé (960 × 540) mise à l'échelle de la fenêtre
                    var stage = new Grid { Width = 960, Height = 540 };
                    stage.Children.Add(shell);
                    var root = new Grid { Background = new SolidColorBrush(Windows.UI.Colors.Black) };
                    root.Children.Add(new Viewbox { Stretch = Stretch.Uniform, Child = stage });
                    Window.Current.Content = root;
                }
                var args = e.Arguments ?? "";
                if (args.StartsWith("test:", StringComparison.Ordinal))
                {
                    // Captures d'écran automatiques du serveur de compilation
                    ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
                    shell.StartTest(args.Substring(5).Trim());
                }
                else
                {
                    shell.Start();
                }
            }
            Window.Current.Activate();
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
            AppState.Current.Toast("Une erreur est survenue");
        }
    }
}
