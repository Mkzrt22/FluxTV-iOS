using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FluxTV.Core
{
    /// <summary>Position de lecture d'un film ou d'un épisode.</summary>
    public sealed class PlaybackPosition
    {
        public double Position;
        public double Duration;
        public DateTimeOffset Updated;

        public double Fraction => Duration > 0 ? Math.Min(1, Math.Max(0, Position / Duration)) : 0;
    }

    /// <summary>
    /// Données personnelles : favoris, historique, reprises, contenus vus. Fonctions pures (testées sans
    /// appareil) ; l'enregistrement sur disque est fait par LibraryFile.
    /// </summary>
    public sealed class LibraryData
    {
        public const int HistoryMax = 60;
        public const int WatchedMax = 5000;
        /// <summary>En dessous, on ne propose pas de reprise (début du film).</summary>
        public const double ResumeMinSeconds = 15;
        /// <summary>Au-delà de 95 %, le contenu est considéré comme vu (générique de fin).</summary>
        public const double FinishedRatio = 0.95;

        public List<ContentRef> Favorites = new List<ContentRef>();
        /// <summary>Plus récent en premier.</summary>
        public List<ContentRef> History = new List<ContentRef>();
        public Dictionary<string, PlaybackPosition> Positions = new Dictionary<string, PlaybackPosition>();
        public List<string> Watched = new List<string>();
        public string LastLiveId;

        // ───────────────────────────── Favoris ─────────────────────────────

        public bool IsFavorite(string id) => Favorites.Any(f => f.Id == id);

        /// <summary>Ajoute ou retire ; renvoie true si le contenu est maintenant en favori.</summary>
        public bool ToggleFavorite(ContentRef r)
        {
            int i = Favorites.FindIndex(f => f.Id == r.Id);
            if (i >= 0)
            {
                Favorites.RemoveAt(i);
                return false;
            }
            Favorites.Add(r);
            return true;
        }

        public List<ContentRef> FavoritesOf(ContentKind kind) => Favorites.Where(f => f.Kind == kind).ToList();

        // ───────────────────────────── Historique ─────────────────────────────

        public void AddHistory(ContentRef r)
        {
            History.RemoveAll(h => h.Id == r.Id);
            // Séries : un seul épisode par série dans l'historique (le plus récent)
            if (r.Kind == ContentKind.Episode && r.SeriesId != null)
                History.RemoveAll(h => h.Kind == ContentKind.Episode && h.SeriesId == r.SeriesId);
            History.Insert(0, r);
            if (History.Count > HistoryMax) History.RemoveRange(HistoryMax, History.Count - HistoryMax);
            if (r.Kind == ContentKind.Live) LastLiveId = r.Id;
        }

        public void RemoveHistory(string id)
        {
            History.RemoveAll(h => h.Id == id);
            Positions.Remove(id);
        }

        public List<ContentRef> RecentLive(int limit = 12) => History.Where(h => h.Kind == ContentKind.Live).Take(limit).ToList();

        // ───────────────────────────── Reprises ─────────────────────────────

        /// <summary>
        /// Enregistre la position. Au-delà de 95 %, le contenu passe en « vu » et la reprise est effacée.
        /// Renvoie true si le contenu vient d'être terminé.
        /// </summary>
        public bool SavePosition(ContentRef r, double position, double duration, DateTimeOffset now)
        {
            if (r.Kind == ContentKind.Live || double.IsNaN(position) || double.IsInfinity(position)
                || double.IsNaN(duration) || double.IsInfinity(duration))
                return false;
            if (duration > 0 && position / duration >= FinishedRatio)
            {
                Positions.Remove(r.Id);
                MarkWatched(r.Id);
                return true;
            }
            if (position >= ResumeMinSeconds)
                Positions[r.Id] = new PlaybackPosition { Position = position, Duration = Math.Max(0, duration), Updated = now };
            return false;
        }

        /// <summary>Position de reprise (null si rien à reprendre).</summary>
        public PlaybackPosition Resume(string id) =>
            id != null && Positions.TryGetValue(id, out var p) && p.Position >= ResumeMinSeconds ? p : null;

        /// <summary>« Continuer à regarder » : films et épisodes commencés, du plus récent au plus ancien.</summary>
        public List<ContentRef> ContinueWatching(int limit = 20) =>
            History.Where(h => h.Kind != ContentKind.Live && Resume(h.Id) != null).Take(limit).ToList();

        // ───────────────────────────── Vus ─────────────────────────────

        public bool IsWatched(string id) => Watched.Contains(id);

        public void MarkWatched(string id)
        {
            if (Watched.Contains(id)) return;
            Watched.Add(id);
            Positions.Remove(id);
            if (Watched.Count > WatchedMax) Watched.RemoveRange(0, Watched.Count - WatchedMax);
        }

        public void MarkUnwatched(string id) => Watched.RemoveAll(w => w == id);

        /// <summary>
        /// Prochain épisode à regarder : l'épisode commencé le plus récemment, sinon le premier épisode
        /// non vu après le dernier épisode vu.
        /// </summary>
        public (Episode Episode, PlaybackPosition Resume)? NextEpisode(IList<Episode> episodes)
        {
            if (episodes == null || episodes.Count == 0) return null;
            Episode startedEp = null;
            PlaybackPosition started = null;
            foreach (var e in episodes)
            {
                var p = Resume(e.Id);
                if (p != null && (started == null || p.Updated > started.Updated)) { startedEp = e; started = p; }
            }
            if (startedEp != null) return (startedEp, started);
            int lastWatched = -1;
            for (int i = 0; i < episodes.Count; i++)
                if (IsWatched(episodes[i].Id)) lastWatched = i;
            if (lastWatched >= 0)
                return lastWatched + 1 < episodes.Count ? (episodes[lastWatched + 1], (PlaybackPosition)null) : ((Episode, PlaybackPosition)?)null;
            return (episodes[0], null);
        }

        // ───────────────────────────── JSON ─────────────────────────────

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"favorites\":[").Append(string.Join(",", Favorites.Select(RefJson))).Append("],");
            sb.Append("\"history\":[").Append(string.Join(",", History.Select(RefJson))).Append("],");
            sb.Append("\"positions\":{").Append(string.Join(",", Positions.Select(kv =>
                Json.Quote(kv.Key) + ":{\"position\":" + Json.Number(kv.Value.Position) + ",\"duration\":" + Json.Number(kv.Value.Duration)
                + ",\"updated\":" + kv.Value.Updated.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "}"))).Append("},");
            sb.Append("\"watched\":[").Append(string.Join(",", Watched.Select(Json.Quote))).Append("],");
            sb.Append("\"lastLiveId\":").Append(Json.Quote(LastLiveId)).Append('}');
            return sb.ToString();
        }

        public static LibraryData FromJson(string json)
        {
            var lib = new LibraryData();
            var o = Json.ParseObject(json);
            if (o == null) return lib;
            lib.Favorites = (o.Arr("favorites") ?? new List<object>()).OfType<Dictionary<string, object>>().Select(RefFrom).Where(r => r != null).ToList();
            lib.History = (o.Arr("history") ?? new List<object>()).OfType<Dictionary<string, object>>().Select(RefFrom).Where(r => r != null).ToList();
            var pos = o.Obj("positions");
            if (pos != null)
                foreach (var kv in pos)
                    if (kv.Value is Dictionary<string, object> p)
                        lib.Positions[kv.Key] = new PlaybackPosition
                        {
                            Position = p.Dbl("position") ?? 0,
                            Duration = p.Dbl("duration") ?? 0,
                            Updated = p.UnixDate("updated") ?? DateTimeOffset.FromUnixTimeSeconds(0),
                        };
            lib.Watched = (o.Arr("watched") ?? new List<object>()).OfType<string>().ToList();
            lib.LastLiveId = o.Str("lastLiveId");
            return lib;
        }

        static readonly string[] KindNames = { "live", "movie", "episode", "series", "other" };

        static string RefJson(ContentRef r)
        {
            var parts = new List<string>
            {
                "\"id\":" + Json.Quote(r.Id),
                "\"kind\":" + Json.Quote(KindNames[(int)r.Kind]),
                "\"name\":" + Json.Quote(r.Name),
            };
            void Add(string key, string value) { if (value != null) parts.Add(Json.Quote(key) + ":" + Json.Quote(value)); }
            void AddInt(string key, int? value) { if (value != null) parts.Add(Json.Quote(key) + ":" + value.Value.ToString(CultureInfo.InvariantCulture)); }
            Add("image", r.Image);
            Add("group", r.Group);
            Add("ext", r.Ext);
            Add("seriesId", r.SeriesId);
            Add("seriesName", r.SeriesName);
            AddInt("season", r.Season);
            AddInt("episode", r.Episode);
            Add("url", r.Url);
            Add("userAgent", r.UserAgent);
            Add("tvgId", r.TvgId);
            AddInt("num", r.Num);
            return "{" + string.Join(",", parts) + "}";
        }

        static ContentRef RefFrom(Dictionary<string, object> o)
        {
            var id = o.Str("id");
            if (id == null) return null;
            int kind = Array.IndexOf(KindNames, o.Str("kind") ?? "other");
            return new ContentRef
            {
                Id = id,
                Kind = kind < 0 ? ContentKind.Other : (ContentKind)kind,
                Name = o.Str("name") ?? "",
                Image = o.Str("image"),
                Group = o.Str("group"),
                Ext = o.Str("ext"),
                SeriesId = o.Str("seriesId"),
                SeriesName = o.Str("seriesName"),
                Season = o.Int("season"),
                Episode = o.Int("episode"),
                Url = o.Str("url"),
                UserAgent = o.Str("userAgent"),
                TvgId = o.Str("tvgId"),
                Num = o.Int("num"),
            };
        }
    }

    /// <summary>Enregistrement de LibraryData dans un fichier JSON (écriture atomique).</summary>
    public sealed class LibraryFile
    {
        readonly string path;

        public LibraryFile(string path)
        {
            this.path = path;
        }

        public LibraryData Load()
        {
            try { return File.Exists(path) ? LibraryData.FromJson(File.ReadAllText(path, Encoding.UTF8)) : new LibraryData(); }
            catch (IOException) { return new LibraryData(); }
            catch (UnauthorizedAccessException) { return new LibraryData(); }
        }

        public void Save(LibraryData lib)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, lib.ToJson(), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
