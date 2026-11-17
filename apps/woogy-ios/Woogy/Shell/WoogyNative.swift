import AVFoundation
import UIKit
import WebKit

/// 우기 화면(웹)이 부르는 폰 기능. **우기 주소(https://woopang.com/woogy…)의 맨 위 화면**에서 부를 때만 준다 —
/// 결제창 같은 바깥 화면에서는 부를 수 없다. 웹 쪽 이름은 `window.WoogyApp` (ShellPages.bridgeJS).
///
///   WoogyApp.call('pushRegister')         → {granted, token?, platform: 'ios', error?}   알림 허락 · 기기 토큰(처음 한 번만 묻는다)
///   WoogyApp.call('auth', {url})          → {ok: true} | {ok: false, canceled: true}     간편로그인 — 시스템 브라우저 창으로 열고, 끝나면 화면이 로그인된다
///   WoogyApp.call('openExternal', {url})  → {ok}                                         사파리 · 전화 · 문자로
///   WoogyApp.call('location')             → {ok: true, lat, lon, acc} | {ok: false, code} 지금 위치 한 번(Locator — 허락 창이 한 번만 뜬다)
///   WoogyApp.call('cameraEnsure')         → {granted, status: authorized|denied|restricted}  카메라 허락 — 안 물었으면 한 번 묻고, 정했으면 묻지 않는다
///   WoogyApp.call('openSettings')         → {ok}                                         아이폰 설정 › 우기 (카메라를 껐을 때 안내용)
///
/// 폰 기능을 더하려면 여기에 같은 모양으로 넣는다(앱 다시 빌드).
@MainActor
final class WoogyNative: NSObject, WKScriptMessageHandlerWithReply {
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

        case "location":
            let reply = await Locator.shared.once()
            return (reply, nil)

        case "cameraEnsure":
            // 카메라 허락 — 아직 안 물었으면 지금 한 번 묻고, 이미 정했으면 묻지 않고 그 결과만 알려 준다
            switch AVCaptureDevice.authorizationStatus(for: .video) {
            case .authorized:
                return (["granted": true, "status": "authorized"], nil)
            case .notDetermined:
                let ok = await AVCaptureDevice.requestAccess(for: .video)
                return (["granted": ok, "status": ok ? "authorized" : "denied"], nil)
            case .denied:
                return (["granted": false, "status": "denied"], nil)
            default:
                return (["granted": false, "status": "restricted"], nil)
            }

        case "openSettings":
            guard let url = URL(string: UIApplication.openSettingsURLString) else { return (nil, "설정을 열 수 없어요") }
            let ok = await UIApplication.shared.open(url)
            return (["ok": ok], nil)

        default:
            return (nil, "모르는 기능: \(method)")
        }
    }
}
