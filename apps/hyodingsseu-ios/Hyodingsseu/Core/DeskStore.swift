import SwiftUI
import UIKit

/// PC 효딩쓰와 같은 대화를 들고 있는다.
/// state 로 지금까지를 받고, events?since= 긴 폴링으로 이어 받는다 (PC 창의 ui.html 과 같은 사건).
@MainActor
final class DeskStore: ObservableObject {
    enum Link: Equatable { case connecting, online, offline(String) }

    @Published private(set) var items: [ChatItem] = []
    @Published private(set) var revision = 0
    @Published private(set) var agentState = "idle"
    @Published private(set) var link: Link = .connecting
    @Published private(set) var name = "효딩쓰"
    @Published private(set) var host = "gui"
    @Published private(set) var model = "normal"
    @Published private(set) var models = ModelOption.defaults
    @Published private(set) var workers: [Worker] = []
    @Published private(set) var uploadingVoice = false
    @Published private(set) var toast: String?
    @Published private(set) var isPaired = false
    @Published var pairMessage: String?
    @Published var speed: Double = 1.2
    @Published var replyVoice: ReplyVoice = .voiceOnly {
        didSet { defaults.set(replyVoice.rawValue, forKey: "replyVoice") }
    }
    @Published var voiceEngine: VoiceEngine = .iphone {
        didSet { defaults.set(voiceEngine.rawValue, forKey: "voiceEngine") }
    }

    let api = DeskAPI()
    let recorder = Recorder()
    let speaker = Speaker()

    private let defaults = UserDefaults.standard
    private var boot = ""
    private var seq = 0
    private var pollTask: Task<Void, Never>?
    private var openAI: [UUID] = []
    private var toolOwner: [String: UUID] = [:]
    private var cardOwner: [String: UUID] = [:]
    private var lastAI: UUID?
    private var lastAIText = ""
    private var lastUserOrigin = ""
    private var speakNext = false
    private var replaying = false

    init() {
        isPaired = api.token != nil
        replyVoice = ReplyVoice(rawValue: defaults.string(forKey: "replyVoice") ?? "") ?? .voiceOnly
        voiceEngine = VoiceEngine(rawValue: defaults.string(forKey: "voiceEngine") ?? "") ?? .iphone
        let sp = defaults.double(forKey: "speed")
        if sp > 0 { speed = sp }
        recorder.onAutoStop = { [weak self] in self?.finishRecording() }
    }

    var busy: Bool { agentState != "idle" }

    var offlineMessage: String? {
        if case .offline(let m) = link { return m }
        return nil
    }

    var hostLabel: String { host == "brain" ? "PC 뒤의 두뇌 (창이 닫혀 있어요)" : "PC 효딩쓰 창" }

    // MARK: - 켜고 끄기

    func scenePhaseChanged(_ phase: ScenePhase) {
        switch phase {
        case .active:
            if isPaired { startPolling() }
        case .background:
            stopPolling()
            if recorder.recording { recorder.cancel() }
        default:
            break
        }
    }

    func startPolling() {
        guard pollTask == nil else { return }
        pollTask = Task { [weak self] in await self?.pollLoop() }
    }

    func stopPolling() {
        pollTask?.cancel()
        pollTask = nil
    }

    func reload() async {
        stopPolling()
        boot = ""
        startPolling()
    }

