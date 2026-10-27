using System;
using FluxTV.Services;
using FluxTV.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace FluxTV.Views
{
    /// <summary>
    /// Premier lancement : choix de la langue (français ou anglais), celle de l'appareil proposée
    /// d'office. Ⓑ (Échap sur PC) garde celle de l'appareil. L'app démarre ensuite dans cette langue.
    /// </summary>
    public sealed partial class LanguagePage : Page
    {
        /// <summary>Langue choisie et enregistrée : l'app peut démarrer.</summary>
        public event Action Chosen;

        bool done;

        public LanguagePage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                Tv.Later(() => (Lang.Device == Lang.Fr ? FrenchChoice : EnglishChoice).Focus(FocusState.Keyboard));
            };
            Unloaded += (s, e) => SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            KeyDown += (s, e) =>
            {
                if (e.Key != Windows.System.VirtualKey.Escape) return;
                e.Handled = true;
                Choose(Lang.Device);
            };
        }

        void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            e.Handled = true;
            Choose(Lang.Device);
        }

        void OnFrench(object sender, RoutedEventArgs e) => Choose(Lang.Fr);

        void OnEnglish(object sender, RoutedEventArgs e) => Choose(Lang.En);

        void Choose(string code)
        {
            if (done) return;
            done = true;
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Lang.Save(code);
            Chosen?.Invoke();
        }
    }
}
