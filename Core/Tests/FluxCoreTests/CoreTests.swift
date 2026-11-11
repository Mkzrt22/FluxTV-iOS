import XCTest
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif
@testable import FluxCore

private func fixture(_ name: String) throws -> Data {
    let url = try XCTUnwrap(Bundle.module.url(forResource: name, withExtension: nil, subdirectory: "Fixtures"))
    return try Data(contentsOf: url)
}

final class TextToolsTests: XCTestCase {
    func testChannelNames() {
        XCTAssertEqual(TextTools.cleanChannel("FR| TF1 FHD").name, "TF1")
        XCTAssertEqual(TextTools.cleanChannel("FR| TF1 FHD").badges, ["FHD"])
        XCTAssertEqual(TextTools.cleanChannel("FR: France 2 HD").name, "France 2")
        XCTAssertEqual(TextTools.cleanChannel("|FR| CANAL+ SPORT 4K").name, "CANAL+ SPORT")
        XCTAssertEqual(TextTools.cleanChannel("|FR| CANAL+ SPORT 4K").badges, ["4K"])
        XCTAssertEqual(TextTools.cleanChannel("[FR] M6 ᴴᴰ").name, "M6")
        XCTAssertEqual(TextTools.cleanChannel("[FR] M6 ᴴᴰ").badges, ["HD"])
        XCTAssertEqual(TextTools.cleanChannel("FR - Arte").name, "Arte")
        XCTAssertEqual(TextTools.cleanChannel("TF1").name, "TF1")
        XCTAssertEqual(TextTools.cleanChannel("RMC: Story").name, "RMC: Story", "un nom de chaîne avec « : » reste entier")
        XCTAssertEqual(TextTools.cleanChannel("HD1").name, "HD1")
        XCTAssertEqual(TextTools.cleanChannel("TV5 Monde").name, "TV5 Monde")
        XCTAssertEqual(TextTools.cleanChannel("FR ▎BFM TV HEVC").name, "BFM TV")
        XCTAssertEqual(TextTools.cleanChannel("FR ▎BFM TV HEVC").badges, ["HEVC"])
        XCTAssertEqual(TextTools.cleanChannel("FHD").name, "FHD", "jamais de nom vide")
    }

    func testTitles() {
        let dune = TextTools.cleanTitle("FR - Dune (2021) [MULTI]")
        XCTAssertEqual(dune.name, "Dune")
        XCTAssertEqual(dune.year, 2021)
        XCTAssertEqual(dune.badges, ["MULTI"])
        XCTAssertEqual(TextTools.cleanTitle("Oppenheimer 4K").name, "Oppenheimer")
        XCTAssertEqual(TextTools.cleanTitle("Oppenheimer 4K").badges, ["4K"])
        XCTAssertEqual(TextTools.cleanTitle("Blade Runner 2049").name, "Blade Runner 2049")
        XCTAssertNil(TextTools.cleanTitle("Blade Runner 2049").year)
        XCTAssertEqual(TextTools.cleanTitle("1917 (2019)").name, "1917")
        XCTAssertEqual(TextTools.cleanTitle("1917 (2019)").year, 2019)
        XCTAssertEqual(TextTools.cleanTitle("Le Parrain - 1972").name, "Le Parrain")
        XCTAssertEqual(TextTools.cleanTitle("Le Parrain - 1972").year, 1972)
        XCTAssertEqual(TextTools.cleanTitle("Amélie (VOSTFR)").badges, ["VOSTFR"])
        XCTAssertEqual(TextTools.cleanTitle("Mission: Impossible").name, "Mission: Impossible")
    }

    func testCategories() {
        XCTAssertEqual(TextTools.cleanCategory("FR | SPORTS"), "Sports")
        XCTAssertEqual(TextTools.cleanCategory("FR| GÉNÉRALISTES"), "Généralistes")
        XCTAssertEqual(TextTools.cleanCategory("|FR| CINÉMA ET SÉRIES"), "Cinéma et Séries")
        XCTAssertEqual(TextTools.cleanCategory("RMC SPORT"), "RMC Sport")
        XCTAssertEqual(TextTools.cleanCategory("LES FILMS"), "Les Films")
        XCTAssertEqual(TextTools.cleanCategory("Documentaires"), "Documentaires")
        XCTAssertEqual(TextTools.cleanCategory("TNT"), "TNT")
        XCTAssertEqual(TextTools.cleanCategory("VOD - ACTION"), "Action")
    }

    func testAdultAndSearch() {
        XCTAssertTrue(TextTools.isAdult("XXX ADULTES"))
        XCTAssertTrue(TextTools.isAdult("Adult 18+"))
        XCTAssertFalse(TextTools.isAdult("Sports"))
        XCTAssertFalse(TextTools.isAdult("Essex TV"))
        XCTAssertEqual(TextTools.fold("Les Misérables"), "les miserables")
        XCTAssertTrue(TextTools.matches("Les Misérables", query: TextTools.queryWords("misera les")))
        XCTAssertFalse(TextTools.matches("Les Misérables", query: TextTools.queryWords("dune")))
        XCTAssertTrue(TextTools.frenchLess("Eau", "Été"))
        XCTAssertTrue(TextTools.frenchLess("Été", "Fargo"))
        XCTAssertTrue(TextTools.frenchLess("Épisode 2", "Épisode 10"))
    }
}

