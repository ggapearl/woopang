import SwiftUI
import UIKit
import WebKit

enum Shell {
    /// 앱이 여는 첫 화면 — 처음 한 번 입장 코드를 넣으면(180일) 그 뒤로는 바로 우기 화면
    static let startURL = URL(string: "https://woopang.com/woogy/")!
    /// 간편로그인을 마치고 앱으로 돌아오는 주소의 scheme — Info.plist 의 CFBundleURLSchemes 와 같아야 한다
    static let scheme = "com.que.woogy"
    /// 우기 바탕 크림색 #FFFBEA — 불러오는 동안 · 새 창 바탕
    static let bgUIColor = UIColor(red: 0xFF / 255.0, green: 0xFB / 255.0, blue: 0xEA / 255.0, alpha: 1)
    static let bgColor = Color(uiColor: bgUIColor)
    /// 우기 초록 #2A6B42 — 새 창 「닫기」
    static let greenUIColor = UIColor(red: 0x2A / 255.0, green: 0x6B / 255.0, blue: 0x42 / 255.0, alpha: 1)
    static let version = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0"
    static let build = Bundle.main.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "0"

    /// 앱 안에서 그대로 보여 줄 우기 주소 — https://woopang.com/woogy…
    static func isOwn(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https", let host = url.host?.lowercased(),
              host == "woopang.com" || host == "www.woopang.com" else { return false }
        return url.path == "/woogy" || url.path.hasPrefix("/woogy/")
    }

    /// com.que.woogy://auth?t=<1회용 토큰> → https://woopang.com/woogy/auth/app?t=<토큰> (서버가 토큰을 로그인으로 바꾼다 — 5분 · 한 번만)
    static func authExchange(_ url: URL) -> URL? {
        guard url.scheme?.lowercased() == scheme, url.host?.lowercased() == "auth",
              let t = URLComponents(url: url, resolvingAgainstBaseURL: false)?
                .queryItems?.first(where: { $0.name == "t" })?.value,
              (16...200).contains(t.count),
              t.allSatisfy({ $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "-" || $0 == "_") }) else { return nil }
        var c = URLComponents(string: "https://woopang.com/woogy/auth/app")!
        c.queryItems = [URLQueryItem(name: "t", value: t)]
        return c.url
    }

    /// 공유 카드 링크(카톡 등에서 누름) https://woopang.com/woogy/c/S<16진 10자리> → 앱 화면 https://woopang.com/woogy/#share=<번호>
    /// — 받은 기부카드가 맨 앞에 뜬다. 서버의 https://woopang.com/.well-known/apple-app-site-association 이 /woogy/c/* 를 이 앱에 맡긴다(1.0.0 (3)).
    static func shareLink(_ url: URL) -> URL? {
        guard isOwn(url) else { return nil }
        let parts = url.path.split(separator: "/")                  // ["woogy", "c", "S…"]
        guard parts.count == 3, parts[1] == "c" else { return nil }
        let no = String(parts[2])
        guard no.count == 11, no.first == "S",
              no.dropFirst().allSatisfy({ ("0"..."9").contains($0) || ("A"..."F").contains($0) }) else { return nil }
        return URL(string: "https://woopang.com/woogy/#share=" + no)
    }

    /// 앱스토어 링크(결제 · 카드 앱 설치 안내) — 사파리를 거치지 않고 앱스토어로
    static func isStore(_ url: URL) -> Bool {
        let h = url.host?.lowercased() ?? ""
        return h == "apps.apple.com" || h == "itunes.apple.com"
    }

    /// 웹 주소가 아닌 것(전화 tel: · 문자 sms: · 카드/결제 앱 ispmobile:// 등 · 앱스토어 itms-apps:)
    static func isAppLink(_ url: URL) -> Bool {
        let s = url.scheme?.lowercased() ?? ""
        return !["http", "https", "about", "blob", "data"].contains(s)
    }
}

