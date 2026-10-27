import SwiftUI

/// Charte FluxTV : nuit profonde, indigo et violet, typographies Sora (titres) et Manrope.
enum Theme {
    // ───────────────────────────── Couleurs ─────────────────────────────
    static let abyss = Color(hex: 0x04040D)
    static let bg = Color(hex: 0x080818)
    static let surface = Color(hex: 0x10102A)
    static let surface2 = Color(hex: 0x181838)
    static let surface3 = Color(hex: 0x23235A)
    static let accent = Color(hex: 0x5B5CF0)
    static let accentBright = Color(hex: 0x8E97FF)
    static let violet = Color(hex: 0x9B4DEB)
    static let violetBright = Color(hex: 0xC084FC)
    static let onAir = Color(hex: 0xF43F5E)
    static let ok = Color(hex: 0x34D399)
    static let gold = Color(hex: 0xFBBF24)
    static let text = Color(hex: 0xF1F2FF)
    static let textDim = Color(hex: 0xA6ABD2)
    static let textFaint = Color(hex: 0x7D83B3)
    static let glass = Color.white.opacity(0.07)
    static let glassStrong = Color.white.opacity(0.14)
    static let stroke = Color.white.opacity(0.10)
    static let error = Color(hex: 0xFB7185)

    static let accentGradient = LinearGradient(colors: [accent, violet], startPoint: .leading, endPoint: .trailing)

    // ───────────────────────────── Échelle ─────────────────────────────
    /// L'Apple TV se regarde à 3 mètres : tout est plus grand.
    #if os(tvOS)
    static let scale: CGFloat = 1.55
    static let pagePadding: CGFloat = 80
    #else
    static let scale: CGFloat = 1
    static let pagePadding: CGFloat = 20
    #endif

    static func s(_ v: CGFloat) -> CGFloat { v * scale }

    // ───────────────────────────── Typographie ─────────────────────────────
    static func display(_ size: CGFloat = 34) -> Font { .custom("Sora-Bold", size: s(size)) }
    static func title(_ size: CGFloat = 22) -> Font { .custom("Sora-SemiBold", size: s(size)) }
    static func body(_ size: CGFloat = 15) -> Font { .custom("Manrope-Medium", size: s(size)) }
    static func bodyRegular(_ size: CGFloat = 15) -> Font { .custom("Manrope-Regular", size: s(size)) }
    static func label(_ size: CGFloat = 15) -> Font { .custom("Manrope-SemiBold", size: s(size)) }
    static func strong(_ size: CGFloat = 15) -> Font { .custom("Manrope-Bold", size: s(size)) }
    static func eyebrow(_ size: CGFloat = 11) -> Font { .custom("Manrope-Bold", size: s(size)) }
    static func mono(_ size: CGFloat = 13) -> Font { .custom("Manrope-SemiBold", size: s(size)).monospacedDigit() }
}

extension Color {
    init(hex: UInt32, opacity: Double = 1) {
        self.init(.sRGB,
                  red: Double((hex >> 16) & 0xFF) / 255,
                  green: Double((hex >> 8) & 0xFF) / 255,
                  blue: Double(hex & 0xFF) / 255,
                  opacity: opacity)
    }
}

/// Fond de l'app : nuit dégradée et deux lueurs (indigo, violet).
struct AppBackground: View {
    var body: some View {
        ZStack {
            LinearGradient(colors: [Theme.bg, Theme.abyss], startPoint: .top, endPoint: .bottom)
            RadialGradient(colors: [Theme.accent.opacity(0.22), .clear], center: UnitPoint(x: 0.12, y: 0.0),
                           startRadius: 0, endRadius: Theme.s(520))
            RadialGradient(colors: [Theme.violet.opacity(0.16), .clear], center: UnitPoint(x: 0.95, y: 0.25),
                           startRadius: 0, endRadius: Theme.s(460))
        }
        .ignoresSafeArea()
    }
}

/// Petite étiquette en capitales espacées (« EN DIRECT », « FILMS »…).
struct Eyebrow: View {
    let text: String
    var color: Color = Theme.accentBright
    var body: some View {
        Text(text.uppercased())
            .font(Theme.eyebrow())
            .tracking(Theme.s(1.6))
            .foregroundStyle(color)
    }
}

/// Badge (DIRECT, FHD, 4K, MULTI…).
struct Badge: View {
    let text: String
    var style: Style = .neutral

    enum Style { case neutral, live, accent, gold }

    var body: some View {
        HStack(spacing: Theme.s(4)) {
            if style == .live {
                Circle().fill(Color.white).frame(width: Theme.s(5), height: Theme.s(5))
            }
            Text(text.uppercased())
                .font(Theme.eyebrow(10))
                .tracking(Theme.s(0.8))
        }
        .padding(.horizontal, Theme.s(7))
        .padding(.vertical, Theme.s(3))
        .foregroundStyle(foreground)
        .background(background, in: RoundedRectangle(cornerRadius: Theme.s(6), style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: Theme.s(6), style: .continuous).strokeBorder(border, lineWidth: 1))
    }

