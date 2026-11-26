// 우팡 길안내 — 홈으로 나가도 아일랜드 · 잠금 화면에 남은 거리 · 방향 (11-26 대표님 10번 ② · 시안 우팡_노치_시안_1124)
// · 유니티(R0926LiveGuide.cs)가 WoopangLiveGuide.mm 을 거쳐 부른다. 이 파일을 빼고 빌드해도 앱은 그대로 돈다 (WoopangLiveGuide.mm 이 이름으로 찾는다)
// · 유니티는 홈으로 나가면 멈추므로 거리 · 방향은 여기서 직접 잰다 — 백그라운드 위치는 길안내 중에만 (Info.plist 에 location 이 있을 때만 켠다)
// · 도착(10m 안 1초) · 끝내기 · 20분이면 바로 끈다. 실시간 현황은 앱이 화면에 떠 있을 때만 시작할 수 있어 안내를 시작할 때 연다
import Foundation
import CoreLocation
import UIKit
#if canImport(ActivityKit)
import ActivityKit
#endif

@objc(WoopangLiveGuideObjC)
public final class WoopangLiveGuideObjC: NSObject {
    @objc public static func begin(withName name: String, lat: Double, lon: Double, arrive: Double, maxSeconds: Double) -> Bool {
        return WoopangLiveGuide.shared.begin(name: name, lat: lat, lon: lon, arriveM: arrive, maxSec: maxSeconds)
    }

    @objc public static func finishArrived(_ arrived: Bool) {
        WoopangLiveGuide.shared.finish(arrived: arrived)
    }
}

final class WoopangLiveGuide: NSObject, CLLocationManagerDelegate {
    static let shared = WoopangLiveGuide()

    private let manager = CLLocationManager()
    private var target: CLLocation?
    private var arriveM = 10.0
    private var maxSec = 1200.0
    private var startedAt = Date()
    private var firstDist = -1.0
    private var heading = -1.0
    private var lastLoc: CLLocation?
    private var nearSince: Date?
    private var lastPush = Date.distantPast
    private var sentKey = ""
    private var running = false
    private var stopTimer: Timer?
    private var activity: AnyObject?   // Activity<WoopangGuideAttributes> (iOS 16.1+)

    private override init() {
        super.init()
        // 안내 중에 앱을 완전히 끄면(앱 전환기에서 밀어 올림) 잠금 화면에 멈춘 카드가 남지 않게 — 끝나기 전 잠깐 기다려 닫는다
        NotificationCenter.default.addObserver(forName: UIApplication.willTerminateNotification, object: nil, queue: .main) { [weak self] _ in
            self?.endAllNow()
        }
    }

    private func endAllNow() {
        running = false
        stopTimer?.invalidate()
        stopTimer = nil
        stopLocation()
        activity = nil
        #if canImport(ActivityKit)
        guard #available(iOS 16.1, *) else { return }
        let all = Activity<WoopangGuideAttributes>.activities
        if all.isEmpty { return }
        let done = DispatchSemaphore(value: 0)
        Task.detached {
            for a in all {
                if #available(iOS 16.2, *) { await a.end(nil, dismissalPolicy: .immediate) }
                else { await a.end(using: nil, dismissalPolicy: .immediate) }
            }
            done.signal()
        }
        _ = done.wait(timeout: .now() + 2)
        #endif
    }

    private var canBackground: Bool {
        (Bundle.main.object(forInfoDictionaryKey: "UIBackgroundModes") as? [String])?.contains("location") ?? false
    }

