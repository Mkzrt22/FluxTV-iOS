import Foundation

/**
 Contenu de démonstration : mire de test publique (Unified Streaming) et films libres de droits
 (Blender Foundation, licence Creative Commons BY). Même contenu que sur Xbox et Android. Il permet d'essayer FluxTV sans abonnement — et à
 l'équipe de validation d'Apple de tester l'app.

 Les visuels « asset:… » sont des images fournies avec l'app. Noms, résumés et vignettes suivent
 la langue de l'app (voir `Tr`) : tout est recalculé à chaque lecture.
 */
enum Demo {
    static var liveCategory: Category { Category(id: "demo-live", rawName: T("Démo · Direct", "Demo · Live")) }
    static var vodCategory: Category { Category(id: "demo-vod", rawName: T("Démo · Films libres", "Demo · Open movies")) }
    static var seriesCategory: Category { Category(id: "demo-series", rawName: T("Démo · Séries", "Demo · Series")) }

    /// Vignettes des deux premières chaînes : « EN DIRECT » écrit dans la langue de l'app.
    private static func logo(_ n: Int) -> String { "asset:DemoLive\(n)" + (Tr.english && n < 3 ? "En" : "") }

    /// Affiches dont le sous-titre est écrit dans la langue de l'app (« SÉRIE DE DÉMONSTRATION »…).
    private static func poster(_ name: String) -> String { "asset:DemoPoster\(name)" + (Tr.english ? "En" : "") }

    static var channels: [LiveChannel] { [
        LiveChannel(id: "demo:live1", streamId: nil, rawName: T("Mire en direct", "Live test pattern"), logo: logo(1),
                    categoryId: liveCategory.id, tvgId: "demo.live1", num: 1,
                    url: "https://demo.unified-streaming.com/k8s/live/stable/live.isml/.m3u8"),
        LiveChannel(id: "demo:live2", streamId: nil, rawName: T("Ciné libre", "Open Cinema"), logo: logo(2),
                    categoryId: liveCategory.id, tvgId: "demo.live2", num: 2,
                    url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
        LiveChannel(id: "demo:live3", streamId: nil, rawName: T("Animation libre", "Open Animation"), logo: logo(3),
                    categoryId: liveCategory.id, tvgId: "demo.live3", num: 3,
                    url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
    ] }

    static var movies: [Movie] { [
        Movie(id: "demo:tos", streamId: nil, rawName: "Tears of Steel", poster: poster("Tos"), rating: 6.9,
              added: Date(timeIntervalSince1970: 1_700_000_000), ext: "mov", categoryId: vodCategory.id, year: 2012,
              url: "https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov"),
        Movie(id: "demo:bbb", streamId: nil, rawName: "Big Buck Bunny", poster: "asset:DemoPosterBbb", rating: 7.2,
              added: Date(timeIntervalSince1970: 1_690_000_000), ext: "m3u8", categoryId: vodCategory.id, year: 2008,
              url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
    ] }

    static var movieInfo: [String: MovieInfo] { [
        "demo:tos": MovieInfo(
            plot: T("Dans un Amsterdam futuriste, un groupe de scientifiques tente de sauver le monde d'une invasion de robots en rejouant une rupture amoureuse. Court métrage libre de la Blender Foundation.",
                    "In a futuristic Amsterdam, a group of scientists tries to save the world from a robot invasion by reliving a breakup. An open short film by the Blender Foundation."),
            cast: "Derek de Lint, Sergio Hasselbaink, Rogier Schippers", director: "Ian Hubert", genre: T("Science-fiction", "Science fiction"),
            releaseDate: "2012-09-26", durationSecs: 734, rating: 6.9, cover: poster("Tos"),
            backdrop: "asset:DemoBackdropTos", trailer: nil),
        "demo:bbb": MovieInfo(
            plot: T("Un lapin géant au grand cœur décide de donner une leçon à trois rongeurs qui s'en prennent aux animaux de la forêt. Court métrage d'animation libre de la Blender Foundation.",
                    "A giant rabbit with a big heart decides to teach a lesson to three rodents who pick on the forest animals. An open animated short film by the Blender Foundation."),
            cast: nil, director: "Sacha Goedegebure", genre: T("Animation, Comédie", "Animation, Comedy"),
            releaseDate: "2008-05-30", durationSecs: 596, rating: 7.2, cover: "asset:DemoPosterBbb",
            backdrop: "asset:DemoBackdropBbb", trailer: nil),
    ] }

    static var series: [Series] { [
        Series(seriesId: "demo-open-movies", rawName: "Open Movies", cover: poster("Open"),
               plot: T("Les films d'animation et de science-fiction libres de la Blender Foundation, réunis en une série de démonstration.",
                       "The Blender Foundation's open animated and science fiction films, gathered into a demo series."),
               genre: "Animation", rating: 7.0, categoryId: seriesCategory.id, lastModified: nil, year: 2008,
               episodes: [
                Episode(id: "demo:ep1", episodeId: "ep1", title: "Big Buck Bunny", season: 1, number: 1, ext: "m3u8",
                        plot: T("Un lapin géant face à trois rongeurs farceurs.", "A giant rabbit faces three prankster rodents."), image: "asset:DemoBackdropBbb",
                        durationSecs: 596, url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
                Episode(id: "demo:ep2", episodeId: "ep2", title: "Tears of Steel", season: 1, number: 2, ext: "m3u8",
                        plot: T("Amsterdam, des robots et une histoire d'amour à réparer.", "Amsterdam, robots and a love story to mend."), image: "asset:DemoBackdropTos",
                        durationSecs: 734, url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
               ]),
    ] }

    static var catalog: M3UCatalog {
        var c = M3UCatalog()
        c.liveCategories = [liveCategory]
        c.live = channels
        c.vodCategories = [vodCategory]
        c.movies = movies
        c.seriesCategories = [seriesCategory]
        c.series = series
        return c
    }

    /// Guide des chaînes de démonstration (créneaux d'une heure autour de maintenant).
    static func programmes(for channelId: String, now: Date = Date()) -> [Programme] {
        let titles: [String: [String]] = [
            "demo:live1": [T("Mire de test", "Test pattern"), T("Horloge en direct", "Live clock"), T("Signal de référence", "Reference signal")],
            "demo:live2": ["Tears of Steel", T("Court métrage libre", "Open short film"), T("Science-fiction", "Science fiction")],
            "demo:live3": ["Big Buck Bunny", T("Court métrage libre", "Open short film"), "Animation"],
        ]
        guard let names = titles[channelId] else { return [] }
        let hour = floor(now.timeIntervalSince1970 / 3600) * 3600
        return (-1...4).map { i in
            let start = Date(timeIntervalSince1970: hour + Double(i) * 3600)
            return Programme(start: start, stop: start.addingTimeInterval(3600),
                             title: names[(i + 3) % names.count], desc: T("Programme de démonstration.", "Demo program."))
        }
    }
}
