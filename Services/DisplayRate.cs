using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display.Core;

namespace FluxTV.Services
{
    /// <summary>
    /// Fréquence de la télé calée sur celle de la vidéo (24, 50 ou 60 Hz) : mouvements fluides, sans
    /// saccades. Seulement sur Xbox, et si les modes 24/50 Hz sont autorisés dans les réglages de la console.
    /// </summary>
    public static class DisplayRate
    {
        static bool changed;

        /// <summary>Change le mode de l'écran si besoin. Renvoie true si l'écran est à la bonne fréquence.</summary>
        public static async Task<bool> MatchAsync(double fps)
        {
            try
            {
                var hdmi = HdmiDisplayInformation.GetForCurrentView();
                if (hdmi == null) return false;
                var current = hdmi.GetCurrentDisplayMode();
                var targets = TargetRates(fps);
                if (current == null || targets.Length == 0) return false;
                if (Near(current.RefreshRate, targets[0])) return true;
                var modes = hdmi.GetSupportedDisplayModes();
                foreach (var t in targets)
                {
                    var candidates = modes.Where(m =>
                        m.ResolutionWidthInRawPixels == current.ResolutionWidthInRawPixels
                        && m.ResolutionHeightInRawPixels == current.ResolutionHeightInRawPixels
                        && !m.StereoEnabled
                        && Near(m.RefreshRate, t)).ToList();
                    var mode = candidates.FirstOrDefault(m => m.ColorSpace == current.ColorSpace && m.BitsPerPixel == current.BitsPerPixel)
                               ?? candidates.FirstOrDefault(m => m.ColorSpace == current.ColorSpace)
                               ?? candidates.FirstOrDefault();
                    if (mode == null) continue;
                    if (Near(current.RefreshRate, mode.RefreshRate)) return true;
                    var ok = await hdmi.RequestSetCurrentDisplayModeAsync(mode);
                    if (ok) changed = true;
                    return ok;
                }
            }
            catch (Exception e)
            {
                Journal.Write("Fréquence de l'écran : " + e.Message);
            }
            return false;
        }

        /// <summary>Mode d'origine de la console (fermeture du lecteur).</summary>
        public static async Task RestoreAsync()
        {
            if (!changed) return;
            changed = false;
            try
            {
                var hdmi = HdmiDisplayInformation.GetForCurrentView();
                if (hdmi != null) await hdmi.SetDefaultDisplayModeAsync();
            }
            catch (Exception e)
            {
                Journal.Write("Fréquence de l'écran (retour) : " + e.Message);
            }
        }

        /// <summary>Fréquences possibles, de la meilleure à la moins bonne.</summary>
        static double[] TargetRates(double fps)
        {
            if (Near(fps, 23.976)) return new[] { 23.976, 24.0 };
            if (Near(fps, 24)) return new[] { 24.0, 23.976 };
            if (Near(fps, 25) || Near(fps, 50)) return new[] { 50.0 };
            if (Near(fps, 29.97) || Near(fps, 59.94)) return new[] { 59.94, 60.0 };
            if (Near(fps, 30) || Near(fps, 60)) return new[] { 60.0, 59.94 };
            return new double[0];
        }

        static bool Near(double a, double b) => Math.Abs(a - b) < 0.012;
    }
}
