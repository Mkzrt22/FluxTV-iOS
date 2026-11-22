using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluxTV.Core;

namespace FluxTV.Tests
{
    /// <summary>Petit lanceur de tests sans dépendance : chaque méthode « Test… » est exécutée.</summary>
    static class Program
    {
        static int Main()
        {
            int ok = 0, failed = 0;
            foreach (var m in typeof(CoreTests).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name.StartsWith("Test")))
            {
                try
                {
                    var result = m.Invoke(null, null);
                    if (result is Task t) t.GetAwaiter().GetResult();
                    ok++;
                    Console.WriteLine("✔ " + m.Name);
                }
                catch (Exception e)
                {
                    failed++;
                    var inner = e is TargetInvocationException tie ? tie.InnerException : e;
                    Console.WriteLine("✘ " + m.Name + " : " + inner?.Message);
                }
            }
            Console.WriteLine($"{ok + failed} tests, {failed} échec(s)");
            return failed == 0 ? 0 : 1;
        }
    }

    static class Assert
    {
        public static void Equal<T>(T expected, T actual, string what = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"{what ?? "valeur"} : attendu « {expected} », obtenu « {actual} »");
        }

        public static void Seq<T>(IEnumerable<T> expected, IEnumerable<T> actual, string what = null)
        {
            var e = expected.ToList();
            var a = actual?.ToList() ?? new List<T>();
            if (!e.SequenceEqual(a))
                throw new Exception($"{what ?? "liste"} : attendu [{string.Join(", ", e)}], obtenu [{string.Join(", ", a)}]");
        }

        public static void True(bool v, string what) { if (!v) throw new Exception(what + " : attendu vrai"); }
        public static void False(bool v, string what) { if (v) throw new Exception(what + " : attendu faux"); }
        public static void Null(object v, string what) { if (v != null) throw new Exception(what + " : attendu null, obtenu « " + v + " »"); }

        public static void Throws(Action a, string what)
        {
            try { a(); }
            catch (Exception) { return; }
            throw new Exception(what + " : une erreur était attendue");
        }
    }

    public static class CoreTests
    {
        static string Fixture(string name) =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), Encoding.UTF8);

        static byte[] FixtureBytes(string name) =>
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

        // ───────────────────────────── Noms ─────────────────────────────

        public static void TestChannelNames()
        {
            Assert.Equal("TF1", TextTools.CleanChannel("FR| TF1 FHD").Name);
            Assert.Seq(new[] { "FHD" }, TextTools.CleanChannel("FR| TF1 FHD").Badges);
            Assert.Equal("France 2", TextTools.CleanChannel("FR: France 2 HD").Name);
            Assert.Equal("CANAL+ SPORT", TextTools.CleanChannel("|FR| CANAL+ SPORT 4K").Name);
            Assert.Seq(new[] { "4K" }, TextTools.CleanChannel("|FR| CANAL+ SPORT 4K").Badges);
            Assert.Equal("M6", TextTools.CleanChannel("[FR] M6 ᴴᴰ").Name);
            Assert.Seq(new[] { "HD" }, TextTools.CleanChannel("[FR] M6 ᴴᴰ").Badges);
            Assert.Equal("Arte", TextTools.CleanChannel("FR - Arte").Name);
            Assert.Equal("TF1", TextTools.CleanChannel("TF1").Name);
            Assert.Equal("RMC: Story", TextTools.CleanChannel("RMC: Story").Name, "un nom avec « : » reste entier");
            Assert.Equal("HD1", TextTools.CleanChannel("HD1").Name);
            Assert.Equal("TV5 Monde", TextTools.CleanChannel("TV5 Monde").Name);
            Assert.Equal("BFM TV", TextTools.CleanChannel("FR ▎BFM TV HEVC").Name);
            Assert.Seq(new[] { "HEVC" }, TextTools.CleanChannel("FR ▎BFM TV HEVC").Badges);
            Assert.Equal("FHD", TextTools.CleanChannel("FHD").Name, "jamais de nom vide");
        }

        public static void TestTitles()
        {
            var dune = TextTools.CleanTitle("FR - Dune (2021) [MULTI]");
            Assert.Equal("Dune", dune.Name);
            Assert.Equal(2021, dune.Year);
            Assert.Seq(new[] { "MULTI" }, dune.Badges);
            Assert.Equal("Oppenheimer", TextTools.CleanTitle("Oppenheimer 4K").Name);
            Assert.Seq(new[] { "4K" }, TextTools.CleanTitle("Oppenheimer 4K").Badges);
            Assert.Equal("Blade Runner 2049", TextTools.CleanTitle("Blade Runner 2049").Name);
            Assert.Null(TextTools.CleanTitle("Blade Runner 2049").Year, "année");
            Assert.Equal("1917", TextTools.CleanTitle("1917 (2019)").Name);
            Assert.Equal(2019, TextTools.CleanTitle("1917 (2019)").Year);
            Assert.Equal("Le Parrain", TextTools.CleanTitle("Le Parrain - 1972").Name);
            Assert.Equal(1972, TextTools.CleanTitle("Le Parrain - 1972").Year);
            Assert.Seq(new[] { "VOSTFR" }, TextTools.CleanTitle("Amélie (VOSTFR)").Badges);
            Assert.Equal("Mission: Impossible", TextTools.CleanTitle("Mission: Impossible").Name);
        }

        public static void TestCategories()
        {
            Assert.Equal("Sports", TextTools.CleanCategory("FR | SPORTS"));
            Assert.Equal("Généralistes", TextTools.CleanCategory("FR| GÉNÉRALISTES"));
            Assert.Equal("Cinéma et Séries", TextTools.CleanCategory("|FR| CINÉMA ET SÉRIES"));
            Assert.Equal("RMC Sport", TextTools.CleanCategory("RMC SPORT"));
            Assert.Equal("Les Films", TextTools.CleanCategory("LES FILMS"));
            Assert.Equal("Documentaires", TextTools.CleanCategory("Documentaires"));
            Assert.Equal("TNT", TextTools.CleanCategory("TNT"));
            Assert.Equal("Action", TextTools.CleanCategory("VOD - ACTION"));
        }

        public static void TestAdultAndSearch()
        {
            Assert.True(TextTools.IsAdult("XXX ADULTES"), "XXX ADULTES");
            Assert.True(TextTools.IsAdult("Adult 18+"), "Adult 18+");
            Assert.False(TextTools.IsAdult("Sports"), "Sports");
            Assert.False(TextTools.IsAdult("Essex TV"), "Essex TV");
            Assert.Equal("les miserables", TextTools.Fold("Les Misérables"));
            Assert.True(TextTools.Matches("Les Misérables", TextTools.QueryWords("misera les")), "recherche");
            Assert.False(TextTools.Matches("Les Misérables", TextTools.QueryWords("dune")), "recherche négative");
            Assert.True(TextTools.FrenchCompare("Eau", "Été") < 0, "Eau < Été");
            Assert.True(TextTools.FrenchCompare("Été", "Fargo") < 0, "Été < Fargo");
            Assert.True(TextTools.FrenchCompare("Épisode 2", "Épisode 10") < 0, "Épisode 2 < Épisode 10");
        }

        // ───────────────────────────── Xtream ─────────────────────────────

        public static void TestServerNormalization()
        {
            Assert.Equal("http://srv.example:8080", XtreamAccount.NormalizeServer("srv.example:8080"));
            Assert.Equal("http://srv.example:8080", XtreamAccount.NormalizeServer("http://srv.example:8080/"));
            Assert.Equal("https://srv.example", XtreamAccount.NormalizeServer("https://srv.example/player_api.php?username=a"));
            Assert.Equal("http://srv.example:80/iptv", XtreamAccount.NormalizeServer(" http://srv.example:80/iptv/get.php "));
        }

        public static void TestPlaylistLinkBecomesAccount()
        {
            var a = XtreamAccount.FromPlaylistUrl("http://srv.example:8080/get.php?username=kev&password=s3cr%2Bt&type=m3u_plus&output=ts");
            Assert.True(a != null, "compte reconnu");
            Assert.Equal("http://srv.example:8080", a.Server);
            Assert.Equal("kev", a.Username);
            Assert.Equal("s3cr+t", a.Password);
            Assert.Null(XtreamAccount.FromPlaylistUrl("http://cdn.example/list.m3u"), "playlist ordinaire");
        }

        public static void TestUrls()
        {
            var a = new XtreamAccount("http://srv.example:8080", "kev in", "p+w/d");
            Assert.Equal("http://srv.example:8080/live/kev%20in/p%2Bw%2Fd/101.ts", a.LiveUrl("101", "ts"));
            Assert.Equal("http://srv.example:8080/movie/kev%20in/p%2Bw%2Fd/5001.mkv", a.MovieUrl("5001", "mkv"));
            Assert.Equal("http://srv.example:8080/series/kev%20in/p%2Bw%2Fd/801.mkv", a.EpisodeUrl("801", ""));
            var api = a.ApiUrl("get_vod_streams", ("category_id", "20"));
            Assert.True(api.StartsWith("http://srv.example:8080/player_api.php?username=kev%20in&password=p%2Bw%2Fd&action=get_vod_streams"), api);
            Assert.True(api.EndsWith("&category_id=20"), api);
            var ts = a.TimeshiftUrl("101", DateTimeOffset.FromUnixTimeSeconds(1760000000), 60, TimeSpan.FromHours(2));
            Assert.Equal("http://srv.example:8080/timeshift/kev%20in/p%2Bw%2Fd/60/2025-10-09:10-53/101.ts", ts);
        }

        public static void TestAccount()
        {
            var info = XtreamParse.Account(Fixture("account.json"));
            Assert.True(info.IsActive, "compte actif");
            Assert.Equal(1, info.MaxConnections);
            Assert.Equal("Europe/Paris", info.ServerTimeZone);
            Assert.Equal("m3u8", info.LiveExtension, "HLS autorisé : préféré sur Xbox");
            Assert.Equal(10, info.DaysLeft(DateTimeOffset.FromUnixTimeSeconds(1893456000 - 86400 * 10)));
            Assert.Throws(() => XtreamParse.Account("[]"), "réponse inattendue");
        }

        public static void TestCategoriesAndLive()
        {
            var cats = XtreamParse.Categories(Fixture("live_categories.json"));
            Assert.Seq(new[] { "10", "11", "12" }, cats.Select(c => c.Id));
            Assert.Seq(new[] { "Généralistes", "Sports", "XXX Adultes" }, cats.Select(c => c.Name));
            Assert.Seq(new[] { false, false, true }, cats.Select(c => c.IsAdult));

            var live = XtreamParse.LiveStreams(Fixture("live_streams.json"));
            Assert.Seq(new[] { "live:101", "live:102", "live:103", "live:104" }, live.Select(c => c.Id));
            Assert.Equal("TF1", live[0].Name);
            Assert.Seq(new[] { "FHD" }, live[0].Badges);
            Assert.Equal(7, live[0].ArchiveDays);
            Assert.Equal("TF1.fr", live[0].TvgId);
            Assert.Equal(2, live[1].Num);
            Assert.Equal("France 2", live[1].Name);
            Assert.Null(live[1].Logo, "logo vide");
            Assert.Null(live[1].TvgId, "tvg-id vide");
            Assert.Equal("10", live[1].CategoryId);
            Assert.Equal("CANAL+ SPORT", live[2].Name);
            Assert.Equal("BeIN SPORTS 1", live[3].Name);
        }

        public static void TestMovies()
        {
            var m = XtreamParse.VodStreams(Fixture("vod_streams.json"));
            Assert.Equal(3, m.Count);
            Assert.Equal("Dune", m[0].Name);
            Assert.Equal(2021, m[0].Year);
            Assert.Equal("mkv", m[0].Ext);
            Assert.Equal(7.9, m[0].Rating);
            Assert.Equal(2023, m[1].Year);
            Assert.Null(m[1].Poster, "affiche vide");
            Assert.Equal("mp4", m[1].Ext);
            Assert.Equal(8.3, m[1].Rating);
            Assert.Null(m[2].Rating, "note");
            Assert.Equal("mp4", m[2].Ext);
            Assert.Equal("Blade Runner 2049", m[2].Name);
        }

        public static void TestSeriesInfoObject()
        {
            var d = XtreamParse.SeriesInfo(Fixture("series_info_object.json"));
            Assert.Seq(new[] { 1, 2 }, d.SeasonNumbers);
            Assert.Seq(new[] { "Le Phare", "Marée basse" }, d.Seasons[1].Select(e => e.Title));
            Assert.Seq(new[] { 1, 2 }, d.Seasons[1].Select(e => e.Number));
            Assert.Equal("Retour", d.Seasons[2][0].Title);
            Assert.Equal(2700, d.Seasons[2][0].DurationSecs);
            Assert.Equal(2650, d.Seasons[1].Last().DurationSecs);
            Assert.Equal("http://img.example/b1.jpg", d.Backdrop);
            Assert.Equal(2023, d.Year);
            Assert.Equal(8.2, d.Rating);
            Assert.Seq(new[] { "ep:801", "ep:802", "ep:902" }, d.AllEpisodes.Select(e => e.Id));
        }

        public static void TestSeriesInfoArray()
        {
            var d = XtreamParse.SeriesInfo(Fixture("series_info_array.json"));
            Assert.Seq(new[] { 1, 2 }, d.SeasonNumbers);
            Assert.Equal(1, d.Seasons[2][0].Number);
            Assert.Equal("http://img.example/x.jpg", d.Backdrop);
        }

        public static void TestShortEpg()
        {
            var list = XtreamParse.EpgListings(Fixture("short_epg.json"));
            Assert.Seq(new[] { "Le Journal de 20 h", "Film" }, list.Select(p => p.Title));
            Assert.Equal("L'actualité", list[0].Desc);
            Assert.True(list[0].HasArchive, "replay");
            var nn = Programme.NowNext(list, DateTimeOffset.FromUnixTimeSeconds(1760001000));
            Assert.Equal("Le Journal de 20 h", nn.Now?.Title);
            Assert.Equal("Film", nn.Next?.Title);
            Assert.True(Math.Abs(list[0].Progress(DateTimeOffset.FromUnixTimeSeconds(1760001800)) - 0.5) < 0.001, "avancement");
            var before = Programme.NowNext(list, DateTimeOffset.FromUnixTimeSeconds(1759990000));
            Assert.Null(before.Now, "rien en cours");
            Assert.Equal("Le Journal de 20 h", before.Next?.Title);
        }

        public static void TestHelpers()
        {
            Assert.Equal(6730, XtreamParse.ParseDuration("01:52:10"));
            Assert.Equal(6720, XtreamParse.ParseDuration("112 min"));
            Assert.Equal(6730, XtreamParse.ParseDuration("6730"));
            Assert.Equal("dQw4w9WgXcQ", XtreamParse.YoutubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
            Assert.Equal("dQw4w9WgXcQ", XtreamParse.YoutubeId("dQw4w9WgXcQ"));
            Assert.Equal("Les Disparus", XtreamParse.CleanEpisodeTitle("Brumes - S01E03 - Les Disparus"));
        }

        // ───────────────────────────── JSON ─────────────────────────────

        public static void TestJson()
        {
            var o = Json.ParseObject("{\"a\":\"x\\u00e9\\n\",\"b\":12,\"c\":1.5,\"d\":null,\"e\":true,\"f\":[1,\"2\"],\"g\":{\"h\":\"\"},\"i\":\"null\"}");
            Assert.Equal("xé", o.Str("a"));
            Assert.Equal(12, o.Int("b"));
            Assert.Equal("12", o.Str("b"));
            Assert.Equal(1.5, o.Dbl("c"));
            Assert.Null(o.Str("d"), "null");
            Assert.True(o.Flag("e"), "true");
            Assert.Equal(2, o.Arr("f").Count);
            Assert.Null(o.Obj("g").Str("h"), "texte vide");
            Assert.Null(o.Str("i"), "\"null\"");
            Assert.Null(Json.Parse("{\"a\":"), "JSON tronqué");
            var count = 0;
            Json.EachObject("[{\"x\":1},{\"x\":2},3,{\"x\":4}]", _ => count++);
            Assert.Equal(3, count);
            Assert.Equal("\"a\\\"b\\\\c\\n\"", Json.Quote("a\"b\\c\n"));
        }

        // ───────────────────────────── M3U ─────────────────────────────

        public static void TestM3UParse()
        {
            var (entries, epg) = M3U.Parse(Fixture("playlist.m3u"));
            Assert.Equal("http://epg.example/guide.xml.gz", epg);
            Assert.Equal(8, entries.Count);
            Assert.Equal("Canal+ Sport, HD", entries[1].Name);
            Assert.Equal("MonLecteur/1.0", entries[1].UserAgent);
            Assert.Equal("Autres", entries[7].Group);
        }

        public static void TestM3UCatalog()
        {
            var cat = M3U.Catalog(Fixture("playlist.m3u"));
            Assert.Seq(new[] { "live:101", "live:202", cat.Live.Last().Id }, cat.Live.Select(c => c.Id));
            Assert.Equal("TF1", cat.Live[0].Name);
            Assert.Seq(new[] { "HD" }, cat.Live[0].Badges);
            Assert.Seq(new[] { "Généralistes", "Sport", "Autres" }, cat.LiveCategories.Select(c => c.Name));
            Assert.Equal("vod:5001", cat.Movies[0].Id);
            Assert.Equal(2, cat.Movies.Count);
            Assert.Equal("mp4", cat.Movies[1].Ext);
            Assert.Equal(2, cat.Series.Count);
            var brumes = cat.Series[0];
            Assert.Equal("Brumes", brumes.Name);
            var d = M3U.Details(brumes);
            Assert.Seq(new[] { 1, 2 }, d.AllEpisodes.Select(e => e.Number));
            Assert.Equal("Marée basse", d.AllEpisodes.Last().Title);
            Assert.Equal("Autre Série", cat.Series[1].Name);
        }

        public static void TestAttributes()
        {
            var a = M3U.Attributes("#EXTINF:-1 tvg-id=\"a=b\" tvg-logo=\"http://x/y.png\" group-title=\"\",Nom");
            Assert.Equal("a=b", a["tvg-id"]);
            Assert.Equal("http://x/y.png", a["tvg-logo"]);
            Assert.Equal("", a["group-title"]);
        }

        public static void TestIds()
        {
            Assert.Equal("live:123", Ids.FromUrl("http://h:8080/live/u/p/123.ts"));
            Assert.Equal("vod:456", Ids.FromUrl("http://h:8080/movie/u/p/456.mkv?x=1"));
            Assert.Equal("ep:789", Ids.FromUrl("http://h:8080/series/u/p/789.mp4"));
            Assert.Equal("live:321", Ids.FromUrl("http://h:8080/u/p/321"));
            Assert.True(Ids.FromUrl("http://cdn/x/y.m3u8").StartsWith("u:"), "empreinte");
            Assert.Equal(Ids.FromUrl("http://cdn/x/y.m3u8"), Ids.FromUrl("http://cdn/x/y.m3u8"));
            Assert.Equal("456", Ids.NumericPart("vod:456"));
            Assert.Null(Ids.NumericPart("u:abc"), "pas numérique");
        }

        // ───────────────────────────── Guide XMLTV ─────────────────────────────

        public static void TestXmlTv()
        {
            var now = XmlTv.Date("20261022183000 +0200").Value;
            var map = XmlTv.Parse(FixtureBytes("guide.xml"), new HashSet<string> { "TF1.fr" }, now);
            Assert.Seq(new[] { "tf1.fr" }, map.Keys.OrderBy(k => k));
            Assert.Seq(new[] { "Le 18 h", "Jeu & Quiz" }, map["tf1.fr"].Select(p => p.Title));
            Assert.Equal("Infos.", map["tf1.fr"][0].Desc);
            Assert.Equal(XmlTv.Date("20261022160000 +0000"), XmlTv.Date("20261022180000 +0200"));
            Assert.Equal(XmlTv.Date("20261022160000 +0000"), XmlTv.Date("20261022160000"));
            Assert.Null(XmlTv.Date("2026"), "date incomplète");
        }

        // ───────────────────────────── Bibliothèque ─────────────────────────────

        static readonly ContentRef MovieRef = new ContentRef { Id = "vod:1", Kind = ContentKind.Movie, Name = "Dune" };
        static readonly ContentRef LiveRef = new ContentRef { Id = "live:1", Kind = ContentKind.Live, Name = "TF1" };

        public static void TestFavorites()
        {
            var lib = new LibraryData();
            Assert.True(lib.ToggleFavorite(MovieRef), "ajouté");
            Assert.True(lib.IsFavorite("vod:1"), "favori");
            lib.ToggleFavorite(LiveRef);
            Assert.False(lib.ToggleFavorite(MovieRef), "retiré");
            Assert.Equal(1, lib.FavoritesOf(ContentKind.Live).Count);
        }

        public static void TestResumeAndWatched()
        {
            var lib = new LibraryData();
            var now = DateTimeOffset.UtcNow;
            lib.AddHistory(MovieRef);
            Assert.False(lib.SavePosition(MovieRef, 10, 6000, now), "pas fini");
            Assert.Null(lib.Resume("vod:1"), "pas de reprise avant 15 s");
            lib.SavePosition(MovieRef, 1200, 6000, now);
            Assert.Equal(1200.0, lib.Resume("vod:1")?.Position);
            Assert.Seq(new[] { "vod:1" }, lib.ContinueWatching().Select(r => r.Id));
            Assert.True(lib.SavePosition(MovieRef, 5800, 6000, now), "fini");
            Assert.True(lib.IsWatched("vod:1"), "vu");
            Assert.Null(lib.Resume("vod:1"), "reprise effacée");
            Assert.Equal(0, lib.ContinueWatching().Count);
            Assert.False(lib.SavePosition(LiveRef, 500, 0, now), "pas de reprise du direct");
        }

        public static void TestHistoryKeepsOneEpisodePerSeries()
        {
            var lib = new LibraryData();
            lib.AddHistory(new ContentRef { Id = "ep:1", Kind = ContentKind.Episode, Name = "S01E01", SeriesId = "9" });
            lib.AddHistory(LiveRef);
            lib.AddHistory(new ContentRef { Id = "ep:2", Kind = ContentKind.Episode, Name = "S01E02", SeriesId = "9" });
            Assert.Seq(new[] { "ep:2", "live:1" }, lib.History.Select(h => h.Id));
            Assert.Equal("live:1", lib.LastLiveId);
            Assert.Seq(new[] { "live:1" }, lib.RecentLive().Select(h => h.Id));
        }

        public static void TestNextEpisode()
        {
            var eps = Enumerable.Range(1, 3).Select(i => new Episode("ep:" + i, "" + i, "E" + i, 1, i, "mkv", null, null, null, null)).ToList();
            var lib = new LibraryData();
            Assert.Equal("ep:1", lib.NextEpisode(eps)?.Episode.Id);
            lib.MarkWatched("ep:1");
            Assert.Equal("ep:2", lib.NextEpisode(eps)?.Episode.Id);
            lib.SavePosition(new ContentRef { Id = "ep:3", Kind = ContentKind.Episode, Name = "E3" }, 300, 2400, DateTimeOffset.UtcNow);
            Assert.Equal("ep:3", lib.NextEpisode(eps)?.Episode.Id);
            Assert.Equal(300.0, lib.NextEpisode(eps)?.Resume?.Position);
        }

        public static void TestLibraryFileRoundTrip()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var file = new LibraryFile(Path.Combine(dir, "library.json"));
            var lib = new LibraryData();
            lib.ToggleFavorite(MovieRef);
            lib.AddHistory(new ContentRef { Id = "ep:7", Kind = ContentKind.Episode, Name = "S01E07 · « Titre »", SeriesId = "9", Season = 1, Episode = 7 });
            lib.SavePosition(MovieRef, 600, 6000, DateTimeOffset.FromUnixTimeSeconds(1760000000));
            lib.MarkWatched("ep:1");
            file.Save(lib);
            var back = file.Load();
            Assert.Equal(lib.ToJson(), back.ToJson(), "relu à l'identique");
            Assert.Equal(7, back.History[0].Episode);
            Assert.Equal(ContentKind.Episode, back.History[0].Kind);
            Directory.Delete(dir, true);
        }

        // ───────────────────────────── Sources, lecture, démo ─────────────────────────────

        public static void TestPlaybackUrlsAndSources()
        {
            var source = Source.Xtream(new XtreamAccount("http://h:8080", "u", "p"));
            var ep = new ContentRef { Id = "ep:801", Kind = ContentKind.Episode, Name = "x", Ext = "mp4" };
            Assert.Equal("http://h:8080/series/u/p/801.mp4", PlaybackResolver.Url(ep, source));
            var live = new ContentRef { Id = "live:101", Kind = ContentKind.Live, Name = "TF1" };
            Assert.Equal("http://h:8080/live/u/p/101.m3u8", PlaybackResolver.Url(live, source));
            Assert.Equal("http://h:8080/live/u/p/101.ts", PlaybackResolver.Url(live, source, "ts"));
            var m3u = new ContentRef { Id = "u:1", Kind = ContentKind.Live, Name = "x", Url = "http://cdn/x.m3u8" };
            Assert.Equal("http://cdn/x.m3u8", PlaybackResolver.Url(m3u, Source.Demo));
            Assert.True(Source.Demo.CacheKey != source.CacheKey, "caches séparés");
            Assert.False(source.CacheKey.Contains("u"), "aucun identifiant dans le nom du cache");
            Assert.True(Source.FromJson(source.ToJson()).Equals(source), "source relue");
            Assert.True(Source.FromJson(Source.Playlist("http://x/l.m3u", "").ToJson()).Equals(Source.Playlist("http://x/l.m3u", null)), "playlist relue");
            Assert.True(ReferenceEquals(Source.FromJson("{\"kind\":\"demo\"}"), Source.Demo), "démo relue");
        }

        public static void TestFavoriteSeriesKeepsItsId()
        {
            var xtream = new Series("42", "FR - Dark", null, null, null, null, "1", null);
            Assert.Equal("ser:42", xtream.Id);
            var restored = Series.FromRef(xtream.Ref("Séries"));
            Assert.Equal("ser:42", restored.Id);
            Assert.Equal("42", restored.SeriesId);
            Assert.Equal("Dark", restored.Name);
            var demo = Demo.SeriesList[0];
            Assert.Equal(demo.Id, Series.FromRef(demo.Ref(null)).Id);
        }

        public static async Task TestRepositoryDemo()
        {
            var repo = new Repository(Source.Demo, Path.GetTempPath());
            var live = await repo.LiveAsync();
            Assert.Equal(3, live.Channels.Count);
            Assert.True(live.Channels.TrueForAll(c => !c.Url.Contains("apple.com")), "démo sans flux Apple");
            var cats = await repo.VodCategoriesAsync();
            var movies = await repo.MoviesAsync(cats[0].Id);
            Assert.Equal(2, movies.Count);
            var info = await repo.MovieInfoAsync(movies[0]);
            Assert.True(info?.Plot != null, "résumé");
            var series = await repo.AllSeriesAsync();
            var d = await repo.SeriesDetailsAsync(series[0]);
            Assert.Equal(2, d.AllEpisodes.Count);
            var p = await repo.ProgrammesAsync(live.Channels[0]);
            Assert.True(Programme.NowNext(p, DateTimeOffset.UtcNow).Now != null, "programme en cours");
        }

        // ───────────────────────────── Robustesse (1.3.1) ─────────────────────────────

        static byte[] Gzipped(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
                    zip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "fluxtv-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void TestM3UKodiOptionsAndBom()
        {
            var (url, ua) = M3U.SplitUrlOptions("http://cdn.example/a.m3u8|User-Agent=VLC%2F3.0&Referer=http://x/");
            Assert.Equal("http://cdn.example/a.m3u8", url);
            Assert.Equal("VLC/3.0", ua);
            Assert.Equal("http://cdn.example/a.m3u8", M3U.SplitUrlOptions("http://cdn.example/a.m3u8|Referer=http://x/").Url);
            Assert.Null(M3U.SplitUrlOptions("http://cdn.example/a.m3u8|Referer=http://x/").UserAgent, "pas d'agent demandé");
            // Une barre qui n'annonce pas d'options reste dans l'adresse
            Assert.Equal("http://cdn.example/a|b.m3u8", M3U.SplitUrlOptions("http://cdn.example/a|b.m3u8").Url);
            Assert.Equal("rtmp://x/y|z=1", M3U.SplitUrlOptions("rtmp://x/y|z=1").Url);

            var text = "\uFEFF#EXTM3U x-tvg-url=\"http://epg.example/g.xml\"\r\n" +
                       "#EXTINF:-1 tvg-id=\"A\",Une\r\nhttp://cdn.example/1.m3u8|User-Agent=Kodi+Lecteur\r\n" +
                       "#EXTINF:-1,Deux\n#EXTVLCOPT:http-user-agent=VLC\nhttp://cdn.example/2.m3u8|User-Agent=Autre\n";
            var (entries, epg) = M3U.Parse(text);
            Assert.Equal("http://epg.example/g.xml", epg, "guide annoncé malgré le BOM");
            Assert.Seq(new[] { "http://cdn.example/1.m3u8", "http://cdn.example/2.m3u8" }, entries.Select(e => e.Url));
            // #EXTVLCOPT garde la priorité sur l'option collée à l'adresse
            Assert.Seq(new[] { "Kodi Lecteur", "VLC" }, entries.Select(e => e.UserAgent));
            // Même résultat quand la playlist est lue en flux
            using (var reader = new StringReader(text))
                Assert.Equal(2, M3U.Catalog(reader).Live.Count);
        }

        public static void TestPlaylistFileGzipAndBom()
        {
            var dir = TempDir();
            try
            {
                var text = "#EXTM3U\n#EXTINF:-1 group-title=\"Été\",Chaîne Une\nhttp://cdn.example/1.m3u8\n";
                var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
                var plain = Path.Combine(dir, "a.m3u");
                var packed = Path.Combine(dir, "b.m3u");
                File.WriteAllBytes(plain, withBom);
                File.WriteAllBytes(packed, Gzipped(withBom));
                foreach (var file in new[] { plain, packed })
                    using (var reader = Gzip.OpenText(file))
                    {
                        var cat = M3U.Catalog(reader);
                        Assert.Equal("Chaîne Une", cat.Live.Single().Name);
                        Assert.Equal("Été", cat.LiveCategories.Single().Name);
                    }
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void TestXmlTvIdsIgnoreCaseAndStreams()
        {
            var now = XmlTv.Date("20261022183000 +0200").Value;
            // « tf1.FR » dans la playlist, « TF1.fr » dans le guide : la même chaîne
            var wanted = new HashSet<string> { " tf1.FR " };
            var map = XmlTv.Parse(FixtureBytes("guide.xml"), wanted, now);
            Assert.Seq(new[] { "tf1.fr" }, map.Keys);
            Assert.Equal(2, map[XmlTv.Key("TF1.fr")].Count);
            Assert.Equal(2, XmlTv.Parse(Gzipped(FixtureBytes("guide.xml")), wanted, now)["tf1.fr"].Count);

            var dir = TempDir();
            try
            {
                var gz = Path.Combine(dir, "guide.xml.gz");
                File.WriteAllBytes(gz, Gzipped(FixtureBytes("guide.xml")));
                Assert.Equal(2, XmlTv.ParseFile(gz, wanted, now)["tf1.fr"].Count);
                var plain = Path.Combine(dir, "guide.xml");
                File.WriteAllBytes(plain, FixtureBytes("guide.xml"));
                Assert.Equal(2, XmlTv.ParseFile(plain, wanted, now)["tf1.fr"].Count);
                // Fichier coupé en route ou absent : pas d'erreur, ce qui a été lu est gardé
                var all = File.ReadAllBytes(gz);
                var cut = Path.Combine(dir, "coupe.xml.gz");
                File.WriteAllBytes(cut, all.Take(all.Length / 2).ToArray());
                XmlTv.ParseFile(cut, wanted, now);
                Assert.Equal(0, XmlTv.ParseFile(Path.Combine(dir, "absent.xml"), wanted, now).Count);
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void TestJsonFalseMeansNothing()
        {
            var o = Json.ParseObject("{\"a\":false,\"b\":true,\"n\":0,\"t\":\"x\"}");
            Assert.Null(o.Str("a"), "false");
            Assert.Equal("1", o.Str("b"));
            Assert.Equal("0", o.Str("n"));
            Assert.False(o.Flag("a"), "drapeau faux");
            Assert.True(o.Flag("b"), "drapeau vrai");
            // Panels qui envoient « false » à la place d'un logo ou d'un identifiant de guide
            var live = XtreamParse.LiveStreams("[{\"stream_id\":5,\"name\":\"Une\",\"stream_icon\":false,\"epg_channel_id\":false,\"tv_archive\":false,\"num\":false}]");
            Assert.Equal("Une", live.Single().Name);
            Assert.Null(live[0].Logo, "logo");
            Assert.Null(live[0].TvgId, "identifiant du guide");
            Assert.Equal(0, live[0].ArchiveDays);
            Assert.Equal(0, live[0].Num);
        }

        public static void TestResumePositionsAreCapped()
        {
            var lib = new LibraryData();
            var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            int total = LibraryData.PositionsMax + 120;
            for (int i = 0; i < total; i++)
                lib.SavePosition(new ContentRef { Id = "vod:" + i, Kind = ContentKind.Movie, Name = "Film " + i }, 120, 0, t0.AddMinutes(i));
            Assert.True(lib.Positions.Count <= LibraryData.PositionsMax, "plafond respecté");
            Assert.True(lib.Positions.Count >= LibraryData.PositionsMax - 60, "pas de purge excessive");
            Assert.True(lib.Resume("vod:" + (total - 1)) != null, "la plus récente reste");
            Assert.Null(lib.Resume("vod:0"), "la plus ancienne part");
        }

        /// <summary>
        /// Playlist et guide servis par un petit serveur local : téléchargement vers le disque, lecture en
        /// flux, et surtout une page d'erreur qui ne remplace jamais une playlist qui marche.
        /// </summary>
        public static async Task TestPlaylistAndGuideOverHttp()
        {
            int port;
            {
                var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                probe.Start();
                port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
            }
            var prefix = "http://127.0.0.1:" + port + "/";
            var listener = new System.Net.HttpListener();
            listener.Prefixes.Add(prefix);
            try { listener.Start(); }
            catch (System.Net.HttpListenerException e)
            {
                Console.WriteLine("  (serveur local indisponible ici, essai ignoré : " + e.Message + ")");
                return;
            }
            var now = DateTimeOffset.UtcNow;
            string Stamp(DateTimeOffset d) => d.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + " +0000";
            var guide = Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><tv>" +
                "<programme start=\"" + Stamp(now.AddMinutes(-30)) + "\" stop=\"" + Stamp(now.AddMinutes(30)) + "\" channel=\"TF1.fr\"><title>En cours</title></programme>" +
                "</tv>");
            string playlistBody = "\uFEFF#EXTM3U\n#EXTINF:-1 tvg-id=\"TF1.FR\",TF1\n" + prefix + "live/1.m3u8|User-Agent=Essai\n";
            int playlistStatus = 200;
            var serving = Task.Run(async () =>
            {
                while (true)
                {
                    System.Net.HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); }
                    catch (Exception) { return; }
                    try
                    {
                        byte[] body;
                        if (ctx.Request.Url.AbsolutePath == "/guide.xml.gz") body = Gzipped(guide);
                        else
                        {
                            ctx.Response.StatusCode = playlistStatus;
                            body = Encoding.UTF8.GetBytes(playlistBody);
                        }
                        ctx.Response.ContentLength64 = body.Length;
                        await ctx.Response.OutputStream.WriteAsync(body, 0, body.Length);
                        ctx.Response.Close();
                    }
                    catch (Exception) { }
                }
            });
            var dir = TempDir();
            try
            {
                var source = Source.Playlist(prefix + "liste.m3u", prefix + "guide.xml.gz");
                var live = await new Repository(source, dir).LiveAsync();
                Assert.Equal(prefix + "live/1.m3u8", live.Channels.Single().Url);
                Assert.Equal("Essai", live.Channels[0].UserAgent);

                // Guide : « TF1.FR » dans la playlist, « TF1.fr » dans le guide compressé
                var repo = new Repository(source, dir);
                var channel = (await repo.LiveAsync()).Channels[0];
                var programmes = await repo.ProgrammesAsync(channel);
                Assert.Equal("En cours", programmes.Single().Title);

                // Le guide a plus de 6 h et le serveur répond n'importe quoi : celui qui marche reste
                var guideFile = Directory.GetFiles(dir, "epg.xml", SearchOption.AllDirectories).Single();
                File.SetLastWriteTimeUtc(guideFile, DateTime.UtcNow.AddHours(-7));
                guide = Encoding.UTF8.GetBytes("<html><body>Maintenance</body></html>");
                var later = new Repository(source, dir);
                Assert.Equal("En cours", (await later.ProgrammesAsync((await later.LiveAsync()).Channels[0])).Single().Title);
                Assert.True(DateTime.UtcNow - File.GetLastWriteTimeUtc(guideFile) > TimeSpan.FromHours(5), "nouvel essai du guide bientôt");
                Assert.Equal(0, Directory.GetFiles(dir, "epg.new*", SearchOption.AllDirectories).Length);

                // Le serveur répond par une page d'erreur : la playlist qui marche reste, maintenant…
                playlistBody = "<html><body>Maintenance</body></html>";
                Assert.Equal(1, (await new Repository(source, dir).LiveAsync(true)).Channels.Count);
                // … et au lancement suivant (avant, la page d'erreur restait 12 h dans le cache)
                Assert.Equal(1, (await new Repository(source, dir).LiveAsync()).Channels.Count);
                // Serveur en panne : idem
                playlistStatus = 503;
                Assert.Equal(1, (await new Repository(source, dir).LiveAsync(true)).Channels.Count);

                // Aucune playlist connue et une page d'erreur : message clair, rien en cache
                playlistStatus = 200;
                var other = Source.Playlist(prefix + "autre.m3u", null);
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        await new Repository(other, dir).LiveAsync();
                        throw new Exception("une erreur était attendue");
                    }
                    catch (FluxException e) { Assert.Equal(FluxError.EmptyPlaylist, e.Kind); }
                }
                // Le serveur se rétablit : la playlist arrive aussitôt
                playlistBody = "#EXTM3U\n#EXTINF:-1,Une\n" + prefix + "live/9.m3u8\n";
                Assert.Equal(1, (await new Repository(other, dir).LiveAsync()).Channels.Count);
            }
            finally
            {
                listener.Stop();
                listener.Close();
                try { await serving; } catch (Exception) { }
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        public static void TestFormat()
        {
            Assert.Equal("1 h 58", Format.Duration(7080));
            Assert.Equal("42 min", Format.Duration(2520));
            Assert.Equal("Reprendre", Format.Resume(20));
            Assert.Equal("Reprendre à 4 min", Format.Resume(250));
            Assert.Equal("1:02:03", Format.Clock(3723));
            Assert.Equal("12:34", Format.Clock(754));
            Assert.Equal("★ 7,5", Format.Rating(7.5));
        }

        /// <summary>App en anglais : formats, messages et démonstration suivent la langue choisie.</summary>
        public static void TestRatePolicy()
        {
            var u = new RateUsage();
            var monday = new DateTime(2026, 11, 2, 20, 0, 0);
            // Premier jour : deux heures d'un coup ne suffisent pas, il faut revenir d'autres jours
            RatePolicy.Add(u, 7200, monday);
            RatePolicy.Add(u, 600, monday.AddHours(2));
            Assert.Equal(1, u.Days, "un seul jour compté");
            Assert.False(RatePolicy.ShouldAsk(u, 3600, false), "dès le premier jour");
            RatePolicy.Add(u, 900, monday.AddDays(1));
            Assert.False(RatePolicy.ShouldAsk(u, 3600, false), "deux jours seulement");
            RatePolicy.Add(u, 900, monday.AddDays(3));
            Assert.Equal(3, u.Days, "trois jours différents");
            Assert.True(RatePolicy.ShouldAsk(u, 900, false), "usage suffisant, lecture de 15 min");
            // Pas après une lecture courte ou ratée
            Assert.False(RatePolicy.ShouldAsk(u, 120, false), "lecture de 2 min");
            Assert.False(RatePolicy.ShouldAsk(u, 900, true), "lecture finie sur une erreur");
            // Une seule fois
            u.Asked = true;
            Assert.False(RatePolicy.ShouldAsk(u, 3600, false), "déjà proposé");
            // Pas assez d'heures, même sur plusieurs jours
            var light = new RateUsage();
            for (var i = 0; i < 5; i++) RatePolicy.Add(light, 600, monday.AddDays(i));
            Assert.Equal(5, light.Days, "cinq jours");
            Assert.False(RatePolicy.ShouldAsk(light, 600, false), "50 min en tout");
            // Valeurs absurdes ignorées
            var before = light.Seconds;
            RatePolicy.Add(light, double.NaN, monday);
            RatePolicy.Add(light, -30, monday);
            RatePolicy.Add(light, double.PositiveInfinity, monday);
            Assert.Equal(before, light.Seconds, "NaN, négatif, infini");
            Assert.False(RatePolicy.ShouldAsk(null, 3600, false), "sans compteur");
        }

        public static void TestEnglish()
        {
            Tr.English = true;
            try
            {
                Assert.Equal("1h 58", Format.Duration(7080));
                Assert.Equal("Resume", Format.Resume(20));
                Assert.Equal("Resume at 4 min", Format.Resume(250));
                Assert.Equal("★ 7.5", Format.Rating(7.5));
                Assert.Equal("Thursday, October 2, 2025", Format.Day(new DateTimeOffset(2025, 10, 2, 12, 0, 0, TimeSpan.Zero)));
                Assert.Equal("Live test pattern", Demo.Channels[0].Name);
                Assert.True(Demo.Channels[0].Logo.EndsWith("DemoLive1En.png"), "vignette « LIVE »");
                Assert.Equal("Demo · Live", Demo.LiveCategory.Name);
                Assert.True(Demo.Movies[0].Poster.EndsWith("DemoPosterTosEn.png"), "affiche « SCIENCE FICTION »");
                Assert.True(Demo.SeriesList[0].Cover.EndsWith("DemoPosterOpenEn.png"), "affiche « DEMO SERIES »");
                Assert.Equal("No internet connection.", new FluxException(FluxError.Offline).Message);
                Assert.Equal("Other", M3U.Parse("#EXTM3U\n#EXTINF:-1,Chaîne\nhttp://h/1.ts\n").Entries[0].Group);
                Assert.Equal("42 min left", Format.Remaining(2520));
                Assert.Equal("1 channel", Format.Count(1, "chaîne", "chaînes", "channel", "channels"));
                Assert.Equal("0 channels", Format.Count(0, "chaîne", "chaînes", "channel", "channels"));
                var evening = new DateTimeOffset(new DateTime(2025, 10, 2, 20, 5, 0, DateTimeKind.Local));
                Assert.Equal("20:05", Format.Hour(evening));
                Tr.Clock24 = false;
                Assert.Equal("8:05 PM", Format.Hour(evening));
            }
            finally
            {
                Tr.English = false;
                Tr.Clock24 = true;
            }
            Assert.Equal("Mire en direct", Demo.Channels[0].Name);
            Assert.Equal("Reste 42 min", Format.Remaining(2520));
            Assert.Equal("0 chaîne", Format.Count(0, "chaîne", "chaînes", "channel", "channels"));
            Assert.Equal("2 chaînes", Format.Count(2, "chaîne", "chaînes", "channel", "channels"));
            Tr.Clock24 = false;
            Assert.Equal("20:05", Format.Hour(new DateTimeOffset(new DateTime(2025, 10, 2, 20, 5, 0, DateTimeKind.Local))));
            Tr.Clock24 = true;
            Assert.Equal("Jeudi 2 octobre 2025", Format.Day(new DateTimeOffset(2025, 10, 2, 12, 0, 0, TimeSpan.Zero)));
        }

        // ───────────────────────────── Téléphone : QR code ─────────────────────────────

        /// <summary>Bits de format de la norme (ISO/IEC 18004, annexe C), par niveau L, M, Q, H et par masque.</summary>
        static readonly int[,] FormatTable =
        {
            { 0x77C4, 0x72F3, 0x7DAA, 0x789D, 0x662F, 0x6318, 0x6C41, 0x6976 },
            { 0x5412, 0x5125, 0x5E7C, 0x5B4B, 0x45F9, 0x40CE, 0x4F97, 0x4AA0 },
            { 0x355F, 0x3068, 0x3F31, 0x3A06, 0x24B4, 0x2183, 0x2EDA, 0x2BED },
            { 0x1689, 0x13BE, 0x1CE7, 0x19D0, 0x0762, 0x0255, 0x0D0C, 0x083B },
        };

        public static void TestQrCode()
        {
            // Adresse type affichée par la console : version 3 (29 × 29 modules), facile à lire de loin
            var q = QrCode.Encode("http://192.168.100.200:8765/K7P2QX");
            Assert.Equal(3, q.Version, "version");
            Assert.Equal(29, q.Size, "taille");
            Assert.Equal(QrCode.Correction.M, q.Level, "niveau de correction");

            // Trois repères de coin, entourés d'une bande claire
            foreach (var (fx, fy) in new[] { (0, 0), (q.Size - 7, 0), (0, q.Size - 7) })
                for (int dy = 0; dy < 7; dy++)
                    for (int dx = 0; dx < 7; dx++)
                        Assert.Equal(Math.Max(Math.Abs(dx - 3), Math.Abs(dy - 3)) != 2, q[fx + dx, fy + dy], $"repère ({fx + dx}, {fy + dy})");
            for (int i = 0; i < 8; i++)
            {
                Assert.False(q[7, i] || q[i, 7], "bande claire en haut à gauche");
                Assert.False(q[q.Size - 8, i] || q[q.Size - 1 - i, 7], "bande claire en haut à droite");
                Assert.False(q[7, q.Size - 1 - i] || q[i, q.Size - 8], "bande claire en bas à gauche");
            }
            // Lignes de synchronisation et module sombre fixe
            for (int i = 8; i < q.Size - 8; i++)
            {
                Assert.Equal(i % 2 == 0, q[i, 6], "ligne de synchronisation");
                Assert.Equal(i % 2 == 0, q[6, i], "colonne de synchronisation");
            }
            Assert.True(q[8, q.Size - 8], "module sombre fixe");

            // Bits de format (deux copies) = table de la norme, pour chaque niveau
            foreach (QrCode.Correction level in Enum.GetValues(typeof(QrCode.Correction)))
            {
                var c = QrCode.Encode("FluxTV " + level, level);
                int first = 0, second = 0;
                for (int i = 0; i <= 5; i++) if (c[8, i]) first |= 1 << i;
                if (c[8, 7]) first |= 1 << 6;
                if (c[8, 8]) first |= 1 << 7;
                if (c[7, 8]) first |= 1 << 8;
                for (int i = 9; i < 15; i++) if (c[14 - i, 8]) first |= 1 << i;
                for (int i = 0; i < 8; i++) if (c[c.Size - 1 - i, 8]) second |= 1 << i;
                for (int i = 8; i < 15; i++) if (c[8, c.Size - 15 + i]) second |= 1 << i;
                Assert.Equal(FormatTable[(int)level, c.Mask], first, "bits de format " + level);
                Assert.Equal(first, second, "deuxième copie des bits de format " + level);
            }

            // À partir de la version 7 : bloc de version (norme, annexe D : 0x07C94 pour la version 7)
            var big = QrCode.Encode(new string('a', 110));
            Assert.Equal(7, big.Version, "version 7");
            int v1 = 0, v2 = 0;
            for (int i = 0; i < 18; i++)
            {
                if (big[big.Size - 11 + i % 3, i / 3]) v1 |= 1 << i;
                if (big[i / 3, big.Size - 11 + i % 3]) v2 |= 1 << i;
            }
            Assert.Equal(0x07C94, v1, "bloc de version en haut à droite");
            Assert.Equal(0x07C94, v2, "bloc de version en bas à gauche");

            // Capacités exactes en octets (niveau M) : 14 en version 1, 26 en version 2
            Assert.Equal(1, QrCode.Encode(new string('x', 14)).Version, "14 octets");
            Assert.Equal(2, QrCode.Encode(new string('x', 15)).Version, "15 octets");
            Assert.Equal(2, QrCode.Encode(new string('x', 26)).Version, "26 octets");
            Assert.Equal(3, QrCode.Encode(new string('x', 27)).Version, "27 octets");
            Assert.Equal(40, QrCode.Encode(new string('x', 1273), QrCode.Correction.H).Version, "maximum en H");
            Assert.Throws(() => QrCode.Encode(new string('x', 1274), QrCode.Correction.H), "texte trop long");
        }

        // ───────────────────────────── Téléphone : page locale ─────────────────────────────

        static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

        public static void TestPhoneHttp()
        {
            var raw = Bytes("POST /ABCDEF?x=1 HTTP/1.1\r\nHost: 192.168.1.5:8765\r\ncontent-length: 11\r\n\r\ntype=m3u&a=");
            var head = MiniHttp.TryParseHead(raw, raw.Length);
            Assert.Equal("POST", head.Method, "méthode");
            Assert.Equal("/ABCDEF", head.Path, "chemin");
            Assert.Equal("x=1", head.Query, "paramètres");
            Assert.Equal(11, head.ContentLength, "longueur du corps");
            Assert.Equal("192.168.1.5:8765", head.Headers["Host"], "en-têtes sans casse");
            Assert.Equal(raw.Length - 11, head.HeadLength, "début du corps");
            Assert.Null(MiniHttp.TryParseHead(raw, 20), "en-tête incomplet");

            // Requêtes invalides ou trop grosses
            Assert.Throws(() => MiniHttp.TryParseHead(Bytes("BONJOUR\r\n\r\n"), 11), "ligne de requête");
            Assert.Throws(() => MiniHttp.TryParseHead(Bytes("GET x HTTP/1.1\r\n\r\n"), 18), "chemin sans « / »");
            var huge = Bytes("POST / HTTP/1.1\r\nContent-Length: 999999\r\n\r\n");
            Assert.Throws(() => MiniHttp.TryParseHead(huge, huge.Length), "corps trop long");
            var negative = Bytes("POST / HTTP/1.1\r\nContent-Length: -1\r\n\r\n");
            Assert.Throws(() => MiniHttp.TryParseHead(negative, negative.Length), "longueur négative");
            var endless = Bytes(new string('a', MiniHttp.MaxHead));
            Assert.Throws(() => MiniHttp.TryParseHead(endless, endless.Length), "en-tête sans fin");

            // Formulaire : « + », caractères accentués, « % » mal formé, champ en double
            var form = MiniHttp.ParseForm("server=http%3A%2F%2Fh%3A8080&user=jean+luc&pass=%C3%A9t%C3%A9%21&x=%ZZ&user=autre&vide");
            Assert.Equal("http://h:8080", form["server"], "adresse décodée");
            Assert.Equal("jean luc", form["user"], "premier champ gardé");
            Assert.Equal("été!", form["pass"], "UTF-8");
            Assert.Equal("%ZZ", form["x"], "« % » mal formé");
            Assert.Equal("", form["vide"], "champ sans valeur");

            // Réponse : jamais en cache ni dans un cadre, longueur en octets
            var text = Encoding.UTF8.GetString(MiniHttp.Response(200, "text/html; charset=utf-8", "é"));
            Assert.True(text.StartsWith("HTTP/1.1 200 OK\r\n"), "ligne d'état");
            Assert.True(text.Contains("\r\nContent-Length: 2\r\n"), "longueur en octets");
            Assert.True(text.Contains("\r\nCache-Control: no-store\r\n") && text.Contains("\r\nX-Frame-Options: DENY\r\n"), "ni cache ni cadre");
            Assert.True(text.Contains("\r\nConnection: close\r\n") && text.EndsWith("\r\n\r\né"), "corps");

            // Adresses du réseau local et codes
            foreach (var ip in new[] { "192.168.1.20", "10.0.0.7", "172.16.0.1", "172.31.255.254" })
                Assert.True(PhoneLink.IsPrivateIPv4(ip), ip);
            foreach (var ip in new[] { "172.32.0.1", "8.8.8.8", "169.254.3.4", "127.0.0.1", "192.168.1", "192.168.1.256", "a.b.c.d", "", null })
                Assert.False(PhoneLink.IsPrivateIPv4(ip), ip ?? "null");
            var codes = Enumerable.Range(0, 50).Select(_ => PhoneLink.NewCode()).ToList();
            Assert.True(codes.All(c => c.Length == 6 && c.All(ch => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".IndexOf(ch) >= 0)), "codes lisibles (sans 0, O, 1 ni I)");
            Assert.True(codes.Distinct().Count() > 45, "codes tirés au hasard");
            Assert.Equal("http://192.168.1.20:8765/K7P2QX", new PhoneLink("K7P2QX").Url("192.168.1.20", 8765), "adresse du QR code");
        }

        static PhoneLink.Result Ask(PhoneLink link, string method, string path, string body = "")
        {
            var raw = Bytes(method + " " + path + " HTTP/1.1\r\nHost: test\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n\r\n" + body);
            return link.Handle(MiniHttp.TryParseHead(raw, raw.Length), body);
        }

        static int StatusOf(PhoneLink.Result r) => int.Parse(Encoding.ASCII.GetString(r.Response, 9, 3));

        static string BodyOf(PhoneLink.Result r)
        {
            var s = Encoding.UTF8.GetString(r.Response);
            return s.Substring(s.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4);
        }

        public static void TestPhoneLinkForm()
        {
            var link = new PhoneLink("K7P2QX");

            // Accueil, formulaire (code aussi en minuscules), rien reçu sur une simple visite
            Assert.Equal(200, StatusOf(Ask(link, "GET", "/")), "accueil");
            Assert.Equal(404, StatusOf(Ask(link, "GET", "/ZZZZZZ")), "autre code");
            Assert.False(link.Visited, "page pas encore ouverte par le téléphone");
            var page = Ask(link, "GET", "/K7P2QX");
            Assert.Equal(200, StatusOf(page), "formulaire");
            Assert.True(BodyOf(page).Contains("action=\"/K7P2QX\""), "formulaire envoyé à la bonne adresse");
            Assert.True(BodyOf(page).Contains("lang=\"fr\"") && BodyOf(page).Contains("Envoyer à la télé"), "page en français");
            Assert.False(BodyOf(page).Contains("http-equiv") || BodyOf(page).Contains("src=\"http"), "aucune ressource externe");
            Assert.Null(page.Source, "simple visite");
            Assert.True(link.Visited, "page ouverte par le téléphone");
            Assert.Equal(200, StatusOf(Ask(link, "GET", "/k7p2qx/")), "code en minuscules");
            Assert.Equal(PhoneLinkState.Waiting, link.State, "en attente");

            // Champs manquants : message, valeurs gardées et échappées, mot de passe jamais renvoyé
            var missing = Ask(link, "POST", "/K7P2QX", "type=xtream&server=%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E&user=&pass=secret42");
            Assert.Null(missing.Source, "compte incomplet");
            var html = BodyOf(missing);
            Assert.True(html.Contains("class=\"err\"") && html.Contains("Remplis le serveur"), "message d'erreur");
            Assert.True(html.Contains("&quot;&gt;&lt;script&gt;") && !html.Contains("<script>alert"), "valeur échappée");
            Assert.False(html.Contains("secret42"), "mot de passe jamais renvoyé");
            Assert.Equal(PhoneLinkState.Waiting, link.State, "toujours en attente");

            // Compte Xtream : la source part vers la console, une seule fois
            var sent = Ask(link, "POST", "/K7P2QX", "type=xtream&server=mon-serveur.example%3A8080&user=moi&pass=s%C3%A9cret+%26+co");
            Assert.Equal(200, StatusOf(sent), "envoi");
            Assert.Equal(SourceKind.Xtream, sent.Source.Kind, "source Xtream");
            Assert.Equal("http://mon-serveur.example:8080", sent.Source.Account.Server, "serveur");
            Assert.Equal("moi", sent.Source.Account.Username, "identifiant");
            Assert.Equal("sécret & co", sent.Source.Account.Password, "mot de passe");
            Assert.True(BodyOf(sent).Contains("/K7P2QX/etat"), "page d'attente qui suit la connexion");
            Assert.Equal(PhoneLinkState.Connecting, link.State, "connexion en cours");
            Assert.Null(Ask(link, "POST", "/K7P2QX", "type=xtream&server=a&user=b&pass=c").Source, "pas de deuxième envoi pendant la connexion");
            Assert.True(BodyOf(Ask(link, "GET", "/K7P2QX/etat")).Contains("\"etat\":\"connexion\""), "état : connexion");
            Assert.True(BodyOf(Ask(link, "GET", "/K7P2QX")).Contains("/K7P2QX/etat"), "page d'attente si on revient au lien");

            // Erreur rapportée au téléphone (texte échappé pour le JSON et la page)
            link.Report(PhoneLinkState.Failed, "Identifiants refusés <b>\"x\"</b>");
            Assert.Equal("{\"etat\":\"erreur\",\"message\":\"Identifiants refusés \\u003cb\\u003e\\\"x\\\"\\u003c/b\\u003e\"}",
                         BodyOf(Ask(link, "GET", "/K7P2QX/etat")), "état : erreur");
            // « Corriger » : le formulaire revient rempli (sauf le mot de passe), avec l'erreur en haut
            var fix = BodyOf(Ask(link, "GET", "/K7P2QX"));
            Assert.True(fix.Contains("class=\"err\">Identifiants refusés &lt;b&gt;&quot;x&quot;&lt;/b&gt;</div>"), "erreur en haut du formulaire");
            Assert.True(fix.Contains("value=\"http://mon-serveur.example:8080\"") || fix.Contains("value=\"mon-serveur.example:8080\""), "serveur déjà rempli");
            Assert.True(fix.Contains("value=\"moi\""), "identifiant déjà rempli");
            Assert.False(fix.Contains("sécret"), "mot de passe jamais renvoyé");

            // Le téléphone corrige : lien Xtream complet collé dans « Serveur »
            var pasted = Ask(link, "POST", "/K7P2QX", "type=xtream&user=&pass=&server=" +
                             Uri.EscapeDataString("http://h.example:80/get.php?username=u1&password=p1&type=m3u_plus"));
            Assert.Equal("http://h.example:80", pasted.Source.Account.Server, "serveur du lien");
            Assert.Equal("u1", pasted.Source.Account.Username, "identifiant du lien");
            Assert.Equal("p1", pasted.Source.Account.Password, "mot de passe du lien");

            // Playlist M3U (adresse sans « http:// », guide TV), et lien Xtream collé côté playlist
            link.Report(PhoneLinkState.Failed, "x");
            var m3u = Ask(link, "POST", "/K7P2QX", "type=m3u&url=listes.example%2Ftv.m3u&epg=+http%3A%2F%2Fg.example%2Fguide.xml+");
            Assert.Equal(SourceKind.Playlist, m3u.Source.Kind, "source playlist");
            Assert.Equal("http://listes.example/tv.m3u", m3u.Source.PlaylistUrl, "adresse complétée");
            Assert.Equal("http://g.example/guide.xml", m3u.Source.EpgUrl, "guide TV");
            link.Report(PhoneLinkState.Failed, "x");
            var viaM3u = Ask(link, "POST", "/K7P2QX", "type=m3u&url=" +
                             Uri.EscapeDataString("http://h.example:8080/get.php?username=u2&password=p2&type=m3u_plus&output=ts"));
            Assert.Equal(SourceKind.Xtream, viaM3u.Source.Kind, "playlist Xtream → compte Xtream");
            Assert.Equal("u2", viaM3u.Source.Account.Username, "identifiant de la playlist");
            link.Report(PhoneLinkState.Failed, "x");
            var empty = Ask(link, "POST", "/K7P2QX", "type=m3u&url=+&epg=");
            Assert.Null(empty.Source, "adresse vide");
            Assert.True(BodyOf(empty).Contains("class=\"err\"") && BodyOf(empty).Contains("id=\"t-m\" checked"), "erreur sur l'onglet playlist");

            // Réussite : plus aucun envoi accepté
            link.Report(PhoneLinkState.Connected);
            Assert.True(BodyOf(Ask(link, "GET", "/K7P2QX/etat")).Contains("\"etat\":\"ok\""), "état : ok");
            Assert.True(BodyOf(Ask(link, "GET", "/K7P2QX")).Contains("est fait"), "page « c'est fait »");
            Assert.Null(Ask(link, "POST", "/K7P2QX", "type=xtream&server=a&user=b&pass=c").Source, "rien après la réussite");

            // Mauvais code : 404, et la page se ferme après trop d'essais (même avec le bon code ensuite)
            var other = new PhoneLink("AAAAAA");
            Assert.Equal(404, StatusOf(Ask(other, "GET", "/AAAAAB")), "mauvais code");
            Assert.Equal(404, StatusOf(Ask(other, "GET", "/AAAAAA/autre")), "page inconnue");
            Assert.Equal(404, StatusOf(Ask(other, "GET", "/AAAAAA/etat/x")), "chemin trop long");
            Assert.Equal(404, StatusOf(Ask(other, "GET", "/favicon.ico")), "icône");
            Assert.Equal(405, StatusOf(Ask(other, "PUT", "/AAAAAA")), "méthode refusée");
            Assert.False(other.Closed, "quelques erreurs");
            for (int i = 0; i < PhoneLink.MaxBadRequests; i++) Ask(other, "GET", "/X" + i);
            Assert.True(other.Closed, "fermée après trop d'essais");
            Assert.Equal(404, StatusOf(Ask(other, "GET", "/AAAAAA")), "plus rien, même avec le bon code");

            // Pages en anglais quand l'app est en anglais
            Tr.English = true;
            try
            {
                var en = BodyOf(Ask(new PhoneLink("K7P2QX"), "GET", "/K7P2QX"));
                Assert.True(en.Contains("lang=\"en\"") && en.Contains("Send to the TV") && en.Contains("Xtream account"), "page en anglais");
            }
            finally { Tr.English = false; }
        }

        /// <summary>Connexion qui n'envoie jamais rien et ignore l'annulation (comme certains flux réseau).</summary>
        sealed class MuteStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken token) =>
                new TaskCompletionSource<int>().Task;
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>Le vrai trajet : un « téléphone » (HttpClient) parle à la page servie sur une connexion TCP.</summary>
        public static async Task TestPhoneLinkOverNetwork()
        {
            // Client muet abandonné au bout du délai, requête invalide refusée proprement
            var started = DateTime.UtcNow;
            Assert.Null(await new PhoneLink().ServeAsync(new MuteStream(), Stream.Null, TimeSpan.FromMilliseconds(300)), "client muet");
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(3), "abandonné au bout du délai");
            var refused = new MemoryStream();
            Assert.Null(await new PhoneLink().ServeAsync(new MemoryStream(Bytes("BONJOUR\r\n\r\n")), refused, TimeSpan.FromSeconds(2)), "requête invalide");
            Assert.True(Encoding.ASCII.GetString(refused.ToArray()).StartsWith("HTTP/1.1 400 "), "réponse 400");
            // Essai en HTTPS (début d'une poignée de main TLS) : coupé tout de suite, sans réponse
            var tls = new MemoryStream();
            started = DateTime.UtcNow;
            Assert.Null(await new PhoneLink().ServeAsync(new MemoryStream(new byte[] { 0x16, 0x03, 0x01, 0x02, 0x00, 0x01 }), tls, TimeSpan.FromSeconds(5)), "essai HTTPS");
            Assert.Equal(0L, tls.Length, "rien répondu à un essai HTTPS");
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1), "coupé sans attendre");
            var cut = new MemoryStream();
            Assert.Null(await new PhoneLink().ServeAsync(new MemoryStream(Bytes("GET / HT")), cut, TimeSpan.FromSeconds(2)), "connexion coupée");
            Assert.Equal(0L, cut.Length, "rien répondu à une connexion coupée");

            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var link = new PhoneLink("K7P2QX");
            var received = new System.Collections.Concurrent.BlockingCollection<Source>();
            var serving = Task.Run(async () =>
            {
                while (true)
                {
                    System.Net.Sockets.TcpClient client;
                    try { client = await listener.AcceptTcpClientAsync(); }
                    catch (Exception) { return; }
                    _ = Task.Run(async () =>
                    {
                        using (client)
                        {
                            var stream = client.GetStream();
                            var source = await link.ServeAsync(stream, stream, TimeSpan.FromSeconds(5));
                            if (source != null) received.Add(source);
                        }
                    });
                }
            });
            try
            {
                var url = link.Url("127.0.0.1", port);
                using (var http = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) })
                {
                    Assert.True((await http.GetStringAsync(url)).Contains("name=\"server\""), "formulaire reçu par le téléphone");
                    var post = await http.PostAsync(url, new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["type"] = "xtream", ["server"] = "http://serveur.example:8080", ["user"] = "moi", ["pass"] = "mot de passe & co ✓",
                    }));
                    Assert.Equal(System.Net.HttpStatusCode.OK, post.StatusCode, "envoi");
                    Assert.True((await post.Content.ReadAsStringAsync()).Contains("/K7P2QX/etat"), "page d'attente");
                    Assert.True(received.TryTake(out var source, 5000), "source reçue par la console");
                    Assert.Equal("mot de passe & co ✓", source.Account.Password, "mot de passe intact");
                    link.Report(PhoneLinkState.Failed, "Serveur injoignable");
                    Assert.True((await http.GetStringAsync(url + "/etat")).Contains("Serveur injoignable"), "le téléphone voit l'erreur");
                    var wrong = await http.GetAsync(link.Url("127.0.0.1", port).Replace("K7P2QX", "ABCDEF"));
                    Assert.Equal(System.Net.HttpStatusCode.NotFound, wrong.StatusCode, "mauvais code");
                    Assert.True(wrong.Headers.CacheControl?.NoStore == true, "jamais en cache");
                }

                // Corps arrivé en deux fois (réseau lent)
                using (var phone = new System.Net.Sockets.TcpClient())
                {
                    await phone.ConnectAsync(System.Net.IPAddress.Loopback, port);
                    var stream = phone.GetStream();
                    var body = Bytes("type=m3u&url=http%3A%2F%2Flistes.example%2Ftv.m3u");
                    var head = Bytes("POST /K7P2QX HTTP/1.1\r\nHost: x\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: " + body.Length + "\r\n\r\n");
                    await stream.WriteAsync(head, 0, head.Length);
                    await Task.Delay(150);
                    await stream.WriteAsync(body, 0, body.Length);
                    var reply = await new StreamReader(stream, Encoding.UTF8).ReadToEndAsync();
                    Assert.True(reply.StartsWith("HTTP/1.1 200 OK"), "réponse au corps en deux fois");
                    Assert.True(received.TryTake(out var playlist, 5000), "playlist reçue");
                    Assert.Equal("http://listes.example/tv.m3u", playlist.PlaylistUrl, "adresse de la playlist");
                }
            }
            finally
            {
                listener.Stop();
                await Task.WhenAny(serving, Task.Delay(2000));
            }
        }
    }
}
