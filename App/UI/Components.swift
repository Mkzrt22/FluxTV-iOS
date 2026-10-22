import SwiftUI

// ───────────────────────────── Cartes ─────────────────────────────

/// Affiche verticale (film, série) avec titre, année ou avancement.
struct PosterCard: View {
    let title: String
    let image: String?
    var subtitle: String?
    var rating: Double?
    var badges: [String] = []
    var progress: Double?
    var watched = false
    var width: CGFloat = 116

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.s(6)) {
            ZStack(alignment: .bottomLeading) {
                RemoteImage(url: image, contentMode: .fill, maxSize: width * 1.5, placeholderTitle: title)
                    .frame(width: Theme.s(width), height: Theme.s(width * 1.5))
                    .clipped()
                if let progress {
                    ProgressLine(value: progress, color: Theme.accent, height: 4)
                        .padding(.horizontal, Theme.s(8)).padding(.bottom, Theme.s(8))
                }
            }
            .overlay(alignment: .topLeading) {
                HStack(spacing: Theme.s(4)) {
                    if watched { Badge(text: "Vu", style: .accent) }
                    ForEach(badges.prefix(2), id: \.self) { Badge(text: $0) }
                }
                .padding(Theme.s(6))
            }
            .clipShape(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))

            Text(title)
                .font(Theme.label(13))
                .foregroundStyle(Theme.text)
                .lineLimit(1)
            HStack(spacing: Theme.s(6)) {
                if let subtitle { Text(subtitle).foregroundStyle(Theme.textFaint) }
                if let rating { RatingLabel(rating: rating) }
            }
            .font(Theme.body(11))
            .lineLimit(1)
        }
        .frame(width: Theme.s(width), alignment: .leading)
        .contentShape(Rectangle())
    }
}

/// Vignette horizontale 16:9 (reprises, épisodes, chaînes de l'accueil).
struct WideCard: View {
    let title: String
    let image: String?
    var subtitle: String?
    var progress: Double?
    var progressColor: Color = Theme.accent
    var live = false
    var number: Int?
    var fitImage = false
    var width: CGFloat = 220

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.s(6)) {
            ZStack(alignment: .bottomLeading) {
                ZStack {
                    LinearGradient(colors: [Theme.surface3, Theme.surface], startPoint: .topLeading, endPoint: .bottomTrailing)
                    RemoteImage(url: image, contentMode: fitImage ? .fit : .fill, maxSize: width, placeholderTitle: title,
                                placeholderIcon: live ? "tv" : "film")
                        .padding(fitImage ? Theme.s(width * 0.14) : 0)
                }
                .frame(width: Theme.s(width), height: Theme.s(width * 9 / 16))
                .clipped()
                if let progress {
                    ProgressLine(value: progress, color: progressColor, height: 4)
                        .padding(.horizontal, Theme.s(10)).padding(.bottom, Theme.s(9))
                }
            }
            .overlay(alignment: .topLeading) {
                HStack(spacing: Theme.s(5)) {
                    if let number, number > 0 {
                        Text(String(format: "%03d", number))
                            .font(Theme.mono(10)).foregroundStyle(Theme.text)
                            .padding(.horizontal, Theme.s(6)).padding(.vertical, Theme.s(2))
                            .background(RoundedRectangle(cornerRadius: Theme.s(5)).fill(Color.black.opacity(0.45)))
                    }
                    if live { Badge(text: "Direct", style: .live) }
                }
                .padding(Theme.s(8))
            }
            .clipShape(RoundedRectangle(cornerRadius: Theme.s(14), style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Theme.s(14), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))

            Text(title).font(Theme.label(13)).foregroundStyle(Theme.text).lineLimit(1)
            if let subtitle {
                Text(subtitle).font(Theme.body(11)).foregroundStyle(Theme.textFaint).lineLimit(1)
            }
        }
        .frame(width: Theme.s(width), alignment: .leading)
        .contentShape(Rectangle())
    }
}

/// Ligne de chaîne : numéro, logo, nom, programme en cours avec avancement.
struct ChannelRow: View {
    @EnvironmentObject private var app: AppModel
    let channel: LiveChannel
    var playing = false

