import SwiftUI

/// Fiche d'une série : saisons, épisodes (résumé, avancement), reprise de l'épisode en cours.
struct SeriesDetailView: View {
    @EnvironmentObject private var app: AppModel
    let series: Series
    @State private var resolved: Series?
    @State private var details: SeriesDetails?
    @State private var season: Int?
    @State private var error: String?

    private var current: Series { resolved ?? series }
    private var episodes: [Episode] { details?.allEpisodes ?? [] }
    private var seasonEpisodes: [Episode] { season.flatMap { details?.seasons[$0] } ?? [] }
    private var cover: String? { details?.cover ?? current.cover }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                header
                VStack(alignment: .leading, spacing: Theme.s(18)) {
                    actions
                    if let plot = details?.plot ?? current.plot {
                        Text(plot).font(Theme.bodyRegular(15)).foregroundStyle(Theme.text.opacity(0.9))
                            .lineSpacing(Theme.s(3))
                            .lineLimit(6)
                            .frame(maxWidth: Theme.s(720), alignment: .leading)
                    }
                    if let error {
                        StateMessage(systemImage: "wifi.exclamationmark", title: "Épisodes indisponibles", message: error,
                                     actionTitle: "Réessayer") { Task { await load() } }
                            .frame(height: Theme.s(260))
                    } else if details == nil {
                        LoadingView(text: "Chargement des épisodes…").frame(height: Theme.s(200))
                    } else {
                        seasonPicker
                        episodeList
                    }
                }
                .padding(.horizontal, Theme.pagePadding)
                .padding(.top, Theme.s(18))
                .padding(.bottom, Theme.s(40))
            }
        }
        .ignoresSafeArea(edges: .top)
        .fluxScreen()
        #if os(iOS)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .task(id: series.id) { await load() }
    }

    private var header: some View {
        ZStack(alignment: .bottomLeading) {
            RemoteImage(url: details?.backdrop ?? cover, contentMode: .fill, maxSize: 1200, placeholderTitle: current.name)
                .filling(height: Theme.s(280))
                .overlay(LinearGradient(colors: [.clear, Theme.bg.opacity(0.6), Theme.bg], startPoint: .top, endPoint: .bottom))
            HStack(alignment: .bottom, spacing: Theme.s(18)) {
                RemoteImage(url: cover, contentMode: .fill, maxSize: 180, placeholderTitle: current.name)
                    .frame(width: Theme.s(112), height: Theme.s(168))
                    .clipShape(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous))
                    .overlay(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
                    .shadow(color: .black.opacity(0.5), radius: 18, y: 10)
                VStack(alignment: .leading, spacing: Theme.s(8)) {
                    Eyebrow(text: "Série")
                    Text(current.name).font(Theme.display(28)).foregroundStyle(.white).lineLimit(3)
                        .fixedSize(horizontal: false, vertical: true)
                    FlowLayout(spacing: Theme.s(6)) {
                        if let y = details?.year ?? current.year { Badge(text: String(y)) }
                        if let g = (details?.genre ?? current.genre)?.split(separator: ",").first {
                            Badge(text: String(g).trimmingCharacters(in: .whitespaces))
                        }
                        if let d = details, !d.seasons.isEmpty {
                            Badge(text: "\(d.seasons.count) saison\(d.seasons.count > 1 ? "s" : "")")
                        }
                        if let r = details?.rating ?? current.rating { Badge(text: "★ " + String(format: "%.1f", r), style: .gold) }
                    }
                }
            }
            .padding(.horizontal, Theme.pagePadding)
            .padding(.bottom, Theme.s(4))
        }
    }

    private var actions: some View {
        HStack(spacing: Theme.s(12)) {
            if let next = app.library.data.nextEpisode(in: episodes) {
                PillButton(title: next.resume != nil ? "Reprendre \(next.episode.label)" :
                            (next.episode.id == episodes.first?.id ? "Lecture \(next.episode.label)" : "Épisode suivant \(next.episode.label)"),
                           systemImage: "play.fill") {
                    play(next.episode)
                }
            }
            let ref = current.ref(group: nil)
            let fav = app.library.isFavorite(ref.id)
            RoundIconButton(systemImage: fav ? "star.fill" : "star", tint: fav ? Theme.gold : Theme.text,
                            label: fav ? "Retirer des favoris" : "Ajouter aux favoris") {
                var r = ref
                r.image = cover
                app.toggleFavorite(r)
            }
        }
    }

    @ViewBuilder private var seasonPicker: some View {
        if let d = details, d.seasons.count > 1 {
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: Theme.s(8)) {
                    ForEach(d.seasonNumbers, id: \.self) { n in
                        let selected = n == season
                        Button { withAnimation(.easeOut(duration: 0.2)) { season = n } } label: {
                            Text("Saison \(n)")
                                .font(Theme.label(13))
                                .padding(.horizontal, Theme.s(14)).padding(.vertical, Theme.s(8))
                                .foregroundStyle(selected ? Color.white : Theme.textDim)
                                .background(Capsule().fill(selected ? AnyShapeStyle(Theme.accentGradient) : AnyShapeStyle(Color.white.opacity(0.07))))
                        }
                        .buttonStyle(FluxButtonStyle(focusScale: 1.08, cornerRadius: 40))
                    }
                }
                .padding(.vertical, Theme.s(6))
            }
            .tvFocusSection()
        }
    }

    private var episodeList: some View {
        LazyVStack(alignment: .leading, spacing: Theme.s(10)) {
            ForEach(seasonEpisodes) { ep in
                Button { play(ep) } label: { EpisodeRow(episode: ep, fallbackImage: cover) }
                    .buttonStyle(FluxButtonStyle(focusScale: 1.02, cornerRadius: 16))
                    .contextMenu {
                        let watched = app.library.isWatched(ep.id)
                        Button { app.library.setWatched(ep.id, !watched) } label: {
                            Label(watched ? "Marquer comme non vu" : "Marquer comme vu", systemImage: watched ? "eye.slash" : "eye")
                        }
                        if app.library.resume(ep.id) != nil {
                            Button { play(ep, fromStart: true) } label: { Label("Lire depuis le début", systemImage: "arrow.counterclockwise") }
                        }
                    }
            }
        }
        .tvFocusSection()
    }

    private func play(_ ep: Episode, fromStart: Bool = false) {
        app.play(episode: ep, of: current, episodes: episodes, cover: cover, fromStart: fromStart)
    }

    private func load() async {
        guard let repo = app.repo else { return }
        error = nil
        do {
            var s = series
            // Série de playlist (ou de la démo) ouverte depuis un favori : on retrouve ses épisodes
            if s.episodes == nil, s.id.hasPrefix("m3us:") {
                if let found = try await repo.allSeries().first(where: { $0.id == s.id }) { s = found }
            }
            resolved = s
            let d = try await repo.seriesDetails(s)
            details = d
            let next = app.library.data.nextEpisode(in: d.allEpisodes)?.episode
            season = next?.season ?? d.seasonNumbers.first
            if d.allEpisodes.isEmpty { error = "Aucun épisode disponible pour le moment." }
        } catch {
            self.error = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        }
    }
}

