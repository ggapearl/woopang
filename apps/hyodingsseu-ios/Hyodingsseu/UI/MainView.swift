import SwiftUI

enum Route: Hashable {
    case worker(Worker)
    case settings
}

/// 첫 화면 — 효딩쓰와의 대화. ☰ 로 AI Office 직원·설정을 고른다.
struct MainView: View {
    @EnvironmentObject var store: DeskStore
    @State var drawerOpen = false
    @State var path: [Route] = []

    var body: some View {
        NavigationStack(path: $path) {
            ZStack(alignment: .leading) {
                VStack(spacing: 0) {
                    TopBar(recorder: store.recorder, speaker: store.speaker) { setDrawer(true) }
                    LinkBanner()
                    ChatList()
                    Composer(recorder: store.recorder)
                }
                .background(Palette.ground.ignoresSafeArea())

                if drawerOpen {
                    Color.black.opacity(0.36)
                        .ignoresSafeArea()
                        .onTapGesture { setDrawer(false) }
                        .transition(.opacity)
                    DrawerView { route in
                        setDrawer(false)
                        if let route { path.append(route) }
                    }
                    .transition(.move(edge: .leading))
                    .gesture(DragGesture(minimumDistance: 20).onEnded { v in
                        if v.translation.width < -60 { setDrawer(false) }
                    })
                }
            }
            .overlay(alignment: .top) { ToastView() }
            .toolbar(.hidden, for: .navigationBar)
            .navigationDestination(for: Route.self) { route in
                switch route {
                case .worker(let w): WorkerChatView(worker: w)
                case .settings: SettingsView()
                }
            }
        }
        .tint(Palette.navy)
    }

    private func setDrawer(_ open: Bool) {
        withAnimation(.easeOut(duration: 0.22)) { drawerOpen = open }
    }
}

// MARK: - 제목줄

struct TopBar: View {
    @EnvironmentObject var store: DeskStore
    @ObservedObject var recorder: Recorder
    @ObservedObject var speaker: Speaker
    let onMenu: () -> Void

    var body: some View {
        HStack(spacing: 6) {
            Button(action: onMenu) {
                Image(systemName: "line.3.horizontal")
                    .font(.system(size: 19, weight: .semibold))
                    .frame(width: 44, height: 44)
            }
            .foregroundStyle(Palette.barInk)
            .accessibilityLabel("메뉴 — 대화 상대 고르기")

            OrbView(state: orbState, size: 26, level: recorder.level)

            VStack(alignment: .leading, spacing: 1) {
                Text(store.name)
                    .font(.headline)
                    .foregroundStyle(Palette.barInk)
                Text(statusText)
                    .font(.caption)
                    .foregroundStyle(Palette.barInk2)
                    .lineLimit(1)
            }
            Spacer(minLength: 4)

            Menu {
                Picker("생각의 깊이", selection: Binding(get: { store.model }, set: { store.setModel($0) })) {
                    ForEach(store.models) { m in
                        Text(m.label).tag(m.key)
                    }
                }
            } label: {
                Text(store.models.first { $0.key == store.model }?.label ?? "보통")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Palette.barInk)
                    .padding(.horizontal, 11)
                    .padding(.vertical, 6)
                    .background(Color.white.opacity(0.13))
                    .clipShape(Capsule())
            }
            .accessibilityLabel("생각의 깊이")
        }
        .padding(.leading, 4)
        .padding(.trailing, 12)
        .frame(height: 58)
        .background(BarBackground())
    }

    private var orbState: String {
        if recorder.recording { return "recording" }
        if speaker.speaking { return "speaking" }
        if store.offlineMessage != nil { return "offline" }
        return store.agentState
    }

    private var statusText: String {
        if recorder.recording { return "듣고 있어요 — 다시 누르면 보내요" }
        if store.uploadingVoice { return "받아적는 중" }
        if speaker.speaking { return "말하는 중" }
        switch store.agentState {
        case "thinking": return "생각하는 중"
        case "working": return "일하는 중"
        case "waiting": return "대표님 허락을 기다려요"
        default: break
        }
        switch store.link {
        case .online: return store.host == "brain" ? "PC 뒤의 두뇌와 연결됨" : "PC 창과 연결됨"
        case .connecting: return "연결하는 중"
        case .offline: return "PC 에 닿지 않아요"
        }
    }
}

struct BarBackground: View {
    var body: some View {
        ZStack {
            Palette.bar
            RadialGradient(colors: [Color(hex: "#E91E63").opacity(0.42), .clear],
                           center: UnitPoint(x: 0.18, y: 0.5), startRadius: 0, endRadius: 170)
            RadialGradient(colors: [Color(hex: "#5C7CFF").opacity(0.38), .clear],
                           center: UnitPoint(x: 0.82, y: 0.35), startRadius: 0, endRadius: 190)
        }
    }
}

struct LinkBanner: View {
    @EnvironmentObject var store: DeskStore

    var body: some View {
        if let msg = store.offlineMessage {
            HStack(spacing: 8) {
                ProgressView().controlSize(.small).tint(Palette.pinkDeep)
                Text(msg + " · 다시 잇는 중").font(.footnote)
                Spacer(minLength: 0)
            }
            .foregroundStyle(Palette.pinkDeep)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .background(Palette.pinkSoft)
        }
    }
}

// MARK: - 대화