    private func pollLoop() async {
        var delay: UInt64 = 1
        boot = ""
        while !Task.isCancelled {
            do {
                if boot.isEmpty {
                    if case .online = link {} else { link = .connecting }
                    ingestState(try await api.get("state"))
                    link = .online            // 지금까지를 받았으면 연결된 것 — 첫 긴 폴링(최대 25초)을 기다리지 않는다
                    if workers.isEmpty { await refreshWorkers() }
                }
                let r = try await api.get("events", query: ["since": String(seq), "boot": boot, "wait": "25"], timeout: 40)
                link = .online
                delay = 1
                if r["reset"]?.bool == true {
                    boot = ""
                    continue
                }
                for e in r["events"]?.array ?? [] {
                    apply(e)
                    if let s = e["seq"]?.int { seq = max(seq, s) }
                }
                if let s = r["seq"]?.int { seq = max(seq, s) }
                if let st = r["state"]?.string { agentState = st }
                revision += 1
            } catch let e as DeskError where e.status == 401 {
                unpair(message: e.message, tellServer: false)
                return
            } catch {
                if Task.isCancelled { return }
                link = .offline((error as? DeskError)?.message ?? "PC 효딩쓰에 닿지 않아요")
                try? await Task.sleep(nanoseconds: delay * 1_000_000_000)
                delay = min(delay * 2, 20)
                boot = ""
            }
        }
    }

    // MARK: - 짝 짓기

    func pair(code: String) async -> String? {
        do {
            let r = try await api.post("pair", ["code": code, "device": UIDevice.current.name], auth: false)
            guard let token = r["token"]?.string else { return "연결하지 못했어요." }
            api.token = token
            pairMessage = nil
            items = []
            isPaired = true
            startPolling()
            return nil
        } catch {
            return error.localizedDescription
        }
    }

    func unpair(message: String? = nil, tellServer: Bool = true) {
        if tellServer {
            let api = self.api
            Task { try? await api.post("unpair") }       // PC 쪽 열쇠도 지운다
        }
        stopPolling()
        speaker.stop()
        if recorder.recording { recorder.cancel() }
        Task { @MainActor in
            try? await Task.sleep(nanoseconds: 400_000_000)
            self.api.token = nil
        }
        isPaired = false
        items = []
        boot = ""
        seq = 0
        pairMessage = message
    }

    // MARK: - 보내기

    func send(_ text: String) {
        let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !t.isEmpty else { return }
        speaker.stop()
        Haptics.tap()
        Task { await fire("send", ["text": t]) }
    }

    func decide(_ id: String, _ decision: String) {
        Haptics.tap()
        Task { await fire("decide", ["id": id, "decision": decision]) }
    }

    func answer(_ id: String, _ answers: [String: String]) {
        Task { await fire("answer", ["id": id, "answers": answers]) }
    }

    func stop() {
        speaker.stop()
        Task { await fire("stop") }
    }

    func newChat() {
        Task { await fire("new") }
    }

    func setModel(_ key: String) {
        model = key
        Task { await fire("model", ["key": key]) }
    }

    func commitSpeed() {
        defaults.set(speed, forKey: "speed")
        Task { await fire("speed", ["speed": speed]) }
    }

    private func fire(_ path: String, _ body: [String: Any] = [:]) async {
        do {
            try await api.post(path, body)
        } catch {
            show(error)
        }
    }

    // MARK: - 말로

    func toggleRecording() {
        if recorder.recording {
            finishRecording()
            return
        }
        speaker.stop()
        Task {
            if await recorder.start() {
                Haptics.tap()
            } else {
                showToast("마이크를 쓸 수 없어요 — 설정 › 효딩쓰 › 마이크를 켜 주세요.")
            }
        }
    }

    func finishRecording() {
        guard recorder.recording, let data = recorder.stop() else { return }
        guard data.count > 3000 else {
            showToast("너무 짧아요. 누르고 말씀한 뒤 다시 누르세요.")
            return
        }
        uploadingVoice = true
        Task {
            defer { uploadingVoice = false }
            do {
                let r = try JSON.parse(try await api.raw("voice", method: "POST", body: data,
                                                         contentType: "audio/mp4", timeout: 100))
                if (r["text"]?.string ?? "").isEmpty {
                    showToast("잘 들리지 않았어요. 다시 말씀해 주세요.")
                } else {
                    speakNext = true
                }
            } catch {
                show(error)
            }
        }
    }

