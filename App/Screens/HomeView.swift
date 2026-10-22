import SwiftUI

/// Accueil : reprise en grand, puis rangées (reprises, chaînes, nouveautés, séries, favoris).
struct HomeView: View {
    @EnvironmentObject private var app: AppModel
    @State private var latestMovies: [Movie] = []
    @State private var featuredSeries: [Series] = []
    @State private var moviesGroup: String?
    @State private var loadedFor: String?

    private var resume: [ContentRef] { app.library.data.continueWatching() }
    private var favoriteMovies: [ContentRef] { app.library.data.favorites(of: .movie) }
    private var favoriteSeries: [ContentRef] { app.library.data.favorites(of: .series) }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(26)) {
                header
                hero
                resumeRow
                channelsRow(title: "Mes chaînes", channels: app.favoriteChannels)
                channelsRow(title: "Dernières chaînes", channels: recentNotFavorite)
                channelsRow(title: "Chaînes en direct", channels: discoverChannels)
                moviesRow
                seriesRow
                favoriteMoviesRow
                favoriteSeriesRow
                liveState
            }
            .padding(.vertical, Theme.s(16))
        }
        .fluxScreen()
        .detailDestinations()
        #if os(iOS)
        .toolbar(.hidden, for: .navigationBar)
        #endif
        .task(id: app.source) { await loadRows() }
    }

    private var recentNotFavorite: [LiveChannel] {
        let favs = Set(app.favoriteChannels.map(\.id))
        return app.recentChannels.filter { !favs.contains($0.id) }
    }

    /// Premières chaînes de la source, tant qu'il n'y a ni favori ni chaîne récente.
    private var discoverChannels: [LiveChannel] {
        guard app.favoriteChannels.isEmpty && app.recentChannels.isEmpty else { return [] }
        return Array(app.visibleChannels().prefix(16))
    }

    // ───────────────────────────── Rangées ─────────────────────────────

    @ViewBuilder private var resumeRow: some View {
        if !resume.isEmpty {
            CardRow(title: "Continuer à regarder", items: resume) { ref in
                resumeCard(ref)
            }
        }
    }

    private func resumeCard(_ ref: ContentRef) -> some View {
        Button { app.play(ref: ref) } label: {
            WideCard(title: ref.kind == .episode ? (ref.seriesName ?? ref.name) : ref.name,
                     image: ref.image,
                     subtitle: subtitle(for: ref),
                     progress: app.library.resume(ref.id)?.fraction)
        }
        .buttonStyle(FluxButtonStyle())
        .contextMenu {
            Button(role: .destructive) { app.library.removeHistory(ref.id) } label: {
                Label("Retirer de la liste", systemImage: "xmark.circle")
            }
        }
    }

    @ViewBuilder private var moviesRow: some View {
        if !latestMovies.isEmpty {
            CardRow(title: moviesGroup ?? "Films", items: latestMovies) { m in
                movieCard(m)
            }
        }
    }

    private func movieCard(_ m: Movie) -> some View {
        NavigationLink(value: m) {
            PosterCard(title: m.name, image: m.poster, subtitle: m.yearText, rating: m.rating,
                       badges: m.badges, progress: app.library.resume(m.id)?.fraction,
                       watched: app.library.isWatched(m.id))
        }
        .buttonStyle(FluxButtonStyle())
    }

    @ViewBuilder private var seriesRow: some View {
        if !featuredSeries.isEmpty {
            CardRow(title: "Séries", items: featuredSeries) { s in
                seriesCard(s)
            }
        }
    }

    private func seriesCard(_ s: Series) -> some View {
        NavigationLink(value: s) {
            PosterCard(title: s.name, image: s.cover, subtitle: s.yearText, rating: s.rating)
        }
        .buttonStyle(FluxButtonStyle())
    }

    @ViewBuilder private var favoriteMoviesRow: some View {
        if !favoriteMovies.isEmpty {
            CardRow(title: "Films favoris", items: favoriteMovies) { ref in
                movieCard(Movie(ref: ref))
            }
        }
    }

    @ViewBuilder private var favoriteSeriesRow: some View {
        if !favoriteSeries.isEmpty {
            CardRow(title: "Séries favorites", items: favoriteSeries) { ref in
                seriesCard(Series(ref: ref))
            }
        }
    }

    @ViewBuilder private var liveState: some View {
        if app.liveLoading && app.live == nil {
            LoadingView(text: "Chargement des chaînes…").frame(height: Theme.s(160))
        } else if let err = app.liveError, app.live == nil {
            StateMessage(systemImage: "wifi.exclamationmark", title: "Impossible de charger tes chaînes", message: err,
                         actionTitle: "Réessayer") { Task { await app.loadLive(force: true) } }
        }
    }

    // ───────────────────────────── En-tête, mise en avant ─────────────────────────────

    private var header: some View {
        HStack(alignment: .center, spacing: Theme.s(14)) {
            BrandLogo(size: 32)
            Text(greeting).font(Theme.body(14)).foregroundStyle(Theme.textDim)
            Spacer()
            if let days = app.account?.daysLeft(), days <= 30 {
                Text(days == 0 ? "Abonnement expiré" : "Abonnement : \(days) j")
                    .font(Theme.label(12))
                    .foregroundStyle(days <= 7 ? Theme.gold : Theme.textDim)
                    .padding(.horizontal, Theme.s(10)).padding(.vertical, Theme.s(5))
                    .background(Capsule().fill(Color.white.opacity(0.07)))
            }
            #if os(tvOS)
            ClockLabel()
            #else
            NavigationLink {
                SettingsView()
            } label: {
                Image(systemName: "gearshape.fill")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundStyle(Theme.text)
                    .frame(width: 42, height: 42)
                    .background(Circle().fill(Theme.glassStrong))
            }
            .buttonStyle(FluxButtonStyle(cornerRadius: 40))
            .accessibilityLabel("Réglages")
            #endif
        }
        .padding(.horizontal, Theme.pagePadding)
    }

    private var greeting: String {
        let h = Calendar.current.component(.hour, from: Date())
        switch h {
        case 5..<12: return "Bonjour"
        case 12..<18: return "Bon après-midi"
        default: return "Bonsoir"
        }
    }

    @ViewBuilder private var hero: some View {
        if let ref = resume.first {
            HeroCard(eyebrow: ref.kind == .episode ? "Reprendre la série" : "Reprendre",
                     title: ref.kind == .episode ? (ref.seriesName ?? ref.name) : ref.name,
                     subtitle: subtitle(for: ref),
                     image: ref.image,
                     progress: app.library.resume(ref.id)?.fraction,
                     buttonTitle: "Reprendre") { app.play(ref: ref) }
        } else if let ch = app.favoriteChannels.first ?? app.recentChannels.first {
            let now = app.nowNext(ch).now
            HeroCard(eyebrow: "En direct", title: now?.title ?? ch.name, subtitle: now == nil ? nil : ch.name,
                     image: ch.logo, fitImage: true, progress: now?.progress(), progressColor: Theme.onAir,
                     buttonTitle: "Regarder") {
                app.play(channel: ch, in: app.visibleChannels(in: ch.categoryId))
            }
            .onAppear { app.requestProgrammes(for: ch) }
        } else if let m = latestMovies.first(where: { $0.poster != nil }) {
            NavigationLink(value: m) {
                HeroCardBody(eyebrow: moviesGroup ?? "À voir", title: m.name, subtitle: m.yearText,
                             image: m.poster, buttonTitle: "Voir la fiche")
            }
            .buttonStyle(FluxButtonStyle(focusScale: 1.02, cornerRadius: 26))
            .padding(.horizontal, Theme.pagePadding)
        }
    }

    @ViewBuilder private func channelsRow(title: String, channels: [LiveChannel]) -> some View {
        if !channels.isEmpty {
            CardRow(title: title, items: channels) { ch in
                channelCard(ch, in: channels)
            }
        }
    }

    private func channelCard(_ ch: LiveChannel, in channels: [LiveChannel]) -> some View {
        let now = app.nowNext(ch).now
        return Button { app.play(channel: ch, in: channels) } label: {
            WideCard(title: ch.name, image: ch.logo, subtitle: now?.title, progress: now?.progress(),
                     progressColor: Theme.onAir, live: false, number: ch.num, fitImage: true, width: 200)
        }
        .buttonStyle(FluxButtonStyle())
        .onAppear { app.requestProgrammes(for: ch) }
    }

    private func subtitle(for ref: ContentRef) -> String? {
        guard let p = app.library.resume(ref.id) else { return ref.group }
        let left = max(0, p.duration - p.position)
        let base = ref.kind == .episode ? ref.name : nil
        let remain = p.duration > 0 ? "Reste \(Format.duration(left))" : "Reprendre à \(Format.duration(p.position))"
        return [base, remain].compactMap { $0 }.joined(separator: " · ")
    }

    // ───────────────────────────── Rangées de films et séries ─────────────────────────────

    private func loadRows() async {
        guard let repo = app.repo, let key = app.source?.cacheKey, loadedFor != key else { return }
        loadedFor = key
        if let cats = try? await repo.vodCategories() {
            let visible = app.visible(cats)
            // Catégorie « nouveautés » si le fournisseur en a une, sinon la première
            let pick = visible.first { c in
                let f = TextTools.fold(c.name)
                return f.contains("nouveau") || f.contains("new") || f.contains("recent") || f.contains("dernier")
            } ?? visible.first
            if let pick, let movies = try? await repo.movies(category: pick.id) {
                moviesGroup = pick.name
                let sorted = movies.sorted { ($0.added ?? .distantPast) > ($1.added ?? .distantPast) }
                latestMovies = Array(sorted.prefix(24))
            }
        }
        if let cats = try? await repo.seriesCategories(), let first = app.visible(cats).first,
           let list = try? await repo.series(category: first.id) {
            featuredSeries = Array(list.sorted { ($0.lastModified ?? .distantPast) > ($1.lastModified ?? .distantPast) }.prefix(24))
        }
    }
}

