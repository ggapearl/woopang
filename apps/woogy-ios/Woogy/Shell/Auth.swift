import AuthenticationServices
import UIKit

/// 간편로그인(카카오 · 구글 · 네이버 · 애플)을 앱 안 웹 화면이 아니라 **시스템 브라우저 창**(ASWebAuthenticationSession)으로 연다.
/// 구글은 앱 안 웹 화면(WKWebView)의 로그인을 막는다(disallowed_useragent). 그래서 농민닷컴 앱과 같은 길로 간다:
///   시스템 브라우저 창 → 우기 서버 → (1회용 토큰, 5분 · 한 번만) → com.que.woogy://auth?t=… → 창이 닫히고
///   → 앱이 웹 화면에서 https://woopang.com/woogy/auth/app?t=… 를 연다 → 서버가 토큰을 로그인으로 바꾼다
/// 사파리와 쿠키를 같이 써서(ephemeral 끔) 사파리에 이미 로그인해 둔 카카오 · 구글 계정은 누르기만 하면 된다.
@MainActor
final class Auth {
    static let shared = Auth()

    enum Outcome: Sendable {
        case done(URL)
        case canceled
        case failed(String)
    }

    private var session: ASWebAuthenticationSession?
    private var anchor: Anchor?
    private var waiting: CheckedContinuation<Outcome, Never>?
    /// 지금 열린 창의 번호 — 닫고 새로 연 뒤에 옛 창의 늦은 소식이 새 창 결과로 섞이지 않게
    private var serial = 0

    func start(_ url: URL, window: UIWindow?) async -> Outcome {
        end(.canceled)                                   // 창은 하나만 — 이미 열려 있으면 닫고 새로 연다
        guard let window = window ?? Self.keyWindow() else { return .failed("화면을 찾지 못했어요") }
        serial += 1
        let id = serial
        return await withCheckedContinuation { (k: CheckedContinuation<Outcome, Never>) in
            waiting = k
            let s = ASWebAuthenticationSession(url: url, callbackURLScheme: Shell.scheme) { back, error in
                let outcome: Outcome
                if let back {
                    outcome = .done(back)
                } else if let e = error as? ASWebAuthenticationSessionError, e.code == .canceledLogin {
                    outcome = .canceled
                } else {
                    outcome = .failed(error?.localizedDescription ?? "로그인을 마치지 못했어요")
                }
                Task { @MainActor in Auth.shared.end(outcome, id: id) }
            }
            let a = Anchor(window: window)
            s.presentationContextProvider = a
            s.prefersEphemeralWebBrowserSession = false
            anchor = a
            session = s
            if !s.start() { end(.failed("로그인 창을 열지 못했어요"), id: id) }
        }
    }

    /// 사파리 쪽에서 로그인을 마치고 com.que.woogy://auth 로 앱이 열렸을 때(WoogyApp.onOpenURL).
    /// 기다리던 창이 있으면 그 결과로 끝내고 true — 화면을 여는 건 창을 연 쪽(WoogyNative)이 한다.
    func finish(with url: URL) -> Bool {
        guard waiting != nil else { return false }
        end(.done(url))
        return true
    }

    private func end(_ outcome: Outcome, id: Int? = nil) {
        if let id, id != serial { return }
        guard let k = waiting else { return }
        waiting = nil
        let s = session
        session = nil
        anchor = nil
        s?.cancel()
        k.resume(returning: outcome)
    }

    private static func keyWindow() -> UIWindow? {
        UIApplication.shared.connectedScenes
            .compactMap { $0 as? UIWindowScene }
            .flatMap(\.windows)
            .first(where: \.isKeyWindow)
    }
}

/// 로그인 창을 띄울 화면(창)
private final class Anchor: NSObject, ASWebAuthenticationPresentationContextProviding {
    let window: UIWindow

    init(window: UIWindow) {
        self.window = window
    }

    func presentationAnchor(for session: ASWebAuthenticationSession) -> ASPresentationAnchor {
        window
    }
}
