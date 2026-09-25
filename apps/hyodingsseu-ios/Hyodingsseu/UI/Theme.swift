import SwiftUI
import UIKit

extension UIColor {
    convenience init(rgb: UInt32) {
        self.init(red: CGFloat((rgb >> 16) & 0xFF) / 255,
                  green: CGFloat((rgb >> 8) & 0xFF) / 255,
                  blue: CGFloat(rgb & 0xFF) / 255,
                  alpha: 1)
    }
}

extension Color {
    init(light: UInt32, dark: UInt32) {
        self.init(uiColor: UIColor { trait in
            trait.userInterfaceStyle == .dark ? UIColor(rgb: dark) : UIColor(rgb: light)
        })
    }

    init(hex: String) {
        var s = hex.trimmingCharacters(in: .whitespaces)
        if s.hasPrefix("#") { s.removeFirst() }
        self.init(uiColor: UIColor(rgb: UInt32(s, radix: 16) ?? 0x8D92AE))
    }
}

/// PC 효딩쓰 창과 같은 색 — 농핑크 + 밝은 남색 (라이트·다크)
enum Palette {
    static let ground = Color(light: 0xFAF5F8, dark: 0x0D1330)
    static let surface = Color(light: 0xFFFFFF, dark: 0x141B3D)
    static let surface2 = Color(light: 0xF5EDF2, dark: 0x1A2350)
    static let line = Color(light: 0xEBDDE5, dark: 0x27315F)
    static let ink = Color(light: 0x1A2142, dark: 0xEDEFFA)
    static let ink2 = Color(light: 0x5A6184, dark: 0xA9B0D6)
    static let ink3 = Color(light: 0x8D92AE, dark: 0x7880A8)
    static let navy = Color(light: 0x2E4FCB, dark: 0x7B93FF)
    static let navySoft = Color(light: 0xE9EDFC, dark: 0x1E2A62)
    static let onNavy = Color(light: 0xFFFFFF, dark: 0x0D1330)
    static let bar = Color(light: 0x1B2A6B, dark: 0x0A0F27)
    static let barInk = Color(light: 0xF3F4FF, dark: 0xEDEFFA)
    static let barInk2 = Color(light: 0xAEB8E8, dark: 0x8F99CC)
    static let pink = Color(light: 0xE91E63, dark: 0xFF5C93)
    static let pinkDeep = Color(light: 0xC2185B, dark: 0xFF8AB0)
    static let pinkSoft = Color(light: 0xFFE8F0, dark: 0x35162F)
    static let pinkLine = Color(light: 0xF7B8CE, dark: 0x6B2A4F)
    static let ok = Color(light: 0x1F8A5B, dark: 0x5ED3A0)
}

struct PrimaryButtonStyle: ButtonStyle {
    var wide = false
    @Environment(\.isEnabled) var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(Palette.onNavy)
            .padding(.horizontal, 16)
            .padding(.vertical, 10)
            .frame(maxWidth: wide ? .infinity : nil)
            .background(Palette.navy.opacity(configuration.isPressed ? 0.8 : 1))
            .clipShape(RoundedRectangle(cornerRadius: 11, style: .continuous))
            .opacity(isEnabled ? 1 : 0.45)
    }
}

struct GhostButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(Palette.ink)
            .padding(.horizontal, 14)
            .padding(.vertical, 9)
            .overlay(RoundedRectangle(cornerRadius: 11, style: .continuous)
                .stroke(configuration.isPressed ? Palette.navy : Palette.line, lineWidth: 1.5))
    }
}

struct NoButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline.weight(.semibold))
            .foregroundStyle(Palette.pinkDeep)
            .padding(.horizontal, 14)
            .padding(.vertical, 9)
            .background(configuration.isPressed ? Palette.pinkSoft : Color.clear)
            .clipShape(RoundedRectangle(cornerRadius: 11, style: .continuous))
    }
}

struct ChipStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.footnote)
            .foregroundStyle(configuration.isPressed ? Palette.pinkDeep : Palette.ink2)
            .padding(.horizontal, 12)
            .padding(.vertical, 6)
            .background(Palette.ground)
            .overlay(Capsule().stroke(configuration.isPressed ? Palette.pink : Palette.line, lineWidth: 1))
            .clipShape(Capsule())
    }
}

struct SuggestionStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.subheadline)
            .foregroundStyle(configuration.isPressed ? Palette.pinkDeep : Palette.ink)
            .padding(.horizontal, 14)
            .padding(.vertical, 11)
            .background(Palette.surface)
            .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous)
                .stroke(configuration.isPressed ? Palette.pink : Palette.line, lineWidth: 1.5))
            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}

/// 직원 얼굴 — 색 동그라미 + 머리글자, 일하는 중이면 분홍 고리가 번진다.
struct WorkerAvatar: View {
    let worker: Worker
    var size: CGFloat = 32

    var body: some View {
        ZStack {
            Circle().fill(Color(hex: worker.colorHex))
            Text(worker.initial)
                .font(.system(size: size * 0.4, weight: .bold))
                .foregroundStyle(.white)
            if worker.busy { BusyRing(size: size) }
        }
        .frame(width: size, height: size)
    }
}

struct BusyRing: View {
    let size: CGFloat
    @State var on = false

    var body: some View {
        Circle()
            .stroke(Palette.pink, lineWidth: 2)
            .frame(width: size + 6, height: size + 6)
            .scaleEffect(on ? 1.25 : 0.95)
            .opacity(on ? 0 : 0.9)
            .animation(.easeOut(duration: 1.6).repeatForever(autoreverses: false), value: on)
            .onAppear { on = true }
    }
}
