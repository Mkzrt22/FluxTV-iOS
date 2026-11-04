using System;
using System.Globalization;

namespace FluxTV.Core
{
    /// <summary>Usage de l'app retenu pour l'invitation à la noter (gardé dans les paramètres locaux).</summary>
    public sealed class RateUsage
    {
        /// <summary>Jours différents où quelque chose a été regardé.</summary>
        public int Days;
        /// <summary>Dernier jour compté (« 2026-11-04 », date locale).</summary>
        public string LastDay;
        /// <summary>Temps de visionnage cumulé, en secondes.</summary>
        public double Seconds;
        /// <summary>Invitation déjà montrée, ou app notée depuis les réglages : elle ne revient jamais.</summary>
        public bool Asked;
    }

    /// <summary>
    /// Quand inviter à noter l'app : une seule fois, à qui s'en sert vraiment (des jours différents de
    /// visionnage, plusieurs heures au total), en fermant une lecture assez longue qui s'est bien passée.
    /// Jamais pendant une vidéo. Calculs seuls, sans stockage ni interface (voir Services/Rating.cs).
    /// </summary>
    public static class RatePolicy
    {
        /// <summary>Jours différents avec au moins une lecture.</summary>
        public const int DaysNeeded = 3;
        /// <summary>Temps de visionnage cumulé (heures).</summary>
        public const double HoursNeeded = 2;
        /// <summary>Durée de la lecture qui vient de se terminer (minutes) : la personne vient d'en profiter.</summary>
        public const double SessionMinutesNeeded = 10;

        /// <summary>Temps de lecture ajouté ; le jour compte s'il est nouveau.</summary>
        public static void Add(RateUsage usage, double seconds, DateTime localNow)
        {
            if (usage == null || !(seconds > 0) || double.IsInfinity(seconds)) return;
            usage.Seconds += seconds;
            var day = localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (day == usage.LastDay) return;
            usage.Days++;
            usage.LastDay = day;
        }

        /// <summary>Invitation à montrer à la fermeture d'une lecture de cette durée (secondes regardées).</summary>
        public static bool ShouldAsk(RateUsage usage, double sessionSeconds, bool sessionFailed) =>
            usage != null && !usage.Asked && !sessionFailed
            && usage.Days >= DaysNeeded
            && usage.Seconds >= HoursNeeded * 3600
            && sessionSeconds >= SessionMinutesNeeded * 60;
    }
}