    func testVoice() {
        speakReply("대표님, 이 목소리로 말씀드릴게요.")
    }

    private func maybeSpeak() {
        defer { speakNext = false }
        guard lastUserOrigin == "app", !lastAIText.isEmpty else { return }
        switch replyVoice {
        case .off: return
        case .voiceOnly: if !speakNext { return }
        case .always: break
        }
        speakReply(lastAIText)
    }

    /// 답 옆 「읽기」 — 끝까지 읽는다. 읽는 중에 누르면 멈춘다.
    func readAloud(_ text: String) {
        if speaker.speaking {
            speaker.stop()
        } else {
            speakReply(text, full: true)
        }
    }

    private func speakReply(_ text: String, full: Bool = false) {
        let clean = SpeechText.clean(text, limit: full ? 6000 : 300)
        guard !clean.isEmpty else { return }
        switch voiceEngine {
        case .iphone:
            speaker.speak(clean, speed: speed)
        case .pc:
            Task {
                do {
                    let body = try JSONSerialization.data(withJSONObject: ["text": text])
                    speaker.play(try await api.raw("tts", method: "POST", body: body, timeout: 100))
                } catch {
                    speaker.speak(clean, speed: speed)       // PC 목소리가 안 되면 아이폰 목소리로
                }
            }
        }
    }

    // MARK: - AI Office

    func refreshWorkers() async {
        guard let r = try? await api.get("office/workers") else { return }
        let ws = (r["workers"]?.array ?? []).compactMap(Worker.init(json:))
        if !ws.isEmpty { workers = ws }
    }

    func officeHistory(_ id: String) async throws -> [OfficeMessage] {
        let r = try await api.get("office/history", query: ["id": id])
        return (r["messages"]?.array ?? []).compactMap(OfficeMessage.init(json:))
    }

    func officeChat(_ id: String, _ text: String) async throws {
        try await api.post("office/chat", ["id": id, "text": text])
    }

    // MARK: - PC 문서 링크 (2026-09-28)

    /// 답 속 `C:\que-desk\…` 경로(MarkdownText 가 hyodoc:// 링크로 바꾼 것)를 누르면 — PC 가 서명한 링크를 받아 사파리로.
    /// 보통 링크(https)는 그대로 시스템이 연다.
    /// nonisolated — OpenURLAction 이 어느 맥락에서 불러도 되게. 문서 열기는 메인 액터에서(openDoc).
    nonisolated func handleLink(_ url: URL) -> OpenURLAction.Result {
        guard url.scheme == DocLinks.scheme else { return .systemAction }
        if let path = URLComponents(url: url, resolvingAgainstBaseURL: false)?
            .queryItems?.first(where: { $0.name == "p" })?.value {
            Task { await self.openDoc(path) }
        }
        return .handled
    }

    func openDoc(_ path: String) async {
        do {
            let r = try await api.get("doc", query: ["path": path])
            guard let s = r["url"]?.string, let u = URL(string: s) else {
                throw DeskError(status: 0, message: "링크를 받지 못했어요.")
            }
            _ = await UIApplication.shared.open(u)
        } catch {
            show(error)
        }
    }

    /// 사건의 images(서명 링크 배열)·files([{name, url}])
    private static func attachments(_ e: JSON) -> ([URL], [FileLink]) {
        let imgs = (e["images"]?.array ?? []).compactMap { j -> URL? in
            guard let s = j.string else { return nil }
            return URL(string: s)
        }
        let files = (e["files"]?.array ?? []).compactMap { f -> FileLink? in
            guard let s = f["url"]?.string, let u = URL(string: s) else { return nil }
            return FileLink(name: f["name"]?.string ?? u.lastPathComponent, url: u)
        }
        return (imgs, files)
    }

    // MARK: - 알림