final class XtreamTests: XCTestCase {
    func testServerNormalization() {
        XCTAssertEqual(XtreamAccount.normalizeServer("srv.example:8080"), "http://srv.example:8080")
        XCTAssertEqual(XtreamAccount.normalizeServer("http://srv.example:8080/"), "http://srv.example:8080")
        XCTAssertEqual(XtreamAccount.normalizeServer("https://srv.example/player_api.php?username=a"), "https://srv.example")
        XCTAssertEqual(XtreamAccount.normalizeServer(" http://srv.example:80/iptv/get.php "), "http://srv.example:80/iptv")
    }

    func testPlaylistLinkBecomesAccount() throws {
        let a = try XCTUnwrap(XtreamAccount.fromPlaylistURL("http://srv.example:8080/get.php?username=kev&password=s3cr%2Bt&type=m3u_plus&output=ts"))
        XCTAssertEqual(a.server, "http://srv.example:8080")
        XCTAssertEqual(a.username, "kev")
        XCTAssertEqual(a.password, "s3cr+t")
        XCTAssertNil(XtreamAccount.fromPlaylistURL("http://cdn.example/list.m3u"))
    }

    func testURLs() throws {
        let a = XtreamAccount(server: "http://srv.example:8080", username: "kev in", password: "p+w/d")
        XCTAssertEqual(a.liveURL("101")?.absoluteString, "http://srv.example:8080/live/kev%20in/p%2Bw%2Fd/101.ts")
        XCTAssertEqual(a.movieURL("5001", ext: "mkv")?.absoluteString, "http://srv.example:8080/movie/kev%20in/p%2Bw%2Fd/5001.mkv")
        XCTAssertEqual(a.episodeURL("801", ext: "")?.absoluteString, "http://srv.example:8080/series/kev%20in/p%2Bw%2Fd/801.mkv")
        let api = try XCTUnwrap(a.apiURL("get_vod_streams", [("category_id", "20")])).absoluteString
        XCTAssertTrue(api.hasPrefix("http://srv.example:8080/player_api.php?username=kev%20in&password=p%2Bw/d&action=get_vod_streams"), api)
        XCTAssertTrue(api.hasSuffix("&category_id=20"))
        let paris = TimeZone(identifier: "Europe/Paris")
        let ts = a.timeshiftURL("101", start: Date(timeIntervalSince1970: 1760000000), minutes: 60, serverTimeZone: paris)
        XCTAssertEqual(ts?.absoluteString, "http://srv.example:8080/timeshift/kev%20in/p%2Bw%2Fd/60/2025-10-09:10-53/101.ts")
    }

    func testAccount() throws {
        let info = try XtreamParse.account(try fixture("account.json"))
        XCTAssertTrue(info.isActive)
        XCTAssertEqual(info.maxConnections, 1)
        XCTAssertEqual(info.serverTimeZone, "Europe/Paris")
        XCTAssertEqual(info.liveExtension, "ts")
        XCTAssertEqual(info.daysLeft(now: Date(timeIntervalSince1970: 1893456000 - 86400 * 10)), 10)
        XCTAssertThrowsError(try XtreamParse.account(Data("[]".utf8)))
    }

    func testCategoriesAndLive() throws {
        let cats = XtreamParse.categories(try fixture("live_categories.json"))
        XCTAssertEqual(cats.map(\.id), ["10", "11", "12"])
        XCTAssertEqual(cats.map(\.name), ["Généralistes", "Sports", "XXX Adultes"])
        XCTAssertEqual(cats.map(\.isAdult), [false, false, true])

        let live = XtreamParse.liveStreams(try fixture("live_streams.json"))
        XCTAssertEqual(live.map(\.id), ["live:101", "live:102", "live:103", "live:104"])
        XCTAssertEqual(live[0].name, "TF1")
        XCTAssertEqual(live[0].badges, ["FHD"])
        XCTAssertEqual(live[0].archiveDays, 7)
        XCTAssertEqual(live[0].tvgId, "TF1.fr")
        XCTAssertEqual(live[1].num, 2)
        XCTAssertEqual(live[1].name, "France 2")
        XCTAssertNil(live[1].logo)
        XCTAssertNil(live[1].tvgId)
        XCTAssertEqual(live[1].categoryId, "10")
        XCTAssertEqual(live[2].name, "CANAL+ SPORT")
        XCTAssertEqual(live[3].name, "BeIN SPORTS 1")
    }

    func testMovies() throws {
        let m = XtreamParse.vodStreams(try fixture("vod_streams.json"))
        XCTAssertEqual(m.count, 3)
        XCTAssertEqual(m[0].name, "Dune")
        XCTAssertEqual(m[0].year, 2021)
        XCTAssertEqual(m[0].ext, "mkv")
        XCTAssertEqual(m[0].rating, 7.9)
        XCTAssertEqual(m[1].year, 2023)
        XCTAssertNil(m[1].poster)
        XCTAssertEqual(m[1].ext, "mp4")
        XCTAssertEqual(m[1].rating, 8.3)
        XCTAssertNil(m[2].rating)
        XCTAssertEqual(m[2].ext, "mp4")
        XCTAssertEqual(m[2].name, "Blade Runner 2049")
    }

