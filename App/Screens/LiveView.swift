import SwiftUI

/// TV en direct : catégories et chaînes, programme en cours.
struct LiveView: View {
    @EnvironmentObject private var app: AppModel
    @Environment(\.horizontalSizeClass) private var sizeClass
    @State private var selection: String? = LiveView.favoritesId

    static let favoritesId = "__fav"
    static let recentId = "__recent"
    static let allId = "__all"

    private var chips: [CategoryChips.Chip] {
        var out: [CategoryChips.Chip] = []
        if !app.favoriteChannels.isEmpty { out.append(CategoryChips.Chip(id: Self.favoritesId, title: "Favoris", icon: "star.fill")) }
        if !app.recentChannels.isEmpty { out.append(CategoryChips.Chip(id: Self.recentId, title: "Récentes", icon: "clock.arrow.circlepath")) }
        out.append(CategoryChips.Chip(id: Self.allId, title: "Toutes", icon: "square.grid.2x2"))
        if let live = app.live {
            out += app.visible(live.categories).map { CategoryChips.Chip(id: $0.id, title: $0.name) }
        }
        return out
    }

    private var channels: [LiveChannel] {
        switch selection {
        case Self.favoritesId: return app.favoriteChannels
        case Self.recentId: return app.recentChannels
        case Self.allId, nil: return app.visibleChannels()
        case let id?: return app.visibleChannels(in: id)
        }
    }

    private var title: String {
        chips.first { $0.id == selection }?.title ?? "Chaînes"
    }

    private var useSidebar: Bool {
        #if os(tvOS)
        return true
        #else
        return sizeClass == .regular
        #endif
    }

    var body: some View {
        Group {
            if app.live == nil && app.liveLoading {
                LoadingView(text: "Chargement des chaînes…")
            } else if app.live == nil, let err = app.liveError {
                StateMessage(systemImage: "wifi.exclamationmark", title: "Impossible de charger les chaînes", message: err,
                             actionTitle: "Réessayer") { Task { await app.loadLive(force: true) } }
            } else if app.live?.channels.isEmpty ?? true {
                StateMessage(systemImage: "tv", title: "Aucune chaîne", message: "Ta source ne contient pas de chaînes en direct.")
            } else if useSidebar {
                HStack(alignment: .top, spacing: Theme.s(18)) {
                    sidebar
                    channelList
                }
                .padding(.leading, Theme.pagePadding)
            } else {
                VStack(spacing: 0) {
                    CategoryChips(chips: chips, selection: $selection)
                    channelList
                }
            }
        }
        .fluxScreen()
        .detailDestinations()
        .navigationTitle("TV en direct")
        #if os(iOS)
        .navigationBarTitleDisplayMode(.inline)
        #endif
        .onAppear(perform: fixSelection)
        .onChange(of: app.live?.channels.count ?? 0) { _, _ in fixSelection() }
    }

    /// Pas de favoris : on ouvre directement « Toutes ».
    private func fixSelection() {
        guard app.live != nil else { return }
        if !chips.contains(where: { $0.id == selection }) { selection = chips.first?.id }
        if selection == Self.favoritesId && app.favoriteChannels.isEmpty { selection = Self.allId }
    }

    private var sidebar: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Theme.s(4)) {
                ForEach(chips) { chip in
                    let selected = chip.id == selection
                    Button {
                        selection = chip.id
                    } label: {
                        HStack(spacing: Theme.s(10)) {
                            Image(systemName: chip.icon ?? "circle.fill")
                                .font(.system(size: Theme.s(chip.icon == nil ? 6 : 13), weight: .bold))
                                .frame(width: Theme.s(18))
                                .foregroundStyle(chip.id == Self.favoritesId ? Theme.gold : (selected ? Theme.accentBright : Theme.textFaint))
                            Text(chip.title).font(selected ? Theme.strong(14) : Theme.body(14)).lineLimit(1)
                            Spacer(minLength: 0)
                        }
                        .foregroundStyle(selected ? Theme.text : Theme.textDim)
                        .padding(.horizontal, Theme.s(12))
                        .padding(.vertical, Theme.s(10))
                        .background(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous)
                            .fill(selected ? Color.white.opacity(0.1) : Color.clear))
                    }
                    .buttonStyle(FluxButtonStyle(focusScale: 1.03, cornerRadius: 12))
                }
            }
            .padding(Theme.s(8))
        }
        .frame(width: Theme.s(240))
        .background(RoundedRectangle(cornerRadius: Theme.s(20), style: .continuous).fill(Color.white.opacity(0.04)))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(20), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
        .clipShape(RoundedRectangle(cornerRadius: Theme.s(20), style: .continuous))
        .padding(.vertical, Theme.s(8))
        .tvFocusSection()
    }

    private var channelList: some View {
        let list = channels
        return ScrollView {
            LazyVStack(alignment: .leading, spacing: Theme.s(8)) {
                HStack(alignment: .firstTextBaseline) {
                    Text(title).font(Theme.title(20)).foregroundStyle(Theme.text)
                    Spacer()
                    Text("\(list.count) chaîne\(list.count > 1 ? "s" : "")")
                        .font(Theme.label(12)).foregroundStyle(Theme.textFaint)
                }
                .padding(.bottom, Theme.s(4))
                if list.isEmpty {
                    Text(selection == Self.favoritesId ? "Appui long sur une chaîne pour l'ajouter à tes favoris." : "Aucune chaîne dans cette catégorie.")
                        .font(Theme.body(14)).foregroundStyle(Theme.textDim)
                        .padding(.vertical, Theme.s(30))
                }
                ForEach(list) { ch in
                    Button { app.play(channel: ch, in: list) } label: {
                        ChannelRow(channel: ch)
                    }
                    .buttonStyle(FluxButtonStyle(focusScale: 1.02, cornerRadius: 14))
                    .contextMenu {
                        let fav = app.library.isFavorite(ch.id)
                        Button { app.toggleFavorite(ch.ref(group: app.categoryName(ch.categoryId))) } label: {
                            Label(fav ? "Retirer des favoris" : "Ajouter aux favoris", systemImage: fav ? "star.slash" : "star")
                        }
                    }
                }
            }
            .padding(.horizontal, useSidebar ? Theme.s(4) : Theme.pagePadding)
            .padding(.trailing, useSidebar ? Theme.pagePadding : 0)
            .padding(.vertical, Theme.s(10))
        }
        .id(selection)
        .tvFocusSection()
    }
}
