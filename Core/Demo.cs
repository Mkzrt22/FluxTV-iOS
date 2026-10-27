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

        // Textes dans la langue de l'app : recalculés à chaque lecture (la langue peut changer en cours de route)
        public static Category LiveCategory => new Category("demo-live", Tr.T("Démo · Direct", "Demo · Live"));
        public static Category VodCategory => new Category("demo-vod", Tr.T("Démo · Films libres", "Demo · Open movies"));
        public static Category SeriesCategory => new Category("demo-series", Tr.T("Démo · Séries", "Demo · Series"));

        /// <summary>Vignettes sans texte français (« DIRECT », « CINÉ LIBRE ») pour l'app en anglais.</summary>
        static string Logo(int n) => Img + "DemoLive" + n + (Tr.English && n < 3 ? "En" : "") + ".png";

        public static List<LiveChannel> Channels => new List<LiveChannel>
        {
            new LiveChannel("demo:live1", null, Tr.T("Mire en direct", "Live test pattern"), Logo(1), LiveCategory.Id, "demo.live1", 1,
                url: "https://demo.unified-streaming.com/k8s/live/stable/live.isml/.m3u8"),
            new LiveChannel("demo:live2", null, Tr.T("Ciné libre", "Open Cinema"), Logo(2), LiveCategory.Id, "demo.live2", 2,
                url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
            new LiveChannel("demo:live3", null, Tr.T("Animation libre", "Open Animation"), Logo(3), LiveCategory.Id, "demo.live3", 3,
                url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
        };

        public static List<Movie> Movies => new List<Movie>
        {
            new Movie("demo:tos", null, "Tears of Steel", Img + "DemoPosterTos.png", 6.9, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
                "mov", VodCategory.Id, 2012, "https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov"),
            new Movie("demo:bbb", null, "Big Buck Bunny", Img + "DemoPosterBbb.png", 7.2, DateTimeOffset.FromUnixTimeSeconds(1_690_000_000),
                "m3u8", VodCategory.Id, 2008, "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
        };

        public static Dictionary<string, MovieInfo> MovieInfos => new Dictionary<string, MovieInfo>
        {
            ["demo:tos"] = new MovieInfo
            {
                Plot = Tr.T("Dans un Amsterdam futuriste, un groupe de scientifiques tente de sauver le monde d'une invasion de robots en rejouant une rupture amoureuse. Court métrage libre de la Blender Foundation.",
                    "In a futuristic Amsterdam, a group of scientists tries to save the world from a robot invasion by reliving a breakup. An open short film by the Blender Foundation."),
                Cast = "Derek de Lint, Sergio Hasselbaink, Rogier Schippers", Director = "Ian Hubert", Genre = Tr.T("Science-fiction", "Science fiction"),
                ReleaseDate = "2012-09-26", DurationSecs = 734, Rating = 6.9, Cover = Img + "DemoPosterTos.png",
                Backdrop = Img + "DemoBackdropTos.png",
            },
            ["demo:bbb"] = new MovieInfo
            {
                Plot = Tr.T("Un lapin géant au grand cœur décide de donner une leçon à trois rongeurs qui s'en prennent aux animaux de la forêt. Court métrage d'animation libre de la Blender Foundation.",
                    "A giant rabbit with a big heart decides to teach a lesson to three rodents who pick on the forest animals. An open animated short film by the Blender Foundation."),
                Director = "Sacha Goedegebure", Genre = Tr.T("Animation, Comédie", "Animation, Comedy"), ReleaseDate = "2008-05-30", DurationSecs = 596, Rating = 7.2,
                Cover = Img + "DemoPosterBbb.png", Backdrop = Img + "DemoBackdropBbb.png",
            },
        };

        public static List<Series> SeriesList => new List<Series>
        {
            new Series("demo-open-movies", "Open Movies", Img + "DemoPosterOpen.png",
                Tr.T("Les films d'animation et de science-fiction libres de la Blender Foundation, réunis en une série de démonstration.",
                    "The Blender Foundation's open animated and science fiction films, gathered into a demo series."),
                "Animation", 7.0, SeriesCategory.Id, null, 2008, new List<Episode>
                {
                    new Episode("demo:ep1", "ep1", "Big Buck Bunny", 1, 1, "m3u8", Tr.T("Un lapin géant face à trois rongeurs farceurs.", "A giant rabbit faces three prankster rodents."),
                        Img + "DemoBackdropBbb.png", 596, "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
                    new Episode("demo:ep2", "ep2", "Tears of Steel", 1, 2, "m3u8", Tr.T("Amsterdam, des robots et une histoire d'amour à réparer.", "Amsterdam, robots and a love story to mend."),
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

        static Dictionary<string, string[]> Titles => new Dictionary<string, string[]>
        {
            ["demo:live1"] = new[] { Tr.T("Mire de test", "Test pattern"), Tr.T("Horloge en direct", "Live clock"), Tr.T("Signal de référence", "Reference signal") },
            ["demo:live2"] = new[] { "Tears of Steel", Tr.T("Court métrage libre", "Open short film"), Tr.T("Science-fiction", "Science fiction") },
            ["demo:live3"] = new[] { "Big Buck Bunny", Tr.T("Court métrage libre", "Open short film"), "Animation" },
        };

        /// <summary>Guide des chaînes de démonstration (créneaux d'une heure autour de maintenant).</summary>
        public static List<Programme> Programmes(string channelId, DateTimeOffset now)
        {
            if (!Titles.TryGetValue(channelId, out var names)) return new List<Programme>();
            var hour = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 3600 * 3600);
            return Enumerable.Range(-1, 6).Select(i =>
            {
                var start = hour.AddHours(i);
                return new Programme(start, start.AddHours(1), names[(i + 3) % names.Length], Tr.T("Programme de démonstration.", "Demo program."));
            }).ToList();
        }
    }
}
