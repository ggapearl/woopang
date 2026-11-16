import CoreLocation

/// 지금 위치 한 번 — 웹의 `navigator.geolocation` 대신 쓴다. 그걸 쓰면 아이폰 허락 다음에
/// 「woopang.com 이(가) 현재 위치를 사용하려고 합니다」 창이 한 번 더 뜨고, 앱을 켤 때마다 또 묻는다.
/// 허락 창 문구: Info.plist 의 `NSLocationWhenInUseUsageDescription` (언어별 — `*.lproj/InfoPlist.strings`).
///
///   WoogyApp.call('location') → {ok: true, lat, lon, acc} | {ok: false, code}
///   code 는 웹 표준(GeolocationPositionError)과 같다: 1 허락 안 함 · 2 못 찾음 · 3 시간 넘김(15초)
@MainActor
final class Locator {
    static let shared = Locator()

    private let manager = CLLocationManager()
    private let relay = Relay()
    /// 위치를 기다리는 호출들 — 찾는 중에 또 불러도 같은 답을 함께 받는다
    private var waiting: [CheckedContinuation<[String: Any], Never>] = []
    /// 지금 찾기의 번호 — 끝난 찾기의 늦은 시간 넘김이 새 찾기를 끝내지 않게
    private var serial = 0

    private init() {
        manager.delegate = relay
        manager.desiredAccuracy = kCLLocationAccuracyBest
    }

    func once() async -> [String: Any] {
        await withCheckedContinuation { (k: CheckedContinuation<[String: Any], Never>) in
            waiting.append(k)
            guard waiting.count == 1 else { return }
            serial += 1
            let id = serial
            Task {
                try? await Task.sleep(for: .seconds(15))
                self.finish(["ok": false, "code": 3], id: id)
            }
            proceed()
        }
    }

    private func proceed() {
        switch manager.authorizationStatus {
        case .notDetermined:
            manager.requestWhenInUseAuthorization()          // 답은 Relay → authChanged
        case .denied, .restricted:
            finish(["ok": false, "code": 1])
        default:
            manager.requestLocation()
        }
    }

    // MARK: Relay 가 부른다

    fileprivate func authChanged() {
        guard !waiting.isEmpty, manager.authorizationStatus != .notDetermined else { return }
        proceed()
    }

    fileprivate func found(lat: Double, lon: Double, acc: Double) {
        finish(["ok": true, "lat": lat, "lon": lon, "acc": acc])
    }

    fileprivate func failed(denied: Bool) {
        finish(["ok": false, "code": denied ? 1 : 2])
    }

    private func finish(_ reply: [String: Any], id: Int? = nil) {
        if let id, id != serial { return }
        let all = waiting
        waiting.removeAll()
        all.forEach { $0.resume(returning: reply) }
    }
}

/// CLLocationManager 의 소식을 Locator(메인)로 넘긴다 — 숫자만 넘긴다
private final class Relay: NSObject, CLLocationManagerDelegate {
    func locationManagerDidChangeAuthorization(_ manager: CLLocationManager) {
        Task { @MainActor in Locator.shared.authChanged() }
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let l = locations.last else { return }
        let lat = l.coordinate.latitude, lon = l.coordinate.longitude, acc = l.horizontalAccuracy
        Task { @MainActor in Locator.shared.found(lat: lat, lon: lon, acc: acc) }
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {
        let denied = (error as? CLError)?.code == .denied
        Task { @MainActor in Locator.shared.failed(denied: denied) }
    }
}