    func testSeriesInfoObject() throws {
        let d = XtreamParse.seriesInfo(try fixture("series_info_object.json"))
        XCTAssertEqual(d.seasonNumbers, [1, 2])
        XCTAssertEqual(d.seasons[1]?.map(\.title), ["Le Phare", "Marée basse"])
        XCTAssertEqual(d.seasons[1]?.map(\.number), [1, 2])
        XCTAssertEqual(d.seasons[2]?.first?.title, "Retour")
        XCTAssertEqual(d.seasons[2]?.first?.durationSecs, 2700)
        XCTAssertEqual(d.seasons[1]?.last?.durationSecs, 2650)
        XCTAssertEqual(d.backdrop, "http://img.example/b1.jpg")
        XCTAssertEqual(d.year, 2023)
        XCTAssertEqual(d.rating, 8.2)
        XCTAssertEqual(d.allEpisodes.map(\.id), ["ep:801", "ep:802", "ep:902"])
    }

    func testSeriesInfoArray() throws {
        let d = XtreamParse.seriesInfo(try fixture("series_info_array.json"))
        XCTAssertEqual(d.seasonNumbers, [1, 2])
        XCTAssertEqual(d.seasons[2]?.first?.number, 1)
        XCTAssertEqual(d.backdrop, "http://img.example/x.jpg")
    }

    func testShortEPG() throws {
        let list = XtreamParse.epgListings(try fixture("short_epg.json"))
        XCTAssertEqual(list.map(\.title), ["Le Journal de 20 h", "Film"])
        XCTAssertEqual(list[0].desc, "L'actualité")
        XCTAssertTrue(list[0].hasArchive)
        let nn = Programme.nowNext(list, at: Date(timeIntervalSince1970: 1760001000))
        XCTAssertEqual(nn.now?.title, "Le Journal de 20 h")
        XCTAssertEqual(nn.next?.title, "Film")
        XCTAssertEqual(list[0].progress(Date(timeIntervalSince1970: 1760001800)), 0.5, accuracy: 0.001)
        let before = Programme.nowNext(list, at: Date(timeIntervalSince1970: 1759990000))
        XCTAssertNil(before.now)
        XCTAssertEqual(before.next?.title, "Le Journal de 20 h")
    }

    func testHelpers() {
        XCTAssertEqual(XtreamParse.parseDuration("01:52:10"), 6730)
        XCTAssertEqual(XtreamParse.parseDuration("112 min"), 6720)
        XCTAssertEqual(XtreamParse.parseDuration("6730"), 6730)
        XCTAssertEqual(XtreamParse.youtubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ"), "dQw4w9WgXcQ")
        XCTAssertEqual(XtreamParse.youtubeId("dQw4w9WgXcQ"), "dQw4w9WgXcQ")
        XCTAssertEqual(XtreamParse.cleanEpisodeTitle("Brumes - S01E03 - Les Disparus"), "Les Disparus")
    }
}

final class M3UTests: XCTestCase {
    func testParse() throws {
        let text = String(decoding: try fixture("playlist.m3u"), as: UTF8.self)
        let (entries, epg) = M3U.parse(text)
        XCTAssertEqual(epg, "http://epg.example/guide.xml.gz")
        XCTAssertEqual(entries.count, 8)
        XCTAssertEqual(entries[1].name, "Canal+ Sport, HD")
        XCTAssertEqual(entries[1].userAgent, "MonLecteur/1.0")
        XCTAssertEqual(entries[7].group, "Autres")
    }

    func testCatalog() throws {
        let cat = M3U.catalog(from: String(decoding: try fixture("playlist.m3u"), as: UTF8.self))
        XCTAssertEqual(cat.live.map(\.id), ["live:101", "live:202", cat.live.last!.id])
        XCTAssertEqual(cat.live[0].name, "TF1")
        XCTAssertEqual(cat.live[0].badges, ["HD"])
        XCTAssertEqual(cat.liveCategories.map(\.name), ["Généralistes", "Sport", "Autres"])
        XCTAssertEqual(cat.movies.map(\.id).first, "vod:5001")
        XCTAssertEqual(cat.movies.count, 2)
        XCTAssertEqual(cat.movies[1].ext, "mp4")
        XCTAssertEqual(cat.series.count, 2)
        let brumes = try XCTUnwrap(cat.series.first)
        XCTAssertEqual(brumes.name, "Brumes")
        let d = M3U.details(of: brumes)
        XCTAssertEqual(d.allEpisodes.map(\.number), [1, 2])
        XCTAssertEqual(d.allEpisodes.last?.title, "Marée basse")
        XCTAssertEqual(cat.series[1].name, "Autre Série")
    }

