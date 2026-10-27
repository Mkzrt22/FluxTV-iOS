import SwiftUI

/// Recherche dans les chaînes, les films et les séries (accents et majuscules ignorés).
struct SearchView: View {
    @EnvironmentObject private var app: AppModel
    @State private var query = ""
    @State private var allMovies: [Movie] = []
    @State private var allSeries: [Series] = []
    @State private var catalogsLoaded = false
    @State private var loadingCatalogs = false
    @State private var results = Results()
    @State private var searchTask: Task<Void, Never>?

    struct Results: Sendable {
        var channels: [LiveChannel] = []
        var movies: [Movie] = []
        var series: [Series] = []
        var isEmpty: Bool { channels.isEmpty && movies.isEmpty && series.isEmpty }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(24)) {
                if query.trimmingCharacters(in: .whitespaces).count < 2 {
                    StateMessage(systemImage: "magnifyingglass", title: T("Rechercher", "Search"),
                                 message: T("Une chaîne, un film, une série… Les accents et les majuscules n'ont pas d'importance.",
                                            "A channel, a movie, a series… Accents and capital letters don't matter."))
                        .frame(height: Theme.s(320))
                } else if results.isEmpty {
                    if loadingCatalogs {
                        LoadingView(text: T("Recherche dans tout le catalogue…", "Searching the whole catalog…")).frame(height: Theme.s(260))
                    } else {
                        StateMessage(systemImage: "questionmark.circle", title: T("Aucun résultat", "No results"),
                                     message: T("Essaie avec un autre mot.", "Try another word."))
                            .frame(height: Theme.s(300))
                    }
                } else {
                    if !results.channels.isEmpty {
                        CardRow(title: T("Chaînes", "Channels"), items: results.channels) { ch in
                            Button { app.play(channel: ch, in: results.channels) } label: {
                                WideCard(title: ch.name, image: ch.logo, subtitle: app.categoryName(ch.categoryId),
                                         number: ch.num, fitImage: true, width: 190)
                            }
                            .buttonStyle(FluxButtonStyle())
                        }
                    }
                    if !results.movies.isEmpty {
                        CardRow(title: T("Films", "Movies"), items: results.movies) { m in
                            NavigationLink(value: m) {
                                PosterCard(title: m.name, image: m.poster, subtitle: m.yearText, rating: m.rating,
                                           badges: m.badges, progress: app.library.resume(m.id)?.fraction)
                            }
                            .buttonStyle(FluxButtonStyle())
                        }
                    }
                    if !results.series.isEmpty {
                        CardRow(title: T("Séries", "Series"), items: results.series) { s in
                            NavigationLink(value: s) {
                                PosterCard(title: s.name, image: s.cover, subtitle: s.yearText, rating: s.rating)
                            }
                            .buttonStyle(FluxButtonStyle())
                        }
                    }
                    if loadingCatalogs {
                        HStack(spacing: Theme.s(8)) {
                            ProgressView().tint(Theme.accentBright)
                            Text(T("Recherche dans les films et les séries…", "Searching movies and series…")).font(Theme.label(13)).foregroundStyle(Theme.textDim)
                        }
                        .padding(.horizontal, Theme.pagePadding)
                    }
                }
            }
            .padding(.vertical, Theme.s(12))
        }
        .fluxScreen()
        .detailDestinations()
        .navigationTitle(T("Recherche", "Search"))
        .searchable(text: $query, prompt: T("Chaînes, films, séries", "Channels, movies, series"))
        .onChange(of: query) { _, _ in scheduleSearch() }
        .onChange(of: catalogsLoaded) { _, _ in scheduleSearch() }
    }

    private func scheduleSearch() {
        searchTask?.cancel()
        let q = query
        searchTask = Task {
            try? await Task.sleep(nanoseconds: 250_000_000)
            if Task.isCancelled { return }
            await search(q)
        }
    }

    private func search(_ q: String) async {
        let words = TextTools.queryWords(q)
        guard q.trimmingCharacters(in: .whitespaces).count >= 2, !words.isEmpty else {
            results = Results()
            return
        }
        if !catalogsLoaded && !loadingCatalogs { Task { await loadCatalogs() } }
        let channels = app.visibleChannels()
        let movies = allMovies
        let series = allSeries
        let hiddenVod: Set<String> = []
        // Filtrage hors du fil principal : les catalogues comptent souvent des dizaines de milliers d'entrées
        let found = await Task.detached(priority: .userInitiated) { () -> Results in
            var r = Results()
            r.channels = Array(channels.filter { TextTools.matches($0.name, query: words) }.prefix(40))
            r.movies = Array(movies.filter { !hiddenVod.contains($0.categoryId) && TextTools.matches($0.name, query: words) }.prefix(60))
            r.series = Array(series.filter { TextTools.matches($0.name, query: words) }.prefix(60))
            return r
        }.value
        if query == q { results = found }
    }

    private func loadCatalogs() async {
        guard let repo = app.repo else { return }
        loadingCatalogs = true
        async let m = try? repo.allMovies()
        async let s = try? repo.allSeries()
        let (movies, series) = await (m, s)
        let hidden = await hiddenCategories(repo)
        allMovies = (movies ?? []).filter { !hidden.vod.contains($0.categoryId) }
        allSeries = (series ?? []).filter { !hidden.series.contains($0.categoryId) }
        loadingCatalogs = false
        catalogsLoaded = true
    }

    /// Catégories adultes masquées (si le réglage est actif).
    private func hiddenCategories(_ repo: Repository) async -> (vod: Set<String>, series: Set<String>) {
        guard app.settings.hideAdult else { return ([], []) }
        let v = (try? await repo.vodCategories()) ?? []
        let s = (try? await repo.seriesCategories()) ?? []
        return (Set(v.filter(\.isAdult).map(\.id)), Set(s.filter(\.isAdult).map(\.id)))
    }
}
