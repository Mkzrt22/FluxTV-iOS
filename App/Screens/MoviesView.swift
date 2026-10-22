import SwiftUI

/// Tri des listes de films et de séries.
enum CatalogSort: String, CaseIterable {
    case provider, recent, title, rating

    var label: String {
        switch self {
        case .provider: return "Ordre du fournisseur"
        case .recent: return "Ajouts récents"
        case .title: return "Titre (A → Z)"
        case .rating: return "Mieux notés"
        }
    }
}

/// Films : catégories en pastilles, grille d'affiches.
struct MoviesView: View {
    @EnvironmentObject private var app: AppModel
    @Environment(\.horizontalSizeClass) private var sizeClass
    @State private var categories: [Category] = []
    @State private var selection: String?
    @State private var movies: [Movie] = []
    @State private var loading = false
    @State private var error: String?
    @State private var sort: CatalogSort = .provider

    private static let favoritesId = "__fav"

    private var chips: [CategoryChips.Chip] {
        var out: [CategoryChips.Chip] = []
        if !app.library.data.favorites(of: .movie).isEmpty { out.append(CategoryChips.Chip(id: Self.favoritesId, title: "Favoris", icon: "star.fill")) }
        out += app.visible(categories).map { CategoryChips.Chip(id: $0.id, title: $0.name) }
        return out
    }

