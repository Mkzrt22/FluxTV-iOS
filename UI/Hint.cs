using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace FluxTV.UI
{
    /// <summary>Aide de manette : pastille du bouton (Ⓐ vert, Ⓑ rouge, Ⓧ bleu, Ⓨ jaune) et action.</summary>
    public sealed class Hint : StackPanel
    {
        readonly Border badge;
        readonly TextBlock keyText;
        readonly TextBlock label;

        public Hint()
        {
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 0, 18, 0);
            keyText = new TextBlock
            {
                FontFamily = (FontFamily)Application.Current.Resources["FontBold"],
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                FontSize = 11,
                Foreground = Tv.Brush(0xFF04040D),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsTextScaleFactorEnabled = false,
            };
            badge = new Border
            {
                MinWidth = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = keyText,
            };
            label = new TextBlock
            {
                FontFamily = (FontFamily)Application.Current.Resources["FontSemi"],
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                FontSize = 12,
                Foreground = Palette.TextDim,
                Margin = new Thickness(7, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsTextScaleFactorEnabled = false,
            };
            Children.Add(badge);
            Children.Add(label);
            Apply();
        }

        public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
            nameof(Key), typeof(string), typeof(Hint), new PropertyMetadata("A", (d, e) => ((Hint)d).Apply()));

        /// <summary>A, B, X, Y, LB, RB, LT, RT, Menu ou Vue.</summary>
        public string Key
        {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(Hint), new PropertyMetadata("", (d, e) => ((Hint)d).label.Text = (string)e.NewValue ?? ""));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        void Apply()
        {
            var b = Key ?? "A";
            uint color;
            switch (b)
            {
                case "A": color = 0xFF4CC35A; break;
                case "B": color = 0xFFF0545C; break;
                case "X": color = 0xFF4A90E8; break;
                case "Y": color = 0xFFF5C33B; break;
                default: color = 0xFFC9CCE6; break;
            }
            badge.Background = Tv.Brush(color);
            keyText.Text = b == "Menu" ? "☰" : b == "Vue" ? "⧉" : b;
        }
    }
}
