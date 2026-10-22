import Foundation
#if canImport(FoundationNetworking)
import FoundationNetworking
#endif

/// Erreurs affichées à l'utilisateur, en français.
enum FluxError: LocalizedError, Equatable {
    case badAddress
    case http(Int)
    case offline
    case timeout
    case network(String)
    case authRefused
    case accountInactive(String)
    case unexpectedResponse
    case emptyPlaylist

    var errorDescription: String? {
        switch self {
        case .badAddress: return "Adresse invalide. Vérifie le serveur (exemple : http://mon-serveur.com:8080)."
        case .http(let code):
            switch code {
            case 401, 403: return "Accès refusé par le serveur (code \(code)). Vérifie tes identifiants."
            case 404: return "Adresse introuvable sur le serveur (code 404)."
            case 500...599: return "Le serveur du fournisseur ne répond pas correctement (code \(code)). Réessaie plus tard."
            default: return "Le serveur a répondu avec le code \(code)."
            }
        case .offline: return "Pas de connexion internet."
        case .timeout: return "Le serveur met trop de temps à répondre."
        case .network(let m): return "Connexion impossible : \(m)"
        case .authRefused: return "Identifiants refusés par le serveur."
        case .accountInactive(let s): return "Compte \(s) chez le fournisseur."
        case .unexpectedResponse: return "Réponse inattendue du serveur. Vérifie l'adresse du serveur."
        case .emptyPlaylist: return "Cette playlist ne contient aucune chaîne ni aucun film."
        }
    }
}

/// Requêtes HTTP simples (API du fournisseur, playlists, guide TV, images).
final class HTTPClient: @unchecked Sendable {
    static let shared = HTTPClient()

    /// Navigateur ordinaire : certains serveurs refusent les clients inconnus.
    static let userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1"

    let session: URLSession

    init(configuration: URLSessionConfiguration? = nil) {
        let cfg = configuration ?? {
            let c = URLSessionConfiguration.default
            c.timeoutIntervalForRequest = 30
            c.timeoutIntervalForResource = 300
            c.httpMaximumConnectionsPerHost = 6
            c.requestCachePolicy = .reloadIgnoringLocalCacheData
            c.httpAdditionalHeaders = ["User-Agent": HTTPClient.userAgent]
            return c
        }()
        session = URLSession(configuration: cfg)
    }

    func data(_ url: URL, timeout: TimeInterval = 30) async throws -> Data {
        var req = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData, timeoutInterval: timeout)
        req.setValue(HTTPClient.userAgent, forHTTPHeaderField: "User-Agent")
        return try await withCheckedThrowingContinuation { cont in
            let task = session.dataTask(with: req) { data, response, error in
                if let error {
                    cont.resume(throwing: HTTPClient.map(error))
                    return
                }
                if let http = response as? HTTPURLResponse, !(200...299).contains(http.statusCode) {
                    cont.resume(throwing: FluxError.http(http.statusCode))
                    return
                }
                cont.resume(returning: data ?? Data())
            }
            task.resume()
        }
    }

    static func map(_ error: Error) -> FluxError {
        let ns = error as NSError
        guard ns.domain == NSURLErrorDomain else { return .network(error.localizedDescription) }
        switch ns.code {
        case NSURLErrorNotConnectedToInternet, NSURLErrorNetworkConnectionLost, NSURLErrorDataNotAllowed: return .offline
        case NSURLErrorTimedOut: return .timeout
        case NSURLErrorBadURL, NSURLErrorUnsupportedURL: return .badAddress
        case NSURLErrorCannotFindHost, NSURLErrorDNSLookupFailed: return .network("serveur introuvable")
        case NSURLErrorCannotConnectToHost: return .network("le serveur refuse la connexion")
        case NSURLErrorSecureConnectionFailed, NSURLErrorServerCertificateUntrusted, NSURLErrorServerCertificateHasBadDate:
            return .network("connexion sécurisée impossible (essaie avec http://)")
        case -1022: // NSURLErrorAppTransportSecurityRequiresSecureConnection
            return .network("le système refuse les connexions http de l'app")
        default: return .network(ns.localizedDescription)
        }
    }
}

/// Décompression gzip (guides TV « .xml.gz »).
enum Gzip {
    static func isGzip(_ data: Data) -> Bool { data.count > 2 && data[data.startIndex] == 0x1f && data[data.startIndex + 1] == 0x8b }

    /// Données décompressées, ou nil si le format n'est pas pris en charge sur cette plateforme.
    static func decompress(_ data: Data) -> Data? {
        guard isGzip(data), data.count > 18 else { return nil }
        let bytes = [UInt8](data)
        let flags = bytes[3]
        var i = 10
        if flags & 0x04 != 0, i + 2 <= bytes.count { i += 2 + (Int(bytes[i]) | (Int(bytes[i + 1]) << 8)) } // FEXTRA
        if flags & 0x08 != 0 { while i < bytes.count, bytes[i] != 0 { i += 1 }; i += 1 }                    // FNAME
        if flags & 0x10 != 0 { while i < bytes.count, bytes[i] != 0 { i += 1 }; i += 1 }                    // FCOMMENT
        if flags & 0x02 != 0 { i += 2 }                                                                      // FHCRC
        guard i < bytes.count - 8 else { return nil }
        let deflate = Data(bytes[i..<(bytes.count - 8)])
        #if canImport(Darwin)
        return try? (deflate as NSData).decompressed(using: .zlib) as Data
        #else
        return nil
        #endif
    }
}
