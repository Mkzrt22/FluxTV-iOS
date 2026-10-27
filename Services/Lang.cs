using System.Globalization;
using FluxTV.Core;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.UserProfile;

namespace FluxTV.Services
{
    /// <summary>
    /// Langue de l'app : français ou anglais. Demandée au premier lancement (<see cref="Views.LanguagePage"/>),
    /// modifiable dans les réglages. Tant qu'aucun choix n'est fait : celle de la console ou du PC (la
    /// première de ses langues qui est le français ou l'anglais, l'anglais pour toutes les autres).
    /// </summary>
    public static class Lang
    {
        public const string Fr = "fr", En = "en";
        const string Key = "lang";

        /// <summary>Langue choisie, ou null tant que l'écran de choix n'a pas été validé.</summary>
        public static string Chosen
        {
            get
            {
                var v = ApplicationData.Current.LocalSettings.Values[Key] as string;
                return v == Fr || v == En ? v : null;
            }
        }

        /// <summary>Langue proposée d'office, d'après les langues réglées sur l'appareil.</summary>
        public static string Device
        {
            get
            {
                foreach (var l in GlobalizationPreferences.Languages)
                {
                    var c = l.Length >= 2 ? l.Substring(0, 2).ToLowerInvariant() : l;
                    if (c == Fr || c == En) return c;
                }
                return En;
            }
        }

        public static string Current => Tr.English ? En : Fr;

        /// <summary>Nom de la langue, toujours écrit dans cette langue (comme dans tous les sélecteurs).</summary>
        public static string Name(string code) => code == Fr ? "Français" : "English";

        /// <summary>Au démarrage : la langue choisie (ou celle de l'appareil) s'applique aux textes, dates et nombres.</summary>
        public static void Apply() => Use(Chosen ?? Device);

        /// <summary>Mémorise le choix et l'applique.</summary>
        public static void Save(string code)
        {
            ApplicationData.Current.LocalSettings.Values[Key] = code;
            Use(code);
        }

        /// <summary>Captures du serveur de compilation : langue imposée, sans rien enregistrer.</summary>
        public static void Use(string code)
        {
            Tr.English = code == En;
            // En anglais, l'heure suit l'horloge réglée sur l'appareil (« 8:05 PM » ou « 20:05 »)
            var clocks = GlobalizationPreferences.Clocks;
            Tr.Clock24 = clocks.Count == 0 || clocks[0] != ClockIdentifiers.TwelveHour;
            var tag = Tr.English ? "en-US" : "fr-FR";
            ApplicationLanguages.PrimaryLanguageOverride = tag;
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Tr.Culture;
        }
    }
}
