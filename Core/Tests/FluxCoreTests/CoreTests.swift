import XCTest
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
        XCTAssertEqual(map.keys.sorted(), ["TF1.fr"])
        XCTAssertEqual(map["TF1.fr"]?.map(\.title), ["Le 18 h", "Jeu & Quiz"])
        XCTAssertEqual(map["TF1.fr"]?.first?.desc, "Infos.")
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
