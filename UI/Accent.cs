using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace FluxTV.UI
{
    /// <summary>
    /// Couleur d'accent de l'interface : indigo (charte de FluxTV) ou or (Pack Soutien). Les pinceaux de la
    /// charte sont partagés par tous les écrans : changer leur couleur repeint aussitôt toute l'interface.
    /// </summary>
    public static class Accent
    {
        // Pinceau de la charte, couleur indigo, couleur or
        static readonly (string Key, uint Indigo, uint Gold)[] Brushes =
        {
            ("AccentBrush", 0xFF5B5CF0, 0xFFE0A126),
            ("AccentBrightBrush", 0xFF8E97FF, 0xFFFCD34D),
            ("AccentSoftBrush", 0x2E5B5CF0, 0x2ED4950F),
            ("VioletBrush", 0xFF9B4DEB, 0xFFF59E0B),
            ("TextControlBorderBrushFocused", 0xFF8E97FF, 0xFFFCD34D),
            ("TextControlSelectionHighlightColor", 0xFF5B5CF0, 0xFFD4950F),
            ("ToggleSwitchFillOn", 0xFF5B5CF0, 0xFFD4950F),
            ("ToggleSwitchFillOnPointerOver", 0xFF7476F3, 0xFFE0A126),
            ("ToggleSwitchFillOnPressed", 0xFF4A4BD6, 0xFFB45309),
            ("ProgressRingForegroundThemeBrush", 0xFF8E97FF, 0xFFFCD34D),
        };

        // Dégradé des boutons principaux : indigo → violet, ou or → ambre (texte blanc lisible dessus)
        static readonly (uint Indigo, uint Gold)[] Gradient = { (0xFF5B5CF0, 0xFFD4950F), (0xFF9B4DEB, 0xFFB45309) };

        /// <summary>Accent doré à l'écran.</summary>
        public static bool Gold { get; private set; }

        public static void Apply(bool gold)
        {
            Gold = gold;
            foreach (var b in Brushes)
                if (Find(b.Key) is SolidColorBrush brush) brush.Color = ToColor(gold ? b.Gold : b.Indigo);
            if (Find("AccentGradient") is LinearGradientBrush g)
                for (var i = 0; i < g.GradientStops.Count && i < Gradient.Length; i++)
                    g.GradientStops[i].Color = ToColor(gold ? Gradient[i].Gold : Gradient[i].Indigo);
        }

        /// <summary>Pinceau de la charte, dans les ressources de l'app ou leurs dictionnaires fusionnés.</summary>
        static object Find(string key)
        {
            var res = Application.Current.Resources;
            if (res.TryGetValue(key, out var o)) return o;
            foreach (var d in res.MergedDictionaries)
                if (d.TryGetValue(key, out o)) return o;
            return null;
        }

        static Color ToColor(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }
}