/// Ligne d'épisode : vignette, numéro et titre, résumé, durée, avancement.
private struct EpisodeRow: View {
    @EnvironmentObject private var app: AppModel
    let episode: Episode
    let fallbackImage: String?

    var body: some View {
        let resume = app.library.resume(episode.id)
        let watched = app.library.isWatched(episode.id)
        HStack(alignment: .top, spacing: Theme.s(14)) {
            ZStack(alignment: .bottomLeading) {
                RemoteImage(url: episode.image ?? fallbackImage, contentMode: .fill, maxSize: 200, placeholderTitle: episode.label)
                    .frame(width: Theme.s(150), height: Theme.s(84))
                    .clipped()
                if let r = resume {
                    ProgressLine(value: r.fraction, color: Theme.accent, height: 3)
                        .padding(.horizontal, Theme.s(6)).padding(.bottom, Theme.s(6))
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: Theme.s(10), style: .continuous))
            .overlay(alignment: .center) {
                Image(systemName: "play.fill")
                    .font(.system(size: Theme.s(14), weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: Theme.s(32), height: Theme.s(32))
                    .background(Circle().fill(.black.opacity(0.45)))
            }

            VStack(alignment: .leading, spacing: Theme.s(4)) {
                HStack(spacing: Theme.s(8)) {
                    Text(episode.label).font(Theme.mono(12)).foregroundStyle(Theme.accentBright)
                    if watched { Badge(text: "Vu", style: .accent) }
                    if let d = episode.durationSecs, d > 0 {
                        Text(Format.duration(Double(d))).font(Theme.label(11)).foregroundStyle(Theme.textFaint)
                    }
                }
                Text(episode.title).font(Theme.label(15)).foregroundStyle(Theme.text).lineLimit(2)
                if let plot = episode.plot {
                    Text(plot).font(Theme.bodyRegular(12)).foregroundStyle(Theme.textDim).lineLimit(2)
                } else if let r = resume, r.duration > 0 {
                    Text("Reste \(Format.duration(r.duration - r.position))").font(Theme.body(12)).foregroundStyle(Theme.textDim)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(Theme.s(10))
        .background(RoundedRectangle(cornerRadius: Theme.s(16), style: .continuous).fill(Color.white.opacity(0.045)))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(16), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
        .opacity(watched ? 0.75 : 1)
        .contentShape(Rectangle())
    }
}
