import SwiftUI

/// ☰ — 대화 상대 고르기: 효딩쓰 · AI Office 직원 15명 · 설정.
struct DrawerView: View {
    @EnvironmentObject var store: DeskStore
    let onPick: (Route?) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                Text("대화 상대")
                    .font(.title3.weight(.bold))
                    .foregroundStyle(Palette.ink)
                Spacer()
                Button { onPick(nil) } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(Palette.ink2)
                        .frame(width: 40, height: 40)
                }
                .accessibilityLabel("닫기")
            }
            .padding(.leading, 20)
            .padding(.trailing, 8)
            .padding(.top, 8)

            ScrollView {
                VStack(alignment: .leading, spacing: 2) {
                    DrawerRow(title: store.name, subtitle: "비서실장 · 지금 이 대화", current: true) {
                        OrbView(state: "idle", size: 26)
                    } action: { onPick(nil) }

                    SectionLabel(text: "AI OFFICE 직원")
                    if store.workers.isEmpty {
                        Text("명단을 불러오는 중이거나 AI Office 가 꺼져 있어요.")
                            .font(.footnote)
                            .foregroundStyle(Palette.ink3)
                            .padding(.horizontal, 12)
                            .padding(.vertical, 6)
                    }
                    ForEach(store.workers) { w in
                        DrawerRow(title: w.role.isEmpty ? w.name : "\(w.name) · \(w.role)", subtitle: w.statusLine, current: false) {
                            WorkerAvatar(worker: w, size: 32)
                        } action: { onPick(.worker(w)) }
                    }

                    SectionLabel(text: "더 보기")
                    DrawerRow(title: "설정", subtitle: "목소리 · 말 빠르기 · 연결", current: false) {
                        IconBadge(system: "gearshape.fill")
                    } action: { onPick(.settings) }
                    DrawerRow(title: "새 대화", subtitle: "효딩쓰와 처음부터 다시", current: false) {
                        IconBadge(system: "square.and.pencil")
                    } action: {
                        store.newChat()
                        onPick(nil)
                    }
                }
                .padding(.horizontal, 8)
                .padding(.bottom, 20)
            }

            HStack(spacing: 6) {
                Circle()
                    .fill(store.offlineMessage == nil ? Palette.ok : Palette.pink)
                    .frame(width: 7, height: 7)
                Text(store.offlineMessage ?? store.hostLabel)
                    .font(.caption)
                    .foregroundStyle(Palette.ink3)
                    .lineLimit(1)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 12)
        }
        .frame(width: 308)
        .frame(maxHeight: .infinity)
        .background(Palette.surface.ignoresSafeArea())
        .task { await store.refreshWorkers() }
    }
}

struct DrawerRow<Avatar: View>: View {
    let title: String
    let subtitle: String
    let current: Bool
    @ViewBuilder let avatar: () -> Avatar
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 11) {
                avatar()
                    .frame(width: 38, height: 38)
                VStack(alignment: .leading, spacing: 1) {
                    Text(title)
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(Palette.ink)
                        .lineLimit(1)
                    if !subtitle.isEmpty {
                        Text(subtitle)
                            .font(.caption)
                            .foregroundStyle(Palette.ink3)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: 0)
                if !current {
                    Image(systemName: "chevron.right")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(Palette.ink3)
                }
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(current ? Palette.pinkSoft : Color.clear)
            .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

struct SectionLabel: View {
    let text: String

    var body: some View {
        Text(text)
            .font(.caption2.weight(.bold))
            .tracking(0.8)
            .foregroundStyle(Palette.ink3)
            .padding(.horizontal, 12)
            .padding(.top, 16)
            .padding(.bottom, 4)
    }
}

struct IconBadge: View {
    let system: String

    var body: some View {
        Image(systemName: system)
            .font(.system(size: 14, weight: .semibold))
            .foregroundStyle(Palette.navy)
            .frame(width: 32, height: 32)
            .background(Circle().fill(Palette.navySoft))
    }
}
