import SwiftUI
import UIKit
import WebKit

enum Shell {
    /// 앱이 여는 첫 화면 — 연결 화면이 기기 열쇠로 들어간 뒤 관리자 화면(/admin)으로 넘어간다 (서버 admin_m.py)
    static let startURL = URL(string: "https://woopang.com/admin/m/")!
    /// 관리자 화면 머리 막대 색 #14171A — 상태 표시줄 칸 · 불러오는 동안 바탕
    static let barUIColor = UIColor(red: 0x14 / 255.0, green: 0x17 / 255.0, blue: 0x1A / 255.0, alpha: 1)
    static let barColor = Color(uiColor: barUIColor)

    static func isOwn(_ url: URL) -> Bool {
        guard url.scheme == "https", let host = url.host?.lowercased() else { return false }
        return host == "woopang.com" || host == "www.woopang.com"
    }

    /// 연결 화면 주소. next = 들어간 뒤 갈 곳(/admin…) · out = 로그아웃한 뒤(저절로 다시 들어가지 않고 버튼을 보여 준다)
    static func connect(next: String? = nil, out: Bool = false) -> URL {
        var c = URLComponents(url: startURL, resolvingAgainstBaseURL: false)!
        var q: [URLQueryItem] = []
        if let next, next.hasPrefix("/admin") { q.append(URLQueryItem(name: "next", value: next)) }
        if out { q.append(URLQueryItem(name: "out", value: "1")) }
        c.queryItems = q.isEmpty ? nil : q
        return c.url ?? startURL
    }

    /// 앱이 대신 정하는 주소 (GET 만)
    ///  - 비밀번호 로그인 화면 → 연결 화면 (세션이 끝났거나 관리자 웹이 401 을 받았을 때). 「비밀번호로 들어가기」(pw=1)만 그대로
    ///  - 첫 화면(/) → 「로그아웃했어요」 (관리자 웹의 로그아웃은 / 로 보낸다)
    static func redirect(for url: URL) -> URL? {
        guard isOwn(url) else { return nil }
        let items = URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems ?? []
        if url.path == "/admin/login" {
            if items.contains(where: { $0.name == "pw" && $0.value == "1" }) { return nil }
            return connect(next: items.first(where: { $0.name == "next" })?.value)
        }
        if url.path.isEmpty || url.path == "/" { return connect(out: true) }
        return nil
    }
}

/// 관리자 웹을 꽉 채워 보여 주는 WKWebView. 폰 기능은 `WoopangMNative`, 그 밖의 결정(주소 · 새 창 · 알림창 · 오프라인)은 여기서.
struct WebShell: UIViewRepresentable {
    let start: URL

    func makeCoordinator() -> Coordinator { Coordinator(start: start) }

    func makeUIView(context: Context) -> WKWebView {
        let cfg = WKWebViewConfiguration()
        cfg.websiteDataStore = .default()                           // 관리자 세션 쿠키(30일)가 앱을 껐다 켜도 남게
        cfg.allowsInlineMediaPlayback = true
        cfg.userContentController.addScriptMessageHandler(context.coordinator.native, contentWorld: .page, name: "woopangM")

        let wv = WKWebView(frame: .zero, configuration: cfg)
        wv.navigationDelegate = context.coordinator
        wv.uiDelegate = context.coordinator
        wv.allowsBackForwardNavigationGestures = true               // 밀어서 뒤로
        wv.scrollView.contentInsetAdjustmentBehavior = .never       // 아래(홈 막대) 비켜 앉기는 화면(CSS)이 한다
        wv.isOpaque = false
        wv.backgroundColor = Shell.barUIColor
        wv.scrollView.backgroundColor = Shell.barUIColor
        if #available(iOS 16.4, *) { wv.isInspectable = true }      // Mac 사파리 › 개발 메뉴로 들여다보기 (대표님 전용 앱)
        context.coordinator.webView = wv
        wv.load(URLRequest(url: start))
        return wv
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}

