// swift-tools-version:5.9
// Cœur de FluxTV (sans interface) : compilé dans l'app iOS / tvOS, et testé seul sur
// n'importe quelle machine avec `swift test` (y compris Linux).
import PackageDescription

let package = Package(
    name: "FluxCore",
    platforms: [.iOS(.v17), .tvOS(.v17), .macOS(.v14)],
    products: [.library(name: "FluxCore", targets: ["FluxCore"])],
    targets: [
        .target(name: "FluxCore", path: "Sources/FluxCore"),
        .testTarget(name: "FluxCoreTests", dependencies: ["FluxCore"], path: "Tests/FluxCoreTests",
                    resources: [.copy("Fixtures")])
    ]
)
