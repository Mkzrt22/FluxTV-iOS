using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FluxTV.Core
{
    /// <summary>En-tête d'une requête reçue par le petit serveur web local.</summary>
    public sealed class HttpRequestHead
    {
        public string Method = "";
        public string Path = "/";
        public string Query = "";
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int ContentLength;
        /// <summary>Taille de l'en-tête, ligne vide comprise : le corps commence juste après.</summary>
        public int HeadLength;
    }

    /// <summary>Le strict nécessaire de HTTP/1.1 pour une page de formulaire : une requête par connexion.</summary>
    public static class MiniHttp
    {
        public const int MaxHead = 8 * 1024;
        public const int MaxBody = 16 * 1024;

        /// <summary>En-tête complet dans les octets reçus, sinon null (il en manque). FormatException si invalide.</summary>
        public static HttpRequestHead TryParseHead(byte[] buffer, int count)
        {
            int end = -1;
            for (int i = 3; i < count; i++)
                if (buffer[i] == '\n' && buffer[i - 1] == '\r' && buffer[i - 2] == '\n' && buffer[i - 3] == '\r') { end = i + 1; break; }
            if (end < 0)
            {
                if (count >= MaxHead) throw new FormatException("en-tête trop long");
                return null;
            }
            var lines = Encoding.ASCII.GetString(buffer, 0, end - 4).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length != 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal)) throw new FormatException("ligne de requête invalide");
            var head = new HttpRequestHead { Method = first[0].ToUpperInvariant(), HeadLength = end };
            var target = first[1];
            int q = target.IndexOf('?');
            head.Path = q >= 0 ? target.Substring(0, q) : target;
            head.Query = q >= 0 ? target.Substring(q + 1) : "";
            if (!head.Path.StartsWith("/", StringComparison.Ordinal)) throw new FormatException("chemin invalide");
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                head.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            if (head.Headers.TryGetValue("Content-Length", out var len))
            {
                if (!int.TryParse(len, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0) throw new FormatException("longueur invalide");
                if (n > MaxBody) throw new FormatException("corps trop long");
                head.ContentLength = n;
            }
            return head;
        }

        /// <summary>Champs d'un formulaire (application/x-www-form-urlencoded), en UTF-8.</summary>
        public static Dictionary<string, string> ParseForm(string body)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in (body ?? "").Split('&'))
            {
                if (part.Length == 0) continue;
                int eq = part.IndexOf('=');
                var k = Decode(eq >= 0 ? part.Substring(0, eq) : part);
                var v = eq >= 0 ? Decode(part.Substring(eq + 1)) : "";
                if (!d.ContainsKey(k)) d[k] = v;
            }
            return d;
        }

        static string Decode(string s)
        {
            try { return Uri.UnescapeDataString(s.Replace('+', ' ')); }
            catch (UriFormatException) { return s; }
        }

        /// <summary>Réponse complète : l'app ne garde jamais rien en cache ni n'ouvre de cadre ailleurs.</summary>
        public static byte[] Response(int status, string contentType, string body)
        {
            var b = Encoding.UTF8.GetBytes(body ?? "");
            var reason = status == 200 ? "OK" : status == 404 ? "Not Found" : status == 405 ? "Method Not Allowed" : "Bad Request";
            var head = new StringBuilder()
                .Append("HTTP/1.1 ").Append(status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(reason).Append("\r\n")
                .Append("Content-Type: ").Append(contentType).Append("\r\n")
                .Append("Content-Length: ").Append(b.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n")
                .Append("Cache-Control: no-store\r\n")
                .Append("X-Content-Type-Options: nosniff\r\n")
                .Append("X-Frame-Options: DENY\r\n")
                .Append("Referrer-Policy: no-referrer\r\n")
                .Append("Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; form-action 'self'\r\n")
                .Append("Connection: close\r\n\r\n");
            var h = Encoding.ASCII.GetBytes(head.ToString());
            var all = new byte[h.Length + b.Length];
            Buffer.BlockCopy(h, 0, all, 0, h.Length);
            Buffer.BlockCopy(b, 0, all, h.Length, b.Length);
            return all;
        }
    }

    public enum PhoneLinkState { Waiting, Connecting, Connected, Failed }

    /// <summary>
    /// Ajout de la source depuis le téléphone. La console affiche un QR code vers une petite page web
    /// qu'elle sert elle-même sur le réseau local ; le téléphone y colle son lien de playlist ou son
    /// compte, et la console s'y connecte. Rien ne passe par Internet ni par un serveur de FluxTV.
    /// Sécurité : un code à usage unique dans l'adresse (6 caractères, environ un milliard de
    /// combinaisons), et la page se ferme après 30 requêtes avec un mauvais code.
    /// </summary>
    public sealed class PhoneLink
    {
        public const int MaxBadRequests = 30;
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // sans 0/O ni 1/I

        readonly object gate = new object();
        PhoneLinkState state = PhoneLinkState.Waiting;
        string message;
        int badRequests;
        /// <summary>Champs du dernier envoi, sans le mot de passe : « Corriger » les redonne au téléphone.</summary>
        Dictionary<string, string> lastValues;

        public string Code { get; }

        public PhoneLink(string code = null)
        {
            Code = code ?? NewCode();
        }

        public static string NewCode()
        {
            var bytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var sb = new StringBuilder(6);
            foreach (var b in bytes) sb.Append(Alphabet[b % Alphabet.Length]);
            return sb.ToString();
        }

        public PhoneLinkState State { get { lock (gate) return state; } }

        /// <summary>Trop de requêtes avec un mauvais code : la page ne répond plus.</summary>
        public bool Closed { get { lock (gate) return badRequests >= MaxBadRequests; } }

        /// <summary>Adresse à ouvrir sur le téléphone (celle du QR code).</summary>
        public string Url(string host, int port) => "http://" + host + ":" + port.ToString(CultureInfo.InvariantCulture) + "/" + Code;

        /// <summary>Avancement affiché par le téléphone (connexion en cours, réussie, ou l'erreur à corriger).</summary>
        public void Report(PhoneLinkState newState, string text = null)
        {
            lock (gate)
            {
                state = newState;
                message = text;
            }
        }

        /// <summary>Adresse IPv4 d'un réseau local (10.x, 172.16-31.x, 192.168.x).</summary>
        public static bool IsPrivateIPv4(string ip)
        {
            var p = (ip ?? "").Split('.');
            if (p.Length != 4) return false;
            var n = new int[4];
            for (int i = 0; i < 4; i++)
                if (!int.TryParse(p[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]) || n[i] > 255) return false;
            return n[0] == 10 || (n[0] == 172 && n[1] >= 16 && n[1] <= 31) || (n[0] == 192 && n[1] == 168);
        }

        // ───────────────────────────── Une connexion ─────────────────────────────

        /// <summary>
        /// Lit une requête, répond, et rend la source envoyée par le téléphone (ou null). Les flux viennent
        /// d'une connexion réseau : un client trop lent est abandonné au bout du délai.
        /// </summary>
        public async Task<Source> ServeAsync(Stream input, Stream output, TimeSpan timeout)
        {
            var buffer = new byte[MiniHttp.MaxHead + MiniHttp.MaxBody];
            int count = 0;
            HttpRequestHead head = null;
            byte[] reply;
            Source received = null;
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    while (true)
                    {
                        if (head == null) head = MiniHttp.TryParseHead(buffer, count);
                        if (head != null && count >= head.HeadLength + head.ContentLength) break;
                        if (count == buffer.Length) throw new FormatException("requête trop longue");
                        var read = input.ReadAsync(buffer, count, buffer.Length - count, cts.Token);
                        if (await Task.WhenAny(read, Task.Delay(Timeout.Infinite, cts.Token)) != read) return null;
                        int n = await read;
                        if (n <= 0) return null;
                        // Pas une requête HTTP (essai en HTTPS d'un navigateur, par exemple) : on coupe tout
                        // de suite, le navigateur repasse aussitôt en HTTP au lieu d'attendre le délai
                        if (count == 0 && (buffer[0] < 'A' || buffer[0] > 'Z')) return null;
                        count += n;
                    }
                }
                catch (OperationCanceledException) { return null; }
                catch (FormatException) { head = null; }
                catch (IOException) { return null; }
                if (head == null)
                {
                    reply = MiniHttp.Response(400, "text/plain; charset=utf-8", "Bad Request");
                }
                else
                {
                    var body = Encoding.UTF8.GetString(buffer, head.HeadLength, head.ContentLength);
                    var r = Handle(head, body);
                    reply = r.Response;
                    received = r.Source;
                }
                try
                {
                    await output.WriteAsync(reply, 0, reply.Length, cts.Token);
                    await output.FlushAsync(cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            }
            return received;
        }

        public sealed class Result
        {
            public byte[] Response;
            public Source Source;
        }

        /// <summary>Réponse à une requête (sans réseau : testée telle quelle).</summary>
        public Result Handle(HttpRequestHead head, string body)
        {
            var path = head.Path.TrimEnd('/');
            if (path.Length == 0) return Html(200, Pages.Welcome());
            if (path == "/favicon.ico" || path == "/apple-touch-icon.png" || path == "/apple-touch-icon-precomposed.png")
                return new Result { Response = MiniHttp.Response(404, "text/plain; charset=utf-8", "") };
            var parts = path.Substring(1).Split('/');
            bool codeOk = parts.Length <= 2 && string.Equals(parts[0], Code, StringComparison.OrdinalIgnoreCase);
            if (!codeOk || Closed)
            {
                lock (gate) badRequests++;
                return Html(404, Pages.WrongCode());
            }
            if (parts.Length == 2)
            {
                if (parts[1] != "etat") return Html(404, Pages.WrongCode());
                PhoneLinkState s;
                string m;
                lock (gate) { s = state; m = message; }
                var json = "{\"etat\":\"" + StateName(s) + "\",\"message\":" + JsonString(m) + "}";
                return new Result { Response = MiniHttp.Response(200, "application/json; charset=utf-8", json) };
            }
            if (head.Method == "GET")
            {
                PhoneLinkState now;
                string why;
                Dictionary<string, string> last;
                lock (gate) { now = state; why = message; last = lastValues; }
                switch (now)
                {
                    case PhoneLinkState.Connected: return Html(200, Pages.AlreadyDone());
                    case PhoneLinkState.Connecting: return Html(200, Pages.Sent(Code));
                    // Après une erreur : le formulaire déjà rempli (sauf le mot de passe), l'erreur en haut
                    case PhoneLinkState.Failed: return Html(200, Pages.Form(Code, string.IsNullOrEmpty(why) ? Tr.T("La connexion n'a pas marché.", "The connection didn't work.") : why, last));
                    default: return Html(200, Pages.Form(Code, null, null));
                }
            }
            if (head.Method != "POST") return Html(405, Pages.Form(Code, null, null));

            var form = MiniHttp.ParseForm(body);
            string Field(string k) => form.TryGetValue(k, out var v) ? (v ?? "").Trim() : "";
            Source source;
            string error = null;
            if (Field("type") == "m3u")
            {
                var url = Field("url");
                if (url.Length == 0)
                {
                    error = Tr.T("Indique l'adresse de la playlist.", "Enter the playlist URL.");
                    source = null;
                }
                else
                {
                    if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        url = "http://" + url;
                    // Playlist d'un serveur Xtream : l'app passe par l'API (plus rapide, guide TV inclus)
                    var xtream = XtreamAccount.FromPlaylistUrl(url);
                    source = xtream != null ? Source.Xtream(xtream) : Source.Playlist(url, Field("epg"));
                }
            }
            else
            {
                // Lien complet de playlist Xtream collé dans « Serveur » : identifiants repris du lien
                var account = XtreamAccount.FromPlaylistUrl(Field("server")) ?? new XtreamAccount(Field("server"), Field("user"), Field("pass"));
                source = account.IsComplete ? Source.Xtream(account) : null;
                if (source == null) error = Tr.T("Remplis le serveur, l'identifiant et le mot de passe.", "Fill in the server, username and password.");
            }
            if (error != null) return Html(200, Pages.Form(Code, error, form));
            lock (gate)
            {
                // Un seul envoi à la fois, et plus rien une fois la console connectée
                if (state == PhoneLinkState.Connecting) return Html(200, Pages.Sent(Code));
                if (state == PhoneLinkState.Connected) return Html(200, Pages.AlreadyDone());
                state = PhoneLinkState.Connecting;
                message = null;
                lastValues = new Dictionary<string, string>(form, StringComparer.Ordinal);
                lastValues.Remove("pass");
            }
            return new Result { Response = MiniHttp.Response(200, "text/html; charset=utf-8", Pages.Sent(Code)), Source = source };
        }

        static Result Html(int status, string page) =>
            new Result { Response = MiniHttp.Response(status, "text/html; charset=utf-8", page) };

        static string StateName(PhoneLinkState s)
        {
            switch (s)
            {
                case PhoneLinkState.Connecting: return "connexion";
                case PhoneLinkState.Connected: return "ok";
                case PhoneLinkState.Failed: return "erreur";
                default: return "attente";
            }
        }

        static string JsonString(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20 || c == '<' || c == '>' || c == '&') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        // ───────────────────────────── Pages du téléphone ─────────────────────────────

        /// <summary>Pages HTML du téléphone : autonomes (aucune ressource externe), dans la langue de l'app.</summary>
        static class Pages
        {
            /// <summary>Échappement HTML (texte et attributs entre guillemets) ; les accents restent tels quels.</summary>
            static string E(string s)
            {
                if (string.IsNullOrEmpty(s)) return "";
                var sb = new StringBuilder(s.Length + 16);
                foreach (var c in s)
                {
                    switch (c)
                    {
                        case '&': sb.Append("&amp;"); break;
                        case '<': sb.Append("&lt;"); break;
                        case '>': sb.Append("&gt;"); break;
                        case '"': sb.Append("&quot;"); break;
                        case '\'': sb.Append("&#39;"); break;
                        default: sb.Append(c); break;
                    }
                }
                return sb.ToString();
            }

            static string Frame(string title, string content, string script = "")
            {
                return "<!doctype html><html lang=\"" + Tr.T("fr", "en") + "\"><head><meta charset=\"utf-8\">" +
                       "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,viewport-fit=cover\">" +
                       "<meta name=\"color-scheme\" content=\"dark\"><meta name=\"robots\" content=\"noindex\">" +
                       "<title>" + E(title) + "</title><style>" + Css + "</style></head><body><main>" +
                       "<div class=\"brand\"><span class=\"logo\" aria-hidden=\"true\">▶</span><b>FluxTV</b></div>" +
                       content + "</main>" + (script.Length > 0 ? "<script>" + script + "</script>" : "") + "</body></html>";
            }

            const string Css =
                ":root{--bg:#0b0a1e;--card:#17153a;--line:#2e2a5c;--text:#f3f1ff;--dim:#b9b4e0;--accent:#7c6cff;--accent2:#b24dff;--ok:#34d399;--err:#ff6b8a}" +
                "*{box-sizing:border-box}html{-webkit-text-size-adjust:100%}" +
                "body{margin:0;min-height:100vh;background:radial-gradient(120% 80% at 10% 0%,#25206b 0%,transparent 55%),radial-gradient(120% 80% at 100% 100%,#3d1559 0%,transparent 55%),var(--bg);" +
                "color:var(--text);font:16px/1.45 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif}" +
                "main{max-width:460px;margin:0 auto;padding:28px 18px 40px}" +
                ".brand{display:flex;align-items:center;gap:10px;font-size:20px;margin-bottom:22px}" +
                ".logo{display:inline-grid;place-items:center;width:34px;height:34px;border-radius:10px;font-size:14px;background:linear-gradient(135deg,var(--accent),var(--accent2))}" +
                "h1{font-size:25px;line-height:1.2;margin:0 0 8px}p{color:var(--dim);margin:0 0 14px}" +
                ".tabs{display:grid;grid-template-columns:1fr 1fr;gap:6px;background:var(--card);border:1px solid var(--line);border-radius:14px;padding:5px;margin:18px 0}" +
                ".tabs label{text-align:center;padding:10px 6px;border-radius:10px;font-weight:600;color:var(--dim);cursor:pointer}" +
                "input[type=radio]{position:absolute;opacity:0;pointer-events:none}" +
                "#t-x:checked~.tabs label[for=t-x],#t-m:checked~.tabs label[for=t-m]{background:linear-gradient(135deg,var(--accent),var(--accent2));color:#fff}" +
                ".pane{display:none}#t-x:checked~.px,#t-m:checked~.pm{display:block}" +
                "label.f{display:block;font-size:14px;color:var(--dim);margin:14px 0 6px}" +
                "input[type=text],input[type=url],input[type=password]{width:100%;font-size:17px;padding:13px 14px;border-radius:12px;border:1px solid var(--line);background:#0f0d29;color:var(--text);outline:none}" +
                "input:focus{border-color:var(--accent)}.hint{font-size:13px;color:#8f89c2;margin-top:8px}" +
                "button,.btn{display:block;width:100%;margin-top:22px;padding:15px;border:0;border-radius:14px;font-size:17px;font-weight:700;color:#fff;text-align:center;text-decoration:none;background:linear-gradient(135deg,var(--accent),var(--accent2))}" +
                ".err{background:rgba(255,107,138,.12);border:1px solid rgba(255,107,138,.45);color:#ffd3dc;border-radius:12px;padding:12px 14px;margin:14px 0}" +
                ".card{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:18px}" +
                ".spin{width:34px;height:34px;border-radius:50%;border:4px solid var(--line);border-top-color:var(--accent);animation:r 1s linear infinite;margin:6px 0 14px}" +
                "@keyframes r{to{transform:rotate(360deg)}}.done{display:grid;place-items:center;width:52px;height:52px;border-radius:50%;background:var(--ok);color:#0b0a1e;font-size:28px;font-weight:800;margin:2px 0 14px}.small{font-size:13px;color:#8f89c2;margin-top:18px}";

            public static string Welcome() => Frame("FluxTV", "<h1>" + E(Tr.T("Scanne le QR code de ta télé", "Scan the QR code on your TV")) + "</h1><p>" +
                E(Tr.T("Sur ta console, choisis « Depuis mon téléphone » : un QR code s'affiche. Scanne-le avec l'appareil photo de ce téléphone.",
                       "On your console, choose “From my phone”: a QR code appears. Scan it with this phone's camera.")) + "</p>");

            public static string WrongCode() => Frame("FluxTV", "<h1>" + E(Tr.T("Ce lien n'est plus valable", "This link is no longer valid")) + "</h1><p>" +
                E(Tr.T("Scanne à nouveau le QR code affiché sur ta télé : il change à chaque fois.",
                       "Scan the QR code shown on your TV again: it changes every time.")) + "</p>");

            public static string AlreadyDone() => Frame("FluxTV", "<div class=\"card\"><div class=\"done\">✓</div><h1>" +
                E(Tr.T("C'est fait !", "All done!")) + "</h1><p>" + E(Tr.T("Ta télé affiche tes chaînes. Tu peux fermer cette page.",
                "Your TV is showing your channels. You can close this page.")) + "</p></div>");

            public static string Form(string code, string error, Dictionary<string, string> values)
            {
                string V(string k) => values != null && values.TryGetValue(k, out var v) ? E(v) : "";
                bool m3u = values != null && values.TryGetValue("type", out var t) && t == "m3u";
                var sb = new StringBuilder();
                sb.Append("<h1>").Append(E(Tr.T("Ajoute ta source", "Add your source"))).Append("</h1><p>")
                  .Append(E(Tr.T("Colle ici les infos de ton abonnement : elles partent directement vers ta console, sur ton réseau.",
                                 "Paste your subscription details here: they go straight to your console, on your home network.")))
                  .Append("</p>");
                if (error != null) sb.Append("<div class=\"err\">").Append(E(error)).Append("</div>");
                sb.Append("<form method=\"post\" action=\"/").Append(E(code)).Append("\" autocomplete=\"on\">")
                  .Append("<input type=\"radio\" name=\"type\" value=\"xtream\" id=\"t-x\"").Append(m3u ? "" : " checked").Append(">")
                  .Append("<input type=\"radio\" name=\"type\" value=\"m3u\" id=\"t-m\"").Append(m3u ? " checked" : "").Append(">")
                  .Append("<div class=\"tabs\"><label for=\"t-x\">").Append(E(Tr.T("Compte Xtream", "Xtream account")))
                  .Append("</label><label for=\"t-m\">").Append(E(Tr.T("Playlist M3U", "M3U playlist"))).Append("</label></div>")
                  // Compte Xtream
                  .Append("<div class=\"pane px\"><label class=\"f\" for=\"server\">").Append(E(Tr.T("Serveur", "Server"))).Append("</label>")
                  .Append("<input type=\"url\" id=\"server\" name=\"server\" inputmode=\"url\" autocapitalize=\"off\" autocorrect=\"off\" spellcheck=\"false\" placeholder=\"")
                  .Append(E(Tr.T("http://mon-serveur.com:8080", "http://my-server.com:8080"))).Append("\" value=\"").Append(V("server")).Append("\">")
                  .Append("<label class=\"f\" for=\"user\">").Append(E(Tr.T("Identifiant", "Username"))).Append("</label>")
                  .Append("<input type=\"text\" id=\"user\" name=\"user\" autocomplete=\"username\" autocapitalize=\"off\" autocorrect=\"off\" spellcheck=\"false\" value=\"")
                  .Append(V("user")).Append("\">")
                  .Append("<label class=\"f\" for=\"pass\">").Append(E(Tr.T("Mot de passe", "Password"))).Append("</label>")
                  .Append("<input type=\"password\" id=\"pass\" name=\"pass\" autocomplete=\"current-password\" autocapitalize=\"off\" autocorrect=\"off\" spellcheck=\"false\">")
                  .Append("<div class=\"hint\">").Append(E(Tr.T("Tu peux aussi coller le lien complet de ta playlist Xtream dans « Serveur ».",
                                                                 "You can also paste your full Xtream playlist link into “Server”."))).Append("</div></div>")
                  // Playlist M3U
                  .Append("<div class=\"pane pm\"><label class=\"f\" for=\"url\">").Append(E(Tr.T("Adresse de la playlist", "Playlist URL"))).Append("</label>")
                  .Append("<input type=\"url\" id=\"url\" name=\"url\" inputmode=\"url\" autocapitalize=\"off\" autocorrect=\"off\" spellcheck=\"false\" placeholder=\"http://…/playlist.m3u\" value=\"")
                  .Append(V("url")).Append("\">")
                  .Append("<label class=\"f\" for=\"epg\">").Append(E(Tr.T("Guide TV (facultatif)", "TV guide (optional)"))).Append("</label>")
                  .Append("<input type=\"url\" id=\"epg\" name=\"epg\" inputmode=\"url\" autocapitalize=\"off\" autocorrect=\"off\" spellcheck=\"false\" placeholder=\"http://…/guide.xml\" value=\"")
                  .Append(V("epg")).Append("\"></div>")
                  .Append("<button type=\"submit\">").Append(E(Tr.T("Envoyer à la télé", "Send to the TV"))).Append("</button></form>")
                  .Append("<p class=\"small\">").Append(E(Tr.T("FluxTV est un lecteur : il ne fournit aucune chaîne. Tes identifiants restent chiffrés sur ta console.",
                                                               "FluxTV is a player: it doesn't provide any channels. Your credentials stay encrypted on your console.")))
                  .Append("</p>");
                return Frame(Tr.T("FluxTV : ajoute ta source", "FluxTV: add your source"), sb.ToString());
            }

            public static string Sent(string code)
            {
                var content = "<div class=\"card\" id=\"box\"><div class=\"spin\" id=\"spin\"></div><h1 id=\"t\">" +
                              E(Tr.T("Envoyé à la télé", "Sent to the TV")) + "</h1><p id=\"m\">" +
                              E(Tr.T("Connexion à ton abonnement…", "Connecting to your subscription…")) + "</p>" +
                              "<a class=\"btn\" id=\"fix\" href=\"/" + E(code) + "\" style=\"display:none\">" + E(Tr.T("Corriger", "Fix it")) + "</a></div>" +
                              "<noscript><p>" + E(Tr.T("Regarde ta télé pour la suite.", "Check your TV for what happens next.")) + "</p></noscript>";
                var okTitle = JsonString(Tr.T("C'est fait !", "All done!"));
                var okText = JsonString(Tr.T("Ta télé affiche maintenant tes chaînes. Tu peux fermer cette page.", "Your TV now shows your channels. You can close this page."));
                var errTitle = JsonString(Tr.T("La connexion n'a pas marché", "The connection didn't work"));
                var lost = JsonString(Tr.T("La télé ne répond plus : regarde-la, la connexion a peut-être réussi.", "The TV stopped answering: check it, the connection may have worked."));
                var script =
                    "var u='/" + code + "/etat',n=0;" +
                    "function q(i){return document.getElementById(i)}" +
                    "function f(){fetch(u,{cache:'no-store'}).then(function(r){return r.json()}).then(function(s){" +
                    "if(s.etat==='ok'){q('spin').style.display='none';q('t').textContent=" + okTitle + ";q('m').textContent=" + okText + ";q('t').insertAdjacentHTML('beforebegin','<div class=done>✓</div>');return}" +
                    "if(s.etat==='erreur'){q('spin').style.display='none';q('t').textContent=" + errTitle + ";q('m').textContent=s.message||'';q('fix').style.display='block';return}" +
                    "setTimeout(f,1500)}).catch(function(){if(++n>3){q('spin').style.display='none';q('m').textContent=" + lost + "}else setTimeout(f,2000)})}" +
                    "setTimeout(f,1200);";
                return Frame(Tr.T("FluxTV : envoyé", "FluxTV: sent"), content, script);
            }
        }
    }
}