/// 웹 화면 하나를 앱 곳곳(알림을 눌러 들어옴 · 간편로그인 복귀)에서 같은 길로 움직인다.
/// 화면이 아직 없으면(앱이 꺼져 있다 알림으로 켜짐) 주소를 들고 있다가 화면이 생길 때 첫 화면 대신 연다.
@MainActor
final class Router {
    static let shared = Router()
    private weak var webView: WKWebView?
    private var pending: URL?

    func open(_ url: URL) {
        guard Shell.isOwn(url) else { return }
        if let wv = webView { wv.load(URLRequest(url: url)) } else { pending = url }
    }

    /// 화면이 생길 때 — 기다리던 주소가 있으면 돌려준다
    func attach(_ wv: WKWebView) -> URL? {
        webView = wv
        defer { pending = nil }
        return pending
    }
}

/// 우기 웹을 꽉 채워 보여 주는 WKWebView. 폰 기능은 `WoogyNative`, 그 밖의 결정(주소 · 새 창 · 알림창 · 오프라인)은 여기서.
struct WebShell: UIViewRepresentable {
    let start: URL

    func makeCoordinator() -> Coordinator { Coordinator(start: start) }

    func makeUIView(context: Context) -> WKWebView {
        let cfg = WKWebViewConfiguration()
        cfg.websiteDataStore = .default()                           // 입장 코드(180일) · 로그인(90일) 쿠키가 앱을 껐다 켜도 남게
        cfg.allowsInlineMediaPlayback = true
        // 기본값 "Mobile/15E148" 은 그대로 두고(결제창이 모바일로 알아보게) 뒤에 앱 표시만 붙인다 — 서버가 앱인지 알 수 있게
        cfg.applicationNameForUserAgent = "Mobile/15E148 WoogyApp/\(Shell.version)"
        let ucc = cfg.userContentController
        ucc.addUserScript(WKUserScript(source: ShellPages.bridgeJS, injectionTime: .atDocumentStart, forMainFrameOnly: true))
        ucc.addScriptMessageHandler(context.coordinator.native, contentWorld: .page, name: "woogy")

        let wv = WKWebView(frame: .zero, configuration: cfg)
        wv.navigationDelegate = context.coordinator
        wv.uiDelegate = context.coordinator
        wv.allowsBackForwardNavigationGestures = false              // 탐색 카드(좌우로 넘기기)와 겹친다 — 뒤로는 화면 안의 버튼으로
        wv.scrollView.contentInsetAdjustmentBehavior = .never       // 비켜 앉기는 화면(CSS)이 한다
        wv.scrollView.bounces = false                               // 화면 전체가 고무줄처럼 끌려 내려가지 않게(스크롤은 화면 안의 칸이 한다)
        wv.isOpaque = false
        wv.backgroundColor = Shell.bgUIColor
        wv.scrollView.backgroundColor = Shell.bgUIColor
        wv.isInspectable = true                                     // Mac 사파리 › 개발 메뉴로 들여다보기 (시험 기간 — 앱스토어 출시 전 끈다)
        context.coordinator.attach(wv)
        wv.load(URLRequest(url: Router.shared.attach(wv) ?? start))
        return wv
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}

    @MainActor
    final class Coordinator: NSObject, WKNavigationDelegate, WKUIDelegate {
        let start: URL
        let native = WoogyNative()
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

        /// 앱으로 돌아오면 아이콘의 알림 숫자를 지우고, 안내 화면(인터넷 안 됨)이 떠 있으면 다시 불러온다
        @objc private func becameActive() {
            Push.shared.clearBadge()
            guard let wv = webView else { return }
            if wv.url == nil || wv.url?.scheme == "about" { wv.load(URLRequest(url: start)) }
        }