    private var foreground: Color {
        switch style {
        case .live: return .white
        case .gold: return Theme.gold
        case .accent: return Theme.accentBright
        case .neutral: return Theme.textDim
        }
    }

    private var background: Color {
        switch style {
        case .live: return Theme.onAir
        case .gold: return Theme.gold.opacity(0.12)
        case .accent: return Theme.accent.opacity(0.18)
        case .neutral: return Color.white.opacity(0.08)
        }
    }

    private var border: Color {
        switch style {
        case .live: return .clear
        case .gold: return Theme.gold.opacity(0.4)
        case .accent: return Theme.accentBright.opacity(0.45)
        case .neutral: return Color.white.opacity(0.12)
        }
    }
}

/// Barre d'avancement fine (programme en cours, reprise).
struct ProgressLine: View {
    let value: Double
    var color: Color = Theme.accent
    var height: CGFloat = 4

    var body: some View {
        GeometryReader { g in
            ZStack(alignment: .leading) {
                Capsule().fill(Color.white.opacity(0.14))
                Capsule().fill(color).frame(width: max(0, min(1, value)) * g.size.width)
            }
        }
        .frame(height: Theme.s(height))
    }
}

/// Note sur 10 (« ★ 7.8 »).
struct RatingLabel: View {
    let rating: Double
    var body: some View {
        Text("★ " + String(format: "%.1f", rating))
            .font(Theme.label(13))
            .foregroundStyle(Theme.gold)
    }
}

/// Bouton de la charte : focus Apple TV (agrandi, lueur), appui iPhone (léger enfoncement).
struct FluxButtonStyle: ButtonStyle {
    var focusScale: CGFloat = 1.06
    var cornerRadius: CGFloat = 14
    var showsRing = true

    func makeBody(configuration: Configuration) -> some View {
        FluxButtonBody(configuration: configuration, focusScale: focusScale, cornerRadius: cornerRadius, showsRing: showsRing)
    }
}

private struct FluxButtonBody: View {
    @Environment(\.isFocused) private var focused
    let configuration: ButtonStyleConfiguration
    let focusScale: CGFloat
    let cornerRadius: CGFloat
    let showsRing: Bool

    var body: some View {
        configuration.label
            .overlay(
                RoundedRectangle(cornerRadius: Theme.s(cornerRadius), style: .continuous)
                    .strokeBorder(Color.white, lineWidth: focused && showsRing ? Theme.s(3) : 0)
            )
            .scaleEffect(configuration.isPressed ? 0.97 : (focused ? focusScale : 1))
            .shadow(color: focused ? Theme.accent.opacity(0.55) : .clear, radius: focused ? Theme.s(18) : 0, y: focused ? Theme.s(8) : 0)
            .brightness(configuration.isPressed ? -0.05 : 0)
            .animation(.spring(response: 0.28, dampingFraction: 0.78), value: focused)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

/// Bouton principal (dégradé indigo → violet) ou secondaire (verre).
struct PillButton: View {
    let title: String
    var systemImage: String?
    var primary = true
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: Theme.s(8)) {
                if let systemImage { Image(systemName: systemImage).font(.system(size: Theme.s(15), weight: .bold)) }
                Text(title).font(Theme.strong(15)).lineLimit(1)
            }
            .padding(.horizontal, Theme.s(20))
            .padding(.vertical, Theme.s(12))
            .foregroundStyle(primary ? Color.white : Theme.text)
            .background {
                if primary {
                    Capsule().fill(Theme.accentGradient)
                } else {
                    Capsule().fill(Theme.glassStrong)
                }
            }
            .overlay(Capsule().strokeBorder(Color.white.opacity(primary ? 0 : 0.12), lineWidth: 1))
            .contentShape(Capsule())
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.08, cornerRadius: 40))
    }
}

/// Bouton rond à icône (favori, recommencer…).
struct RoundIconButton: View {
    let systemImage: String
    var tint: Color = Theme.text
    var label: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: Theme.s(17), weight: .semibold))
                .foregroundStyle(tint)
                .frame(width: Theme.s(46), height: Theme.s(46))
                .background(Circle().fill(Theme.glassStrong))
                .overlay(Circle().strokeBorder(Color.white.opacity(0.12), lineWidth: 1))
                .contentShape(Circle())
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.12, cornerRadius: 40))
        .accessibilityLabel(label)
    }
}

/// Titre de rangée.
struct SectionTitle: View {
    let title: String
    var count: Int?
    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Theme.s(10)) {
            Text(title).font(Theme.title(19)).foregroundStyle(Theme.text)
            if let count { Text("\(count)").font(Theme.label(13)).foregroundStyle(Theme.textFaint) }
            Spacer(minLength: 0)
        }
    }
}

/// Message d'état (vide, erreur, chargement) au centre d'un écran.
struct StateMessage: View {
    let systemImage: String
    let title: String
    var message: String?
    var actionTitle: String?
    var action: (() -> Void)?

