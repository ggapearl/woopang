import SwiftUI

/// AI Office 직원 한 명과의 대화 — PC 의 직원 작은 창과 같은 기록.
struct WorkerChatView: View {
    @EnvironmentObject var store: DeskStore
    let worker: Worker
    @State var messages: [OfficeMessage] = []
    @State var draft = ""
    @State var waiting = false
    @State var loading = true
    @FocusState var focused: Bool

    var body: some View {
        VStack(spacing: 0) {
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 10) {
                        header
                        if loading {
                            ProgressView().frame(maxWidth: .infinity).padding()
                        } else if messages.isEmpty {
                            Text("아직 나눈 이야기가 없어요. 아래에 지시를 적어 보세요.")
                                .font(.footnote)
                                .foregroundStyle(Palette.ink3)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 20)
                        }
                        ForEach(messages) { m in
                            OfficeBubble(message: m, worker: worker)
                        }
                        if waiting { TypingRow(worker: worker) }
                        Color.clear.frame(height: 1).id("bottom")
                    }
                    .padding(16)
                }
                .scrollDismissesKeyboard(.interactively)
                .refreshable { await load() }
                .onChange(of: messages.count) { _, _ in
                    withAnimation { proxy.scrollTo("bottom", anchor: .bottom) }
                }
                .onChange(of: waiting) { _, _ in
                    withAnimation { proxy.scrollTo("bottom", anchor: .bottom) }
                }
            }
            composer
        }
        .background(Palette.ground.ignoresSafeArea())
        .navigationTitle(worker.name)
        .navigationBarTitleDisplayMode(.inline)
        .toolbarBackground(Palette.bar, for: .navigationBar)
        .toolbarBackground(.visible, for: .navigationBar)
        .toolbarColorScheme(.dark, for: .navigationBar)
        .task { await load() }
    }

    private var header: some View {
        HStack(spacing: 12) {
            WorkerAvatar(worker: worker, size: 46)
            VStack(alignment: .leading, spacing: 2) {
                Text(worker.role.isEmpty ? worker.name : "\(worker.name) · \(worker.role)")
                    .font(.headline)
                    .foregroundStyle(Palette.ink)
                Text(worker.team)
                    .font(.caption)
                    .foregroundStyle(Palette.ink3)
                if !worker.task.isEmpty {
                    Text("지금: " + worker.task)
                        .font(.caption)
                        .foregroundStyle(Palette.ink2)
                        .lineLimit(2)
                }
            }
        }
        .padding(.bottom, 6)
    }

    private var composer: some View {
        HStack(alignment: .bottom, spacing: 8) {
            TextField("\(worker.name) 님에게 지시", text: $draft, axis: .vertical)
                .lineLimit(1...5)
                .focused($focused)
                .foregroundStyle(Palette.ink)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(Palette.ground)
                .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .stroke(focused ? Palette.navy : Palette.line, lineWidth: 1.5))
                .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))
            Button(action: send) {
                Image(systemName: "arrow.up")
                    .font(.system(size: 17, weight: .bold))
                    .foregroundStyle(Palette.onNavy)
                    .frame(width: 44, height: 44)
                    .background(Circle().fill(Palette.navy))
            }
            .disabled(waiting || draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            .opacity(waiting || draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? 0.45 : 1)
            .accessibilityLabel("보내기")
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .background(Palette.surface.ignoresSafeArea(edges: .bottom))
        .overlay(alignment: .top) { Rectangle().fill(Palette.line).frame(height: 1) }
    }

    private func load() async {
        do {
            messages = try await store.officeHistory(worker.id)
        } catch {
            store.showToast(error.localizedDescription)
        }
        loading = false
    }

    /// 지시를 보내고, 직원 답이 올 때까지 3초마다 기록을 다시 본다 (최대 4분).
    private func send() {
        let t = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty, !waiting else { return }
        draft = ""
        let lastReply = messages.last(where: { !$0.mine })?.content
        messages.append(OfficeMessage(sender: "ceo", name: "", content: t, time: ""))
        waiting = true
        Task {
            do {
                try await store.officeChat(worker.id, t)
                for _ in 0..<80 {
                    try await Task.sleep(nanoseconds: 3_000_000_000)
                    let list = try await store.officeHistory(worker.id)
                    if let last = list.last, !last.mine, last.content != lastReply {
                        messages = list
                        waiting = false
                        return
                    }
                }
                waiting = false
                store.showToast("\(worker.name) 님이 아직 답하지 않았어요. 나중에 다시 열어 보세요.")
            } catch {
                waiting = false
                store.showToast(error.localizedDescription)
            }
        }
    }
}

struct OfficeBubble: View {
    let message: OfficeMessage
    let worker: Worker

    var body: some View {
        if message.mine {
            HStack {
                Spacer(minLength: 44)
                Text(message.content)
                    .textSelection(.enabled)
                    .foregroundStyle(Palette.onNavy)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 9)
                    .background(Palette.navy)
                    .clipShape(UnevenRoundedRectangle(topLeadingRadius: 18, bottomLeadingRadius: 18,
                                                      bottomTrailingRadius: 6, topTrailingRadius: 18, style: .continuous))
            }
        } else {
            HStack(alignment: .top, spacing: 8) {
                WorkerAvatar(worker: worker, size: 28)
                VStack(alignment: .leading, spacing: 3) {
                    HStack(spacing: 6) {
                        Text(message.name.isEmpty ? worker.name : message.name)
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(Palette.ink2)
                        if !message.time.isEmpty {
                            Text(message.time).font(.caption2).foregroundStyle(Palette.ink3).monospacedDigit()
                        }
                    }
                    MarkdownText(text: message.content)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 9)
                        .background(Palette.surface)
                        .overlay(UnevenRoundedRectangle(topLeadingRadius: 6, bottomLeadingRadius: 16,
                                                        bottomTrailingRadius: 16, topTrailingRadius: 16, style: .continuous)
                            .stroke(Palette.line, lineWidth: 1))
                        .clipShape(UnevenRoundedRectangle(topLeadingRadius: 6, bottomLeadingRadius: 16,
                                                          bottomTrailingRadius: 16, topTrailingRadius: 16, style: .continuous))
                }
                Spacer(minLength: 24)
            }
        }
    }
}

struct TypingRow: View {
    let worker: Worker

    var body: some View {
        HStack(spacing: 8) {
            WorkerAvatar(worker: worker, size: 28)
            ProgressView().controlSize(.small)
            Text("\(worker.name) 님이 답을 쓰는 중")
                .font(.footnote)
                .foregroundStyle(Palette.ink3)
        }
    }
}
