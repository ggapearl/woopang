import SwiftUI

/// 우팡M — 대표님 전용 우팡 관리자 앱 (1.0, 2026-11-14).
///
/// 화면은 웹이다: https://woopang.com/admin (관리자 웹 — 수정·삭제 요청 · 신고 · 알림 보내기). 앱은 그 화면을 꽉 채워 띄우는
/// 「포장지」라 관리자 화면을 고쳐도 **다시 빌드하지 않는다**. 다시 빌드하는 건 폰 기능(Shell/)을 바꿀 때와 TestFlight 90일 만료 전뿐.
///
/// 비밀번호 없이 — 처음 켜면 Keychain 에 기기 열쇠를 만들고(`DeviceKey`), 연결 화면(/admin/m/)이 그 열쇠로 들어간다.
/// 처음 한 번만 효딩쓰 앱으로 온 허용 링크에서 [허용] → 그 뒤로는 켜자마자 관리자 화면. 오프닝 없음(검은 실행 화면 → 바로 화면).
@main
struct WoopangMApp: App {
    var body: some Scene {
        WindowGroup {
            WebShell(start: Shell.startURL)
                // 위(상태 표시줄)는 비켜 앉아 관리자 머리 막대 색으로 채우고, 아래(홈 막대)는 화면이 env(safe-area-inset-bottom) 으로 비켜 앉는다.
                // 키보드는 비켜 간다(.container 만 무시) — 입력칸이 가려지지 않게
                .ignoresSafeArea(.container, edges: .bottom)
                .background(Shell.barColor.ignoresSafeArea())
                .preferredColorScheme(.dark)
        }
    }
}