/// Grande carte de l'accueil (bouton intégré).
private struct HeroCard: View {
    let eyebrow: String
    let title: String
    var subtitle: String?
    var image: String?
    var fitImage = false
    var progress: Double?
    var progressColor: Color = Theme.accent
    let buttonTitle: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HeroCardBody(eyebrow: eyebrow, title: title, subtitle: subtitle, image: image, fitImage: fitImage,
                         progress: progress, progressColor: progressColor, buttonTitle: buttonTitle)
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.02, cornerRadius: 26))
        .padding(.horizontal, Theme.pagePadding)
    }
}

private struct HeroCardBody: View {
    let eyebrow: String
    let title: String
    var subtitle: String?
    var image: String?
    var fitImage = false
    var progress: Double?
    var progressColor: Color = Theme.accent
    let buttonTitle: String

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            ZStack {
                LinearGradient(colors: [Theme.surface3, Theme.abyss], startPoint: .topLeading, endPoint: .bottomTrailing)
                if fitImage {
                    RemoteImage(url: image, contentMode: .fit, maxSize: 260, placeholderTitle: title, placeholderIcon: "tv")
                        .frame(maxWidth: Theme.s(240), maxHeight: Theme.s(130))
                        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .trailing)
                        .padding(.trailing, Theme.s(36))
                        .opacity(0.9)
                } else {
                    RemoteImage(url: image, contentMode: .fill, maxSize: 900, placeholderTitle: title)
                        .blur(radius: 0)
                }
            }
            .frame(maxWidth: .infinity)
            .frame(height: Theme.s(230))
            .clipped()

            LinearGradient(colors: [.clear, Theme.abyss.opacity(0.92)], startPoint: .top, endPoint: .bottom)

            VStack(alignment: .leading, spacing: Theme.s(8)) {
                Eyebrow(text: eyebrow)
                Text(title).font(Theme.display(26)).foregroundStyle(.white).lineLimit(2)
                if let subtitle { Text(subtitle).font(Theme.body(14)).foregroundStyle(Theme.textDim).lineLimit(1) }
                if let progress {
                    ProgressLine(value: progress, color: progressColor, height: 4).frame(maxWidth: Theme.s(260))
                }
                HStack(spacing: Theme.s(8)) {
                    Image(systemName: "play.fill").font(.system(size: Theme.s(13), weight: .bold))
                    Text(buttonTitle).font(Theme.strong(14))
                }
                .foregroundStyle(.white)
                .padding(.horizontal, Theme.s(18)).padding(.vertical, Theme.s(10))
                .background(Capsule().fill(Theme.accentGradient))
                .padding(.top, Theme.s(4))
            }
            .padding(Theme.s(22))
        }
        .frame(height: Theme.s(230))
        .clipShape(RoundedRectangle(cornerRadius: Theme.s(26), style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(26), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
        .contentShape(Rectangle())
    }
}
