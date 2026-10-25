using Windows.UI.Xaml.Navigation;

namespace FluxTV.Views
{
    /// <summary>Films : catalogue par catégories.</summary>
    public sealed partial class MoviesPage : TvPage
    {
        public MoviesPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public override void FocusDefault() => Catalog.FocusDefault();
    }
}
