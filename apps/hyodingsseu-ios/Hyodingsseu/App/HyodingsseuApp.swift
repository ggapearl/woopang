import AVFoundation
import SwiftUI

/// 효딩쓰 iOS — 「포장지」 앱 (2.0, 2026-10-01).
///
/// 화면은 웹이다: https://woopang.com/hyodingsseu/ (= 저장소 apps/hyodingsseu-android/web, 안드로이드 앱과 같은 화면).
/// 화면·기능을 고치면 서버에서 바로 바뀌므로 **앱을 다시 빌드하지 않는다**. 다시 빌드하는 건 폰 기능
/// (목소리·진동·마이크·링크 — `DeskNative`)을 바꿀 때와 TestFlight 90일 만료 전뿐.
/// 1.x 의 SwiftUI 화면(DeskStore·MainView 등)은 커밋 d4a47df 에 있다.
@main
struct HyodingsseuApp: App {
    init() {
        // PC 목소리(m4a)·아이폰 목소리는 무음 스위치와 상관없이 스피커로, 마이크도 함께 — 1.x 와 같은 설정
        try? AVAudioSession.sharedInstance().setCategory(.playAndRecord, mode: .default,
                                                        options: [.defaultToSpeaker, .allowBluetoothA2DP])
    }

    var body: some Scene {
        WindowGroup {
            WebShell(start: Shell.startURL)
                // 노치·홈 막대 쪽은 화면(CSS)이 env(safe-area-inset-*) 로 직접 비켜 앉는다(viewport-fit=cover).
                // 키보드는 비켜 간다(.container 만 무시) — 웹 화면이 키보드 위로 줄어 입력칸이 가려지지 않는다(안드로이드와 같은 방식)
                .ignoresSafeArea(.container)
                .background(Color(red: 0x1B / 255.0, green: 0x2A / 255.0, blue: 0x6B / 255.0))   // 불러오는 동안 — 머리 막대 남색
        }
    }
}
