using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluxTV.Core;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;

namespace FluxTV.Services
{
    public enum PhoneLinkEnd { TooManyAttempts, Expired }

    /// <summary>
    /// Petite page web servie par la console pendant l'ajout de la source : le téléphone l'ouvre en scannant
    /// le QR code et y colle ses infos (voir Core/PhoneLink.cs). Réseau local uniquement, un code différent à
    /// chaque ouverture ; la page se ferme une fois la source ajoutée, après 15 minutes, ou après trop
    /// d'essais avec un mauvais code. Rien ne passe par Internet.
    /// </summary>
    public sealed class PhoneLinkServer
    {
        public const int PreferredPort = 8765;
        static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
        static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

        readonly object gate = new object();
        StreamSocketListener listener;
        Timer expiry;
        bool stopped;

        public PhoneLink Link { get; } = new PhoneLink();

        /// <summary>Adresse à ouvrir sur le téléphone (celle du QR code).</summary>
        public string Url { get; private set; }

        /// <summary>Infos envoyées par le téléphone. Appelé hors du fil de l'interface.</summary>
        public event Action<Source> SourceReceived;

        /// <summary>Page fermée d'elle-même (trop d'essais, ou 15 minutes passées). Appelé hors du fil de l'interface.</summary>
        public event Action<PhoneLinkEnd> Ended;

        PhoneLinkServer() { }

        bool IsStopped { get { lock (gate) return stopped; } }

        /// <summary>
        /// Ouvre la page sur le réseau local. Null si la console n'a pas d'adresse sur un réseau local
        /// (ni Wi-Fi ni câble) ; une erreur si le système refuse d'ouvrir la page.
        /// </summary>
        public static async Task<PhoneLinkServer> StartAsync()
        {
            var ip = LocalAddress();
            if (ip == null) return null;
            var server = new PhoneLinkServer();
            var host = new HostName(ip);
            StreamSocketListener l;
            try { l = await ListenAsync(host, PreferredPort.ToString(CultureInfo.InvariantCulture), server.OnConnection); }
            catch (Exception)
            {
                // Port déjà pris par une autre app : le système en choisit un libre
                l = await ListenAsync(host, "", server.OnConnection);
            }
            var port = int.Parse(l.Information.LocalPort, CultureInfo.InvariantCulture);
            server.listener = l;
            server.Url = server.Link.Url(ip, port);
            server.expiry = new Timer(server.OnExpiry, null, Lifetime, Timeout.InfiniteTimeSpan);
            return server;
        }

        static async Task<StreamSocketListener> ListenAsync(HostName host, string port,
            TypedEventHandler<StreamSocketListener, StreamSocketListenerConnectionReceivedEventArgs> handler)
        {
            var l = new StreamSocketListener();
            l.ConnectionReceived += handler;
            try
            {
                await l.BindEndpointAsync(host, port);
                return l;
            }
            catch
            {
                l.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Adresse IPv4 de la console sur le réseau local : celle de la connexion utilisée pour Internet
        /// (Wi-Fi ou câble), sinon la première adresse locale trouvée. Null s'il n'y en a pas.
        /// </summary>
        public static string LocalAddress()
        {
            try
            {
                var hosts = NetworkInformation.GetHostNames()
                    .Where(h => h.Type == HostNameType.Ipv4 && h.IPInformation?.NetworkAdapter != null && PhoneLink.IsPrivateIPv4(h.CanonicalName))
                    .ToList();
                var adapter = NetworkInformation.GetInternetConnectionProfile()?.NetworkAdapter?.NetworkAdapterId;
                var best = adapter == null ? null : hosts.FirstOrDefault(h => h.IPInformation.NetworkAdapter.NetworkAdapterId == adapter.Value);
                return (best ?? hosts.FirstOrDefault())?.CanonicalName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        async void OnConnection(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            Source received = null;
            try
            {
                using (var socket = args.Socket)
                {
                    if (IsStopped) return;
                    using (var input = socket.InputStream.AsStreamForRead(0))
                    using (var output = socket.OutputStream.AsStreamForWrite(0))
                        received = await Link.ServeAsync(input, output, RequestTimeout);
                }
            }
            catch (Exception)
            {
                // Connexion ratée (téléphone parti, réseau coupé) : jamais de plantage
            }
            try
            {
                if (received != null && !IsStopped) SourceReceived?.Invoke(received);
                if (Link.Closed) End(PhoneLinkEnd.TooManyAttempts);
            }
            catch (Exception)
            {
            }
        }

        void OnExpiry(object state)
        {
            // Connexion en cours : on laisse le téléphone voir le résultat
            if (Link.State == PhoneLinkState.Connecting)
            {
                lock (gate) if (!stopped) expiry?.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
                return;
            }
            End(PhoneLinkEnd.Expired);
        }

        void End(PhoneLinkEnd reason)
        {
            if (Stop()) Ended?.Invoke(reason);
        }

        /// <summary>Ferme la page tout de suite. Vrai si elle était encore ouverte.</summary>
        public bool Stop()
        {
            StreamSocketListener l;
            lock (gate)
            {
                if (stopped) return false;
                stopped = true;
                l = listener;
                listener = null;
                expiry?.Dispose();
                expiry = null;
            }
            SourceReceived = null;
            try { l?.Dispose(); }
            catch (Exception) { }
            return true;
        }

        /// <summary>Ferme la page un peu plus tard : le téléphone a le temps d'afficher « C'est fait ».</summary>
        public void StopAfter(TimeSpan delay)
        {
            _ = Task.Delay(delay).ContinueWith(_ => Stop(), TaskScheduler.Default);
        }
    }
}