    func begin(name: String, lat: Double, lon: Double, arriveM: Double, maxSec: Double) -> Bool {
        finish(arrived: false)   // 앞 안내가 남아 있으면 정리
        #if canImport(ActivityKit)
        guard #available(iOS 16.1, *) else { return false }
        guard ActivityAuthorizationInfo().areActivitiesEnabled else { return false }
        let status = manager.authorizationStatus   // 앱이 받은 위치 허락 그대로 (새로 묻지 않는다)
        guard status == .authorizedWhenInUse || status == .authorizedAlways else { return false }

        target = CLLocation(latitude: lat, longitude: lon)
        self.arriveM = arriveM > 0 ? arriveM : 10
        self.maxSec = maxSec > 0 ? maxSec : 1200
        startedAt = Date()
        firstDist = -1
        heading = -1
        nearSince = nil
        lastPush = .distantPast
        sentKey = ""
        if let l = manager.location, -l.timestamp.timeIntervalSinceNow < 30 { lastLoc = l } else { lastLoc = nil }

        let attrs = WoopangGuideAttributes(placeName: name.isEmpty ? "우팡 길안내" : name)
        let state = makeState(arrived: false)
        do {
            if #available(iOS 16.2, *) {
                activity = try Activity<WoopangGuideAttributes>.request(attributes: attrs, content: ActivityContent(state: state, staleDate: startedAt.addingTimeInterval(self.maxSec)), pushType: nil)
            } else {
                activity = try Activity<WoopangGuideAttributes>.request(attributes: attrs, contentState: state, pushType: nil)
            }
        } catch {
            activity = nil
            return false
        }
        running = true
        startLocation()
        stopTimer = Timer.scheduledTimer(withTimeInterval: self.maxSec, repeats: false) { [weak self] _ in
            self?.finish(arrived: false)
        }
        return true
        #else
        return false
        #endif
    }

    func finish(arrived: Bool) {
        let wasRunning = running
        running = false
        stopTimer?.invalidate()
        stopTimer = nil
        if wasRunning { stopLocation() }
        #if canImport(ActivityKit)
        guard #available(iOS 16.1, *) else { return }
        let current = activity as? Activity<WoopangGuideAttributes>
        activity = nil
        let last = makeState(arrived: arrived)
        // 앱이 꺼졌다 켜져 남은 것까지 — 지금 목록을 먼저 잡아 둔다 (곧이어 새로 여는 안내는 건드리지 않게)
        let leftovers = Activity<WoopangGuideAttributes>.activities.filter { $0.id != current?.id && $0.activityState == .active }
        Task {
            if let a = current {
                if arrived {
                    let until = ActivityUIDismissalPolicy.after(Date().addingTimeInterval(4))   // '도착했어요' 를 4초 보이고 닫는다
                    if #available(iOS 16.2, *) { await a.end(ActivityContent(state: last, staleDate: nil), dismissalPolicy: until) }
                    else { await a.end(using: last, dismissalPolicy: until) }
                } else {
                    if #available(iOS 16.2, *) { await a.end(nil, dismissalPolicy: .immediate) }
                    else { await a.end(using: nil, dismissalPolicy: .immediate) }
                }
            }
            for o in leftovers {
                if #available(iOS 16.2, *) { await o.end(nil, dismissalPolicy: .immediate) }
                else { await o.end(using: nil, dismissalPolicy: .immediate) }
            }
        }
        #endif
    }

    // ── 위치 · 방향 ─────────────────────────────────────────

    private func startLocation() {
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyBest
        manager.distanceFilter = 3
        manager.activityType = .fitness
        manager.pausesLocationUpdatesAutomatically = false
        if canBackground {
            manager.allowsBackgroundLocationUpdates = true
            manager.showsBackgroundLocationIndicator = true
        }
        manager.startUpdatingLocation()
        if CLLocationManager.headingAvailable() {
            manager.headingFilter = 5
            manager.startUpdatingHeading()
        }
    }

    private func stopLocation() {
        manager.stopUpdatingLocation()
        manager.stopUpdatingHeading()
        if canBackground { manager.allowsBackgroundLocationUpdates = false }
        manager.delegate = nil
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard running, let l = locations.last, l.horizontalAccuracy >= 0 else { return }
        lastLoc = l
        tick()
    }

    func locationManager(_ manager: CLLocationManager, didUpdateHeading newHeading: CLHeading) {
        guard running, newHeading.headingAccuracy >= 0 else { return }
        heading = newHeading.trueHeading >= 0 ? newHeading.trueHeading : newHeading.magneticHeading
        tick()
    }

    func locationManager(_ manager: CLLocationManager, didFailWithError error: Error) {}

    private func tick() {
        guard running, let t = target else { return }
        let now = Date()
        if now.timeIntervalSince(startedAt) >= maxSec { finish(arrived: false); return }
        guard let l = lastLoc else { return }
        let d = l.distance(from: t)
        if firstDist < 0 { firstDist = Swift.max(d, 1) }
        if d <= arriveM {
            if nearSince == nil { nearSince = now }
            if let s = nearSince, now.timeIntervalSince(s) >= 1 { finish(arrived: true); return }
        } else {
            nearSince = nil
        }
        push(now: now)
    }

    // 바뀐 것만 · 1초에 한 번까지 (거리 글자 · 화살표 10° 단위)
    private func push(now: Date) {
        #if canImport(ActivityKit)
        guard #available(iOS 16.1, *), let a = activity as? Activity<WoopangGuideAttributes> else { return }
        let s = makeState(arrived: false)
        let key = "\(s.distance)|\(s.hasAngle ? Int((s.angle / 10).rounded()) : 999)|\(Int(s.progress * 20))"
        if key == sentKey || now.timeIntervalSince(lastPush) < 1 { return }
        sentKey = key
        lastPush = now
        let stale = startedAt.addingTimeInterval(maxSec)
        Task {
            if #available(iOS 16.2, *) { await a.update(ActivityContent(state: s, staleDate: stale)) }
            else { await a.update(using: s) }
        }
        #endif
    }

    #if canImport(ActivityKit)
    @available(iOS 16.1, *)
    private func makeState(arrived: Bool) -> WoopangGuideAttributes.ContentState {
        guard let t = target, let l = lastLoc else {
            return WoopangGuideAttributes.ContentState(distance: "…", angle: 0, hasAngle: false, arrived: arrived, progress: arrived ? 1 : 0)
        }
        let d = l.distance(from: t)
        var dir = heading
        if dir < 0, l.course >= 0, l.speed > 0.7 { dir = l.course }   // 방향을 모르면 걷는 쪽으로
        let has = dir >= 0
        let angle = has ? WoopangLiveGuide.wrap(WoopangLiveGuide.bearing(from: l.coordinate, to: t.coordinate) - dir) : 0
        let p = firstDist > 0 ? Swift.min(1, Swift.max(0, 1 - d / firstDist)) : 0
        return WoopangGuideAttributes.ContentState(distance: WoopangLiveGuide.dist(d), angle: angle, hasAngle: has, arrived: arrived, progress: arrived ? 1 : p)
    }
    #endif

    // R0926Notch.Dist 와 같은 표기
    static func dist(_ m: Double) -> String {
        if m < 100 { return "\(Int(m.rounded()))m" }
        if m < 995 { return "\(Int((m / 10).rounded()) * 10)m" }
        if m < 9950 { return String(format: "%.1fkm", m / 1000) }
        return "\(Int((m / 1000).rounded()))km"
    }

    static func bearing(from a: CLLocationCoordinate2D, to b: CLLocationCoordinate2D) -> Double {
        let la1 = a.latitude * .pi / 180, la2 = b.latitude * .pi / 180, dl = (b.longitude - a.longitude) * .pi / 180
        let y = sin(dl) * cos(la2)
        let x = cos(la1) * sin(la2) - sin(la1) * cos(la2) * cos(dl)
        return atan2(y, x) * 180 / .pi
    }

    static func wrap(_ deg: Double) -> Double {
        var v = fmod(deg + 180, 360)
        if v < 0 { v += 360 }
        return v - 180
    }
}