        // 우기 주소는 앱 안에서. 우기 화면에서 바깥 링크를 누르면 사파리로.
        // 결제창(토스 · 카드사)처럼 화면이 스스로 넘어가는 것은 앱 안에서 — 결제가 끝나면 우기(successUrl)로 돌아와야 해서
        func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction) async -> WKNavigationActionPolicy {
            guard let url = navigationAction.request.url else { return .cancel }
            let scheme = url.scheme?.lowercased() ?? ""
            if ["about", "blob", "data"].contains(scheme) { return .allow }
            // 간편로그인 복귀 주소가 화면 안에서 열림 → 서버에 토큰을 바꾸러 간다(맨 앞 화면에서)
            if let to = Shell.authExchange(url) {
                if webView !== self.webView { close(webView) }
                Task { @MainActor in Router.shared.open(to) }
                return .cancel
            }
            // 전화 · 문자 · 카드/결제 앱 · 앱스토어 → 그 앱으로 (설치 안 된 카드 앱은 결제창이 알아서 앱스토어로 보낸다)
            if Shell.isAppLink(url) || Shell.isStore(url) {
                _ = await UIApplication.shared.open(url)
                return .cancel
            }
            if navigationAction.targetFrame?.isMainFrame == false { return .allow }     // 화면 안의 작은 틀(결제 인증 등)
            if Shell.isOwn(url) { return .allow }
            if navigationAction.navigationType == .linkActivated, let here = webView.url, Shell.isOwn(here) {
                _ = await UIApplication.shared.open(url)
                return .cancel
            }
            return .allow
        }