    @MainActor
    final class Coordinator: NSObject, WKNavigationDelegate, WKUIDelegate {
        let start: URL
        let native = WoopangMNative()
        weak var webView: WKWebView?

        init(start: URL) {
            self.start = start
            super.init()
            NotificationCenter.default.addObserver(self, selector: #selector(becameActive),
                                                   name: UIApplication.didBecomeActiveNotification, object: nil)
        }

        /// 앱으로 돌아왔는데 안내 화면(인터넷 안 됨)이 떠 있으면 다시 불러온다. 관리자 화면은 돌아오면 스스로 새로 고친다(visibilitychange)
        @objc private func becameActive() {
            guard let wv = webView else { return }
            if wv.url == nil || wv.url?.scheme == "about" { wv.load(URLRequest(url: start)) }
        }

        // 우팡 주소는 앱 안에서(로그인·첫 화면은 연결 화면으로 바꿔서), 그 밖(카카오맵 등)은 바깥 앱으로
        func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction) async -> WKNavigationActionPolicy {
            guard let url = navigationAction.request.url else { return .cancel }
            if navigationAction.targetFrame?.isMainFrame == false { return .allow }      // 화면 안의 작은 틀
            if let s = url.scheme, ["about", "blob", "data"].contains(s) { return .allow }
            if Shell.isOwn(url) {
                if (navigationAction.request.httpMethod ?? "GET") == "GET", let to = Shell.redirect(for: url) {
                    DispatchQueue.main.async { webView.load(URLRequest(url: to)) }
                    return .cancel
                }
                return .allow
            }
            _ = await UIApplication.shared.open(url)
            return .cancel
        }

        // target=_blank (사진 크게 · 광고 관리 · DB 관리) — 우팡 주소는 같은 로그인으로 아래에서 올라오는 창에, 바깥은 바깥 앱으로
        func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                     for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
            guard let url = navigationAction.request.url else { return nil }
            guard Shell.isOwn(url), let top = topController() else {
                UIApplication.shared.open(url)
                return nil
            }
            let popup = WKWebView(frame: .zero, configuration: configuration)
            popup.navigationDelegate = self
            popup.uiDelegate = self
            popup.allowsBackForwardNavigationGestures = true
            popup.backgroundColor = Shell.barUIColor
            top.present(PopupController(web: popup), animated: true)
            return popup
        }

        func webViewDidClose(_ webView: WKWebView) {
            if webView !== self.webView { webView.window?.rootViewController?.presentedViewController?.dismiss(animated: true) }
        }

        // 관리자 웹의 alert · confirm · prompt — 받아 주지 않으면 WKWebView 는 아무것도 띄우지 않는다(confirm 은 늘 '아니오')
        func webView(_ webView: WKWebView, runJavaScriptAlertPanelWithMessage message: String,
                     initiatedByFrame frame: WKFrameInfo) async {
            await withCheckedContinuation { (k: CheckedContinuation<Void, Never>) in
                let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
                a.addAction(UIAlertAction(title: "확인", style: .default) { _ in k.resume() })
                if !present(a) { k.resume() }
            }
        }

        func webView(_ webView: WKWebView, runJavaScriptConfirmPanelWithMessage message: String,
                     initiatedByFrame frame: WKFrameInfo) async -> Bool {
            await withCheckedContinuation { (k: CheckedContinuation<Bool, Never>) in
                let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
                a.addAction(UIAlertAction(title: "취소", style: .cancel) { _ in k.resume(returning: false) })
                a.addAction(UIAlertAction(title: "확인", style: .default) { _ in k.resume(returning: true) })
                if !present(a) { k.resume(returning: false) }
            }
        }

        func webView(_ webView: WKWebView, runJavaScriptTextInputPanelWithPrompt prompt: String, defaultText: String?,
                     initiatedByFrame frame: WKFrameInfo) async -> String? {
            await withCheckedContinuation { (k: CheckedContinuation<String?, Never>) in
                let a = UIAlertController(title: nil, message: prompt, preferredStyle: .alert)
                a.addTextField { $0.text = defaultText }
                a.addAction(UIAlertAction(title: "취소", style: .cancel) { _ in k.resume(returning: nil) })
                a.addAction(UIAlertAction(title: "확인", style: .default) { [weak a] _ in
                    k.resume(returning: a?.textFields?.first?.text ?? "")
                })
                if !present(a) { k.resume(returning: nil) }
            }
        }

        private func topController() -> UIViewController? {
            var vc = webView?.window?.rootViewController
            while let p = vc?.presentedViewController { vc = p }
            return vc
        }

        private func present(_ vc: UIViewController) -> Bool {
            guard let top = topController() else { return false }
            top.present(vc, animated: true)
            return true
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
            if e.code == NSURLErrorCancelled || e.domain == "WebKitErrorDomain" && e.code == 102 { return }   // 앱이 바꾼 주소 · 바깥으로 넘긴 것
            let retry = webView === self.webView ? start : (webView.url ?? start)
            webView.loadHTMLString(ShellPages.offlineHTML(retry: retry.absoluteString, reason: e.localizedDescription), baseURL: nil)
        }

        // 웹 화면 프로세스가 죽으면(메모리 부족) 다시 띄운다
        func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
            if webView === self.webView { webView.load(URLRequest(url: start)) } else { webView.reload() }
        }
    }
}

