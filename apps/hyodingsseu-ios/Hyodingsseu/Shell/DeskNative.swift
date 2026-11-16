import AVFoundation
import Photos
import UIKit
import WebKit

/// 웹 화면이 부르는 폰 기능 — 안드로이드의 DeskNativePlugin(Capacitor)과 **같은 이름·같은 모양**.
/// 웹(app.js)은 `window.Capacitor.Plugins.DeskNative` 로 부른다 — `ShellPages.bridgeJS` 가 그 이름을 만들어
/// `webkit.messageHandlers.deskNative` 로 넘긴다. 돌려준 값은 JS 의 Promise 결과가 된다.
///   haptic({kind: 'tap'|'alert'}) · openExternal({url}) · deviceName() → {name} · speak({text, rate}) · stop() · setBars()
///   pushRegister() → {granted, token?, platform: 'ios', error?} (아이폰만 — 알림 허락 + APNs 기기 토큰, `Push.swift`)
///   pushOpened() → {at, body, n} | {} (아이폰만 — 1분 안에 누른 알림. 2.1)
///   saveToPhotos({url, name}) — 사진·영상을 받아 사진첩에 바로 저장 (2.3, `savePhoto`). url 은 https://woopang.com/AI/doc/ 만
///   다 읽으면 'speechDone' 사건 · 알림을 누르면 'pushOpen' 사건 (addListener)
/// 폰 기능을 더하려면 여기와 bridgeJS 에 같은 이름으로 넣고(앱 빌드 필요), 안드로이드 DeskNativePlugin.java 에도 같은 것을.
@MainActor
final class DeskNative: NSObject, WKScriptMessageHandlerWithReply, AVSpeechSynthesizerDelegate {
    weak var webView: WKWebView?
    private let synth = AVSpeechSynthesizer()

    override init() {
        super.init()
        synth.delegate = self
        Push.shared.onOpen = { [weak self] in self?.emit("pushOpen") }
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
        case "pushOpened":                     // 누른 알림 — 화면이 그 말로 간다 (2.1)
            return (Push.shared.lastOpened(), nil)
        case "saveToPhotos":                   // 사진·영상 → 사진첩 (2.3)
            guard let s = a["url"] as? String, let url = URL(string: s), url.scheme == "https",
                  url.host == "woopang.com", url.path.contains("/AI/doc/") else { return (nil, "저장할 수 없는 주소예요") }
            return await savePhoto(url: url, name: a["name"] as? String ?? url.lastPathComponent)
        default:
            return (nil, "모르는 기능: \(method)")
        }
    }

    /// 받아서 사진첩에 추가 — 파일 앱이 아니라 「사진」에 들어간다. 사진 추가 권한(addOnly)만 묻는다.
    private func savePhoto(url: URL, name: String) async -> (Any?, String?) {
        let ext = (name as NSString).pathExtension.lowercased()
        let isVideo = ["mp4", "mov", "m4v"].contains(ext)
        let dest = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString + "." + (ext.isEmpty ? "jpg" : ext))
        defer { try? FileManager.default.removeItem(at: dest) }
        do {
            let (tmp, resp) = try await URLSession.shared.download(from: url)
            guard (resp as? HTTPURLResponse)?.statusCode == 200 else { return (nil, "파일을 받지 못했어요") }
            try FileManager.default.moveItem(at: tmp, to: dest)
            let status = await PHPhotoLibrary.requestAuthorization(for: .addOnly)
            guard status == .authorized || status == .limited else { return (nil, "설정 → 효딩쓰에서 사진 추가를 허락해 주세요") }
            try await PHPhotoLibrary.shared().performChanges {
                if isVideo { _ = PHAssetChangeRequest.creationRequestForAssetFromVideo(atFileURL: dest) }
                else { _ = PHAssetChangeRequest.creationRequestForAssetFromImage(atFileURL: dest) }
            }
            return ([String: Any](), nil)
        } catch {
            return (nil, "사진첩에 저장하지 못했어요")
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
