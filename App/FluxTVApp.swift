import SwiftUI
import UIKit

@main
struct FluxTVApp: App {
    @StateObject private var app = AppModel()
    @Environment(\.scenePhase) private var scenePhase

    init() {
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
                .onAppear { app.start() }
        }
        .onChange(of: scenePhase) { _, phase in
            if phase != .active { app.library.flush() }
        }
    }
}

/// Aiguillage : accueil de bienvenue, configuration ou app.
struct RootView: View {
    @EnvironmentObject private var app: AppModel

    var body: some View {
        ZStack {
            AppBackground()
            switch app.phase {
            case .launching:
                BrandLogo(size: 64)
            case .needsSetup:
                SetupView()
                    .transition(.opacity)
            case .ready:
                MainTabs()
                    .transition(.opacity)
            }
        }
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

/// Onglets de l'app.
struct MainTabs: View {
    @EnvironmentObject private var app: AppModel
    @State private var tab = "home"

    var body: some View {
        TabView(selection: $tab) {
            NavigationStack { HomeView() }
                .tabItem { Label("Accueil", systemImage: "house.fill") }
                .tag("home")
            NavigationStack { LiveView() }
                .tabItem { Label("TV", systemImage: "tv") }
                .tag("live")
            NavigationStack { MoviesView() }
                .tabItem { Label("Films", systemImage: "film") }
                .tag("movies")
            NavigationStack { SeriesListView() }
                .tabItem { Label("Séries", systemImage: "rectangle.stack.fill") }
                .tag("series")
            NavigationStack { SearchView() }
                .tabItem { Label("Recherche", systemImage: "magnifyingglass") }
                .tag("search")
            #if os(tvOS)
            NavigationStack { SettingsView() }
                .tabItem { Label("Réglages", systemImage: "gearshape.fill") }
                .tag("settings")
            #endif
        }
        .onAppear { tab = app.settings.lastTab }
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
