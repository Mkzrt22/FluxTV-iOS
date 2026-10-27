import SwiftUI

/// Premier lancement : choix de la langue (français ou anglais), celle de l'appareil proposée
/// d'office. Textes dans les deux langues, quelle que soit celle de l'appareil.
struct LanguageView: View {
    let onChoose: (String) -> Void

    @FocusState private var focused: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Theme.s(18)) {
                BrandLogo(size: 44)
                    .padding(.bottom, Theme.s(4))
                VStack(alignment: .leading, spacing: Theme.s(6)) {
                    Text(verbatim: "Choisis ta langue").font(Theme.display(30)).foregroundStyle(Theme.text)
                    Text(verbatim: "Choose your language").font(Theme.title(19)).foregroundStyle(Theme.textDim)
                }
                VStack(spacing: Theme.s(12)) {
                    option(Lang.fr, "Français", "Interface en français")
                    option(Lang.en, "English", "English interface")
                }
                Text(verbatim: "Modifiable à tout moment dans les réglages · You can change it anytime in Settings")
                    .font(Theme.bodyRegular(12))
                    .foregroundStyle(Theme.textFaint)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, Theme.s(4))
            }
            .padding(Theme.s(28))
            .frame(maxWidth: Theme.s(560))
            .background(RoundedRectangle(cornerRadius: Theme.s(28), style: .continuous).fill(Theme.surface.opacity(0.82)))
            .overlay(RoundedRectangle(cornerRadius: Theme.s(28), style: .continuous).strokeBorder(Theme.stroke, lineWidth: 1))
            .padding(.horizontal, Theme.pagePadding)
            .padding(.vertical, Theme.s(40))
            .frame(maxWidth: .infinity)
        }
        .defaultFocus($focused, Lang.device)
        #if os(tvOS)
        // Menu : la langue de l'appareil est gardée
        .onExitCommand { onChoose(Lang.device) }
        #endif
    }

    /// Langue proposée d'office : liseré de couleur.
    private func option(_ code: String, _ title: String, _ detail: String) -> some View {
        let shape = RoundedRectangle(cornerRadius: Theme.s(16), style: .continuous)
        return Button { onChoose(code) } label: {
            HStack(spacing: Theme.s(14)) {
                VStack(alignment: .leading, spacing: Theme.s(3)) {
                    Text(verbatim: title).font(Theme.title(18)).foregroundStyle(Theme.text)
                    Text(verbatim: detail).font(Theme.bodyRegular(13)).foregroundStyle(Theme.textDim)
                }
                Spacer(minLength: 0)
                Image(systemName: "chevron.right")
                    .font(.system(size: Theme.s(14), weight: .bold))
                    .foregroundStyle(Theme.textFaint)
            }
            .padding(.horizontal, Theme.s(20))
            .padding(.vertical, Theme.s(16))
            .background(shape.fill(Theme.glassStrong))
            .overlay(shape.strokeBorder(code == Lang.device ? Theme.accentBright.opacity(0.6) : Theme.stroke, lineWidth: 1))
            .contentShape(shape)
        }
        .buttonStyle(FluxButtonStyle(focusScale: 1.04, cornerRadius: 16))
        .focused($focused, equals: code)
    }
}
