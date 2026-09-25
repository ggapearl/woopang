import SwiftUI

/// 효딩쓰 구슬 — PC 창과 같은 상태 표현.
/// idle 숨쉬기 · thinking/working 도는 고리 · waiting 깜빡이는 고리 · recording 목소리 크기 · speaking 말하기 · offline 회색
struct OrbView: View {
    var state: String
    var size: CGFloat = 30
    var level: Double = 0
    @State var breathe = false

    var body: some View {
        ZStack {
            Circle()
                .fill(RadialGradient(colors: [Color(hex: "#FFB3CD"), Color(hex: "#F2487F"),
                                              Color(hex: "#C2185B"), Color(hex: "#8E0F42")],
                                     center: UnitPoint(x: 0.34, y: 0.3), startRadius: 0, endRadius: size * 0.72))
                .frame(width: size, height: size)
                .shadow(color: Color(hex: "#FF5C93").opacity(0.3 + level * 0.55), radius: 3 + level * 14)
                .scaleEffect(scale)
                .animation(.easeInOut(duration: state == "speaking" ? 0.42 : 2.1).repeatForever(autoreverses: true),
                           value: breathe)
                .animation(.easeOut(duration: 0.1), value: level)
            if state == "thinking" || state == "working" {
                SpinRing(size: size + 8)
            } else if state == "waiting" {
                BlinkRing(size: size + 8)
            } else if state == "recording" {
                Circle()
                    .stroke(Color(hex: "#FF7AA8").opacity(0.7), lineWidth: 2)
                    .frame(width: size + 8, height: size + 8)
                    .scaleEffect(1 + level * 0.3)
                    .animation(.easeOut(duration: 0.1), value: level)
            }
        }
        .frame(width: size + 12, height: size + 12)
        .saturation(state == "offline" ? 0 : 1)
        .opacity(state == "offline" ? 0.55 : 1)
        .onAppear { breathe = true }
        .accessibilityHidden(true)
    }

    private var scale: CGFloat {
        switch state {
        case "recording": return 1 + level * 0.1
        case "speaking": return breathe ? 1.07 : 0.97
        case "idle": return breathe ? 1.04 : 1
        default: return 1
        }
    }
}

private struct SpinRing: View {
    let size: CGFloat
    @State var turn = false

    var body: some View {
        Circle()
            .trim(from: 0, to: 0.38)
            .stroke(Color(hex: "#9FB2FF"), style: StrokeStyle(lineWidth: 2.5, lineCap: .round))
            .frame(width: size, height: size)
            .rotationEffect(.degrees(turn ? 360 : 0))
            .animation(.linear(duration: 1).repeatForever(autoreverses: false), value: turn)
            .onAppear { turn = true }
    }
}

private struct BlinkRing: View {
    let size: CGFloat
    @State var dim = false

    var body: some View {
        Circle()
            .stroke(Color(hex: "#FF7AA8"), lineWidth: 2)
            .frame(width: size, height: size)
            .opacity(dim ? 0.2 : 1)
            .animation(.easeInOut(duration: 0.55).repeatForever(autoreverses: true), value: dim)
            .onAppear { dim = true }
    }
}
