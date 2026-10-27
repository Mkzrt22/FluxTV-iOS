import SwiftUI
import UIKit

@main
struct FluxTVApp: App {
    @StateObject private var app = AppModel()
    @Environment(\.scenePhase) private var scenePhase

    init() {
        // Langue choisie au premier lancement (ou celle de l'appareil) : textes et dates
        Lang.apply()
        #if os(iOS)
        // Barres de navigation et d'onglets sombres, aux couleurs de l'app
        let tab = UITabBarAppearance()
        tab.configureWithOpaqueBackground()
        tab.backgroundColor = UIColor(red: 0.016, green: 0.016, blue: 0.05, alpha: 0.96)
        tab.shadowColor = UIColor.white.withAlphaComponent(0.08)
        UITabBar.appearance().standardAppearance = tab
        UITabBar.appearance().scrollEdgeAppearance = tab
        UITabBar.appearance().tintColor = UIColor(red: 0.557, green: 0.592, blue: 1, alpha: 1)

        let nav = UINavigationBarAppearance()
        nav.configureWithTransparentBackground()
        nav.titleTextAttributes = [.foregroundColor: UIColor.white, .font: UIFont(name: "Sora-SemiBold", size: 17) ?? .systemFont(ofSize: 17, weight: .semibold)]
        nav.largeTitleTextAttributes = [.foregroundColor: UIColor.white, .font: UIFont(name: "Sora-Bold", size: 32) ?? .systemFont(ofSize: 32, weight: .bold)]
        UINavigationBar.appearance().standardAppearance = nav
        UINavigationBar.appearance().scrollEdgeAppearance = nav
        UINavigationBar.appearance().compactAppearance = nav
        UINavigationBar.appearance().tintColor = UIColor(red: 0.557, green: 0.592, blue: 1, alpha: 1)
        #endif
    }

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(app)
                .preferredColorScheme(.dark)
                .tint(Theme.accentBright)
        }
        .onChange(of: scenePhase) { _, phase in
            if phase != .active { app.library.flush() }
        }
    }
}

/// Aiguillage : choix de la langue (premier lancement), configuration ou app.
struct RootView: View {
    @EnvironmentObject private var app: AppModel
    @State private var askLanguage = Lang.chosen == nil

    var body: some View {
        ZStack {
            AppBackground()
            if askLanguage {
                LanguageView { code in
                    askLanguage = false
                    app.chooseLanguage(code)
                }
                .transition(.opacity)
            } else {
                switch app.phase {
                case .launching:
                    BrandLogo(size: 64)
                case .needsSetup:
                    SetupView()
                        .transition(.opacity)
                case .ready:
                    // Langue changée dans les réglages : toute l'interface est refaite
                    MainTabs()
                        .id(app.language)
                        .transition(.opacity)
                }
            }
        }
        .onAppear { if !askLanguage { app.start() } }
        .animation(.easeInOut(duration: 0.35), value: askLanguage)
        .animation(.easeInOut(duration: 0.35), value: app.phase)
        .overlay(alignment: .top) {
            if let text = app.toast {
                ToastView(text: text)
                    .padding(.top, Theme.s(12))
                    .transition(.move(edge: .top).combined(with: .opacity))
            }
        }
        .fullScreenCover(item: $app.player) { request in
            PlayerView(request: request)
                .environmentObject(app)
        }
    }
}

/// Écrans ouverts depuis l'accueil (en plus des fiches).
enum AppRoute: Hashable { case settings }

/// Onglets de l'app.
struct MainTabs: View {
    @EnvironmentObject private var app: AppModel
    @State private var tab = "home"
    @State private var homePath = NavigationPath()

    var body: some View {
        TabView(selection: $tab) {
            NavigationStack(path: $homePath) { HomeView() }
                .tabItem { Label(T("Accueil", "Home"), systemImage: "house.fill") }
                .tag("home")
            NavigationStack { LiveView() }
                .tabItem { Label(T("TV", "Live TV"), systemImage: "tv") }
                .tag("live")
            NavigationStack { MoviesView() }
                .tabItem { Label(T("Films", "Movies"), systemImage: "film") }
                .tag("movies")
            NavigationStack { SeriesListView() }
                .tabItem { Label(T("Séries", "Series"), systemImage: "rectangle.stack.fill") }
                .tag("series")
            NavigationStack { SearchView() }
                .tabItem { Label(T("Recherche", "Search"), systemImage: "magnifyingglass") }
                .tag("search")
            #if os(tvOS)
            NavigationStack { SettingsView() }
                .tabItem { Label(T("Réglages", "Settings"), systemImage: "gearshape.fill") }
                .tag("settings")
            #endif
        }
        .onAppear {
            tab = app.settings.lastTab
            // Langue changée dans les réglages : on y revient (Apple TV : c'est déjà l'onglet en cours)
            if app.takeReopenSettings() {
                #if os(iOS)
                tab = "home"
                homePath.append(AppRoute.settings)
                #endif
            }
            #if DEBUG && os(iOS)
            // Essais sur simulateur : « -FluxTVSettings YES » ouvre les réglages
            if UserDefaults.standard.bool(forKey: "FluxTVSettings") {
                tab = "home"
                homePath.append(AppRoute.settings)
            }
            #endif
        }
        .onChange(of: tab) { _, t in app.settings.lastTab = t }
    }
}

/// Destinations communes à tous les onglets (fiches film et série).
struct DetailDestinations: ViewModifier {
    func body(content: Content) -> some View {
        content
            .navigationDestination(for: Movie.self) { MovieDetailView(movie: $0) }
            .navigationDestination(for: Series.self) { SeriesDetailView(series: $0) }
    }
}

extension View {
    func detailDestinations() -> some View { modifier(DetailDestinations()) }

    /// Fond de l'app derrière un écran d'onglet, barre de navigation transparente.
    func fluxScreen() -> some View {
        self
            .background(AppBackground())
            #if os(iOS)
            .toolbarBackground(.hidden, for: .navigationBar)
            .toolbarColorScheme(.dark, for: .navigationBar)
            #endif
    }
}
