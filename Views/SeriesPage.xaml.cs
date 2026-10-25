using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Séries : catalogue par catégories.</summary>
    public sealed partial class SeriesPage : TvPage
    {
        public SeriesPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public override void FocusDefault() => Catalog.FocusDefault();
    }
}
