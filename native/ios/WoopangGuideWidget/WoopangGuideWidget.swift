// 우팡 길안내 실시간 현황(Live Activity) — 아일랜드 · 잠금 화면 모양 (11-26 대표님 10번 ② · 시안 C:\woopang-app\mockups\우팡_노치_시안_1124.html)
// · 유니티 바깥(native/ios)에 둔다 — 빌드 후처리 LiveGuidePostProcess 가 Xcode 프로젝트에 위젯 확장(WoopangGuideWidget)을 만들고 이 폴더를 넣는다.
// · 데이터 모양(WoopangGuideAttributes)은 Assets/Plugins/iOS/WoopangGuideAttributes.swift 를 앱과 같이 쓴다.
// · 위젯 확장의 최소 iOS 는 16.1 (실시간 현황이 16.1 부터) — 앱은 그대로 15.0, 그 아래 기기는 이 확장을 읽지 않는다.
// · 누르면 앱이 열린다 (아이폰 기본 동작).
import ActivityKit
import SwiftUI
import WidgetKit

@main
struct WoopangGuideWidgetBundle: WidgetBundle {
    var body: some Widget {
        WoopangGuideLiveActivity()
    }
}

struct WoopangGuideLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: WoopangGuideAttributes.self) { context in
            LockCard(name: context.attributes.placeName, state: context.state)
                .activityBackgroundTint(GuideColor.card)
                .activitySystemActionForegroundColor(.white)
        } dynamicIsland: { context in
            let s = context.state
            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    DistanceText(state: s, size: 22)
                        .padding(.leading, 6)
                        .frame(maxHeight: .infinity, alignment: .center)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    GuideArrow(state: s, size: 24)
                        .padding(.trailing, 8)
                        .frame(maxHeight: .infinity, alignment: .center)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: 8) {
                            Mark(size: 20)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(context.attributes.placeName)
                                    .font(.system(size: 15, weight: .bold))
                                    .foregroundColor(.white)
                                    .lineLimit(1)
                                Text(s.arrived ? "도착했어요" : "우팡 길안내 · 누르면 앱으로")
                                    .font(.system(size: 11.5, weight: .medium))
                                    .foregroundColor(GuideColor.sub)
                                    .lineLimit(1)
                            }
                            Spacer(minLength: 0)
                        }
                        GuideBar(value: s.arrived ? 1 : s.progress)
                    }
                    .padding(.horizontal, 6)
                    .padding(.bottom, 4)
                }
            } compactLeading: {
                DistanceText(state: s, size: 13.5)
                    .padding(.leading, 2)
            } compactTrailing: {
                GuideArrow(state: s, size: 14)
                    .padding(.trailing, 2)
            } minimal: {
                GuideArrow(state: s, size: 13)
            }
            .keylineTint(GuideColor.arrow)
        }
    }
}

// 시안 색 — 카드 #0d1117 · 86% / 마크 #e95383 / 화살표 #ff5c93 / 보조 글자 #9aa7b2
enum GuideColor {
    static let card = Color(red: 13 / 255, green: 17 / 255, blue: 23 / 255).opacity(0.86)
    static let mark = Color(red: 233 / 255, green: 83 / 255, blue: 131 / 255)
    static let arrow = Color(red: 1, green: 92 / 255, blue: 147 / 255)
    static let sub = Color(red: 154 / 255, green: 167 / 255, blue: 178 / 255)
    static let done = Color(red: 0.40, green: 0.86, blue: 0.55)
}

// 잠금 화면 카드 — 마크 · 장소 이름 · 안내 / 화살표 · 남은 거리 / 진행 막대
struct LockCard: View {
    let name: String
    let state: WoopangGuideAttributes.ContentState

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(alignment: .center, spacing: 10) {
                Mark(size: 26)
                VStack(alignment: .leading, spacing: 2) {
                    Text(name)
                        .font(.system(size: 15, weight: .bold))
                        .foregroundColor(.white)
                        .lineLimit(1)
                    Text(state.arrived ? "도착했어요" : "우팡 길안내 · 누르면 앱으로")
                        .font(.system(size: 11.5, weight: .medium))
                        .foregroundColor(GuideColor.sub)
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                GuideArrow(state: state, size: 15)
                DistanceText(state: state, size: 22)
            }
            GuideBar(value: state.arrived ? 1 : state.progress)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 16)
    }
}

// 「우」 마크 — 26pt · 모서리 7
struct Mark: View {
    let size: CGFloat

    var body: some View {
        RoundedRectangle(cornerRadius: size * 7 / 26, style: .continuous)
            .fill(GuideColor.mark)
            .frame(width: size, height: size)
            .overlay(
                Text("우")
                    .font(.system(size: size * 14 / 26, weight: .bold))
                    .foregroundColor(.white)
            )
    }
}

struct DistanceText: View {
    let state: WoopangGuideAttributes.ContentState
    let size: CGFloat

    var body: some View {
        Text(state.arrived ? "도착" : state.distance)
            .font(.system(size: size, weight: .bold))
            .monospacedDigit()
            .foregroundColor(.white)
            .lineLimit(1)
            .minimumScaleFactor(0.7)
    }
}

// 앱 안 노치와 같은 길안내 화살표 (휴대폰 위쪽 기준 시계 방향) · 도착하면 초록 체크
struct GuideArrow: View {
    let state: WoopangGuideAttributes.ContentState
    let size: CGFloat

    var body: some View {
        if state.arrived {
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: size, weight: .bold))
                .foregroundColor(GuideColor.done)
        } else {
            ArrowShape()
                .fill(GuideColor.arrow)
                .frame(width: size * 0.857, height: size)
                .rotationEffect(.degrees(state.hasAngle ? state.angle : 0))
                .opacity(state.hasAngle ? 1 : 0.45)
                .frame(width: size * 1.15, height: size * 1.15)
        }
    }
}

// 시안의 화살표 — 끝이 위, 뒤쪽 가운데가 파인 모양
struct ArrowShape: Shape {
    func path(in r: CGRect) -> Path {
        var p = Path()
        p.move(to: CGPoint(x: r.midX, y: r.minY))
        p.addLine(to: CGPoint(x: r.maxX, y: r.maxY))
        p.addLine(to: CGPoint(x: r.midX, y: r.minY + r.height * 0.78))
        p.addLine(to: CGPoint(x: r.minX, y: r.maxY))
        p.closeSubpath()
        return p
    }
}

// 진행 막대 — 6pt · 바탕 흰색 14% · 채움 #e95383
struct GuideBar: View {
    let value: Double

    var body: some View {
        GeometryReader { g in
            let v = CGFloat(Swift.min(1, Swift.max(0, value)))
            ZStack(alignment: .leading) {
                Capsule().fill(Color.white.opacity(0.14))
                Capsule()
                    .fill(GuideColor.mark)
                    .frame(width: v <= 0 ? 0 : Swift.max(6, g.size.width * v))
            }
        }
        .frame(height: 6)
    }
}