    var body: some View {
        let nn = app.nowNext(channel)
        HStack(spacing: Theme.s(12)) {
            Text(channel.num > 0 ? String(format: "%03d", channel.num) : "")
                .font(Theme.mono(12)).foregroundStyle(Theme.textFaint)
                .frame(width: Theme.s(34), alignment: .trailing)
            RemoteImage(url: channel.logo, contentMode: .fit, maxSize: 80, placeholderTitle: channel.name, placeholderIcon: "tv")
                .padding(Theme.s(6))
                .frame(width: Theme.s(72), height: Theme.s(46))
                .background(RoundedRectangle(cornerRadius: Theme.s(10), style: .continuous).fill(Color.white.opacity(0.07)))
                .clipShape(RoundedRectangle(cornerRadius: Theme.s(10), style: .continuous))
            VStack(alignment: .leading, spacing: Theme.s(4)) {
                HStack(spacing: Theme.s(6)) {
                    Text(channel.name).font(Theme.label(15)).foregroundStyle(playing ? Theme.accentBright : Theme.text).lineLimit(1)
                    ForEach(channel.badges.prefix(2), id: \.self) { Badge(text: $0) }
                    if channel.archiveDays > 0 { Badge(text: "Replay", style: .accent) }
                    if app.library.isFavorite(channel.id) {
                        Image(systemName: "star.fill").font(.system(size: Theme.s(10))).foregroundStyle(Theme.gold)
                    }
                }
                if let now = nn.now {
                    HStack(spacing: Theme.s(6)) {
                        Text(Format.hour.string(from: now.start)).font(Theme.mono(11)).foregroundStyle(Theme.textDim)
                        Text(now.title).font(Theme.body(12)).foregroundStyle(Theme.textDim).lineLimit(1)
                    }
                    ProgressLine(value: now.progress(), color: Theme.accent, height: 3)
                } else if let next = nn.next {
                    Text("À \(Format.hour.string(from: next.start)) · \(next.title)").font(Theme.body(12)).foregroundStyle(Theme.textFaint).lineLimit(1)
                }
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, Theme.s(12))
        .padding(.vertical, Theme.s(9))
        .background(RoundedRectangle(cornerRadius: Theme.s(14), style: .continuous).fill(playing ? Theme.accent.opacity(0.2) : Color.white.opacity(0.045)))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(14), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
        .contentShape(Rectangle())
        .onAppear { app.requestProgrammes(for: channel) }
    }
}

// ───────────────────────────── Rangées, filtres ─────────────────────────────

/// Rangée horizontale avec titre.
struct CardRow<Item: Identifiable, Card: View>: View {
    let title: String
    let items: [Item]
    var spacing: CGFloat = 14
    @ViewBuilder let card: (Item) -> Card

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.s(10)) {
            SectionTitle(title: title)
                .padding(.horizontal, Theme.pagePadding)
            ScrollView(.horizontal, showsIndicators: false) {
                LazyHStack(alignment: .top, spacing: Theme.s(spacing)) {
                    ForEach(items) { card($0) }
                }
                .padding(.horizontal, Theme.pagePadding)
                .padding(.vertical, Theme.s(10))
            }
            #if os(tvOS)
            .scrollClipDisabled()
            #endif
        }
        .tvFocusSection()
    }
}

/// Filtres de catégories (pastilles horizontales).
struct CategoryChips: View {
    struct Chip: Identifiable, Hashable {
        let id: String
        let title: String
        var icon: String?
    }

    let chips: [Chip]
    @Binding var selection: String?

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView(.horizontal, showsIndicators: false) {
                LazyHStack(spacing: Theme.s(8)) {
                    ForEach(chips) { chip in
                        let selected = chip.id == selection
                        Button {
                            withAnimation(.easeOut(duration: 0.2)) { selection = chip.id }
                        } label: {
                            HStack(spacing: Theme.s(6)) {
                                if let icon = chip.icon { Image(systemName: icon).font(.system(size: Theme.s(12), weight: .bold)) }
                                Text(chip.title).font(Theme.label(13)).lineLimit(1)
                            }
                            .padding(.horizontal, Theme.s(14))
                            .padding(.vertical, Theme.s(8))
                            .foregroundStyle(selected ? Color.white : Theme.textDim)
                            .background(Capsule().fill(selected ? AnyShapeStyle(Theme.accentGradient) : AnyShapeStyle(Color.white.opacity(0.07))))
                            .overlay(Capsule().strokeBorder(Color.white.opacity(selected ? 0 : 0.1), lineWidth: 1))
                        }
                        .buttonStyle(FluxButtonStyle(focusScale: 1.08, cornerRadius: 40))
                        .id(chip.id)
                    }
                }
                .padding(.horizontal, Theme.pagePadding)
                .padding(.vertical, Theme.s(8))
            }
            // Hauteur des pastilles : sans cela, la rangée s'étire dans un écran sans défilement
            .fixedSize(horizontal: false, vertical: true)
            .onAppear { if let s = selection { proxy.scrollTo(s, anchor: .center) } }
        }
        .tvFocusSection()
    }
}