/// 새 창(target=_blank)을 아래에서 올라오는 창으로 — 같은 로그인(쿠키)을 쓴다. 아래로 밀거나 「닫기」로 닫는다.
final class PopupController: UIViewController {
    private let web: WKWebView

    init(web: WKWebView) {
        self.web = web
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) { nil }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = Shell.barUIColor
        let close = UIButton(type: .system)
        close.setTitle("닫기", for: .normal)
        close.titleLabel?.font = .systemFont(ofSize: 17, weight: .semibold)
        close.tintColor = .white
        close.addAction(UIAction { [weak self] _ in self?.dismiss(animated: true) }, for: .touchUpInside)
        close.translatesAutoresizingMaskIntoConstraints = false
        web.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(web)
        view.addSubview(close)
        NSLayoutConstraint.activate([
            close.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor, constant: 6),
            close.trailingAnchor.constraint(equalTo: view.safeAreaLayoutGuide.trailingAnchor, constant: -16),
            close.heightAnchor.constraint(equalToConstant: 36),
            web.topAnchor.constraint(equalTo: close.bottomAnchor, constant: 6),
            web.leadingAnchor.constraint(equalTo: view.leadingAnchor),
            web.trailingAnchor.constraint(equalTo: view.trailingAnchor),
            web.bottomAnchor.constraint(equalTo: view.bottomAnchor),
        ])
    }
}

/// 웹이 부르는 폰 기능 — 지금은 기기 열쇠 하나. 연결 화면(https://woopang.com/admin/m…)에서 부를 때만 준다.
///   window.webkit.messageHandlers.woopangM.postMessage({m: 'deviceKey'}) → Promise {key, name}
/// 폰 기능을 더하려면 여기에 같은 모양으로 넣는다(앱 다시 빌드).
@MainActor
final class WoopangMNative: NSObject, WKScriptMessageHandlerWithReply {
    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) async -> (Any?, String?) {
        guard let body = message.body as? [String: Any], let method = body["m"] as? String else { return (nil, "잘못된 호출") }
        let origin = message.frameInfo.securityOrigin
        guard message.frameInfo.isMainFrame, origin.protocol == "https",
              origin.host == "woopang.com" || origin.host == "www.woopang.com",
              message.frameInfo.request.url?.path.hasPrefix("/admin/m") == true else {
            return (nil, "이 화면에서는 쓸 수 없어요")
        }
        switch method {
        case "deviceKey":
            guard let key = DeviceKey.current() else { return (nil, "기기 열쇠를 만들지 못했어요 (Keychain)") }
            return (["key": key, "name": DeviceKey.label()], nil)
        default:
            return (nil, "모르는 기능: \(method)")
        }
    }
}
