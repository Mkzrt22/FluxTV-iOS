using System;
using System.Collections.Generic;
using System.Linq;

namespace FluxTV.Core
{
    /// <summary>
    /// Contenu de démonstration : mire de test publique (Unified Streaming) et films libres de droits
    /// (Blender Foundation, licence Creative Commons BY 3.0). Il permet d'essayer FluxTV sans abonnement.
    /// Les visuels « ms-appx:/// » sont fournis avec l'app.
    /// </summary>
    public static class Demo
    {
        const string Img = "ms-appx:///Assets/Demo/";

        public static readonly Category LiveCategory = new Category("demo-live", "Démo · Direct");
        public static readonly Category VodCategory = new Category("demo-vod", "Démo · Films libres");
        public static readonly Category SeriesCategory = new Category("demo-series", "Démo · Séries");

        public static readonly List<LiveChannel> Channels = new List<LiveChannel>
        {
            new LiveChannel("demo:live1", null, "Mire en direct", Img + "DemoLive1.png", LiveCategory.Id, "demo.live1", 1,
                url: "https://demo.unified-streaming.com/k8s/live/stable/live.isml/.m3u8"),
            new LiveChannel("demo:live2", null, "Ciné libre", Img + "DemoLive2.png", LiveCategory.Id, "demo.live2", 2,
                url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
            new LiveChannel("demo:live3", null, "Animation libre", Img + "DemoLive3.png", LiveCategory.Id, "demo.live3", 3,
                url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
        };

        public static readonly List<Movie> Movies = new List<Movie>
        {
            new Movie("demo:tos", null, "Tears of Steel", Img + "DemoPosterTos.png", 6.9, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
                "mov", VodCategory.Id, 2012, "https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov"),
            new Movie("demo:bbb", null, "Big Buck Bunny", Img + "DemoPosterBbb.png", 7.2, DateTimeOffset.FromUnixTimeSeconds(1_690_000_000),
                "m3u8", VodCategory.Id, 2008, "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
        };

        public static readonly Dictionary<string, MovieInfo> MovieInfos = new Dictionary<string, MovieInfo>
        {
            ["demo:tos"] = new MovieInfo
            {
                Plot = "Dans un Amsterdam futuriste, un groupe de scientifiques tente de sauver le monde d'une invasion de robots en rejouant une rupture amoureuse. Court métrage libre de la Blender Foundation.",
                Cast = "Derek de Lint, Sergio Hasselbaink, Rogier Schippers", Director = "Ian Hubert", Genre = "Science-fiction",
                ReleaseDate = "2012-09-26", DurationSecs = 734, Rating = 6.9, Cover = Img + "DemoPosterTos.png",
                Backdrop = Img + "DemoBackdropTos.png",
            },
            ["demo:bbb"] = new MovieInfo
            {
                Plot = "Un lapin géant au grand cœur décide de donner une leçon à trois rongeurs qui s'en prennent aux animaux de la forêt. Court métrage d'animation libre de la Blender Foundation.",
                Director = "Sacha Goedegebure", Genre = "Animation, Comédie", ReleaseDate = "2008-05-30", DurationSecs = 596, Rating = 7.2,
                Cover = Img + "DemoPosterBbb.png", Backdrop = Img + "DemoBackdropBbb.png",
            },
        };

        public static readonly List<Series> SeriesList = new List<Series>
        {
            new Series("demo-open-movies", "Open Movies", Img + "DemoPosterOpen.png",
                "Les films d'animation et de science-fiction libres de la Blender Foundation, réunis en une série de démonstration.",
                "Animation", 7.0, SeriesCategory.Id, null, 2008, new List<Episode>
                {
                    new Episode("demo:ep1", "ep1", "Big Buck Bunny", 1, 1, "m3u8", "Un lapin géant face à trois rongeurs farceurs.",
                        Img + "DemoBackdropBbb.png", 596, "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
                    new Episode("demo:ep2", "ep2", "Tears of Steel", 1, 2, "m3u8", "Amsterdam, des robots et une histoire d'amour à réparer.",
                        Img + "DemoBackdropTos.png", 734, "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
                }),
        };

        public static M3UCatalog Catalog => new M3UCatalog
        {
            LiveCategories = new List<Category> { LiveCategory },
            Live = Channels.ToList(),
            VodCategories = new List<Category> { VodCategory },
            Movies = Movies.ToList(),
            SeriesCategories = new List<Category> { SeriesCategory },
            Series = SeriesList.ToList(),
        };

        static readonly Dictionary<string, string[]> Titles = new Dictionary<string, string[]>
        {
            ["demo:live1"] = new[] { "Mire de test", "Horloge en direct", "Signal de référence" },
            ["demo:live2"] = new[] { "Tears of Steel", "Court métrage libre", "Science-fiction" },
            ["demo:live3"] = new[] { "Big Buck Bunny", "Court métrage libre", "Animation" },
        };

        /// <summary>Guide des chaînes de démonstration (créneaux d'une heure autour de maintenant).</summary>
        public static List<Programme> Programmes(string channelId, DateTimeOffset now)
        {
            if (!Titles.TryGetValue(channelId, out var names)) return new List<Programme>();
            var hour = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 3600 * 3600);
            return Enumerable.Range(-1, 6).Select(i =>
            {
                var start = hour.AddHours(i);
                return new Programme(start, start.AddHours(1), names[(i + 3) % names.Length], "Programme de démonstration.");
            }).ToList();
        }
    }
}