    func showToast(_ text: String) {
        withAnimation(.easeOut(duration: 0.2)) { toast = text }
        Task {
            try? await Task.sleep(nanoseconds: 3_200_000_000)
            if toast == text { withAnimation(.easeIn(duration: 0.2)) { toast = nil } }
        }
    }

    private func show(_ error: Error) {
        if let e = error as? DeskError, e.status == 401 {
            unpair(message: e.message, tellServer: false)
            return
        }
        showToast(error.localizedDescription)
    }

    // MARK: - 사건 → 화면

    private func ingestState(_ st: JSON) {
        boot = st["boot"]?.string ?? ""
        seq = st["seq"]?.int ?? 0
        name = st["name"]?.string ?? name
        host = st["host"]?.string ?? host
        model = st["model"]?.string ?? model
        if let ms = st["models"]?.array {
            let list = ms.compactMap { m -> ModelOption? in
                guard let k = m["key"]?.string, let l = m["label"]?.string else { return nil }
                return ModelOption(key: k, label: l)
            }
            if !list.isEmpty { models = list }
        }
        if let sp = st["speed"]?.double { speed = sp }
        agentState = st["state"]?.string ?? "idle"
        let ws = (st["office"]?.array ?? []).compactMap(Worker.init(json:))
        if !ws.isEmpty { workers = ws }

        items = []
        openAI = []
        toolOwner = [:]
        cardOwner = [:]
        lastAI = nil
        replaying = true
        for e in st["events"]?.array ?? [] { apply(e) }
        replaying = false
        if agentState == "idle" { finishOpen() }
        revision += 1
    }

