// 우팡 길안내 실시간 현황(Live Activity)의 모양 — 앱(UnityFramework)과 위젯 확장(WoopangGuideWidget)이 같이 쓴다.
// 11-26 대표님 10번 「위에 노치관련 수정안 확인했어. 1,2번 둘다 진행해」 ② — 홈으로 나가도 아일랜드 · 잠금 화면에 남은 거리 · 방향.
// 이 파일 하나를 두 타깃이 같이 컴파일한다 (LiveGuidePostProcess 가 위젯 타깃에도 넣는다) — 고치면 둘 다 바뀐다.
import Foundation
#if canImport(ActivityKit)
import ActivityKit

@available(iOS 16.1, *)
public struct WoopangGuideAttributes: ActivityAttributes {
    public struct ContentState: Codable, Hashable {
        public var distance: String   // "350m" · "1.2km" — 앱 안 노치와 같은 표기
        public var angle: Double      // 화살표 — 휴대폰 위쪽 기준 시계 방향(도)
        public var hasAngle: Bool     // 방향을 모르면 화살표를 흐리게
        public var arrived: Bool
        public var progress: Double   // 0 ~ 1 — 처음 거리에서 얼마나 왔나 (잠금 화면 막대)

        public init(distance: String, angle: Double, hasAngle: Bool, arrived: Bool, progress: Double) {
            self.distance = distance
            self.angle = angle
            self.hasAngle = hasAngle
            self.arrived = arrived
            self.progress = progress
        }
    }

    public var placeName: String

    public init(placeName: String) {
        self.placeName = placeName
    }
}
#endif