    func testAttributes() {
        let a = M3U.attributes(#"#EXTINF:-1 tvg-id="a=b" tvg-logo="http://x/y.png" group-title="",Nom"#)
        XCTAssertEqual(a["tvg-id"], "a=b")
        XCTAssertEqual(a["tvg-logo"], "http://x/y.png")
        XCTAssertEqual(a["group-title"], "")
    }

    func testIds() {
        XCTAssertEqual(Ids.fromURL("http://h:8080/live/u/p/123.ts"), "live:123")
        XCTAssertEqual(Ids.fromURL("http://h:8080/movie/u/p/456.mkv?x=1"), "vod:456")
        XCTAssertEqual(Ids.fromURL("http://h:8080/series/u/p/789.mp4"), "ep:789")
        XCTAssertEqual(Ids.fromURL("http://h:8080/u/p/321"), "live:321")
        XCTAssertTrue(Ids.fromURL("http://cdn/x/y.m3u8").hasPrefix("u:"))
        XCTAssertEqual(Ids.fromURL("http://cdn/x/y.m3u8"), Ids.fromURL("http://cdn/x/y.m3u8"))
        XCTAssertEqual(Ids.numericPart("vod:456"), "456")
        XCTAssertNil(Ids.numericPart("u:abc"))
    }
}

final class XMLTVTests: XCTestCase {
    func testParse() throws {
        let now = try XCTUnwrap(XMLTV.date("20261022183000 +0200"))
        let map = XMLTV.parse(try fixture("guide.xml"), wanted: ["TF1.fr"], now: now)
        XCTAssertEqual(map.keys.sorted(), ["tf1.fr"])
        XCTAssertEqual(map["tf1.fr"]?.map(\.title), ["Le 18 h", "Jeu & Quiz"])
        XCTAssertEqual(map["tf1.fr"]?.first?.desc, "Infos.")
        XCTAssertEqual(XMLTV.date("20261022180000 +0200"), XMLTV.date("20261022160000 +0000"))
        XCTAssertEqual(XMLTV.date("20261022160000"), XMLTV.date("20261022160000 +0000"))
        XCTAssertNil(XMLTV.date("2026"))
    }
}

final class LibraryTests: XCTestCase {
    private let movie = ContentRef(id: "vod:1", kind: .movie, name: "Dune")
    private let live = ContentRef(id: "live:1", kind: .live, name: "TF1")

    func testFavorites() {
        var lib = LibraryData()
        XCTAssertTrue(lib.toggleFavorite(movie))
        XCTAssertTrue(lib.isFavorite("vod:1"))
        lib.toggleFavorite(live)
        lib.moveFavorite(from: 1, to: 0)
        XCTAssertEqual(lib.favorites.map(\.id), ["live:1", "vod:1"])
        XCTAssertFalse(lib.toggleFavorite(movie))
        XCTAssertEqual(lib.favorites(of: .live).count, 1)
    }

    func testResumeAndWatched() {
        var lib = LibraryData()
        lib.addHistory(movie)
        XCTAssertFalse(lib.savePosition(movie, position: 10, duration: 6000))
        XCTAssertNil(lib.resume("vod:1"), "pas de reprise avant 15 s")
        lib.savePosition(movie, position: 1200, duration: 6000)
        XCTAssertEqual(lib.resume("vod:1")?.position, 1200)
        XCTAssertEqual(lib.continueWatching().map(\.id), ["vod:1"])
        XCTAssertTrue(lib.savePosition(movie, position: 5800, duration: 6000))
        XCTAssertTrue(lib.isWatched("vod:1"))
        XCTAssertNil(lib.resume("vod:1"))
        XCTAssertTrue(lib.continueWatching().isEmpty)
        XCTAssertFalse(lib.savePosition(live, position: 500, duration: 0))
    }

    func testHistoryKeepsOneEpisodePerSeries() {
        var lib = LibraryData()
        lib.addHistory(ContentRef(id: "ep:1", kind: .episode, name: "S01E01", seriesId: "9"))
        lib.addHistory(live)
        lib.addHistory(ContentRef(id: "ep:2", kind: .episode, name: "S01E02", seriesId: "9"))
        XCTAssertEqual(lib.history.map(\.id), ["ep:2", "live:1"])
        XCTAssertEqual(lib.lastLiveId, "live:1")
        XCTAssertEqual(lib.recentLive().map(\.id), ["live:1"])
    }

    func testNextEpisode() {
        let eps = (1...3).map { Episode(id: "ep:\($0)", episodeId: "\($0)", title: "E\($0)", season: 1, number: $0, ext: "mkv",
                                        plot: nil, image: nil, durationSecs: nil, url: nil) }
        var lib = LibraryData()
        XCTAssertEqual(lib.nextEpisode(in: eps)?.episode.id, "ep:1")
        lib.markWatched("ep:1")
        XCTAssertEqual(lib.nextEpisode(in: eps)?.episode.id, "ep:2")
        lib.savePosition(ContentRef(id: "ep:3", kind: .episode, name: "E3"), position: 300, duration: 2400)
        XCTAssertEqual(lib.nextEpisode(in: eps)?.episode.id, "ep:3")
        XCTAssertEqual(lib.nextEpisode(in: eps)?.resume?.position, 300)
    }