    private func apply(_ e: JSON) {
        guard let type = e["type"]?.string else { return }
        switch type {
        case "user":
            let origin = e["origin"]?.string ?? "typed"
            let (imgs, files) = Self.attachments(e)
            append(ChatItem(.user(text: e["text"]?.string ?? "", origin: origin), images: imgs, files: files))
            lastAI = nil
            lastAIText = ""
            lastUserOrigin = origin

        case "text_start":
            let item = ChatItem(.ai(text: "", streaming: true, local: false, meta: nil))
            append(item)
            openAI.append(item.id)

        case "delta":
            let piece = e["text"]?.string ?? ""
            if openAI.isEmpty {
                let item = ChatItem(.ai(text: "", streaming: true, local: false, meta: nil))
                append(item)
                openAI.append(item.id)
            }
            if let id = openAI.last {
                update(id) { item in
                    if case .ai(let t, _, let l, let m) = item.kind {
                        item.kind = .ai(text: t + piece, streaming: true, local: l, meta: m)
                    }
                }
            }

        case "text":
            let text = e["text"]?.string ?? ""
            let local = e["local"]?.bool ?? false
            if !openAI.isEmpty {
                let id = openAI.removeFirst()
                update(id) { $0.kind = .ai(text: text, streaming: false, local: local, meta: nil) }
                lastAI = id
            } else {
                let item = ChatItem(.ai(text: text, streaming: false, local: local, meta: nil))
                append(item)
                lastAI = item.id
            }
            lastAIText = text

        case "state":
            agentState = e["state"]?.string ?? agentState
            if agentState == "idle" { finishOpen() }

        case "tool":
            let id = e["id"]?.string ?? UUID().uuidString
            let entry = ToolEntry(id: id, label: e["label"]?.string ?? "", detail: e["detail"]?.string ?? "",
                                  sub: e["sub"]?.bool ?? false)
            if let last = items.last, case .tools(var list) = last.kind {
                list.append(entry)
                items[items.count - 1].kind = .tools(list)
                toolOwner[id] = last.id
            } else {
                let item = ChatItem(.tools([entry]))
                append(item)
                toolOwner[id] = item.id
            }

        case "tool_done":
            guard let id = e["id"]?.string, let owner = toolOwner[id] else { break }
            let ok = e["ok"]?.bool ?? true
            update(owner) { item in
                if case .tools(var list) = item.kind, let i = list.firstIndex(where: { $0.id == id }) {
                    list[i].status = ok ? .ok : .fail
                    item.kind = .tools(list)
                }
            }

        case "permission":
            let card = PermCard(id: e["id"]?.string ?? "", title: e["title"]?.string ?? "허락이 필요합니다",
                                note: e["note"]?.string ?? "", body: e["body"]?.string ?? "",
                                canAlways: e["can_always"]?.bool ?? false)
            let item = ChatItem(.permission(card))
            append(item)
            cardOwner[card.id] = item.id
            if !replaying { Haptics.alert() }

        case "question":
            let qs: [Question] = (e["questions"]?.array ?? []).map { q in
                Question(question: q["question"]?.string ?? "",
                         multi: q["multiSelect"]?.bool ?? false,
                         options: (q["options"]?.array ?? []).map { o in
                             QuestionOption(label: o["label"]?.string ?? "", desc: o["description"]?.string ?? "")
                         })
            }
            let card = QuestionCard(id: e["id"]?.string ?? "", questions: qs)
            let item = ChatItem(.question(card))
            append(item)
            cardOwner[card.id] = item.id
            if !replaying { Haptics.alert() }

        case "permission_closed":
            guard let id = e["id"]?.string, let owner = cardOwner[id] else { break }
            let decision = e["decision"]?.string ?? "closed"
            update(owner) { item in
                switch item.kind {
                case .permission(var c):
                    c.closed = decision
                    item.kind = .permission(c)
                case .question(var c):
                    c.closed = decision
                    item.kind = .question(c)
                default:
                    break
                }
            }

        case "incoming":
            let kind = e["kind"]?.string ?? ""
            let names = ["peer": "다른 세션", "task-notification": "작업 알림", "channel": "채널",
                         "auto": "자율 점검", "system": "시스템", "phone_out": "📱 휴대폰으로 보냄"]
            var source = names[kind] ?? kind
            if let from = e["from"]?.string, !from.isEmpty { source += " · " + from }
            let (imgs, files) = Self.attachments(e)
            append(ChatItem(.incoming(source: source, text: e["text"]?.string ?? "", auto: kind == "auto" || kind == "system"),
                            images: imgs, files: files))
            lastAI = nil

        case "note":
            append(.note(e["text"]?.string ?? ""))

        case "error":
            append(.error(e["text"]?.string ?? ""))

        case "result":
            if let id = lastAI, let ms = e["ms"]?.double {
                let steps = e["steps"]?.int ?? 1
                let meta = String(format: "%.1f초", ms / 1000) + (steps > 1 ? " · \(steps)단계" : "")
                update(id) { item in
                    if case .ai(let t, let s, let l, _) = item.kind {
                        item.kind = .ai(text: t, streaming: s, local: l, meta: meta)
                    }
                }
            }
            if !replaying { maybeSpeak() }

        case "cleared":
            items = []
            openAI = []
            toolOwner = [:]
            cardOwner = [:]
            lastAI = nil

        case "model":
            model = e["key"]?.string ?? model

        case "office":
            let ws = (e["workers"]?.array ?? []).compactMap(Worker.init(json:))
            if !ws.isEmpty { workers = ws }

        default:
            break
        }
    }

    private func finishOpen() {
        for id in openAI {
            update(id) { item in
                if case .ai(let t, _, let l, let m) = item.kind {
                    item.kind = .ai(text: t, streaming: false, local: l, meta: m)
                }
            }
        }
        openAI = []
    }

    private func append(_ item: ChatItem) {
        items.append(item)
        if items.count > 400 { items.removeFirst(items.count - 400) }
    }

    private func append(_ kind: ChatItem.Kind) {
        append(ChatItem(kind))
    }

    private func update(_ id: UUID, _ change: (inout ChatItem) -> Void) {
        if let i = items.lastIndex(where: { $0.id == id }) { change(&items[i]) }
    }
}
