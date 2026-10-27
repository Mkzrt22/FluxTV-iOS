using System.Globalization;

namespace FluxTV.Core
{
    /// <summary>
    /// Langue des textes affichés : français ou anglais. Réglée au démarrage par l'app (choix du
    /// premier lancement, modifiable dans les réglages) et par les tests.
    /// </summary>
    public static class Tr
    {
        static readonly CultureInfo French = new CultureInfo("fr-FR"), EnglishCulture = new CultureInfo("en-US");

        public static bool English { get; set; }

        /// <summary>Horloge sur 24 heures (en anglais, l'appareil peut préférer « 8:05 PM »).</summary>
        public static bool Clock24 { get; set; } = true;

        /// <summary>Texte dans la langue de l'app : les deux versions côte à côte dans le code.</summary>
        public static string T(string fr, string en) => English ? en : fr;

        /// <summary>Culture des dates et des nombres (noms des jours et des mois, virgule ou point).</summary>
        public static CultureInfo Culture => English ? EnglishCulture : French;
    }
}