    func testFileRoundTrip() throws {
        let dir = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent(UUID().uuidString)
        let file = LibraryFile(url: dir.appendingPathComponent("library.json"))
        var lib = LibraryData()
        lib.toggleFavorite(movie)
        lib.addHistory(movie)
        lib.savePosition(movie, position: 600, duration: 6000, now: Date(timeIntervalSince1970: 1_760_000_000))
        try file.save(lib)
        XCTAssertEqual(file.load(), lib)
        try? FileManager.default.removeItem(at: dir)
    }
}

final class ResolverTests: XCTestCase {
    func testPlaybackURLs() {
        let source = Source.xtream(XtreamAccount(server: "http://h:8080", username: "u", password: "p"))
        let ep = ContentRef(id: "ep:801", kind: .episode, name: "x", ext: "mp4")
        XCTAssertEqual(PlaybackResolver.url(for: ep, source: source)?.absoluteString, "http://h:8080/series/u/p/801.mp4")
        let live = ContentRef(id: "live:101", kind: .live, name: "TF1")
        XCTAssertEqual(PlaybackResolver.url(for: live, source: source, liveExtension: "m3u8")?.absoluteString, "http://h:8080/live/u/p/101.m3u8")
        let m3u = ContentRef(id: "u:1", kind: .live, name: "x", url: "http://cdn/x.m3u8")
        XCTAssertEqual(PlaybackResolver.url(for: m3u, source: .demo)?.absoluteString, "http://cdn/x.m3u8")
        XCTAssertNotEqual(Source.demo.cacheKey, source.cacheKey)
        XCTAssertFalse(source.cacheKey.contains("u"), "aucun identifiant dans le nom du cache")
    }

    func testDemoCatalog() {
        let c = Demo.catalog
        XCTAssertEqual(c.live.count, 3)
        XCTAssertEqual(c.movies.count, 2)
        XCTAssertEqual(M3U.details(of: c.series[0]).allEpisodes.count, 2)
        let p = Demo.programmes(for: "demo:live1", now: Date(timeIntervalSince1970: 1_760_001_000))
        XCTAssertNotNil(Programme.nowNext(p, at: Date(timeIntervalSince1970: 1_760_001_000)).now)
    }

    func testRepositoryDemo() async throws {
        let repo = Repository(source: .demo, cacheBase: URL(fileURLWithPath: NSTemporaryDirectory()))
        let live = try await repo.live()
        XCTAssertEqual(live.channels.count, 3)
        let cats = try await repo.vodCategories()
        let movies = try await repo.movies(category: cats[0].id)
        XCTAssertEqual(movies.count, 2)
        let info = try await repo.movieInfo(movies[0])
        XCTAssertNotNil(info?.plot)
        let s = try await repo.allSeries()
        let d = try await repo.seriesDetails(s[0])
        XCTAssertEqual(d.allEpisodes.count, 2)
        // Série ouverte depuis les favoris : même identifiant, épisodes retrouvés dans la playlist
        let fav = Series(ref: s[0].ref(group: nil))
        XCTAssertEqual(fav.id, s[0].id)
        XCTAssertNil(fav.episodes)
        let found = try await repo.allSeries().first { $0.id == fav.id }
        XCTAssertEqual(found?.episodes?.count, 2)
    }

    func testFavoriteSeriesKeepsItsId() {
        let xtream = Series(seriesId: "42", rawName: "FR - Dark", cover: nil, plot: nil, genre: nil, rating: nil,
                            categoryId: "1", lastModified: nil)
        XCTAssertEqual(xtream.id, "ser:42")
        let restored = Series(ref: xtream.ref(group: "Séries"))
        XCTAssertEqual(restored.id, "ser:42")
        XCTAssertEqual(restored.seriesId, "42")
        XCTAssertEqual(restored.name, "Dark")
    }
}

/// Textes en anglais (choix de la langue au premier lancement ou dans les réglages).
// ───────────────────────────── Robustesse ─────────────────────────────

/// Faux serveur : chaque requête de l'app reçoit la réponse décidée par le test, sans réseau.
final class StubServer: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private static var answer: (@Sendable (URL) -> (Int, Data))?

    static func respond(_ handler: @escaping @Sendable (URL) -> (Int, Data)) {
        lock.lock(); answer = handler; lock.unlock()
    }

    static func makeHTTP() -> HTTPClient {
        let cfg = URLSessionConfiguration.ephemeral
        cfg.protocolClasses = [StubServer.self]
        return HTTPClient(configuration: cfg)
    }

    override class func canInit(with request: URLRequest) -> Bool { true }
    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        StubServer.lock.lock(); let handler = StubServer.answer; StubServer.lock.unlock()
        guard let url = request.url, let (status, body) = handler.map({ $0(url) }),
              let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: nil) else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

/// Réponses du faux serveur, modifiables en cours de test.
final class Answers: @unchecked Sendable {
    private let lock = NSLock()
    private var playlistValue = Data()
    private var guideValue = Data()
    private var statusValue = 200

    var playlist: String {
        get { lock.lock(); defer { lock.unlock() }; return String(decoding: playlistValue, as: UTF8.self) }
        set { lock.lock(); playlistValue = Data(newValue.utf8); lock.unlock() }
    }
    var guide: String {
        get { lock.lock(); defer { lock.unlock() }; return String(decoding: guideValue, as: UTF8.self) }
        set { lock.lock(); guideValue = Data(newValue.utf8); lock.unlock() }
    }
    var status: Int {
        get { lock.lock(); defer { lock.unlock() }; return statusValue }
        set { lock.lock(); statusValue = newValue; lock.unlock() }
    }

    func answer(_ url: URL) -> (Int, Data) {
        lock.lock(); defer { lock.unlock() }
        return url.path.hasSuffix("guide.xml") ? (200, guideValue) : (statusValue, playlistValue)
    }
}

final class RobustnessTests: XCTestCase {

