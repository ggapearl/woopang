import SwiftUI
import UserNotifications

/// 우기 iOS — 「포장지」 앱 (1.0.0, 2026-11-15).
///
/// 화면은 웹이다: https://woogy.app/woogy/ (우기 서버 — nginx /woogy/ → 127.0.0.1:6690).
/// 화면·기능을 고치면 서버에서 바로 바뀌므로 **앱을 다시 빌드하지 않는다**. 다시 빌드하는 건 폰 기능
/// (`WoogyNative` — 알림 · 간편로그인 · 바깥 앱 열기)을 바꿀 때와 TestFlight 90일 만료 전뿐.
@main
struct WoogyApp: App {
    // 알림(APNs) 기기 토큰·알림 처리 — Push.swift
    @UIApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        WindowGroup {
            WebShell(start: Shell.startURL)
                // 노치·홈 막대 쪽은 화면(CSS)이 env(safe-area-inset-*) 로 직접 비켜 앉는다(viewport-fit=cover).
                // 키보드는 비켜 간다(.container 만 무시) — 입력칸이 키보드에 가려지지 않는다(효딩쓰와 같은 방식)
                .ignoresSafeArea(.container)
                .background(Shell.bgColor.ignoresSafeArea())        // 불러오는 동안 — 우기 바탕 크림색
                .preferredColorScheme(.light)
                // ① 공유 카드 링크(유니버설 링크, 1.0.0 (3)): https://woogy.app/woogy/c/<번호> → 그 기부카드를 맨 앞에
                // ② 간편로그인을 사파리 쪽에서 마치고 돌아옴: com.que.woogy://auth?t=<1회용 토큰>
                // ③ 그 밖의 우기 주소는 그대로 앱 안에서
                .onOpenURL { url in
                    if let to = Shell.shareLink(url) { Router.shared.open(to); return }
                    if let to = Shell.authExchange(url) {
                        if !Auth.shared.finish(with: url) { Router.shared.open(to) }
                        return
                    }
                    if Shell.isOwn(url) { Router.shared.open(url) }
                }
        }
    }
}

/// 알림(APNs) — 기기 토큰을 받아 `Push` 에 넘기고, 알림이 왔을 때 어떻게 보일지 정한다.
@MainActor
final class AppDelegate: NSObject, UIApplicationDelegate, UNUserNotificationCenterDelegate {
    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        Push.shared.didRegister(deviceToken)
    }

    func application(_ application: UIApplication, didFailToRegisterForRemoteNotificationsWithError error: Error) {
        Push.shared.didFail(error)
    }

    // 앱을 보고 있을 때 온 알림도 위에 띄운다 — 우기 알림(근처 사연 · 전달 완료 등)은 지금 보는 화면과 다른 소식이라
    nonisolated func userNotificationCenter(_ center: UNUserNotificationCenter, willPresent notification: UNNotification,
                                            withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .sound, .list])
    }

    // 알림을 눌러 들어옴 — 서버가 실은 link(우기 화면 주소, 예: https://woogy.app/woogy/#…)로 간다
    nonisolated func userNotificationCenter(_ center: UNUserNotificationCenter, didReceive response: UNNotificationResponse,
                                            withCompletionHandler completionHandler: @escaping () -> Void) {
        let link = response.notification.request.content.userInfo["link"] as? String
        completionHandler()
        Task { @MainActor in Push.shared.didOpen(link: link) }
    }
}
