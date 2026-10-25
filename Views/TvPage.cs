using FluxTV.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>
    /// Page de l'app : au retour (Ⓑ), le focus revient sur l'élément d'où l'on était parti.
    /// </summary>
    public class TvPage : Page
    {
        object savedFocus;
        bool restorePending;

        public TvPage()
        {
            Loaded += OnLoadedRestore;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            savedFocus = FocusManager.GetFocusedElement();
            base.OnNavigatedFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            restorePending = e.NavigationMode == NavigationMode.Back;
        }

        void OnLoadedRestore(object sender, RoutedEventArgs e)
        {
            if (!restorePending) return;
            restorePending = false;
            var target = savedFocus;
            Tv.Later(() =>
            {
                if (!Tv.FocusControl(target)) FocusDefault();
            });
        }

        /// <summary>Élément qui prend le focus à l'arrivée sur la page.</summary>
        public virtual void FocusDefault() => Tv.FocusFirst(this);

        /// <summary>Ⓑ : true si la page l'a traité (fermeture d'un formulaire, d'un panneau…).</summary>
        public virtual bool HandleBack() => false;
    }
}
