using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace FluxTV.UI
{
    /// <summary>Appareil : la Xbox affiche l'app en plein écran télé (960 × 540 pixels effectifs).</summary>
    public static class Device
    {
        public static readonly bool IsXbox = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox";
    }

    /// <summary>Touches de la manette, de la télécommande et du clavier.</summary>
    public static class Keys
    {
        public static bool IsUp(VirtualKey k) =>
            k == VirtualKey.Up || k == VirtualKey.GamepadDPadUp || k == VirtualKey.GamepadLeftThumbstickUp;

        public static bool IsDown(VirtualKey k) =>
            k == VirtualKey.Down || k == VirtualKey.GamepadDPadDown || k == VirtualKey.GamepadLeftThumbstickDown;

        public static bool IsLeft(VirtualKey k) =>
            k == VirtualKey.Left || k == VirtualKey.GamepadDPadLeft || k == VirtualKey.GamepadLeftThumbstickLeft;

        public static bool IsRight(VirtualKey k) =>
            k == VirtualKey.Right || k == VirtualKey.GamepadDPadRight || k == VirtualKey.GamepadLeftThumbstickRight;

        public static bool IsAccept(VirtualKey k) =>
            k == VirtualKey.GamepadA || k == VirtualKey.Enter || k == VirtualKey.Space;

        /// <summary>Ⓨ ou ☰ : favori.</summary>
        public static bool IsFavorite(VirtualKey k) => k == VirtualKey.GamepadY || k == VirtualKey.F;

        public static bool IsX(VirtualKey k) => k == VirtualKey.GamepadX || k == VirtualKey.L;

        public static bool IsMenu(VirtualKey k) => k == VirtualKey.GamepadMenu || k == VirtualKey.M;

        public static bool IsView(VirtualKey k) => k == VirtualKey.GamepadView || k == VirtualKey.I;

        public static bool IsPreviousPage(VirtualKey k) =>
            k == VirtualKey.GamepadLeftShoulder || k == VirtualKey.PageUp;

        public static bool IsNextPage(VirtualKey k) =>
            k == VirtualKey.GamepadRightShoulder || k == VirtualKey.PageDown;

        public static bool IsLeftTrigger(VirtualKey k) => k == VirtualKey.GamepadLeftTrigger || k == VirtualKey.Home;

        public static bool IsRightTrigger(VirtualKey k) => k == VirtualKey.GamepadRightTrigger || k == VirtualKey.End;
    }

    public static class Tv
    {
        public static CoreDispatcher Dispatcher => Window.Current?.Dispatcher;

        /// <summary>Exécute après la mise en page en cours (focus sur un élément tout juste affiché).</summary>
        public static void Later(Action a)
        {
            var d = Dispatcher;
            if (d == null) { a(); return; }
            _ = d.RunAsync(CoreDispatcherPriority.Low, () => a());
        }

        public static Task Delay(int ms) => Task.Delay(ms);

        /// <summary>Premier élément focalisable dans un conteneur.</summary>
        public static bool FocusFirst(DependencyObject scope)
        {
            if (scope == null) return false;
            if (FocusManager.FindFirstFocusableElement(scope) is Control c)
                return c.Focus(FocusState.Keyboard);
            return false;
        }

        public static bool FocusControl(object element) =>
            element is Control c && c.Visibility == Visibility.Visible && IsInTree(c) && c.Focus(FocusState.Keyboard);

        /// <summary>Focus sur un élément d'une liste (le fait défiler jusqu'à lui au besoin).</summary>
        public static async Task<bool> FocusItemAsync(ListViewBase list, object item)
        {
            if (list == null || item == null) return false;
            list.ScrollIntoView(item);
            for (int i = 0; i < 6; i++)
            {
                list.UpdateLayout();
                if (list.ContainerFromItem(item) is Control c && c.Focus(FocusState.Keyboard)) return true;
                await Task.Delay(40);
            }
            return false;
        }

        public static bool FocusIndex(ListViewBase list, int index)
        {
            if (list == null || list.Items == null || index < 0 || index >= list.Items.Count) return false;
            list.ScrollIntoView(list.Items[index]);
            list.UpdateLayout();
            return list.ContainerFromIndex(index) is Control c && c.Focus(FocusState.Keyboard);
        }

        public static bool IsInTree(DependencyObject e)
        {
            var root = Window.Current?.Content;
            for (var p = e; p != null; p = VisualTreeHelper.GetParent(p))
                if (ReferenceEquals(p, root)) return true;
            return false;
        }

        public static bool IsInside(DependencyObject element, DependencyObject container)
        {
            for (var p = element; p != null; p = VisualTreeHelper.GetParent(p))
                if (ReferenceEquals(p, container)) return true;
            return false;
        }

        /// <summary>Élément d'une liste correspondant à un élément visuel (carte, ligne).</summary>
        public static object ItemOf(ListViewBase list, object originalSource)
        {
            for (var p = originalSource as DependencyObject; p != null; p = VisualTreeHelper.GetParent(p))
            {
                if (p is SelectorItem container)
                {
                    var item = list.ItemFromContainer(container);
                    return item;
                }
                if (ReferenceEquals(p, list)) break;
            }
            return null;
        }

        public static object FocusedItem(ListViewBase list) => ItemOf(list, FocusManager.GetFocusedElement());

        public static SolidColorBrush Brush(uint argb) =>
            new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }

    /// <summary>
    /// Marges gardées autour de l'élément qui reçoit le focus quand une liste défile : la carte agrandie,
    /// son liseré et le titre de la rangée restent visibles, et rien ne sort de la zone sûre de la télé.
    /// </summary>
    public static class TvScroll
    {
        public static readonly DependencyProperty MarginsProperty = DependencyProperty.RegisterAttached(
            "Margins", typeof(Thickness), typeof(TvScroll), new PropertyMetadata(new Thickness(0), OnMarginsChanged));

        public static Thickness GetMargins(DependencyObject d) => (Thickness)d.GetValue(MarginsProperty);
        public static void SetMargins(DependencyObject d, Thickness value) => d.SetValue(MarginsProperty, value);

        static void OnMarginsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListViewBase list)
            {
                list.ContainerContentChanging -= OnContainerChanging;
                list.ContainerContentChanging += OnContainerChanging;
            }
            else if (d is UIElement u)
            {
                u.BringIntoViewRequested -= OnBringIntoView;
                u.BringIntoViewRequested += OnBringIntoView;
            }
        }

        static void OnContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue) return;
            var c = args.ItemContainer;
            c.SetValue(MarginsProperty, GetMargins(sender));
        }

        static void OnBringIntoView(UIElement sender, BringIntoViewRequestedEventArgs args)
        {
            if (!ReferenceEquals(args.TargetElement, sender)) return;
            var m = GetMargins(sender);
            var r = args.TargetRect;
            args.TargetRect = new Rect(r.X - m.Left, r.Y - m.Top, r.Width + m.Left + m.Right, r.Height + m.Top + m.Bottom);
        }
    }
}