    func testKodiOptionsAndBOM() {
        let split = M3U.splitURLOptions("http://cdn.example/a.m3u8|User-Agent=VLC%2F3.0&Referer=http://x/")
        XCTAssertEqual(split.url, "http://cdn.example/a.m3u8")
        XCTAssertEqual(split.userAgent, "VLC/3.0")
        XCTAssertEqual(M3U.splitURLOptions("http://cdn.example/a.m3u8|Referer=http://x/").url, "http://cdn.example/a.m3u8")
        XCTAssertNil(M3U.splitURLOptions("http://cdn.example/a.m3u8|Referer=http://x/").userAgent)
        // Une barre qui n'annonce pas d'options reste dans l'adresse
        XCTAssertEqual(M3U.splitURLOptions("http://cdn.example/a|b.m3u8").url, "http://cdn.example/a|b.m3u8")
        XCTAssertEqual(M3U.splitURLOptions("rtmp://x/y|z=1").url, "rtmp://x/y|z=1")

        let text = "\u{FEFF}#EXTM3U x-tvg-url=\"http://epg.example/g.xml\"\r\n" +
            "#EXTINF:-1 tvg-id=\"A\",Une\r\nhttp://cdn.example/1.m3u8|User-Agent=Kodi+Lecteur\r\n" +
            "#EXTINF:-1,Deux\n#EXTVLCOPT:http-user-agent=VLC\nhttp://cdn.example/2.m3u8|User-Agent=Autre\n"
        let parsed = M3U.parse(text)
        XCTAssertEqual(parsed.epgURL, "http://epg.example/g.xml", "guide annoncé malgré le BOM")
        XCTAssertEqual(parsed.entries.map(\.url), ["http://cdn.example/1.m3u8", "http://cdn.example/2.m3u8"])
        // #EXTVLCOPT garde la priorité sur l'option collée à l'adresse
        XCTAssertEqual(parsed.entries.map(\.userAgent), ["Kodi Lecteur", "VLC"])
    }

    func testGuideIdsIgnoreCase() throws {
        let now = try XCTUnwrap(XMLTV.date("20261022183000 +0200"))
        // « tf1.FR » dans la playlist, « TF1.fr » dans le guide : la même chaîne
        let map = XMLTV.parse(try fixture("guide.xml"), wanted: [" tf1.FR "], now: now)
        XCTAssertEqual(Array(map.keys), ["tf1.fr"])
        XCTAssertEqual(map[XMLTV.key("TF1.fr")]?.count, 2)
    }

