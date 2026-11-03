using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Windows.Services.Store;
using Windows.Storage;
using Windows.UI.Core;

namespace FluxTV.Services
{
    /// <summary>Résultat d'un achat du Pack Soutien.</summary>
    public enum SupportResult { Done, Cancelled, Unavailable, Network, Error }

    /// <summary>
    /// Pack Soutien : extension « durable » du Microsoft Store (identifiant « packsoutien » dans Partner
    /// Center), achetée une fois et gardée pour toujours sur le compte Microsoft. En remerciement, elle
    /// débloque le thème Or et le badge Soutien ; toutes les fonctions de l'app restent gratuites.
    /// </summary>
    public static class Support
    {
        /// <summary>Identifiant de l'extension dans Partner Center (InAppOfferToken côté Store).</summary>
        public const string ProductId = "packsoutien";

        static readonly IPropertySet values = ApplicationData.Current.LocalSettings.Values;
        static StoreContext context;
        static bool? testOwned;
        static string testPrice;

        /// <summary>Pack acheté. Dernier état connu, gardé pour l'affichage hors connexion.</summary>
        public static bool Owned => testOwned ?? Get("supporter", false);

        /// <summary>Thème Or choisi dans les réglages (actif seulement avec le pack).</summary>
        public static bool GoldTheme
        {
            get => Get("goldTheme", true);
            set
            {
                Put("goldTheme", value);
                Raise();
            }
        }

        /// <summary>Couleur d'accent dorée à l'écran.</summary>
        public static bool GoldActive => Owned && GoldTheme;

        /// <summary>Pack acheté ou thème changé : l'écran se met à jour (toujours sur le fil de l'interface).</summary>
        public static event Action Changed;

        static StoreContext Context
        {
            get
            {
                if (context == null)
                {
                    context = StoreContext.GetDefault();
                    // Achat sur une autre console, remboursement… : licence relue
                    context.OfflineLicensesChanged += (s, e) => _ = RefreshAsync();
                }
                return context;
            }
        }

        /// <summary>Relit la licence du pack : au lancement, puis à chaque changement signalé par le Store.</summary>
        public static async Task RefreshAsync()
        {
            if (testOwned != null) return;
            try
            {
                var license = await Context.GetAppLicenseAsync();
                SetOwned(license.AddOnLicenses.Values.Any(l => l.IsActive && IsPack(l.InAppOfferToken)));
            }
            catch (Exception ex)
            {
                // Store injoignable : l'état connu reste valable
                Journal.Write("Licence du Pack Soutien illisible : " + ex.Message);
            }
        }

        /// <summary>Prix dans la monnaie du compte (« 1,99 € »), ou null si le Store ne répond pas.</summary>
        public static async Task<string> PriceAsync()
        {
            if (testOwned != null) return testPrice;
            try
            {
                return (await FindAsync())?.Price?.FormattedPrice;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Achat : la fenêtre du Microsoft Store s'ouvre par-dessus l'app (prix, confirmation, paiement).</summary>
        public static async Task<SupportResult> BuyAsync()
        {
            if (testOwned != null) return SupportResult.Unavailable;
            try
            {
                var product = await FindAsync();
                if (product == null) return SupportResult.Unavailable;
                var r = await product.RequestPurchaseAsync();
                switch (r.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                    case StorePurchaseStatus.AlreadyPurchased:
                        SetOwned(true);
                        return SupportResult.Done;
                    case StorePurchaseStatus.NotPurchased:
                        return SupportResult.Cancelled;
                    case StorePurchaseStatus.NetworkError:
                        return SupportResult.Network;
                    default:
                        Journal.Write("Achat du Pack Soutien : " + r.Status + " " + r.ExtendedError?.Message);
                        return SupportResult.Error;
                }
            }
            catch (Exception ex)
            {
                Journal.Write("Achat du Pack Soutien impossible : " + ex.Message);
                return SupportResult.Unavailable;
            }
        }

        /// <summary>Captures automatiques : pack acheté ou non, sans passer par le Store.</summary>
        public static void UseForTest(bool owned, string price)
        {
            testOwned = owned;
            testPrice = price;
            Raise();
        }

        static async Task<StoreProduct> FindAsync()
        {
            var r = await Context.GetAssociatedStoreProductsAsync(new[] { "Durable" });
            if (r.ExtendedError != null) throw r.ExtendedError;
            return r.Products.Values.FirstOrDefault(p => IsPack(p.InAppOfferToken));
        }

        static void SetOwned(bool owned)
        {
            if (owned == Get("supporter", false)) return;
            Put("supporter", owned);
            Raise();
        }

        static void Raise()
        {
            var dispatcher = CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher == null || dispatcher.HasThreadAccess) Changed?.Invoke();
            else _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Changed?.Invoke());
        }

        static bool IsPack(string token) => string.Equals(token, ProductId, StringComparison.OrdinalIgnoreCase);

        static bool Get(string key, bool fallback)
        {
            try { return values.TryGetValue(key, out var v) && v is bool b ? b : fallback; }
            catch (Exception) { return fallback; }
        }

        static void Put(string key, bool value)
        {
            try { values[key] = value; }
            catch (Exception) { }
        }
    }
}
