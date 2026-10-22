import SwiftUI

/// Fiche d'un film : grand visuel, résumé, casting, lecture / reprise, favori.
struct MovieDetailView: View {
    @EnvironmentObject private var app: AppModel
    @Environment(\.openURL) private var openURL
    let movie: Movie
    @State private var info: MovieInfo?
    @State private var loading = true

    private var resume: PlaybackPosition? { app.library.resume(movie.id) }
    private var backdrop: String? { info?.backdrop ?? info?.cover ?? movie.poster }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                ZStack(alignment: .bottomLeading) {
                    RemoteImage(url: backdrop, contentMode: .fill, maxSize: 1200, placeholderTitle: movie.name)
                        .filling(height: Theme.s(300))
                        .overlay(LinearGradient(colors: [.clear, Theme.bg.opacity(0.65), Theme.bg], startPoint: .top, endPoint: .bottom))
                    HStack(alignment: .bottom, spacing: Theme.s(18)) {
                        RemoteImage(url: info?.cover ?? movie.poster, contentMode: .fill, maxSize: 180, placeholderTitle: movie.name)
                            .frame(width: Theme.s(118), height: Theme.s(177))
                            .clipShape(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous))
                            .overlay(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
                            .shadow(color: .black.opacity(0.5), radius: 18, y: 10)
                        VStack(alignment: .leading, spacing: Theme.s(8)) {
                            Eyebrow(text: "Film")
                            Text(movie.name).font(Theme.display(28)).foregroundStyle(.white).lineLimit(3)
                                .fixedSize(horizontal: false, vertical: true)
                            chips
                        }
                    }
                    .padding(.horizontal, Theme.pagePadding)
                    .padding(.bottom, Theme.s(4))
                }

                VStack(alignment: .leading, spacing: Theme.s(18)) {
                    actions
                    if let r = resume, r.duration > 0 {
                        VStack(alignment: .leading, spacing: Theme.s(6)) {
                            ProgressLine(value: r.fraction, color: Theme.accent, height: 4).frame(maxWidth: Theme.s(320))
                            Text("Reste \(Format.duration(r.duration - r.position))").font(Theme.label(12)).foregroundStyle(Theme.textDim)
                        }
                    }
                    if let plot = info?.plot {
                        Text(plot).font(Theme.bodyRegular(15)).foregroundStyle(Theme.text.opacity(0.9))
                            .lineSpacing(Theme.s(3))
                            .frame(maxWidth: Theme.s(720), alignment: .leading)
                            .fixedSize(horizontal: false, vertical: true)
                    } else if loading {
                        ProgressView().tint(Theme.accentBright)
                    }
                    credits
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
        .task(id: movie.id) {
            loading = true
            info = try? await app.repo?.movieInfo(movie)
            loading = false
        }
    }

    private var chips: some View {
        FlowLayout(spacing: Theme.s(6)) {
            if let y = info?.year ?? movie.year { Badge(text: String(y)) }
            if let d = info?.durationSecs, d > 0 { Badge(text: Format.duration(Double(d))) }
            if let g = info?.genre?.split(separator: ",").first {
                Badge(text: String(g).trimmingCharacters(in: .whitespaces))
            }
            ForEach(movie.badges, id: \.self) { Badge(text: $0, style: .accent) }
            if let r = info?.rating ?? movie.rating { Badge(text: "★ " + String(format: "%.1f", r), style: .gold) }
        }
    }

    private var actions: some View {
        FlowLayout(spacing: Theme.s(12)) {
            if let r = resume {
                PillButton(title: "Reprendre à \(Format.duration(r.position))", systemImage: "play.fill") {
                    app.play(movie: movie, group: info?.genre)
                }
                RoundIconButton(systemImage: "arrow.counterclockwise", label: "Lire depuis le début") {
                    app.play(movie: movie, group: info?.genre, fromStart: true)
                }
            } else {
                PillButton(title: "Lecture", systemImage: "play.fill") { app.play(movie: movie, group: info?.genre) }
            }
            let fav = app.library.isFavorite(movie.id)
            RoundIconButton(systemImage: fav ? "star.fill" : "star", tint: fav ? Theme.gold : Theme.text,
                            label: fav ? "Retirer des favoris" : "Ajouter aux favoris") {
                var ref = movie.ref(group: nil)
                ref.image = movie.poster ?? info?.cover
                app.toggleFavorite(ref)
            }
            if app.library.isWatched(movie.id) {
                Badge(text: "Vu", style: .accent)
            }
            #if os(iOS)
            if let t = info?.trailer, let url = URL(string: "https://www.youtube.com/watch?v=\(t)") {
                RoundIconButton(systemImage: "play.rectangle", label: "Bande-annonce") { openURL(url) }
            }
            #endif
        }
    }

    @ViewBuilder private var credits: some View {
        if let d = info?.director {
            credit("Réalisation", d)
        }
        if let c = info?.cast {
            credit("Avec", c)
        }
    }

    private func credit(_ label: String, _ value: String) -> some View {
        (Text(label + "  ").font(Theme.label(13)).foregroundStyle(Theme.textFaint)
         + Text(value).font(Theme.body(13)).foregroundStyle(Theme.textDim))
            .lineLimit(3)
            .frame(maxWidth: Theme.s(720), alignment: .leading)
    }
}
