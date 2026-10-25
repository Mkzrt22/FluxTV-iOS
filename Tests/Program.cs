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
            Assert.Seq(new[] { "TF1.fr" }, map.Keys.OrderBy(k => k));
            Assert.Seq(new[] { "Le 18 h", "Jeu & Quiz" }, map["TF1.fr"].Select(p => p.Title));
            Assert.Equal("Infos.", map["TF1.fr"][0].Desc);
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
    }
}