    private var shown: [Movie] {
        let list = selection == Self.favoritesId ? app.library.data.favorites(of: .movie).map(Movie.init(ref:)) : movies
        switch sort {
        case .provider: return list
        case .recent: return list.sorted { ($0.added ?? .distantPast) > ($1.added ?? .distantPast) }
        case .title: return list.sorted { TextTools.frenchLess($0.name, $1.name) }
        case .rating: return list.sorted { ($0.rating ?? 0) > ($1.rating ?? 0) }
        }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(12)) {
                if !chips.isEmpty { CategoryChips(chips: chips, selection: $selection) }
                HStack {
                    Text(chips.first { $0.id == selection }?.title ?? "Films").font(Theme.title(20)).foregroundStyle(Theme.text)
                    Spacer()
                    SortMenu(sort: $sort)
                }
                .padding(.horizontal, Theme.pagePadding)

                if loading && movies.isEmpty {
                    LoadingView(text: "Chargement des films…").frame(height: Theme.s(260))
                } else if let error, movies.isEmpty {
                    StateMessage(systemImage: "wifi.exclamationmark", title: "Impossible de charger les films", message: error,
                                 actionTitle: "Réessayer") { Task { await load(force: true) } }
                } else if shown.isEmpty && !loading {
                    StateMessage(systemImage: "film", title: categories.isEmpty ? "Aucun film" : "Catégorie vide",
                                 message: categories.isEmpty ? "Ta source ne propose pas de films." : nil)
                        .frame(height: Theme.s(300))
                } else {
                    PosterGrid(items: shown, minWidth: PosterLayout.minWidth(sizeClass)) { m in
                        NavigationLink(value: m) {
                            PosterCard(title: m.name, image: m.poster, subtitle: m.yearText, rating: m.rating,
                                       badges: m.badges, progress: app.library.resume(m.id)?.fraction,
                                       watched: app.library.isWatched(m.id), width: PosterLayout.cardWidth(sizeClass))
                        }
                        .buttonStyle(FluxButtonStyle())
                    }
                }
            }
            .padding(.vertical, Theme.s(10))
        }
        .fluxScreen()
        .detailDestinations()
        .navigationTitle("Films")
        #if os(iOS)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .task(id: app.source) { await loadCategories() }
        .onChange(of: selection) { _, _ in Task { await load() } }
    }

    private func loadCategories() async {
        guard let repo = app.repo else { return }
        do {
            categories = try await repo.vodCategories()
            error = nil
            if selection == nil || !chips.contains(where: { $0.id == selection }) {
                selection = chips.first { $0.id != Self.favoritesId }?.id ?? chips.first?.id
            }
            await load()
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func load(force: Bool = false) async {
        guard let repo = app.repo, let cat = selection, cat != Self.favoritesId else { return }
        loading = true
        defer { loading = false }
        do {
            let list = try await repo.movies(category: cat, force: force)
            if selection == cat { movies = list; error = nil }
        } catch {
            if selection == cat { movies = []; self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription }
        }
    }
}

/// Séries : catégories en pastilles, grille d'affiches.
struct SeriesListView: View {
    @EnvironmentObject private var app: AppModel
    @Environment(\.horizontalSizeClass) private var sizeClass
    @State private var categories: [Category] = []
    @State private var selection: String?
    @State private var series: [Series] = []
    @State private var loading = false
    @State private var error: String?
    @State private var sort: CatalogSort = .provider

    private static let favoritesId = "__fav"

    private var chips: [CategoryChips.Chip] {
        var out: [CategoryChips.Chip] = []
        if !app.library.data.favorites(of: .series).isEmpty { out.append(CategoryChips.Chip(id: Self.favoritesId, title: "Favoris", icon: "star.fill")) }
        out += app.visible(categories).map { CategoryChips.Chip(id: $0.id, title: $0.name) }
        return out
    }

    private var shown: [Series] {
        let list = selection == Self.favoritesId ? app.library.data.favorites(of: .series).map(Series.init(ref:)) : series
        switch sort {
        case .provider: return list
        case .recent: return list.sorted { ($0.lastModified ?? .distantPast) > ($1.lastModified ?? .distantPast) }
        case .title: return list.sorted { TextTools.frenchLess($0.name, $1.name) }
        case .rating: return list.sorted { ($0.rating ?? 0) > ($1.rating ?? 0) }
        }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(12)) {
                if !chips.isEmpty { CategoryChips(chips: chips, selection: $selection) }
                HStack {
                    Text(chips.first { $0.id == selection }?.title ?? "Séries").font(Theme.title(20)).foregroundStyle(Theme.text)
                    Spacer()
                    SortMenu(sort: $sort)
                }
                .padding(.horizontal, Theme.pagePadding)

                if loading && series.isEmpty {
                    LoadingView(text: "Chargement des séries…").frame(height: Theme.s(260))
                } else if let error, series.isEmpty {
                    StateMessage(systemImage: "wifi.exclamationmark", title: "Impossible de charger les séries", message: error,
                                 actionTitle: "Réessayer") { Task { await load(force: true) } }
                } else if shown.isEmpty && !loading {
                    StateMessage(systemImage: "rectangle.stack", title: categories.isEmpty ? "Aucune série" : "Catégorie vide",
                                 message: categories.isEmpty ? "Ta source ne propose pas de séries." : nil)
                        .frame(height: Theme.s(300))
                } else {
                    PosterGrid(items: shown, minWidth: PosterLayout.minWidth(sizeClass)) { s in
                        NavigationLink(value: s) {
                            PosterCard(title: s.name, image: s.cover, subtitle: s.yearText, rating: s.rating,
                                       width: PosterLayout.cardWidth(sizeClass))
                        }
                        .buttonStyle(FluxButtonStyle())
                    }
                }
            }
            .padding(.vertical, Theme.s(10))
        }
        .fluxScreen()
        .detailDestinations()
        .navigationTitle("Séries")
        #if os(iOS)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .task(id: app.source) { await loadCategories() }
        .onChange(of: selection) { _, _ in Task { await load() } }
    }

    private func loadCategories() async {
        guard let repo = app.repo else { return }
        do {
            categories = try await repo.seriesCategories()
            error = nil
            if selection == nil || !chips.contains(where: { $0.id == selection }) {
                selection = chips.first { $0.id != Self.favoritesId }?.id ?? chips.first?.id
            }
            await load()
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }

    private func load(force: Bool = false) async {
        guard let repo = app.repo, let cat = selection, cat != Self.favoritesId else { return }
        loading = true
        defer { loading = false }
        do {
            let list = try await repo.series(category: cat, force: force)
            if selection == cat { series = list; error = nil }
        } catch {
            if selection == cat { series = []; self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription }
        }
    }
}

/// Tri : menu sur iPhone et iPad ; sur Apple TV, chaque appui passe au tri suivant (le menu du
/// système y rend le bouton illisible quand il est sélectionné).
struct SortMenu: View {
    @Binding var sort: CatalogSort

    var body: some View {
        #if os(tvOS)
        Button {
            let all = CatalogSort.allCases
            sort = all[((all.firstIndex(of: sort) ?? 0) + 1) % all.count]
        } label: {
            label(sort.label)
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.08, cornerRadius: 40))
        #else
        Menu {
            Picker("Trier", selection: $sort) {
                ForEach(CatalogSort.allCases, id: \.self) { Text($0.label).tag($0) }
            }
        } label: {
            label(sort == .provider ? "Trier" : sort.label)
        }
        #endif
    }

    private func label(_ text: String) -> some View {
        HStack(spacing: Theme.s(6)) {
            Image(systemName: "arrow.up.arrow.down")
            Text(text).lineLimit(1)
        }
        .font(Theme.label(12))
        .foregroundStyle(Theme.textDim)
        .padding(.horizontal, Theme.s(12)).padding(.vertical, Theme.s(7))
        .background(Capsule().fill(Color.white.opacity(0.07)))
    }
}