struct ChatList: View {
    @EnvironmentObject var store: DeskStore

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 12) {
                    if store.items.isEmpty { HelloView() }
                    ForEach(store.items) { item in
                        ChatItemView(item: item).id(item.id)
                    }
                    Color.clear.frame(height: 1).id("bottom")
                }
                .padding(.horizontal, 16)
                .padding(.vertical, 14)
            }
            .scrollDismissesKeyboard(.interactively)
            .refreshable { await store.reload() }
            .onChange(of: store.revision) { _, _ in
                withAnimation(.easeOut(duration: 0.2)) { proxy.scrollTo("bottom", anchor: .bottom) }
            }
            .onAppear { proxy.scrollTo("bottom", anchor: .bottom) }
        }
    }
}

struct HelloView: View {
    @EnvironmentObject var store: DeskStore
    let suggestions = ["서버 상태 어때?", "AI Office 직원들 지금 뭐 하고 있어?",
                               "농민닷컴 오늘 주문 현황 알려줘", "오늘 뭐부터 하면 좋을까?"]

    var body: some View {
        VStack(spacing: 12) {
            OrbView(state: "idle", size: 68).padding(.top, 36)
            Text("대표님, 부르시면 바로 옵니다")
                .font(.title3.weight(.bold))
                .foregroundStyle(Palette.ink)
            Text("아래에 적거나 마이크를 눌러 말씀하세요.\nPC 의 효딩쓰와 같은 대화예요.")
                .font(.subheadline)
                .foregroundStyle(Palette.ink2)
                .multilineTextAlignment(.center)
            VStack(spacing: 8) {
                ForEach(suggestions, id: \.self) { s in
                    Button { store.send(s) } label: {
                        Text(s).frame(maxWidth: .infinity, alignment: .leading)
                    }
                    .buttonStyle(SuggestionStyle())
                }
            }
            .padding(.top, 8)
        }
        .frame(maxWidth: .infinity)
    }
}

// MARK: - 입력

struct Composer: View {
    @EnvironmentObject var store: DeskStore
    @ObservedObject var recorder: Recorder
    @State var draft = ""
    @FocusState var focused: Bool

    var body: some View {
        VStack(spacing: 8) {
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: 6) {
                    ForEach(QuickChip.all) { chip in
                        Button(chip.title) { store.send(chip.say) }
                            .buttonStyle(ChipStyle())
                    }
                }
                .padding(.horizontal, 12)
            }
            HStack(alignment: .bottom, spacing: 8) {
                TextField("무엇이든 시키세요", text: $draft, axis: .vertical)
                    .lineLimit(1...5)
                    .focused($focused)
                    .foregroundStyle(Palette.ink)
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(Palette.ground)
                    .overlay(RoundedRectangle(cornerRadius: 20, style: .continuous)
                        .stroke(focused ? Palette.navy : Palette.line, lineWidth: 1.5))
                    .clipShape(RoundedRectangle(cornerRadius: 20, style: .continuous))
                MicButton(recorder: recorder)
                sendButton
            }
            .padding(.horizontal, 12)
        }
        .padding(.vertical, 8)
        .background(Palette.surface.ignoresSafeArea(edges: .bottom))
        .overlay(alignment: .top) { Rectangle().fill(Palette.line).frame(height: 1) }
    }

    private var empty: Bool { draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    private var stopMode: Bool { store.busy && empty }

    private var sendButton: some View {
        Button {
            if stopMode {
                store.stop()
            } else {
                store.send(draft)
                draft = ""
            }
        } label: {
            Image(systemName: stopMode ? "stop.fill" : "arrow.up")
                .font(.system(size: 17, weight: .bold))
                .foregroundStyle(stopMode ? Color.white : Palette.onNavy)
                .frame(width: 44, height: 44)
                .background(Circle().fill(stopMode ? Palette.pink : Palette.navy))
        }
        .disabled(!stopMode && empty)
        .opacity(!stopMode && empty ? 0.45 : 1)
        .accessibilityLabel(stopMode ? "멈추기" : "보내기")
    }
}

struct MicButton: View {
    @EnvironmentObject var store: DeskStore
    @ObservedObject var recorder: Recorder

    var body: some View {
        Button { store.toggleRecording() } label: {
            ZStack {
                Circle().fill(recorder.recording ? Palette.pink : Palette.surface2)
                if recorder.recording {
                    Circle()
                        .stroke(Palette.pink.opacity(0.35), lineWidth: 6)
                        .scaleEffect(1 + recorder.level * 0.35)
                }
                if store.uploadingVoice {
                    ProgressView().tint(Palette.pink)
                } else {
                    Image(systemName: recorder.recording ? "stop.fill" : "mic.fill")
                        .font(.system(size: 17, weight: .semibold))
                        .foregroundStyle(recorder.recording ? Color.white : Palette.ink2)
                }
            }
            .frame(width: 44, height: 44)
            .animation(.easeOut(duration: 0.1), value: recorder.level)
        }
        .disabled(store.uploadingVoice)
        .accessibilityLabel(recorder.recording ? "녹음 끝내고 보내기" : "말로 시키기")
    }
}

struct ToastView: View {
    @EnvironmentObject var store: DeskStore

    var body: some View {
        if let t = store.toast {
            Text(t)
                .font(.footnote.weight(.medium))
                .foregroundStyle(Palette.ink)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(Palette.surface)
                .clipShape(Capsule())
                .overlay(Capsule().stroke(Palette.pinkLine, lineWidth: 1))
                .shadow(color: Color.black.opacity(0.12), radius: 10, y: 4)
                .padding(.top, 64)
                .padding(.horizontal, 20)
                .transition(.move(edge: .top).combined(with: .opacity))
        }
    }
}
