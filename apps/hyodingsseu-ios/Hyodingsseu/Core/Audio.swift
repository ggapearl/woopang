import AVFoundation
import UIKit

enum AudioSession {
    static func activate() {
        let s = AVAudioSession.sharedInstance()
        try? s.setCategory(.playAndRecord, mode: .default, options: [.defaultToSpeaker, .allowBluetoothA2DP])
        try? s.setActive(true)
    }
}

/// 누르면 녹음, 다시 누르면 끝 — m4a 로 PC 에 보내 받아적는다(아이폰에는 남기지 않는다).
@MainActor
final class Recorder: ObservableObject {
    @Published private(set) var recording = false
    @Published private(set) var level: Double = 0

    /// 60초가 지나 저절로 멈췄을 때
    var onAutoStop: (() -> Void)?

    private var recorder: AVAudioRecorder?
    private var timer: Timer?
    private let url = FileManager.default.temporaryDirectory.appendingPathComponent("hyoding-voice.m4a")

    func start() async -> Bool {
        guard await AVAudioApplication.requestRecordPermission() else { return false }
        AudioSession.activate()
        let settings: [String: Any] = [
            AVFormatIDKey: Int(kAudioFormatMPEG4AAC),
            AVSampleRateKey: 16_000.0,
            AVNumberOfChannelsKey: 1,
            AVEncoderAudioQualityKey: AVAudioQuality.high.rawValue,
        ]
        do {
            try? FileManager.default.removeItem(at: url)
            let r = try AVAudioRecorder(url: url, settings: settings)
            r.isMeteringEnabled = true
            guard r.record(forDuration: 60) else { return false }
            recorder = r
            recording = true
            timer = Timer.scheduledTimer(withTimeInterval: 0.08, repeats: true) { [weak self] _ in
                Task { @MainActor in self?.tick() }
            }
            return true
        } catch {
            return false
        }
    }

    private func tick() {
        guard let r = recorder, recording else { return }
        if !r.isRecording {
            onAutoStop?()
            return
        }
        r.updateMeters()
        let db = Double(r.averagePower(forChannel: 0))
        level = max(0, min(1, (db + 50) / 45))
    }

    /// 녹음을 끝내고 소리를 돌려준다.
    func stop() -> Data? {
        recorder?.stop()
        finish()
        defer { try? FileManager.default.removeItem(at: url) }
        return try? Data(contentsOf: url)
    }

    func cancel() {
        recorder?.stop()
        finish()
        try? FileManager.default.removeItem(at: url)
    }

    private func finish() {
        timer?.invalidate()
        timer = nil
        recorder = nil
        recording = false
        level = 0
    }
}

/// 답을 소리로 — 아이폰 목소리(바로) 또는 PC 가 만든 소희 목소리(m4a).
@MainActor
final class Speaker: NSObject, ObservableObject, AVSpeechSynthesizerDelegate, AVAudioPlayerDelegate {
    @Published private(set) var speaking = false

    private let synth = AVSpeechSynthesizer()
    private var player: AVAudioPlayer?

    override init() {
        super.init()
        synth.delegate = self
    }

    func speak(_ text: String, speed: Double) {
        stop()
        AudioSession.activate()
        let u = AVSpeechUtterance(string: text)
        u.voice = Self.koreanVoice()
        u.rate = Float(min(0.62, max(0.38, 0.5 * speed)))
        synth.speak(u)
        speaking = true
    }

    func play(_ data: Data) {
        stop()
        AudioSession.activate()
        guard let p = try? AVAudioPlayer(data: data, fileTypeHint: AVFileType.m4a.rawValue) else { return }
        p.delegate = self
        player = p
        speaking = p.play()
    }

    func stop() {
        if synth.isSpeaking { synth.stopSpeaking(at: .immediate) }
        player?.stop()
        player = nil
        speaking = false
    }

    private static func koreanVoice() -> AVSpeechSynthesisVoice? {
        let ko = AVSpeechSynthesisVoice.speechVoices().filter { $0.language == "ko-KR" }
        return ko.max { $0.quality.rawValue < $1.quality.rawValue } ?? AVSpeechSynthesisVoice(language: "ko-KR")
    }

    nonisolated func speechSynthesizer(_ synthesizer: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
        Task { @MainActor in self.speaking = false }
    }

    nonisolated func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        Task { @MainActor in self.speaking = false }
    }
}

/// 화면용 글(마크다운)을 귀로 듣기 좋은 짧은 말로 — PC 의 for_speech 와 같은 규칙.
enum SpeechText {
    static func clean(_ text: String, limit: Int = 300) -> String {
        var t = text
        let rules: [(String, String)] = [
            ("```[\\s\\S]*?```", " "),
            ("(?m)^\\s*\\|.*\\|\\s*$", " "),
            ("\\[([^\\]]+)\\]\\([^)]+\\)", "$1"),
            ("https?://\\S+", "링크"),
            ("`([^`]+)`", "$1"),
            ("(?m)^[#>\\-*+\\s]+", ""),
            ("[*_~#|]", ""),
            ("\\s+", " "),
        ]
        for (pattern, template) in rules {
            t = t.replacingOccurrences(of: pattern, with: template, options: .regularExpression)
        }
        t = t.trimmingCharacters(in: .whitespacesAndNewlines)
        if t.count <= limit { return t }
        var out = ""
        var sentence = ""
        for ch in t {
            sentence.append(ch)
            if ".!?。".contains(ch) {
                if out.count + sentence.count > limit { break }
                out += sentence
                sentence = ""
            }
        }
        if out.isEmpty { out = String(t.prefix(limit)) }
        return out.trimmingCharacters(in: .whitespaces) + " 자세한 건 화면에 있어요."
    }
}

@MainActor
enum Haptics {
    static func tap() {
        UIImpactFeedbackGenerator(style: .light).impactOccurred()
    }

    static func alert() {
        UINotificationFeedbackGenerator().notificationOccurred(.warning)
    }
}
