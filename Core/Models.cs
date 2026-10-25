using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FluxTV.Core
{
    public enum ContentKind { Live, Movie, Episode, Series, Other }

    /// <summary>
    /// Référence STABLE d'un contenu : c'est ce qui est enregistré dans les favoris, l'historique et
    /// les reprises. Jamais d'identifiants de compte dedans : l'adresse de lecture est reconstruite
    /// avec le compte courant.
    /// Identifiants : "live:123", "vod:456", "ep:789" (Xtream) ou "u:&lt;empreinte&gt;" (playlist M3U).
    /// </summary>
    public sealed class ContentRef
    {
        public string Id;
        public ContentKind Kind;
        public string Name;
        public string Image;
        public string Group;
        /// <summary>Extension du fichier (films et épisodes Xtream) : mp4, mkv…</summary>
        public string Ext;
        public string SeriesId;
        public string SeriesName;
        public int? Season;
        public int? Episode;
        /// <summary>Playlists M3U uniquement : adresse complète du flux.</summary>
        public string Url;
        public string UserAgent;
        public string TvgId;
        public int? Num;

        public string NumericId => Ids.NumericPart(Id);

        public ContentRef Copy() => (ContentRef)MemberwiseClone();
    }

    /// <summary>Catégorie (TV, films ou séries), dans l'ordre du fournisseur.</summary>
    public sealed class Category
    {
        public string Id { get; }
        /// <summary>Nom nettoyé, prêt à afficher (« FR | SPORTS » → « Sports »).</summary>
        public string Name { get; }
        public string RawName { get; }
        public bool IsAdult { get; }

        public Category(string id, string rawName)
        {
            Id = id;
            RawName = rawName ?? "";
            Name = TextTools.CleanCategory(RawName);
            IsAdult = TextTools.IsAdult(RawName);
        }
    }

    /// <summary>Chaîne en direct.</summary>
    public sealed class LiveChannel
    {
        public string Id { get; }
        /// <summary>stream_id Xtream (null en mode playlist M3U).</summary>
        public string StreamId { get; }
        /// <summary>Nom nettoyé (« FR| TF1 FHD » → « TF1 »), badges à part.</summary>
        public string Name { get; }
        public string RawName { get; }
        public List<string> Badges { get; }
        public string Logo { get; }
        public string CategoryId { get; }
        public string TvgId { get; }
        public int Num { get; }
        /// <summary>Jours de replay disponibles (0 = pas de replay).</summary>
        public int ArchiveDays { get; }
        /// <summary>Playlist M3U : adresse complète.</summary>
        public string Url { get; }
        public string UserAgent { get; }

        public LiveChannel(string id, string streamId, string rawName, string logo, string categoryId, string tvgId, int num,
                           int archiveDays = 0, string url = null, string userAgent = null)
        {
            Id = id;
            StreamId = streamId;
            RawName = rawName ?? "";
            var cleaned = TextTools.CleanChannel(RawName);
            Name = cleaned.Name;
            Badges = cleaned.Badges;
            Logo = logo;
            CategoryId = categoryId ?? "";
            TvgId = tvgId;
            Num = num;
            ArchiveDays = archiveDays;
            Url = url;
            UserAgent = userAgent;
        }

        public ContentRef Ref(string group) => new ContentRef
        {
            Id = Id, Kind = ContentKind.Live, Name = Name, Image = Logo, Group = group, Url = Url,
            UserAgent = UserAgent, TvgId = TvgId, Num = Num,
        };
    }

    /// <summary>Film.</summary>
    public sealed class Movie
    {
        public string Id { get; }
        public string StreamId { get; }
        public string Name { get; }
        public string RawName { get; }
        public int? Year { get; }
        public List<string> Badges { get; }
        public string Poster { get; }
        public double? Rating { get; }
        public DateTimeOffset? Added { get; }
        public string Ext { get; }
        public string CategoryId { get; }
        public string Url { get; }

        public Movie(string id, string streamId, string rawName, string poster, double? rating, DateTimeOffset? added,
                     string ext, string categoryId, int? year = null, string url = null)
        {
            Id = id;
            StreamId = streamId;
            RawName = rawName ?? "";
            var cleaned = TextTools.CleanTitle(RawName);
            Name = cleaned.Name;
            Year = year ?? cleaned.Year;
            Badges = cleaned.Badges;
            Poster = poster;
            Rating = rating;
            Added = added;
            Ext = string.IsNullOrEmpty(ext) ? "mp4" : ext;
            CategoryId = categoryId ?? "";
            Url = url;
        }

        /// <summary>Film reconstruit depuis un favori ou l'historique.</summary>
        public static Movie FromRef(ContentRef r) =>
            new Movie(r.Id, r.NumericId, r.Name, r.Image, null, null, r.Ext ?? "mp4", r.Group ?? "", url: r.Url);

        public ContentRef Ref(string group) => new ContentRef
        {
            Id = Id, Kind = ContentKind.Movie, Name = Name, Image = Poster, Group = group, Ext = Ext, Url = Url,
        };
    }

    /// <summary>Série (fiche résumée, telle qu'elle apparaît dans les listes).</summary>
    public sealed class Series
    {
        public string Id { get; }
        public string SeriesId { get; }
        public string Name { get; }
        public string RawName { get; }
        public int? Year { get; }
        public string Cover { get; }
        public string Plot { get; }
        public string Genre { get; }
        public double? Rating { get; }
        public string CategoryId { get; }
        public DateTimeOffset? LastModified { get; }
        /// <summary>Playlist M3U : épisodes déjà connus (regroupés d'après leur nom « S01 E02 »).</summary>
        public List<Episode> Episodes { get; }

        /// <param name="id">Identifiant déjà connu (série reconstruite depuis un favori), sinon déduit de la source.</param>
        public Series(string seriesId, string rawName, string cover, string plot, string genre, double? rating, string categoryId,
                      DateTimeOffset? lastModified, int? year = null, List<Episode> episodes = null, string id = null)
        {
            Id = id ?? ((episodes == null ? "ser:" : "m3us:") + seriesId);
            SeriesId = seriesId;
            RawName = rawName ?? "";
            var cleaned = TextTools.CleanTitle(RawName);
            Name = cleaned.Name;
            Year = year ?? cleaned.Year;
            Cover = cover;
            Plot = plot;
            Genre = genre;
            Rating = rating;
            CategoryId = categoryId ?? "";
            LastModified = lastModified;
            Episodes = episodes;
        }

        public ContentRef Ref(string group) => new ContentRef
        {
            Id = Id, Kind = ContentKind.Series, Name = Name, Image = Cover, Group = group, SeriesId = SeriesId, SeriesName = Name,
        };

        /// <summary>
        /// Série reconstruite depuis un favori (la fiche complète est rechargée à l'ouverture). Elle garde
        /// l'identifiant du favori : « m3us:… » pour une playlist, « ser:… » pour un compte Xtream.
        /// </summary>
        public static Series FromRef(ContentRef r) =>
            new Series(r.SeriesId ?? Ids.NumericPart(r.Id) ?? r.Id, r.Name, r.Image, null, null, null, r.Group ?? "", null, id: r.Id);
    }

    /// <summary>Épisode d'une série.</summary>
    public sealed class Episode
    {
        public string Id { get; }
        public string EpisodeId { get; }
        public string Title { get; }
        public int Season { get; }
        public int Number { get; }
        public string Ext { get; }
        public string Plot { get; }
        public string Image { get; }
        public int? DurationSecs { get; }
        public string Url { get; }

        public Episode(string id, string episodeId, string title, int season, int number, string ext, string plot, string image,
                       int? durationSecs, string url)
        {
            Id = id;
            EpisodeId = episodeId;
            Title = title;
            Season = season;
            Number = number;
            Ext = string.IsNullOrEmpty(ext) ? "mkv" : ext;
            Plot = plot;
            Image = image;
            DurationSecs = durationSecs;
            Url = url;
        }

        public string Label => string.Format(CultureInfo.InvariantCulture, "S{0:00}E{1:00}", Season, Number);

        public ContentRef Ref(string seriesId, string seriesName, string fallbackImage) => new ContentRef
        {
            Id = Id, Kind = ContentKind.Episode, Name = Label + " · " + Title, Image = Image ?? fallbackImage, Group = seriesName,
            Ext = Ext, SeriesId = seriesId, SeriesName = seriesName, Season = Season, Episode = Number, Url = Url,
        };
    }

    /// <summary>Fiche complète d'une série : saisons et épisodes.</summary>
    public sealed class SeriesDetails
    {
        public SortedDictionary<int, List<Episode>> Seasons = new SortedDictionary<int, List<Episode>>();
        public string Plot;
        public string Cover;
        public string Backdrop;
        public string Genre;
        public double? Rating;
        public int? Year;
        public string Cast;
        public string Director;

        public List<int> SeasonNumbers => Seasons.Keys.ToList();

        /// <summary>Tous les épisodes dans l'ordre (saison puis numéro).</summary>
        public List<Episode> AllEpisodes => Seasons.Values.SelectMany(e => e).ToList();
    }

    /// <summary>Fiche complète d'un film.</summary>
    public sealed class MovieInfo
    {
        public string Plot;
        public string Cast;
        public string Director;
        public string Genre;
        public string ReleaseDate;
        public int? DurationSecs;
        public double? Rating;
        public string Cover;
        public string Backdrop;
        /// <summary>Identifiant YouTube de la bande-annonce.</summary>
        public string Trailer;

        public int? Year
        {
            get
            {
                if (ReleaseDate == null || ReleaseDate.Length < 4) return null;
                if (!int.TryParse(ReleaseDate.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y)) return null;
                return y >= 1900 && y <= 2100 ? y : (int?)null;
            }
        }
    }

    /// <summary>Programme du guide TV.</summary>
    public sealed class Programme
    {
        public DateTimeOffset Start { get; }
        public DateTimeOffset Stop { get; }
        public string Title { get; }
        public string Desc { get; }
        public bool HasArchive { get; }

        public Programme(DateTimeOffset start, DateTimeOffset stop, string title, string desc = null, bool hasArchive = false)
        {
            Start = start;
            Stop = stop;
            Title = title;
            Desc = desc;
            HasArchive = hasArchive;
        }

        public bool IsNow(DateTimeOffset now) => Start <= now && now < Stop;

        /// <summary>Avancement 0…1 du programme en cours.</summary>
        public double Progress(DateTimeOffset now)
        {
            var total = (Stop - Start).TotalSeconds;
            if (total <= 0) return 0;
            return Math.Min(1, Math.Max(0, (now - Start).TotalSeconds / total));
        }

        /// <summary>Programme en cours et programme suivant d'une liste triée.</summary>
        public static (Programme Now, Programme Next) NowNext(IList<Programme> list, DateTimeOffset now)
        {
            if (list == null) return (null, null);
            for (int i = 0; i < list.Count; i++)
            {
                if (now >= list[i].Stop) continue;
                if (list[i].Start <= now) return (list[i], i + 1 < list.Count ? list[i + 1] : null);
                return (null, list[i]);
            }
            return (null, null);
        }
    }

    // ───────────────────────────── Identifiants ─────────────────────────────

    public static class Ids
    {
        public static string Live(string streamId) => "live:" + streamId;
        public static string Vod(string streamId) => "vod:" + streamId;
        public static string Episode(string episodeId) => "ep:" + episodeId;

        /// <summary>Partie numérique d'un identifiant typé ("vod:456" → "456"), sinon null.</summary>
        public static string NumericPart(string id)
        {
            if (id == null) return null;
            int colon = id.IndexOf(':');
            if (colon < 0) return null;
            var tail = id.Substring(colon + 1);
            return tail.Length > 0 && tail.All(IsAsciiDigit) ? tail : null;
        }

        public static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        /// <summary>
        /// Identifiant déduit d'une adresse (playlists M3U) : les adresses de type Xtream
        /// (…/live/user/pass/123.ts) donnent "live:123" — les mêmes favoris qu'avec le compte Xtream.
        /// </summary>
        public static string FromUrl(string url)
        {
            url ??= "";
            int q = url.IndexOf('?');
            var path = q >= 0 ? url.Substring(0, q) : url;
            var parts = path.Split('/');
            // …/(live|movie|series)/user/pass/123(.ext)
            if (parts.Length >= 4)
            {
                var kind = parts[parts.Length - 4];
                var last = parts[parts.Length - 1];
                int dot = last.IndexOf('.');
                var num = dot >= 0 ? last.Substring(0, dot) : last;
                if (num.Length > 0 && num.All(IsAsciiDigit))
                {
                    switch (kind)
                    {
                        case "live": return Live(num);
                        case "movie": return Vod(num);
                        case "series": return Episode(num);
                        default:
                            // Forme courte des playlists Xtream : http://hote:port/user/pass/123
                            if (parts.Length == 6 && parts[0].StartsWith("http", StringComparison.Ordinal) && parts[1].Length == 0
                                && (last.EndsWith(".ts", StringComparison.Ordinal) || last.EndsWith(".m3u8", StringComparison.Ordinal) || dot < 0))
                                return Live(num);
                            break;
                    }
                }
            }
            return "u:" + Hash.Fnv1a64Hex(url);
        }
    }

    public static class Hash
    {
        /// <summary>Empreinte courte et stable (FNV-1a 64 bits) — identifiants des entrées de playlist.</summary>
        public static string Fnv1a64Hex(string s)
        {
            ulong h = 0xcbf29ce484222325;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(s ?? ""))
            {
                h ^= b;
                unchecked { h *= 0x100000001b3; }
            }
            return h.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
