import UIKit
import WebKit

/// 우기 화면(웹)이 부르는 폰 기능. **우기 주소(https://woopang.com/woogi…)의 맨 위 화면**에서 부를 때만 준다 —
/// 결제창 같은 바깥 화면에서는 부를 수 없다. 웹 쪽 이름은 `window.WoogiApp` (ShellPages.bridgeJS).
///
///   WoogiApp.call('pushRegister')         → {granted, token?, platform: 'ios', error?}   알림 허락 · 기기 토큰(처음 한 번만 묻는다)
///   WoogiApp.call('auth', {url})          → {ok: true} | {ok: false, canceled: true}     간편로그인 — 시스템 브라우저 창으로 열고, 끝나면 화면이 로그인된다
///   WoogiApp.call('openExternal', {url})  → {ok}                                         사파리 · 전화 · 문자로
///
/// 폰 기능을 더하려면 여기에 같은 모양으로 넣는다(앱 다시 빌드).
@MainActor
final class WoogiNative: NSObject, WKScriptMessageHandlerWithReply {
    weak var webView: WKWebView?

    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) async -> (Any?, String?) {
        guard let body = message.body as? [String: Any], let method = body["m"] as? String else { return (nil, "잘못된 호출") }
        let args = body["a"] as? [String: Any] ?? [:]
        let origin = message.frameInfo.securityOrigin
        guard message.frameInfo.isMainFrame, origin.protocol == "https",
              origin.host == "woopang.com" || origin.host == "www.woopang.com",
              let here = message.frameInfo.request.url, Shell.isOwn(here) else {
            return (nil, "이 화면에서는 쓸 수 없어요")
        }
        switch method {
        case "pushRegister":
            let reply = await Push.shared.register()
            return (reply, nil)

        case "auth":
            guard let s = args["url"] as? String, let url = URL(string: s), Shell.isOwn(url) else {
                return (nil, "로그인 주소가 잘못됐어요")
            }
            switch await Auth.shared.start(url, window: webView?.window) {
            case .done(let back):
                guard let to = Shell.authExchange(back) else { return (nil, "로그인을 마치지 못했어요") }
                Router.shared.open(to)
                return (["ok": true], nil)
            case .canceled:
                return (["ok": false, "canceled": true], nil)
            case .failed(let why):
                return (nil, why)
            }

        case "openExternal":
            guard let s = args["url"] as? String, let url = URL(string: s),
                  let scheme = url.scheme?.lowercased(), ["https", "http", "tel", "sms", "mailto"].contains(scheme) else {
                return (nil, "열 수 없는 주소예요")
            }
            let ok = await UIApplication.shared.open(url)
            return (["ok": ok], nil)

        default:
            return (nil, "모르는 기능: \(method)")
        }
    }
}