    var body: some View {
        VStack(spacing: Theme.s(12)) {
            Image(systemName: systemImage)
                .font(.system(size: Theme.s(36), weight: .semibold))
                .foregroundStyle(Theme.accentBright)
                .frame(width: Theme.s(76), height: Theme.s(76))
                .background(Circle().fill(Theme.accent.opacity(0.14)))
            Text(title).font(Theme.title(18)).foregroundStyle(Theme.text).multilineTextAlignment(.center)
            if let message {
                Text(message).font(Theme.bodyRegular(14)).foregroundStyle(Theme.textDim).multilineTextAlignment(.center)
                    .frame(maxWidth: Theme.s(420))
            }
            if let actionTitle, let action {
                PillButton(title: actionTitle, systemImage: "arrow.clockwise", action: action).padding(.top, Theme.s(6))
            }
        }
        .padding(Theme.s(24))
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

/// Indicateur de chargement de la charte.
struct LoadingView: View {
    var text = T("Chargement…", "Loading…")
    var body: some View {
        VStack(spacing: Theme.s(14)) {
            ProgressView().tint(Theme.accentBright).scaleEffect(Theme.scale)
            Text(text).font(Theme.label(14)).foregroundStyle(Theme.textDim)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

/// Logo FluxTV (pastille + nom).
struct BrandLogo: View {
    var size: CGFloat = 30
    var body: some View {
        HStack(spacing: Theme.s(10)) {
            Image("BrandMark")
                .resizable()
                .scaledToFit()
                .frame(width: Theme.s(size), height: Theme.s(size))
                .clipShape(RoundedRectangle(cornerRadius: Theme.s(size * 0.24), style: .continuous))
            Text("FluxTV").font(Theme.display(size * 0.62)).foregroundStyle(Theme.text)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("FluxTV")
    }
}

extension View {
    /// Groupe de navigation Apple TV (rangées) ; sans effet ailleurs.
    @ViewBuilder func tvFocusSection() -> some View {
        #if os(tvOS)
        self.focusSection()
        #else
        self
        #endif
    }
}

/// Durées, heures et dates lisibles, dans la langue de l'app (voir `Tr`).
enum Format {
    /// « 1 h 58 », « 42 min » ; en anglais « 1h 58 ».
    static func duration(_ seconds: Double) -> String {
        let s = Int(max(0, seconds))
        let h = s / 3600, m = (s % 3600) / 60
        return h > 0 ? String(format: T("%d h %02d", "%dh %02d"), h, m) : "\(m) min"
    }

    /// Bouton de reprise : « Reprendre à 42 min », ou « Reprendre » avant la première minute.
    static func resume(_ seconds: Double) -> String {
        seconds < 60 ? T("Reprendre", "Resume") : T("Reprendre à \(duration(seconds))", "Resume at \(duration(seconds))")
    }

    /// Temps restant : « Reste 42 min » ; en anglais « 42 min left ».
    static func remaining(_ seconds: Double) -> String {
        T("Reste \(duration(seconds))", "\(duration(seconds)) left")
    }

    /// Nombre et nom accordé : « 1 chaîne », « 2 chaînes » ; en anglais « 1 channel », « 0 channels »
    /// (le français garde le singulier pour 0 et 1, l'anglais seulement pour 1).
    static func count(_ n: Int, _ frOne: String, _ frMany: String, _ enOne: String, _ enMany: String) -> String {
        "\(n) " + (Tr.english ? (n == 1 ? enOne : enMany) : (n > 1 ? frMany : frOne))
    }

    /// Horloge « 1:02:03 » / « 12:34 ».
    static func clock(_ seconds: Double) -> String {
        let s = Int(max(0, seconds.isFinite ? seconds : 0))
        let h = s / 3600, m = (s % 3600) / 60, sec = s % 60
        return h > 0 ? String(format: "%d:%02d:%02d", h, m, sec) : String(format: "%d:%02d", m, sec)
    }

    /// « 20:05 » ; en anglais selon l'horloge de l'appareil : « 8:05 PM » ou « 20:05 ».
    static var hour: DateFormatter {
        !Tr.english ? hourFr : (Tr.clock24 ? hourEn24 : hourEn12)
    }

    /// « jeudi 2 octobre » ; en anglais « Thursday, October 2 ».
    static var day: DateFormatter { Tr.english ? dayEn : dayFr }

    private static let hourFr = formatter("fr_FR", "HH:mm")
    private static let hourEn24 = formatter("en_US", "HH:mm")
    private static let hourEn12 = formatter("en_US", "h:mm a")
    private static let dayFr = formatter("fr_FR", "EEEE d MMMM")
    private static let dayEn = formatter("en_US", "EEEE, MMMM d")

    private static func formatter(_ locale: String, _ format: String) -> DateFormatter {
        let f = DateFormatter()
        f.locale = Locale(identifier: locale)
        f.dateFormat = format
        return f
    }
}
