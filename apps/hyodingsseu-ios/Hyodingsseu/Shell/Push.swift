import UIKit
import UserNotifications

/// 아이폰 알림(APNs) — Firebase 없이 Apple 에 직접 (2.0.1, 2026-10-01).
///
/// 웹이 `DeskNative.pushRegister()` 를 부르면: 알림 허락을 묻고(처음 한 번만 — 그 뒤엔 묻지 않고 지금 상태만 돌려준다)
/// → 허락했으면 기기 토큰을 받아 돌려준다. 웹은 그 토큰을 PC 데스크 API `push` 로 보내고,
/// PC 가 팀 키(.p8)로 APNs 에 직접 보낸다(topic `com.que.hyodingsseu`).
/// 토큰은 `AppDelegate` 가 받아 `didRegister`/`didFail` 로 넘겨 준다.
@MainActor
final class Push {
    static let shared = Push()

    /// 마지막으로 받은 기기 토큰(소문자 16진수) — 다시 물었는데 Apple 이 늦으면 이걸 돌려준다
    private var token: String?
    /// 토큰을 기다리는 호출들(웹이 여러 번 불러도 한 번에 답한다)
    private var waiting: [UUID: CheckedContinuation<Result<String, Error>, Never>] = [:]

    /// 웹의 `pushRegister()` 답. 거절해도 Promise 를 깨지 않고 `granted: false` 로 돌려준다.
    ///   허락 + 토큰: `{granted: true, token, platform: "ios"}` · 거절: `{granted: false, platform: "ios"}`
    ///   허락했지만 토큰 실패·10초 넘김: `{granted: true, platform: "ios", error}`
    func register() async -> [String: Any] {
        let granted: Bool
        do {
            granted = try await UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge])
        } catch {
            return ["granted": false, "platform": "ios", "error": error.localizedDescription]
        }
        guard granted else { return ["granted": false, "platform": "ios"] }

        switch await deviceToken() {
        case .success(let hex):
            return ["granted": true, "token": hex, "platform": "ios"]
        case .failure(let e):
            return ["granted": true, "platform": "ios", "error": e.localizedDescription]
        }
    }

    /// 앱을 열면 아이콘의 빨간 숫자를 지운다
    func clearBadge() {
        UNUserNotificationCenter.current().setBadgeCount(0, withCompletionHandler: nil)
    }

    // MARK: AppDelegate 가 부른다

    func didRegister(_ deviceToken: Data) {
        let hex = deviceToken.map { String(format: "%02x", $0) }.joined()
        token = hex
        finishAll(.success(hex))
    }

    func didFail(_ error: Error) {
        finishAll(.failure(error))
    }

    // MARK: -

    /// 토큰은 앱을 지웠다 깔거나 백업에서 되살리면 바뀔 수 있다 → 부를 때마다 Apple 에 다시 받는다(두 번째부터는 금방 온다)
    private func deviceToken() async -> Result<String, Error> {
        let id = UUID()
        let result = await withCheckedContinuation { (c: CheckedContinuation<Result<String, Error>, Never>) in
            waiting[id] = c
            UIApplication.shared.registerForRemoteNotifications()
            Task {
                try? await Task.sleep(for: .seconds(10))
                self.finish(id, .failure(PushError(errorDescription: "10초 안에 기기 토큰을 받지 못했어요")))
            }
        }
        if case .failure = result, let token { return .success(token) }
        return result
    }

    private func finish(_ id: UUID, _ result: Result<String, Error>) {
        waiting.removeValue(forKey: id)?.resume(returning: result)
    }

    private func finishAll(_ result: Result<String, Error>) {
        let all = waiting
        waiting.removeAll()
        all.values.forEach { $0.resume(returning: result) }
    }
}

private struct PushError: LocalizedError {
    let errorDescription: String?
}
