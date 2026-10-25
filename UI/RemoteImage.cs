using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace FluxTV.UI
{
    /// <summary>
    /// Image distante (affiche, logo, vignette) : coins arrondis, apparition en fondu, icône de remplacement
    /// tant que l'image manque ou si elle est illisible. Décodée à la taille affichée (mémoire de la console).
    /// </summary>
    public sealed class RemoteImage : Grid
    {
        readonly TextBlock glyph;
        readonly Border picture;
        ImageBrush brush;
        Storyboard fade;

        /// <summary>Image affichée : sa largeur et sa hauteur d'origine (affiche verticale ou image large).</summary>
        public event Action<RemoteImage, int, int> Opened;

        public RemoteImage()
        {
            IsHitTestVisible = false;
            glyph = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.35,
                Foreground = Palette.TextDim,
                IsTextScaleFactorEnabled = false,
                Text = "",
            };
            picture = new Border { Opacity = 0 };
            Children.Add(glyph);
            Children.Add(picture);
        }

        public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(
            nameof(Url), typeof(string), typeof(RemoteImage), new PropertyMetadata(null, (d, e) => ((RemoteImage)d).Load()));

        public string Url
        {
            get => (string)GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }

        public static readonly DependencyProperty ImageStretchProperty = DependencyProperty.Register(
            nameof(ImageStretch), typeof(Stretch), typeof(RemoteImage), new PropertyMetadata(Stretch.UniformToFill, (d, e) =>
            {
                var r = (RemoteImage)d;
                if (r.brush != null) r.brush.Stretch = (Stretch)e.NewValue;
            }));

        public Stretch ImageStretch
        {
            get => (Stretch)GetValue(ImageStretchProperty);
            set => SetValue(ImageStretchProperty, value);
        }

        public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(
            nameof(DecodeWidth), typeof(int), typeof(RemoteImage), new PropertyMetadata(0));

        /// <summary>Largeur de décodage en pixels effectifs (0 = taille d'origine).</summary>
        public int DecodeWidth
        {
            get => (int)GetValue(DecodeWidthProperty);
            set => SetValue(DecodeWidthProperty, value);
        }

        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
            nameof(Radius), typeof(CornerRadius), typeof(RemoteImage), new PropertyMetadata(new CornerRadius(0), (d, e) =>
                ((RemoteImage)d).picture.CornerRadius = (CornerRadius)e.NewValue));

        public CornerRadius Radius
        {
            get => (CornerRadius)GetValue(RadiusProperty);
            set => SetValue(RadiusProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(RemoteImage), new PropertyMetadata("", (d, e) =>
                ((RemoteImage)d).glyph.Text = (string)e.NewValue ?? ""));

        /// <summary>Icône (Segoe MDL2) affichée sans image.</summary>
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty ImagePaddingProperty = DependencyProperty.Register(
            nameof(ImagePadding), typeof(Thickness), typeof(RemoteImage), new PropertyMetadata(new Thickness(0), (d, e) =>
                ((RemoteImage)d).picture.Margin = (Thickness)e.NewValue));

        public Thickness ImagePadding
        {
            get => (Thickness)GetValue(ImagePaddingProperty);
            set => SetValue(ImagePaddingProperty, value);
        }

        void Load()
        {
            fade?.Stop();
            fade = null;
            if (brush != null)
            {
                brush.ImageOpened -= OnOpened;
                brush.ImageFailed -= OnFailed;
                brush = null;
            }
            picture.Background = null;
            picture.Opacity = 0;
            glyph.Visibility = Visibility.Visible;

            var url = Url?.Trim();
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
            var bitmap = new BitmapImage();
            if (DecodeWidth > 0)
            {
                bitmap.DecodePixelType = DecodePixelType.Logical;
                bitmap.DecodePixelWidth = DecodeWidth;
            }
            try
            {
                bitmap.UriSource = uri;
            }
            catch (Exception)
            {
                return;
            }
            brush = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = ImageStretch,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center,
            };
            brush.ImageOpened += OnOpened;
            brush.ImageFailed += OnFailed;
            picture.Background = brush;
        }

        void OnOpened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(sender, brush)) return;
            glyph.Visibility = Visibility.Collapsed;
            var anim = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(220) };
            Storyboard.SetTarget(anim, picture);
            Storyboard.SetTargetProperty(anim, "Opacity");
            fade = new Storyboard();
            fade.Children.Add(anim);
            fade.Completed += (s, a) => picture.Opacity = 1;
            fade.Begin();
            if (brush.ImageSource is BitmapImage b) Opened?.Invoke(this, b.PixelWidth, b.PixelHeight);
        }

        void OnFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (!ReferenceEquals(sender, brush)) return;
            picture.Background = null;
            glyph.Visibility = Visibility.Visible;
        }
    }
}
