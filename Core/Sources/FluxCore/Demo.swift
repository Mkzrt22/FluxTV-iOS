import Foundation

/**
 Contenu de démonstration : mire de test publique (Unified Streaming) et films libres de droits
 (Blender Foundation, licence Creative Commons BY). Même contenu que sur Xbox et Android. Il permet d'essayer FluxTV sans abonnement — et à
 l'équipe de validation d'Apple de tester l'app.

 Les visuels « asset:… » sont des images fournies avec l'app.
 */
enum Demo {
    static let liveCategory = Category(id: "demo-live", rawName: "Démo · Direct")
    static let vodCategory = Category(id: "demo-vod", rawName: "Démo · Films libres")
    static let seriesCategory = Category(id: "demo-series", rawName: "Démo · Séries")

    static let channels: [LiveChannel] = [
        LiveChannel(id: "demo:live1", streamId: nil, rawName: "Mire en direct", logo: "asset:DemoLive1",
                    categoryId: liveCategory.id, tvgId: "demo.live1", num: 1,
                    url: "https://demo.unified-streaming.com/k8s/live/stable/live.isml/.m3u8"),
        LiveChannel(id: "demo:live2", streamId: nil, rawName: "Ciné libre", logo: "asset:DemoLive2",
                    categoryId: liveCategory.id, tvgId: "demo.live2", num: 2,
                    url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
        LiveChannel(id: "demo:live3", streamId: nil, rawName: "Animation libre", logo: "asset:DemoLive3",
                    categoryId: liveCategory.id, tvgId: "demo.live3", num: 3,
                    url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
    ]

    static let movies: [Movie] = [
        Movie(id: "demo:tos", streamId: nil, rawName: "Tears of Steel", poster: "asset:DemoPosterTos", rating: 6.9,
              added: Date(timeIntervalSince1970: 1_700_000_000), ext: "mov", categoryId: vodCategory.id, year: 2012,
              url: "https://download.blender.org/demo/movies/ToS/tears_of_steel_720p.mov"),
        Movie(id: "demo:bbb", streamId: nil, rawName: "Big Buck Bunny", poster: "asset:DemoPosterBbb", rating: 7.2,
              added: Date(timeIntervalSince1970: 1_690_000_000), ext: "m3u8", categoryId: vodCategory.id, year: 2008,
              url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
    ]

    static let movieInfo: [String: MovieInfo] = [
        "demo:tos": MovieInfo(
            plot: "Dans un Amsterdam futuriste, un groupe de scientifiques tente de sauver le monde d'une invasion de robots en rejouant une rupture amoureuse. Court métrage libre de la Blender Foundation.",
            cast: "Derek de Lint, Sergio Hasselbaink, Rogier Schippers", director: "Ian Hubert", genre: "Science-fiction",
            releaseDate: "2012-09-26", durationSecs: 734, rating: 6.9, cover: "asset:DemoPosterTos",
            backdrop: "asset:DemoBackdropTos", trailer: nil),
        "demo:bbb": MovieInfo(
            plot: "Un lapin géant au grand cœur décide de donner une leçon à trois rongeurs qui s'en prennent aux animaux de la forêt. Court métrage d'animation libre de la Blender Foundation.",
            cast: nil, director: "Sacha Goedegebure", genre: "Animation, Comédie",
            releaseDate: "2008-05-30", durationSecs: 596, rating: 7.2, cover: "asset:DemoPosterBbb",
            backdrop: "asset:DemoBackdropBbb", trailer: nil),
    ]

    static let series: [Series] = [
        Series(seriesId: "demo-open-movies", rawName: "Open Movies", cover: "asset:DemoPosterOpen",
               plot: "Les films d'animation et de science-fiction libres de la Blender Foundation, réunis en une série de démonstration.",
               genre: "Animation", rating: 7.0, categoryId: seriesCategory.id, lastModified: nil, year: 2008,
               episodes: [
                Episode(id: "demo:ep1", episodeId: "ep1", title: "Big Buck Bunny", season: 1, number: 1, ext: "m3u8",
                        plot: "Un lapin géant face à trois rongeurs farceurs.", image: "asset:DemoBackdropBbb",
                        durationSecs: 596, url: "https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8"),
                Episode(id: "demo:ep2", episodeId: "ep2", title: "Tears of Steel", season: 1, number: 2, ext: "m3u8",
                        plot: "Amsterdam, des robots et une histoire d'amour à réparer.", image: "asset:DemoBackdropTos",
                        durationSecs: 734, url: "https://test-streams.mux.dev/tos_ismc/main.m3u8"),
               ]),
    ]

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
            "demo:live1": ["Mire de test", "Horloge en direct", "Signal de référence"],
            "demo:live2": ["Tears of Steel", "Court métrage libre", "Science-fiction"],
            "demo:live3": ["Big Buck Bunny", "Court métrage libre", "Animation"],
        ]
        guard let names = titles[channelId] else { return [] }
        let hour = floor(now.timeIntervalSince1970 / 3600) * 3600
        return (-1...4).map { i in
            let start = Date(timeIntervalSince1970: hour + Double(i) * 3600)
            return Programme(start: start, stop: start.addingTimeInterval(3600),
                             title: names[(i + 3) % names.count], desc: "Programme de démonstration.")
        }
    }
}
