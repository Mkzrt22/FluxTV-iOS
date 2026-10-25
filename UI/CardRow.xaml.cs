using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace FluxTV.UI
{
    /// <summary>Rangée de cartes (accueil, recherche). Ⓐ ouvre la carte, Ⓨ l'ajoute aux favoris.</summary>
    public sealed partial class CardRow : UserControl
    {
        public event Action<CardItem> Invoked;
        public event Action<CardItem> FavoriteRequested;
        /// <summary>Carte affichée à l'écran (chargement du programme d'une chaîne).</summary>
        public event Action<CardItem> Realized;

        public CardRow()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        public string Title
        {
            get => Header.Text;
            set => Header.Text = value ?? "";
        }

        public DataTemplate CardTemplate
        {
            get => Cards.ItemTemplate;
            set => Cards.ItemTemplate = value;
        }

        public bool ShowCount { get; set; }

        public IList<CardItem> Items { get; private set; } = new List<CardItem>();

        /// <summary>Remplace les cartes (rangée masquée si vide).</summary>
        public void SetItems(IList<CardItem> items)
        {
            Items = items ?? new List<CardItem>();
            Cards.ItemsSource = Items;
            CountText.Text = ShowCount && Items.Count > 0 ? Items.Count.ToString() : "";
            Visibility = Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public ListView ListView => Cards;

        public bool FocusFirst() => Items.Count > 0 && Tv.FocusIndex(Cards, 0);

        public bool ContainsFocus => Tv.IsInside(FocusManager.GetFocusedElement() as DependencyObject, Cards);

        void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CardItem item) Invoked?.Invoke(item);
        }

        void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!Keys.IsFavorite(e.Key) && !Keys.IsMenu(e.Key)) return;
            if (Tv.ItemOf(Cards, e.OriginalSource) is CardItem item)
            {
                FavoriteRequested?.Invoke(item);
                e.Handled = true;
            }
        }

        void OnContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            if (args.Item is CardItem item) Realized?.Invoke(item);
        }
    }
}
