import AVFoundation
import UIKit
import WebKit

/// 웹 화면이 부르는 폰 기능 — 안드로이드의 DeskNativePlugin(Capacitor)과 **같은 이름·같은 모양**.
/// 웹(app.js)은 `window.Capacitor.Plugins.DeskNative` 로 부른다 — `ShellPages.bridgeJS` 가 그 이름을 만들어
/// `webkit.messageHandlers.deskNative` 로 넘긴다. 돌려준 값은 JS 의 Promise 결과가 된다.
///   haptic({kind: 'tap'|'alert'}) · openExternal({url}) · deviceName() → {name} · speak({text, rate}) · stop() · setBars()
///   pushRegister() → {granted, token?, platform: 'ios', error?} (아이폰만 — 알림 허락 + APNs 기기 토큰, `Push.swift`)
///   다 읽으면 'speechDone' 사건 (addListener)
/// 폰 기능을 더하려면 여기와 bridgeJS 에 같은 이름으로 넣고(앱 빌드 필요), 안드로이드 DeskNativePlugin.java 에도 같은 것을.
@MainActor
final class DeskNative: NSObject, WKScriptMessageHandlerWithReply, AVSpeechSynthesizerDelegate {
    weak var webView: WKWebView?
    private let synth = AVSpeechSynthesizer()

    override init() {
        super.init()
        synth.delegate = self
    }

    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) async -> (Any?, String?) {
        guard let body = message.body as? [String: Any], let method = body["m"] as? String else { return (nil, "잘못된 호출") }
        let a = body["a"] as? [String: Any] ?? [:]
        switch method {
        case "haptic":
            if (a["kind"] as? String) == "alert" {
                UINotificationFeedbackGenerator().notificationOccurred(.warning)
            } else {
                UIImpactFeedbackGenerator(style: .light).impactOccurred()
            }
            return ([String: Any](), nil)
        case "openExternal":
            guard let s = a["url"] as? String, let url = URL(string: s) else { return (nil, "주소가 이상해요") }
            let ok = await UIApplication.shared.open(url)
            return ok ? ([String: Any](), nil) : (nil, "열 수 없어요")
        case "deviceName":
            return (["name": UIDevice.current.name], nil)
        case "speak":
            speak(a["text"] as? String ?? "", rate: (a["rate"] as? NSNumber)?.doubleValue ?? 1.0)
            return ([String: Any](), nil)
        case "stop":
            synth.stopSpeaking(at: .immediate)
            return ([String: Any](), nil)
        case "setBars":
            return ([String: Any](), nil)      // 아이폰은 상태 표시줄 밑을 화면(웹)이 칠한다 — 할 일 없음
        case "pushRegister":                   // 알림 허락 + 기기 토큰 — Push.swift
            return (await Push.shared.register(), nil)
        default:
            return (nil, "모르는 기능: \(method)")
        }
    }

    /// 아이폰 목소리(한국어). rate 1.0 = 보통 (안드로이드와 같은 0.7~1.6 범위)
    private func speak(_ text: String, rate: Double) {
        synth.stopSpeaking(at: .immediate)
        let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty else {
            emit("speechDone")
            return
        }
        let u = AVSpeechUtterance(string: t)
        u.voice = AVSpeechSynthesisVoice(language: "ko-KR")
        let r = Double(AVSpeechUtteranceDefaultSpeechRate) * rate
        u.rate = Float(min(Double(AVSpeechUtteranceMaximumSpeechRate), max(Double(AVSpeechUtteranceMinimumSpeechRate), r)))
        try? AVAudioSession.sharedInstance().setActive(true)
        synth.speak(u)
    }

    // 끝까지 읽었을 때만 알린다 — 멈춘 건(stop·새로 읽기) 웹이 이미 알고 있다
    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.emit("speechDone") }
    }

    private func emit(_ event: String) {
        webView?.evaluateJavaScript("window.__deskNativeEmit && window.__deskNativeEmit('\(event)', {})", completionHandler: nil)
    }
}