/// Taille des affiches dans les grilles : trois par ligne sur iPhone (largeur compacte).
enum PosterLayout {
    static func minWidth(_ sizeClass: UserInterfaceSizeClass?) -> CGFloat { sizeClass == .compact ? 96 : 112 }
    static func cardWidth(_ sizeClass: UserInterfaceSizeClass?) -> CGFloat { sizeClass == .compact ? 98 : 116 }
}

/// Grille d'affiches adaptée à la largeur de l'écran.
struct PosterGrid<Item: Identifiable, Card: View>: View {
    let items: [Item]
    var minWidth: CGFloat = 112
    @ViewBuilder let card: (Item) -> Card

    var body: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: Theme.s(minWidth), maximum: Theme.s(minWidth * 1.35)), spacing: Theme.s(14), alignment: .top)],
                  alignment: .leading, spacing: Theme.s(18)) {
            ForEach(items) { card($0) }
        }
        .padding(.horizontal, Theme.pagePadding)
    }
}

/// Éléments côte à côte qui passent à la ligne quand la largeur manque (badges, boutons) : une
/// rangée trop longue ne pousse plus l'écran hors des bords.
struct FlowLayout: Layout {
    var spacing: CGFloat = 8

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let rows = arrange(subviews, maxWidth: proposal.width ?? .infinity)
        let height = rows.reduce(0) { $0 + $1.height } + spacing * CGFloat(max(0, rows.count - 1))
        return CGSize(width: rows.map(\.width).max() ?? 0, height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        var y = bounds.minY
        for row in arrange(subviews, maxWidth: bounds.width) {
            var x = bounds.minX
            for (index, size) in zip(row.indices, row.sizes) {
                subviews[index].place(at: CGPoint(x: x, y: y + (row.height - size.height) / 2), proposal: ProposedViewSize(size))
                x += size.width + spacing
            }
            y += row.height + spacing
        }
    }

    private struct Row {
        var indices: [Int] = []
        var sizes: [CGSize] = []
        var width: CGFloat = 0
        var height: CGFloat = 0
    }

    private func arrange(_ subviews: Subviews, maxWidth: CGFloat) -> [Row] {
        var rows: [Row] = []
        var row = Row()
        for (index, view) in subviews.enumerated() {
            let size = view.sizeThatFits(.unspecified)
            if !row.indices.isEmpty && row.width + spacing + size.width > maxWidth {
                rows.append(row)
                row = Row()
            }
            row.width += (row.indices.isEmpty ? 0 : spacing) + size.width
            row.height = max(row.height, size.height)
            row.indices.append(index)
            row.sizes.append(size)
        }
        if !row.indices.isEmpty { rows.append(row) }
        return rows
    }
}

/// Champ de saisie de la charte.
struct FluxField: View {
    let label: String
    @Binding var text: String
    var placeholder = ""
    var secure = false
    #if os(iOS)
    var keyboard: UIKeyboardType = .default
    #endif

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.s(6)) {
            Eyebrow(text: label, color: Theme.textDim)
            Group {
                if secure {
                    SecureField(placeholder, text: $text)
                } else {
                    TextField(placeholder, text: $text)
                }
            }
            .font(Theme.body(16))
            .foregroundStyle(Theme.text)
            .textInputAutocapitalization(.never)
            .autocorrectionDisabled(true)
            #if os(iOS)
            .keyboardType(keyboard)
            .padding(.horizontal, Theme.s(14))
            .padding(.vertical, Theme.s(12))
            .background(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).fill(Color.white.opacity(0.06)))
            .overlay(RoundedRectangle(cornerRadius: Theme.s(12), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
            #endif
        }
    }
}

/// Bandeau d'information temporaire (« Ajouté aux favoris »).
struct ToastView: View {
    let text: String
    var body: some View {
        HStack(spacing: Theme.s(10)) {
            Image(systemName: "checkmark.circle.fill").foregroundStyle(Theme.ok)
            Text(text).font(Theme.label(14)).foregroundStyle(Theme.text)
        }
        .padding(.horizontal, Theme.s(18))
        .padding(.vertical, Theme.s(12))
        .background(Capsule().fill(Theme.surface2.opacity(0.97)))
        .overlay(Capsule().strokeBorder(Theme.stroke, lineWidth: 1))
        .shadow(color: .black.opacity(0.4), radius: 16, y: 8)
    }
}

extension Movie {
    /// Année affichable (« 2021 »).
    var yearText: String? { year.map { String($0) } }
}

extension Series {
    var yearText: String? { year.map { String($0) } }
}
