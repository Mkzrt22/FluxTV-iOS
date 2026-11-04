using System;
using System.Threading.Tasks;
using FluxTV.Core;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;

namespace FluxTV.Services
{
    /// <summary>Résultat d'une demande de note.</summary>
    public enum RateResult { Done, Cancelled, Opened, Network, Unavailable }

    /// <summary>
    /// Note de l'app sur le Microsoft Store. L'invitation n'est proposée qu'une fois, à qui se sert vraiment
    /// de l'app (voir RatePolicy) ; le bouton « Noter FluxTV » des réglages reste toujours là. La note se
    /// donne dans la fenêtre du Store, de 1 à 5 étoiles au choix de la personne : l'app ne suggère aucune
    /// note et n'offre rien en échange (règles du Microsoft Store).
    /// </summary>
    public static class Rating
    {
        /// <summary>Identifiant de FluxTV Player sur le Microsoft Store.</summary>
        public const string StoreId = "9P4NX39QHPW1";

        static readonly IPropertySet values = ApplicationData.Current.LocalSettings.Values;
        static RateUsage usage;

        static RateUsage Usage => usage ?? (usage = Load());

        /// <summary>
        /// Usage réel seulement : ni la démonstration, ni les essais automatiques, ni la copie personnelle
        /// (installée hors du Store).
        /// </summary>
        static bool Counts
        {
            get
            {
                var app = AppState.Current;
                return !app.TestMode && app.Embedded == null && app.Source != null && app.Source.Kind != SourceKind.Demo;
            }
        }

        /// <summary>Temps de lecture (secondes), transmis par le lecteur pendant et à la fin de la lecture.</summary>
        public static void AddWatched(double seconds)
        {
            if (!Counts) return;
            RatePolicy.Add(Usage, seconds, DateTime.Now);
            Save();
        }

        /// <summary>Lecture fermée : faut-il inviter à noter maintenant ?</summary>
        public static bool ShouldAsk(double sessionSeconds, bool sessionFailed) =>
            Counts && RatePolicy.ShouldAsk(Usage, sessionSeconds, sessionFailed);

        /// <summary>Invitation montrée (ou app notée depuis les réglages) : elle ne reviendra pas.</summary>
        public static void MarkAsked()
        {
            if (AppState.Current.TestMode || Usage.Asked) return;
            Usage.Asked = true;
            Save();
        }

        /// <summary>
        /// Fenêtre de note du Microsoft Store, par-dessus l'app (à appeler sur le fil de l'interface). Si elle
        /// ne s'ouvre pas, la page de l'app dans le Store, où la note se donne aussi.
        /// </summary>
        public static async Task<RateResult> RateAsync()
        {
            if (AppState.Current.TestMode) return RateResult.Cancelled;
            try
            {
                var r = await StoreContext.GetDefault().RequestRateAndReviewAppAsync();
                switch (r.Status)
                {
                    case StoreRateAndReviewStatus.Succeeded:
                        return RateResult.Done;
                    case StoreRateAndReviewStatus.CanceledByUser:
                        return RateResult.Cancelled;
                    case StoreRateAndReviewStatus.NetworkError:
                        return RateResult.Network;
                }
                Journal.Write("Fenêtre de note du Store : " + r.Status + " " + r.ExtendedError?.Message);
            }
            catch (Exception ex)
            {
                Journal.Write("Fenêtre de note du Store impossible : " + ex.Message);
            }
            foreach (var page in new[] { "review", "pdp" })
            {
                try
                {
                    if (await Launcher.LaunchUriAsync(new Uri("ms-windows-store://" + page + "/?ProductId=" + StoreId))) return RateResult.Opened;
                }
                catch (Exception ex)
                {
                    Journal.Write("Page du Store impossible à ouvrir (" + page + ") : " + ex.Message);
                }
            }
            return RateResult.Unavailable;
        }

        /// <summary>Message bref après la demande (null : rien à dire).</summary>
        public static string MessageOf(RateResult result, bool fromSettings)
        {
            switch (result)
            {
                case RateResult.Done:
                    return Tr.T("Merci beaucoup pour ta note !", "Thanks a lot for your rating!");
                case RateResult.Network:
                    return fromSettings
                        ? Tr.T("Pas de connexion au Microsoft Store. Réessaie dans un instant.", "Can't reach the Microsoft Store. Try again in a moment.")
                        : Tr.T("Pas de connexion au Microsoft Store. Tu pourras noter FluxTV plus tard dans les réglages.",
                            "Can't reach the Microsoft Store. You can rate FluxTV later in Settings.");
                case RateResult.Unavailable:
                    return fromSettings
                        ? Tr.T("Le Microsoft Store ne s'est pas ouvert. Réessaie plus tard.", "The Microsoft Store didn't open. Try again later.")
                        : Tr.T("Le Microsoft Store ne s'est pas ouvert. Tu pourras noter FluxTV plus tard dans les réglages.",
                            "The Microsoft Store didn't open. You can rate FluxTV later in Settings.");
                default:
                    return null;
            }
        }

        static RateUsage Load()
        {
            var u = new RateUsage();
            try
            {
                if (values.TryGetValue("rateDays", out var d) && d is int days) u.Days = days;
                if (values.TryGetValue("rateLastDay", out var l) && l is string last) u.LastDay = last;
                if (values.TryGetValue("rateSeconds", out var s) && s is double seconds) u.Seconds = seconds;
                if (values.TryGetValue("rateAsked", out var a) && a is bool asked) u.Asked = asked;
            }
            catch (Exception) { }
            return u;
        }

        static void Save()
        {
            var u = Usage;
            try
            {
                values["rateDays"] = u.Days;
                values["rateLastDay"] = u.LastDay ?? "";
                values["rateSeconds"] = u.Seconds;
                values["rateAsked"] = u.Asked;
            }
            catch (Exception) { }
        }
    }
}