        // target=_blank · window.open (약관 · 영수증 · 결제 팝업) → 같은 로그인으로 아래에서 올라오는 창에. 앱 링크는 그 앱으로
        func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                     for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
            if let url = navigationAction.request.url, Shell.isAppLink(url) || Shell.isStore(url) {
                UIApplication.shared.open(url)
                return nil
            }
            guard let top = topController() else { return nil }
            let popup = WKWebView(frame: .zero, configuration: configuration)
            popup.navigationDelegate = self
            popup.uiDelegate = self
            popup.allowsBackForwardNavigationGestures = true
            popup.isOpaque = false
            popup.backgroundColor = Shell.bgUIColor
            popup.scrollView.backgroundColor = Shell.bgUIColor
            popup.isInspectable = true
            top.present(PopupController(web: popup), animated: true)
            return popup
        }

        // 새 창의 window.close()
        func webViewDidClose(_ webView: WKWebView) {
            if webView !== self.webView { close(webView) }
        }

        private func close(_ popup: WKWebView) {
            var vc = self.webView?.window?.rootViewController
            while let p = vc?.presentedViewController {
                if let pop = p as? PopupController, pop.web === popup { pop.dismiss(animated: true); return }
                vc = p
            }
        }

        // 웹 화면의 카메라(기부 스캐너 · 사진 찍기) 요청 — 받아 주지 않으면 WKWebView 는 열 때마다 「카메라를 쓰려 합니다」를 새로 묻는다
        // (기기 허락은 이미 한 번 했는데도). 우기 화면의 카메라만 바로 허락 — 기기 허락 자체는 iOS 가 그대로 지킨다(설정에서 끄면 막힘).
        // 마이크는 쓰지 않으니 묻는다(.prompt). Objective-C 이름표를 붙여 이름이 어긋나면 빌드 때 드러나게 한다.
        @objc(webView:requestMediaCapturePermissionForOrigin:initiatedByFrame:type:decisionHandler:)
        func webView(_ webView: WKWebView, requestMediaCapturePermissionFor origin: WKSecurityOrigin,
                     initiatedByFrame frame: WKFrameInfo, type: WKMediaCaptureType,
                     decisionHandler: @escaping (WKPermissionDecision) -> Void) {
            let own = origin.protocol == "https" && (origin.host == "woopang.com" || origin.host == "www.woopang.com")
            decisionHandler(own && type == .camera ? .grant : .prompt)
        }

        // 웹의 alert · confirm · prompt (기부 취소 · 사연 내리기 · 탈퇴 확인, 복사 안 될 때) — 받아 주지 않으면
        // WKWebView 는 아무것도 띄우지 않는다(confirm 은 늘 '아니오'). 콜백 판 + Objective-C 이름표를 붙여 둔다:
        // 이름이 어긋나면 WebKit 이 조용히 안 부르는데(효딩쓰 1.0.3 마이크), 이름표가 있으면 빌드 때 바로 드러난다.
        @objc(webView:runJavaScriptAlertPanelWithMessage:initiatedByFrame:completionHandler:)
        func webView(_ webView: WKWebView, runJavaScriptAlertPanelWithMessage message: String,
                     initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping () -> Void) {
            let answer = Answer<Void>((), { _ in completionHandler() })
            let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
            a.addAction(UIAlertAction(title: "확인", style: .default) { _ in answer(()) })
            if !present(a) { answer(()) }
        }

        @objc(webView:runJavaScriptConfirmPanelWithMessage:initiatedByFrame:completionHandler:)
        func webView(_ webView: WKWebView, runJavaScriptConfirmPanelWithMessage message: String,
                     initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping (Bool) -> Void) {
            let answer = Answer<Bool>(false, completionHandler)
            let a = UIAlertController(title: nil, message: message, preferredStyle: .alert)
            a.addAction(UIAlertAction(title: "취소", style: .cancel) { _ in answer(false) })
            a.addAction(UIAlertAction(title: "확인", style: .default) { _ in answer(true) })
            if !present(a) { answer(false) }
        }

        @objc(webView:runJavaScriptTextInputPanelWithPrompt:defaultText:initiatedByFrame:completionHandler:)
        func webView(_ webView: WKWebView, runJavaScriptTextInputPanelWithPrompt prompt: String, defaultText: String?,
                     initiatedByFrame frame: WKFrameInfo, completionHandler: @escaping (String?) -> Void) {
            let answer = Answer<String?>(nil, completionHandler)
            let a = UIAlertController(title: nil, message: prompt, preferredStyle: .alert)
            a.addTextField { $0.text = defaultText }
            a.addAction(UIAlertAction(title: "취소", style: .cancel) { _ in answer(nil) })
            a.addAction(UIAlertAction(title: "확인", style: .default) { [weak a] _ in answer(a?.textFields?.first?.text ?? "") })
            if !present(a) { answer(nil) }
        }

        private func topController() -> UIViewController? {
            var vc = webView?.window?.rootViewController
            while let p = vc?.presentedViewController { vc = p }
            return vc
        }

        private func present(_ vc: UIViewController) -> Bool {
            guard let top = topController(), !top.isBeingDismissed else { return false }
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
            let failed = e.userInfo[NSURLErrorFailingURLErrorKey] as? URL
            let retry = failed.flatMap { Shell.isOwn($0) ? $0 : nil } ?? start
            webView.loadHTMLString(ShellPages.offlineHTML(retry: retry.absoluteString, reason: e.localizedDescription), baseURL: nil)
        }

        // 웹 화면 프로세스가 죽으면(메모리 부족) 보던 우기 화면을 다시 띄운다
        func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
            if webView === self.webView {
                let here = webView.url.flatMap { Shell.isOwn($0) ? $0 : nil } ?? start
                webView.load(URLRequest(url: here))
            } else {
                webView.reload()
            }
        }
    }
}

/// alert · confirm · prompt 의 답을 꼭 한 번만 보낸다 — 답 없이 버려지면(알림창이 그냥 사라짐) WebKit 이 앱을 끈다
private final class Answer<T> {
    private var reply: ((T) -> Void)?
    private let fallback: T

    init(_ fallback: T, _ reply: @escaping (T) -> Void) {
        self.fallback = fallback
        self.reply = reply
    }

    func callAsFunction(_ value: T) {
        reply?(value)
        reply = nil
    }

    deinit { reply?(fallback) }
}

/// 새 창(target=_blank)을 아래에서 올라오는 창으로 — 같은 로그인(쿠키)을 쓴다. 아래로 밀거나 「닫기」로 닫는다.
final class PopupController: UIViewController {
    let web: WKWebView

    init(web: WKWebView) {
        self.web = web
        super.init(nibName: nil, bundle: nil)
    }

    required init?(coder: NSCoder) { nil }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = Shell.bgUIColor
        let close = UIButton(type: .system)
        close.setTitle("닫기", for: .normal)
        close.titleLabel?.font = .systemFont(ofSize: 17, weight: .semibold)
        close.tintColor = Shell.greenUIColor
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
