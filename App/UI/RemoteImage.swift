import SwiftUI
import ImageIO
import UIKit

/**
 Chargement des affiches et logos : cache mémoire + cache disque (URLCache), et images
 réduites à la taille d'affichage (une affiche de 2 000 px n'occupe pas 16 Mo de mémoire
 pour une vignette de 150 px). « asset:Nom » désigne une image fournie avec l'app.
 */
final class ImageLoader: @unchecked Sendable {
    static let shared = ImageLoader()

    private let memory = NSCache<NSString, UIImage>()
    private let session: URLSession
    private var inFlight: [String: Task<UIImage?, Never>] = [:]
    private let lock = NSLock()

    init() {
        memory.totalCostLimit = 120 * 1024 * 1024
        let cfg = URLSessionConfiguration.default
        cfg.urlCache = URLCache(memoryCapacity: 16 * 1024 * 1024, diskCapacity: 400 * 1024 * 1024)
        cfg.requestCachePolicy = .returnCacheDataElseLoad
        cfg.timeoutIntervalForRequest = 20
        cfg.httpMaximumConnectionsPerHost = 6
        cfg.httpAdditionalHeaders = ["User-Agent": HTTPClient.userAgent]
        session = URLSession(configuration: cfg)
    }

    func cached(_ key: String, maxPixel: CGFloat) -> UIImage? {
        memory.object(forKey: "\(key)#\(Int(maxPixel))" as NSString)
    }

    func image(_ source: String, maxPixel: CGFloat) async -> UIImage? {
        if source.hasPrefix("asset:") { return UIImage(named: String(source.dropFirst(6))) }
        let key = "\(source)#\(Int(maxPixel))"
        if let img = memory.object(forKey: key as NSString) { return img }

        let task: Task<UIImage?, Never> = lock.withLock {
            if let running = inFlight[key] { return running }
            let t = Task.detached(priority: .utility) { [session] () -> UIImage? in
                guard let url = URL(string: source) else { return nil }
                var req = URLRequest(url: url)
                req.cachePolicy = .returnCacheDataElseLoad
                guard let result = try? await session.data(for: req) else { return nil }
                if let http = result.1 as? HTTPURLResponse, !(200...299).contains(http.statusCode) { return nil }
                return ImageLoader.downsample(result.0, maxPixel: maxPixel)
            }
            inFlight[key] = t
            return t
        }

        let img = await task.value
        lock.withLock { inFlight[key] = nil }
        if let img {
            let cost = Int(img.size.width * img.size.height * img.scale * img.scale * 4)
            memory.setObject(img, forKey: key as NSString, cost: cost)
        }
        return img
    }

    static func downsample(_ data: Data, maxPixel: CGFloat) -> UIImage? {
        let opts = [kCGImageSourceShouldCache: false] as CFDictionary
        guard let src = CGImageSourceCreateWithData(data as CFData, opts) else { return nil }
        let thumbOpts = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceShouldCacheImmediately: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceThumbnailMaxPixelSize: max(64, maxPixel),
        ] as CFDictionary
        guard let cg = CGImageSourceCreateThumbnailAtIndex(src, 0, thumbOpts) else { return UIImage(data: data) }
        return UIImage(cgImage: cg)
    }
}

/// Image distante avec repli (dégradé + initiales, ou icône).
struct RemoteImage: View {
    let url: String?
    var contentMode: ContentMode = .fill
    /// Taille d'affichage la plus grande, en points (sert à réduire l'image).
    var maxSize: CGFloat = 300
    var placeholderTitle: String?
    var placeholderIcon = "film"

    @State private var image: UIImage?
    @State private var failed = false

    private var maxPixel: CGFloat { maxSize * Theme.scale * 2 }

    var body: some View {
        ZStack {
            if let image {
                Image(uiImage: image)
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
                    .transition(.opacity)
            } else {
                Placeholder(title: placeholderTitle, icon: placeholderIcon, loading: url != nil && !failed)
            }
        }
        .task(id: url) {
            guard let url, !url.isEmpty else { image = nil; return }
            if let hit = ImageLoader.shared.cached(url, maxPixel: maxPixel) { image = hit; return }
            image = nil
            failed = false
            let img = await ImageLoader.shared.image(url, maxPixel: maxPixel)
            if Task.isCancelled { return }
            withAnimation(.easeOut(duration: 0.25)) { image = img }
            failed = img == nil
        }
    }
}

extension RemoteImage {
    /**
     Remplit une bande de hauteur fixe sur toute la largeur, sans élargir la mise en page : en
     mode « remplir », une image panoramique est plus large que l'écran et pousserait tout le
     contenu hors des bords (fiche coupée à gauche et à droite).
     */
    func filling(height: CGFloat) -> some View {
        Color.clear
            .frame(maxWidth: .infinity)
            .frame(height: height)
            .overlay { self }
            .clipped()
    }
}

private struct Placeholder: View {
    let title: String?
    let icon: String
    let loading: Bool

    var body: some View {
        ZStack {
            LinearGradient(colors: [Theme.surface3, Theme.surface], startPoint: .topLeading, endPoint: .bottomTrailing)
            if let title, !title.isEmpty {
                Text(initials(title))
                    .font(Theme.display(22))
                    .foregroundStyle(Theme.text.opacity(0.85))
                    .minimumScaleFactor(0.4)
                    .lineLimit(1)
                    .padding(Theme.s(6))
            } else {
                Image(systemName: icon)
                    .font(.system(size: Theme.s(22), weight: .semibold))
                    .foregroundStyle(Theme.textFaint)
            }
        }
        .opacity(loading ? 0.85 : 1)
    }

    private func initials(_ s: String) -> String {
        let words = s.split(separator: " ").filter { $0.first?.isLetter == true || $0.first?.isNumber == true }
        let letters = words.prefix(2).compactMap { $0.first }.map { String($0).uppercased() }
        return letters.isEmpty ? String(s.prefix(2)).uppercased() : letters.joined()
    }
}
