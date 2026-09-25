import Foundation
import Security

struct DeskError: LocalizedError {
    let status: Int
    let message: String
    var errorDescription: String? { message }
}

/// PC 효딩쓰 창구 — https://woopang.com/AI/api/desk/<주소>
/// 인증은 짝을 지을 때 받은 기기 토큰 하나(Keychain 에만 둔다).
@MainActor
final class DeskAPI {
    static let defaultBase = "https://woopang.com/AI/api/desk"

    var base: String {
        get { UserDefaults.standard.string(forKey: "base") ?? Self.defaultBase }
        set { UserDefaults.standard.set(newValue.trimmingCharacters(in: .whitespacesAndNewlines), forKey: "base") }
    }

    private var tokenLoaded = false
    private var tokenValue: String?

    var token: String? {
        get {
            if !tokenLoaded {
                tokenValue = Keychain.read()
                tokenLoaded = true
            }
            return tokenValue
        }
        set {
            if let v = newValue { Keychain.save(v) } else { Keychain.delete() }
            tokenValue = newValue
            tokenLoaded = true
        }
    }

    private let session: URLSession = {
        let c = URLSessionConfiguration.default
        c.timeoutIntervalForRequest = 45
        c.requestCachePolicy = .reloadIgnoringLocalCacheData
        c.urlCache = nil
        return URLSession(configuration: c)
    }()

    func raw(_ path: String, query: [String: String] = [:], method: String = "GET", body: Data? = nil,
             contentType: String = "application/json", timeout: TimeInterval = 30, auth: Bool = true) async throws -> Data {
        var root = base
        while root.hasSuffix("/") { root.removeLast() }
        guard var comps = URLComponents(string: root + "/" + path) else {
            throw DeskError(status: 0, message: "서버 주소가 올바르지 않아요.")
        }
        if !query.isEmpty {
            comps.queryItems = query.map { URLQueryItem(name: $0.key, value: $0.value) }
        }
        guard let url = comps.url else {
            throw DeskError(status: 0, message: "서버 주소가 올바르지 않아요.")
        }
        var req = URLRequest(url: url, timeoutInterval: timeout)
        req.httpMethod = method
        if let body {
            req.httpBody = body
            req.setValue(contentType, forHTTPHeaderField: "Content-Type")
        }
        if auth, let t = token {
            req.setValue("Bearer " + t, forHTTPHeaderField: "Authorization")
        }

        let result: (Data, URLResponse)
        do {
            result = try await session.data(for: req)
        } catch let e as URLError where e.code != .cancelled {
            throw DeskError(status: 0, message: Self.describe(e))
        }
        let (data, resp) = result
        let code = (resp as? HTTPURLResponse)?.statusCode ?? 0
        guard (200..<300).contains(code) else {
            let msg = (try? JSON.parse(data))?["error"]?.string ?? "서버 오류 (\(code))"
            throw DeskError(status: code, message: msg)
        }
        return data
    }

    func get(_ path: String, query: [String: String] = [:], timeout: TimeInterval = 30) async throws -> JSON {
        try JSON.parse(try await raw(path, query: query, timeout: timeout))
    }

    @discardableResult
    func post(_ path: String, _ obj: [String: Any] = [:], timeout: TimeInterval = 30, auth: Bool = true) async throws -> JSON {
        let body = try JSONSerialization.data(withJSONObject: obj)
        return try JSON.parse(try await raw(path, method: "POST", body: body, timeout: timeout, auth: auth))
    }

    private static func describe(_ e: URLError) -> String {
        switch e.code {
        case .notConnectedToInternet, .networkConnectionLost:
            return "인터넷에 연결돼 있지 않아요."
        case .timedOut:
            return "PC 효딩쓰가 답이 늦어요."
        case .cannotFindHost, .cannotConnectToHost, .dnsLookupFailed:
            return "서버에 닿지 않아요."
        default:
            return "연결 문제: \(e.localizedDescription)"
        }
    }
}

/// 기기 토큰 — 이 아이폰에만, 잠금 해제된 뒤에만 읽힌다.
enum Keychain {
    private static let service = "com.que.hyodingsseu"
    private static let account = "desk-token"

    private static var baseQuery: [String: Any] {
        [kSecClass as String: kSecClassGenericPassword,
         kSecAttrService as String: service,
         kSecAttrAccount as String: account]
    }

    static func save(_ value: String) {
        delete()
        var q = baseQuery
        q[kSecValueData as String] = Data(value.utf8)
        q[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        SecItemAdd(q as CFDictionary, nil)
    }

    static func read() -> String? {
        var q = baseQuery
        q[kSecReturnData as String] = true
        q[kSecMatchLimit as String] = kSecMatchLimitOne
        var out: AnyObject?
        guard SecItemCopyMatching(q as CFDictionary, &out) == errSecSuccess, let data = out as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }

    static func delete() {
        SecItemDelete(baseQuery as CFDictionary)
    }
}
