import SwiftUI
import WebKit

enum Shell {
    /// 앱이 여는 화면. nginx → AI Office(8000) 의 /AI/desk-app/ (2026-10-01)
    static let startURL = URL(string: "https://woopang.com/hyodingsseu/")!

    /// 앱 안에서 보여 줄 주소 — 그 밖(문서 서명 링크·기사·전화번호)은 사파리 등 바깥 앱으로
    static func isOwn(_ url: URL) -> Bool {
        if url.scheme == "about" { return true }
        guard url.scheme == "https", let host = url.host?.lowercased(),
              host == "woopang.com" || host == "www.woopang.com" else { return false }
        return url.path.hasPrefix("/hyodingsseu") || url.path.hasPrefix("/AI/desk-app")
    }
}

/// 웹 화면을 꽉 채워 보여 주는 WKWebView. 폰 기능은 `DeskNative`, 그 밖의 결정(바깥 링크·마이크·오프라인)은 여기서.
struct WebShell: UIViewRepresentable {
    let start: URL

    func makeCoordinator() -> Coordinator { Coordinator(start: start) }

    func makeUIView(context: Context) -> WKWebView {
        let cfg = WKWebViewConfiguration()
        cfg.allowsInlineMediaPlayback = true
        cfg.mediaTypesRequiringUserActionForPlayback = []          // PC 목소리를 누르지 않아도 바로 재생
        cfg.websiteDataStore = .default()                           // 연결 토큰(localStorage)이 앱을 껐다 켜도 남게
        let ucc = cfg.userContentController
        ucc.addUserScript(WKUserScript(source: ShellPages.bridgeJS, injectionTime: .atDocumentStart, forMainFrameOnly: true))
        ucc.addScriptMessageHandler(context.coordinator.native, contentWorld: .page, name: "deskNative")

        let wv = WKWebView(frame: .zero, configuration: cfg)
        wv.navigationDelegate = context.coordinator
        wv.uiDelegate = context.coordinator
        wv.allowsBackForwardNavigationGestures = false
        wv.scrollView.contentInsetAdjustmentBehavior = .never       // 비켜 앉기는 화면(CSS)이 한다
        wv.isOpaque = false
        wv.backgroundColor = .clear
        if #available(iOS 16.4, *) { wv.isInspectable = true }      // Mac 사파리 › 개발 메뉴로 들여다보기 (개인용)
        context.coordinator.attach(wv)
        wv.load(URLRequest(url: start))
        return wv
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}

    @MainActor
    final class Coordinator: NSObject, WKNavigationDelegate, WKUIDelegate {
        let start: URL
        let native = DeskNative()
        private weak var webView: WKWebView?

        init(start: URL) {
            self.start = start
            super.init()
            NotificationCenter.default.addObserver(self, selector: #selector(becameActive),
                                                   name: UIApplication.didBecomeActiveNotification, object: nil)
        }

        func attach(_ wv: WKWebView) {
            webView = wv
            native.webView = wv
        }

        /// 앱으로 돌아오면 아이콘의 알림 숫자를 지우고, 안내 화면(오프라인)이 떠 있으면 다시 불러온다
        @objc private func becameActive() {
            Push.shared.clearBadge()
            guard let wv = webView else { return }
            if wv.url == nil || wv.url?.scheme == "about" { wv.load(URLRequest(url: start)) }
        }

        // 앱 밖 주소는 바깥 앱(사파리·전화 등)으로
        func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction) async -> WKNavigationActionPolicy {
            guard let url = navigationAction.request.url else { return .cancel }
            if Shell.isOwn(url) || navigationAction.targetFrame?.isMainFrame == false { return .allow }
            _ = await UIApplication.shared.open(url)
            return .cancel
        }

        // target=_blank · window.open → 바깥 앱으로
        func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                     for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
            if let url = navigationAction.request.url { UIApplication.shared.open(url) }
            return nil
        }

        // 마이크 — 앱이 이미 허락받았으니(Info.plist) 웹이 한 번 더 묻지 않게
        // async 판의 Swift 이름은 decideMediaCapturePermissionsFor:initiatedBy: 다(SDK 의 WK_SWIFT_ASYNC_NAME).
        // 콜백 판 이름표(requestMediaCapturePermissionFor:initiatedByFrame:)를 쓰면 WebKit 이 이 함수를 부르지 않아
        // 매번 웹이 마이크를 다시 묻는다(구현 안 한 것과 같음).
        func webView(_ webView: WKWebView, decideMediaCapturePermissionsFor origin: WKSecurityOrigin,
                     initiatedBy frame: WKFrameInfo, type: WKMediaCaptureType) async -> WKPermissionDecision {
            origin.host == "woopang.com" || origin.host == "www.woopang.com" ? .grant : .deny
        }

        // 인터넷이 안 되면 — 안내 화면 + 다시 시도
        func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
            showOffline(webView, error)
        }

        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            showOffline(webView, error)
        }

        private func showOffline(_ webView: WKWebView, _ error: Error) {
            let e = error as NSError
            if e.code == NSURLErrorCancelled || e.domain == "WebKitErrorDomain" && e.code == 102 { return }   // 취소·바깥으로 넘긴 것
            webView.loadHTMLString(ShellPages.offlineHTML(retry: start.absoluteString, reason: e.localizedDescription), baseURL: nil)
        }

        // 웹 화면 프로세스가 죽으면(메모리 부족) 다시 띄운다
        func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
            webView.load(URLRequest(url: start))
        }
    }
}