    func testJSONFalseMeansNothing() throws {
        let o = try XCTUnwrap(JSON.object(Data(#"{"a":false,"b":true,"n":0,"one":1,"t":"x","d":7.5}"#.utf8)))
        XCTAssertNil(o.str("a"))
        XCTAssertEqual(o.str("b"), "1")
        XCTAssertEqual(o.str("n"), "0")
        XCTAssertEqual(o.str("one"), "1")
        XCTAssertEqual(o.str("d"), "7.5")
        XCTAssertFalse(o.flag("a"))
        XCTAssertTrue(o.flag("b"))
        XCTAssertTrue(o.flag("one"))
        // Panels qui envoient « false » à la place d'un logo ou d'un identifiant de guide
        let live = XtreamParse.liveStreams(Data(#"[{"stream_id":5,"name":"Une","stream_icon":false,"epg_channel_id":false,"tv_archive":false,"num":false}]"#.utf8))
        XCTAssertEqual(live.count, 1)
        XCTAssertNil(live.first?.logo)
        XCTAssertNil(live.first?.tvgId)
        XCTAssertEqual(live.first?.archiveDays, 0)
        XCTAssertEqual(live.first?.num, 0)
    }

    func testBigListsAreReadObjectByObject() {
        // Tableau avec espaces, textes pleins d'accolades et de guillemets, intrus, objets imbriqués,
        // et une fin coupée en route : tout ce qui est entier est gardé
        let array = "\u{FEFF} [ {\"stream_id\": 1, \"name\": \"A } \\\" { [\", \"custom\": {\"a\": [1, {\"b\": 2}]}},\n" +
            "  \"intrus\", 42, null, [ {\"stream_id\": 99, \"name\": \"dans un tableau\"} ],\n" +
            "  {\"stream_id\": \"2\", \"name\": \"Chemin C:\\\\films\\\\\", \"x\": []},\n" +
            "  {\"stream_id\": 3, \"name\": \"Coupé en rou"
        XCTAssertEqual(XtreamParse.vodStreams(Data(array.utf8)).map(\.streamId), ["1", "2"])

        // Certains panels envoient un objet dont les valeurs sont les éléments
        let object = #"{"10": {"stream_id": 10, "name": "X"}, "11": {"series_id": 11, "name": "Y"}, "user_info": {"auth": 0}}"#
        XCTAssertEqual(XtreamParse.vodStreams(Data(object.utf8)).map(\.streamId), ["10"])
        XCTAssertEqual(XtreamParse.series(Data(object.utf8)).map(\.seriesId), ["11"])

        var count = 0
        JSON.eachObject(in: Data(#"[{"a":1},{"b":{"c":[{"d":1}]}},{}]"#.utf8)) { _ in count += 1 }
        XCTAssertEqual(count, 3)

        // Réponses vides ou qui ne sont pas des listes
        for junk in ["", "[]", "{}", "null", "<html><body>Erreur</body></html>", "]]}}", #"{"user_info":{"auth":0}}"#] {
            XCTAssertTrue(XtreamParse.vodStreams(Data(junk.utf8)).isEmpty, junk)
            XCTAssertTrue(XtreamParse.categories(Data(junk.utf8)).isEmpty, junk)
        }

        // Une grande liste : même résultat qu'avec la lecture d'un bloc
        let many = "[" + (1...3000).map { "{\"stream_id\":\($0),\"name\":\"Film \($0)\",\"category_id\":\"\($0 % 7)\"}" }.joined(separator: ",") + "]"
        let movies = XtreamParse.vodStreams(Data(many.utf8))
        XCTAssertEqual(movies.count, 3000)
        XCTAssertEqual(movies.last?.id, "vod:3000")
        XCTAssertEqual(movies.last?.categoryId, "4")
    }

    func testLiveFormatFallback() throws {
        let ts = try XCTUnwrap(URL(string: "http://h:8080/live/jean/code%20secret/123.ts?token=a.b"))
        XCTAssertEqual(PlaybackResolver.liveExtension(of: ts), "ts")
        let hls = try XCTUnwrap(PlaybackResolver.alternateLive(ts))
        XCTAssertEqual(hls.absoluteString, "http://h:8080/live/jean/code%20secret/123.m3u8?token=a.b")
        XCTAssertEqual(PlaybackResolver.liveExtension(of: hls), "m3u8")
        XCTAssertEqual(PlaybackResolver.alternateLive(hls), ts)
        // Serveur installé dans un sous-dossier
        XCTAssertEqual(PlaybackResolver.alternateLive(try XCTUnwrap(URL(string: "https://h/iptv/live/u/p/7.M3U8")))?.absoluteString,
                       "https://h/iptv/live/u/p/7.ts")
        // Tout le reste est laissé tel quel : films, épisodes, playlists M3U quelconques
        for other in ["http://h/movie/u/p/5.mp4", "http://h/series/u/p/5.mkv", "http://cdn/x/y.m3u8",
                      "http://h/live/u/p/chaine.ts", "http://h/live/u/p/12", "http://h/u/p/12.ts"] {
            let url = try XCTUnwrap(URL(string: other))
            XCTAssertNil(PlaybackResolver.liveExtension(of: url), other)
            XCTAssertNil(PlaybackResolver.alternateLive(url), other)
        }
        // Le contenu rouvert garde son titre, sa fiche et son agent utilisateur
        let item = PlayItem(ref: ContentRef(id: "live:123", kind: .live, name: "TF1"), url: ts, title: "TF1", subtitle: "Journal",
                            artwork: nil, isLive: true, userAgent: "Essai", startAt: nil)
        let again = item.with(url: hls)
        XCTAssertEqual(again.url, hls)
        XCTAssertEqual(again.id, "live:123")
        XCTAssertEqual(again.title, "TF1")
        XCTAssertEqual(again.userAgent, "Essai")
        XCTAssertTrue(again.isLive)
    }

    func testResumePositionsAreCapped() {
        var lib = LibraryData()
        let start = Date(timeIntervalSince1970: 1_700_000_000)
        let total = LibraryData.positionsMax + 120
        for i in 0..<total {
            lib.savePosition(ContentRef(id: "vod:\(i)", kind: .movie, name: "Film \(i)"), position: 120, duration: 0,
                             now: start.addingTimeInterval(Double(i) * 60))
        }
        XCTAssertLessThanOrEqual(lib.positions.count, LibraryData.positionsMax)
        XCTAssertGreaterThanOrEqual(lib.positions.count, LibraryData.positionsMax - 60)
        XCTAssertNotNil(lib.resume("vod:\(total - 1)"), "la plus récente reste")
        XCTAssertNil(lib.resume("vod:0"), "la plus ancienne part")
    }

    /// Playlist et guide reçus d'un faux serveur : une page d'erreur ne remplace jamais ce qui marchait.
    func testPlaylistAndGuideSurviveServerErrors() async throws {
        let base = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("fluxtv-tests-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: base) }
        let answers = Answers()
        StubServer.respond { answers.answer($0) }
        let http = StubServer.makeHTTP()

        let stamp = DateFormatter()
        stamp.dateFormat = "yyyyMMddHHmmss"
        stamp.timeZone = TimeZone(secondsFromGMT: 0)
        stamp.locale = Locale(identifier: "en_US_POSIX")
        let now = Date()
        answers.guide = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><tv>" +
            "<programme start=\"\(stamp.string(from: now.addingTimeInterval(-1800))) +0000\" " +
            "stop=\"\(stamp.string(from: now.addingTimeInterval(1800))) +0000\" channel=\"TF1.fr\"><title>En cours</title></programme></tv>"
        answers.playlist = "\u{FEFF}#EXTM3U\n#EXTINF:-1 tvg-id=\"TF1.FR\",TF1\nhttp://serveur.test/live/1.m3u8|User-Agent=Essai\n"

        let source = Source.playlist(url: "http://serveur.test/liste.m3u", epg: "http://serveur.test/guide.xml")
        let first = Repository(source: source, http: http, cacheBase: base)
        let live = try await first.live()
        XCTAssertEqual(live.channels.map(\.url), ["http://serveur.test/live/1.m3u8"])
        XCTAssertEqual(live.channels.first?.userAgent, "Essai")

        // Guide : « TF1.FR » dans la playlist, « TF1.fr » dans le guide
        let channel = try XCTUnwrap(live.channels.first)
        let programmes = await first.programmes(for: channel)
        XCTAssertEqual(programmes.map(\.title), ["En cours"])

        // Le serveur répond par une page d'erreur : la playlist qui marche reste, maintenant…
        answers.playlist = "<html><body>Maintenance</body></html>"
        let forced = try await Repository(source: source, http: http, cacheBase: base).live(force: true)
        XCTAssertEqual(forced.channels.count, 1)
        // … et au lancement suivant (avant, la page d'erreur restait 12 h dans le cache)
        let later = try await Repository(source: source, http: http, cacheBase: base).live()
        XCTAssertEqual(later.channels.count, 1)
        // Serveur en panne : idem
        answers.status = 503
        let down = try await Repository(source: source, http: http, cacheBase: base).live(force: true)
        XCTAssertEqual(down.channels.count, 1)
        answers.status = 200

        // Le guide a plus de 6 h et le serveur répond n'importe quoi : celui qui marche reste
        let files = FileManager.default.enumerator(at: base, includingPropertiesForKeys: nil)?.compactMap { $0 as? URL } ?? []
        let guideFile = try XCTUnwrap(files.first { $0.lastPathComponent == "epg.xml" })
        try FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(-7 * 3600)], ofItemAtPath: guideFile.path)
        answers.guide = "<html><body>Maintenance</body></html>"
        let stale = Repository(source: source, http: http, cacheBase: base)
        let staleLive = try await stale.live()
        let kept = await stale.programmes(for: try XCTUnwrap(staleLive.channels.first))
        XCTAssertEqual(kept.map(\.title), ["En cours"])
        let touched = try XCTUnwrap(FileManager.default.attributesOfItem(atPath: guideFile.path)[.modificationDate] as? Date)
        XCTAssertGreaterThan(Date().timeIntervalSince(touched), 5 * 3600, "nouvel essai du guide bientôt, pas à chaque lancement")
        XCTAssertLessThan(Date().timeIntervalSince(touched), 6 * 3600)

        // Guide injoignable : le guide précédent sert encore (avant : guide vide)
        try FileManager.default.setAttributes([.modificationDate: Date().addingTimeInterval(-7 * 3600)], ofItemAtPath: guideFile.path)
        answers.guide = ""
        let offline = Repository(source: source, http: http, cacheBase: base)
        let offlineLive = try await offline.live()
        let still = await offline.programmes(for: try XCTUnwrap(offlineLive.channels.first))
        XCTAssertEqual(still.map(\.title), ["En cours"])

        // Aucune playlist connue et une page d'erreur : message clair, rien en cache
        let other = Source.playlist(url: "http://serveur.test/autre.m3u", epg: nil)
        for _ in 0..<2 {
            do {
                _ = try await Repository(source: other, http: http, cacheBase: base).live()
                XCTFail("une erreur était attendue")
            } catch let error as FluxError {
                XCTAssertEqual(error, FluxError.emptyPlaylist)
            }
        }
        // Le serveur se rétablit : la playlist arrive aussitôt
        answers.playlist = "#EXTM3U\n#EXTINF:-1,Une\nhttp://serveur.test/live/9.m3u8\n"
        let back = try await Repository(source: other, http: http, cacheBase: base).live()
        XCTAssertEqual(back.channels.count, 1)
    }
}

final class EnglishTests: XCTestCase {
    override func tearDown() {
        Tr.english = false
        super.tearDown()
    }

    func testEnglishTexts() throws {
        Tr.english = true
        XCTAssertEqual(Demo.channels[0].name, "Live test pattern")
        XCTAssertEqual(Demo.channels[0].logo, "asset:DemoLive1En")
        XCTAssertEqual(Demo.channels[2].logo, "asset:DemoLive3")
        XCTAssertEqual(Demo.movies[0].poster, "asset:DemoPosterTosEn")
        XCTAssertEqual(Demo.series[0].cover, "asset:DemoPosterOpenEn")
        XCTAssertEqual(Demo.liveCategory.name, "Demo · Live")
        XCTAssertEqual(Demo.programmes(for: "demo:live1").first?.desc, "Demo program.")
        XCTAssertEqual(FluxError.offline.errorDescription, "No internet connection.")
        XCTAssertEqual(FluxError.http(404).errorDescription, "Address not found on the server (code 404).")
        XCTAssertEqual(M3U.parse("#EXTM3U\n#EXTINF:-1,Chaîne\nhttp://h/1.ts\n").entries.first?.group, "Other")
        XCTAssertEqual(Source.demo.displayName, "Demo")

        Tr.english = false
        XCTAssertEqual(Demo.channels[0].name, "Mire en direct")
        XCTAssertEqual(Demo.channels[0].logo, "asset:DemoLive1")
        XCTAssertEqual(FluxError.offline.errorDescription, "Pas de connexion internet.")
        XCTAssertEqual(M3U.parse("#EXTM3U\n#EXTINF:-1,Chaîne\nhttp://h/1.ts\n").entries.first?.group, "Autres")
    }
}
