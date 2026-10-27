using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluxTV.Core;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.UI.Xaml;

namespace FluxTV.Services
{
    /// <summary>Réglages de l'app (paramètres locaux de la console).</summary>
    public sealed class SettingsStore
    {
        readonly IPropertySet values = ApplicationData.Current.LocalSettings.Values;

        T Get<T>(string key, T fallback)
        {
            try { return values.TryGetValue(key, out var v) && v is T t ? t : fallback; }
            catch (Exception) { return fallback; }
        }

        void Put(string key, object value)
        {
            try { values[key] = value; }
            catch (Exception) { }
        }

        public bool HideAdult { get => Get("hideAdult", true); set => Put("hideAdult", value); }
        public bool AutoplayNext { get => Get("autoplayNext", true); set => Put("autoplayNext", value); }
        /// <summary>Fréquence de l'écran calée sur celle de la vidéo (24, 25/50, 30/60 images par seconde).</summary>
        public bool MatchFrameRate { get => Get("matchFrameRate", true); set => Put("matchFrameRate", value); }
        /// <summary>Format du direct : "auto", "m3u8" (HLS) ou "ts".</summary>
        public string LiveFormat { get => Get("liveFormat", "auto"); set => Put("liveFormat", value); }
    }

    /// <summary>Favoris, historique, reprises : en mémoire, enregistrés peu après chaque changement.</summary>
    public sealed class LibraryStore
    {
        readonly LibraryFile file;
        readonly object gate = new object();
        DispatcherTimer saveTimer;

        public LibraryData Data { get; }

        /// <summary>Favoris ou historique modifiés (les écrans se mettent à jour).</summary>
        public event Action Changed;

        public LibraryStore()
        {
            file = new LibraryFile(Path.Combine(ApplicationData.Current.LocalFolder.Path, "library.json"));
            Data = file.Load();
        }

        public bool IsFavorite(string id) => Data.IsFavorite(id);
        public bool IsWatched(string id) => Data.IsWatched(id);
        public PlaybackPosition Resume(string id) => Data.Resume(id);

        public bool ToggleFavorite(ContentRef r)
        {
            var added = Data.ToggleFavorite(r);
            Commit(true);
            return added;
        }

        public void AddHistory(ContentRef r)
        {
            Data.AddHistory(r);
            Commit(true);
        }

        public void RemoveHistory(string id)
        {
            Data.RemoveHistory(id);
            Commit(true);
        }

        public void SetWatched(string id, bool watched)
        {
            if (watched) Data.MarkWatched(id);
            else Data.MarkUnwatched(id);
            Commit(true);
        }

        /// <summary>Position de lecture (silencieux : les écrans sont rafraîchis à la fermeture du lecteur).</summary>
        public bool SavePosition(ContentRef r, double position, double duration)
        {
            var finished = Data.SavePosition(r, position, duration, DateTimeOffset.UtcNow);
            Commit(false);
            return finished;
        }

        public void NotifyChanged() => Changed?.Invoke();

        /// <summary>Interface refaite (changement de langue) : les anciens écrans ne sont plus prévenus.</summary>
        public void ClearSubscribers() => Changed = null;

        void Commit(bool notify)
        {
            if (notify) Changed?.Invoke();
            if (saveTimer == null)
            {
                saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                saveTimer.Tick += (s, e) => Flush();
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        /// <summary>Enregistre en arrière-plan.</summary>
        public void Flush()
        {
            saveTimer?.Stop();
            var json = Data.ToJson();
            _ = Task.Run(() => Write(json));
        }

        /// <summary>Enregistre tout de suite (mise en veille de l'app).</summary>
        public void FlushNow()
        {
            saveTimer?.Stop();
            Write(Data.ToJson());
        }

        void Write(string json)
        {
            lock (gate)
            {
                try { file.SaveText(json); }
                catch (Exception e) { Journal.Write("Bibliothèque non enregistrée : " + e.Message); }
            }
        }
    }

    /// <summary>
    /// Source choisie (compte Xtream, playlist) : gardée dans le coffre-fort de Windows (chiffré, propre à
    /// l'app), sinon dans un fichier privé de l'app. Jamais de mot de passe dans le journal.
    /// </summary>
    public static class SourceStore
    {
        const string Resource = "FluxTV";
        const string UserName = "source";

        static string FallbackPath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "source.json");

        public static Source Load()
        {
            try
            {
                var vault = new PasswordVault();
                var c = vault.Retrieve(Resource, UserName);
                c.RetrievePassword();
                var s = Source.FromJson(c.Password);
                if (s != null) return s;
            }
            catch (Exception) { }
            try { return File.Exists(FallbackPath) ? Source.FromJson(File.ReadAllText(FallbackPath, Encoding.UTF8)) : null; }
            catch (Exception) { return null; }
        }

        public static void Save(Source s)
        {
            Delete();
            try
            {
                new PasswordVault().Add(new PasswordCredential(Resource, UserName, s.ToJson()));
                return;
            }
            catch (Exception e)
            {
                Journal.Write("Coffre-fort indisponible (" + e.HResult.ToString("X8") + ") : source gardée dans un fichier privé.");
            }
            try { File.WriteAllText(FallbackPath, s.ToJson(), new UTF8Encoding(false)); }
            catch (Exception) { }
        }

        public static void Delete()
        {
            try
            {
                var vault = new PasswordVault();
                foreach (var c in vault.FindAllByResource(Resource)) vault.Remove(c);
            }
            catch (Exception) { }
            try { if (File.Exists(FallbackPath)) File.Delete(FallbackPath); }
            catch (Exception) { }
        }

        /// <summary>
        /// Compte intégré à une copie personnelle de l'app : fichier « compte.flux » du paquet (JSON en
        /// base64 : serveur, utilisateur, motdepasse). Absent de l'app ordinaire.
        /// </summary>
        public static Source LoadEmbedded()
        {
            try
            {
                var path = Path.Combine(Package.Current.InstalledLocation.Path, "compte.flux");
                if (!File.Exists(path)) return null;
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(path).Trim()));
                var o = Json.ParseObject(json);
                if (o == null) return null;
                var a = new XtreamAccount(o.Str("serveur"), o.Str("utilisateur"), o.Str("motdepasse"));
                return a.IsComplete ? Source.Xtream(a) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>Journal d'erreurs de l'app (dossier privé de l'app), sans adresse ni identifiant.</summary>
    public static class Journal
    {
        static readonly object gate = new object();

        public static string FilePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "journal.txt");

        public static void Write(string line)
        {
            try
            {
                lock (gate)
                {
                    var p = FilePath;
                    if (File.Exists(p) && new FileInfo(p).Length > 200_000) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception) { }
        }
    }
}
