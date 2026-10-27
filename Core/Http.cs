using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FluxTV.Core
{
    public enum FluxError
    {
        BadAddress, Http, Offline, Timeout, Network, AuthRefused, AccountInactive, UnexpectedResponse, EmptyPlaylist,
    }

    /// <summary>Erreur affichée à l'utilisateur, en français.</summary>
    public sealed class FluxException : Exception
    {
        public FluxError Kind { get; }
        public int Code { get; }

        public FluxException(FluxError kind, string detail = null, int code = 0) : base(Describe(kind, detail, code))
        {
            Kind = kind;
            Code = code;
        }

        static string Describe(FluxError kind, string detail, int code)
        {
            switch (kind)
            {
                case FluxError.BadAddress: return Tr.T("Adresse invalide. Vérifie le serveur (exemple : http://mon-serveur.com:8080).",
                    "Invalid address. Check the server (example: http://my-server.com:8080).");
                case FluxError.Http:
                    if (code == 401 || code == 403) return Tr.T($"Accès refusé par le serveur (code {code}). Vérifie tes identifiants.",
                        $"Access denied by the server (code {code}). Check your login details.");
                    if (code == 404) return Tr.T("Adresse introuvable sur le serveur (code 404).", "Address not found on the server (code 404).");
                    if (code >= 500 && code <= 599) return Tr.T($"Le serveur du fournisseur ne répond pas correctement (code {code}). Réessaie plus tard.",
                        $"The provider's server isn't responding properly (code {code}). Try again later.");
                    return Tr.T($"Le serveur a répondu avec le code {code}.", $"The server responded with code {code}.");
                case FluxError.Offline: return Tr.T("Pas de connexion internet.", "No internet connection.");
                case FluxError.Timeout: return Tr.T("Le serveur met trop de temps à répondre.", "The server is taking too long to respond.");
                case FluxError.Network: return Tr.T("Connexion impossible : ", "Couldn't connect: ") + (detail ?? Tr.T("erreur réseau", "network error")) + ".";
                case FluxError.AuthRefused: return Tr.T("Identifiants refusés par le serveur.", "The server rejected the login details.");
                case FluxError.AccountInactive: return Tr.T("Compte " + (detail ?? "inactif") + " chez le fournisseur.", "Account " + (detail ?? "inactive") + " with the provider.");
                case FluxError.UnexpectedResponse: return Tr.T("Réponse inattendue du serveur. Vérifie l'adresse du serveur.", "Unexpected response from the server. Check the server address.");
                case FluxError.EmptyPlaylist: return Tr.T("Cette playlist ne contient aucune chaîne ni aucun film.", "This playlist contains no channels and no movies.");
                default: return detail ?? Tr.T("Erreur", "Error");
            }
        }

        /// <summary>Message lisible pour n'importe quelle erreur.</summary>
        public static string MessageOf(Exception e) =>
            e is FluxException f ? f.Message : new FluxException(FluxError.Network, e?.Message).Message;
    }

    /// <summary>Requêtes HTTP simples (API du fournisseur, playlists, guide TV).</summary>
    public static class HttpLoader
    {
        /// <summary>Navigateur ordinaire : certains serveurs refusent les clients inconnus.</summary>
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; Xbox; Xbox Series S) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 Edg/120.0";

        static readonly HttpClient Client = CreateClient();

        static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
            };
            var c = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            return c;
        }

        /// <summary>Corps de la réponse (octets). Lève FluxException avec un message en français.</summary>
        public static async Task<byte[]> BytesAsync(string url, TimeSpan timeout, CancellationToken cancel = default)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new FluxException(FluxError.BadAddress);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                cts.CancelAfter(timeout);
                try
                {
                    using (var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new FluxException(FluxError.Http, code: (int)response.StatusCode);
                        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
                {
                    throw new FluxException(FluxError.Timeout);
                }
                catch (HttpRequestException e)
                {
                    throw Map(e);
                }
            }
        }

        public static async Task<string> TextAsync(string url, TimeSpan timeout, CancellationToken cancel = default)
        {
            var bytes = await BytesAsync(url, timeout, cancel).ConfigureAwait(false);
            return Decode(bytes);
        }

        /// <summary>Texte UTF-8 (BOM retiré) ; gzip décompressé si le serveur l'envoie tel quel.</summary>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            if (Gzip.IsGzip(bytes)) bytes = Gzip.Decompress(bytes) ?? new byte[0];
            int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
        }

        static FluxException Map(HttpRequestException e)
        {
            var msg = (e.InnerException?.Message ?? e.Message ?? "").ToLowerInvariant();
            if (msg.Contains("name") && (msg.Contains("resolve") || msg.Contains("not known") || msg.Contains("introuvable")))
                return new FluxException(FluxError.Network, Tr.T("serveur introuvable", "server not found"));
            if (msg.Contains("refused") || msg.Contains("refus"))
                return new FluxException(FluxError.Network, Tr.T("le serveur refuse la connexion", "the server refuses the connection"));
            if (msg.Contains("certificate") || msg.Contains("ssl") || msg.Contains("tls") || msg.Contains("security"))
                return new FluxException(FluxError.Network, Tr.T("connexion sécurisée impossible (essaie avec http://)", "secure connection impossible (try with http://)"));
            return new FluxException(FluxError.Network, e.InnerException?.Message ?? e.Message);
        }
    }

    /// <summary>Décompression gzip (guides TV « .xml.gz », réponses compressées).</summary>
    public static class Gzip
    {
        public static bool IsGzip(byte[] data) => data != null && data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B;

        public static byte[] Decompress(byte[] data)
        {
            try
            {
                using (var input = new System.IO.MemoryStream(data))
                using (var gz = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new System.IO.MemoryStream())
                {
                    gz.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
