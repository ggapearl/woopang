import Foundation

// MARK: - 대화 한 줄

struct ToolEntry: Identifiable, Equatable {
    enum Status: Equatable { case running, ok, fail }
    let id: String
    var label: String
    var detail: String
    var sub: Bool
    var status: Status = .running
}

struct PermCard: Equatable {
    let id: String
    var title: String
    var note: String
    var body: String
    var canAlways: Bool
    var closed: String? = nil
}

struct QuestionOption: Equatable {
    var label: String
    var desc: String
}

struct Question: Equatable {
    var question: String
    var multi: Bool
    var options: [QuestionOption]
}

struct QuestionCard: Equatable {
    let id: String
    var questions: [Question]
    var closed: String? = nil
}

struct ChatItem: Identifiable, Equatable {
    enum Kind: Equatable {
        case user(text: String, origin: String)
        case ai(text: String, streaming: Bool, local: Bool, meta: String?)
        case tools([ToolEntry])
        case permission(PermCard)
        case question(QuestionCard)
        case incoming(source: String, text: String, auto: Bool)
        case note(String)
        case error(String)
    }

    let id = UUID()
    var kind: Kind

    init(_ kind: Kind) {
        self.kind = kind
    }
}

// MARK: - AI Office

struct Worker: Identifiable, Hashable {
    let id: String
    var name: String
    var team: String
    var role: String
    var colorHex: String
    var task: String
    var busy: Bool

    init?(json: JSON) {
        guard let id = json["id"]?.string else { return nil }
        self.id = id
        name = json["name"]?.string ?? id
        team = json["team_label"]?.string ?? ""
        role = json["role"]?.string ?? ""
        colorHex = json["color"]?.string ?? "#8D92AE"
        task = json["task"]?.string ?? ""
        busy = json["busy"]?.bool ?? false
    }

    /// 「김효딩」 → 「효」 — 사무실 창과 같은 머리글자
    var initial: String {
        let chars = Array(name)
        if chars.count >= 2 { return String(chars[chars.count - 2]) }
        return chars.first.map { String($0) } ?? "?"
    }

    var statusLine: String {
        [team, busy ? "일하는 중" : task].filter { !$0.isEmpty }.joined(separator: " · ")
    }
}

struct OfficeMessage: Identifiable, Equatable {
    let id = UUID()
    var sender: String
    var name: String
    var content: String
    var time: String

    var mine: Bool { sender == "ceo" }

    init(sender: String, name: String, content: String, time: String) {
        self.sender = sender
        self.name = name
        self.content = content
        self.time = time
    }

    init?(json: JSON) {
        guard let content = json["content"]?.string else { return nil }
        sender = json["sender"]?.string ?? ""
        name = json["sender_name"]?.string ?? ""
        self.content = content
        time = json["timestamp"]?.string ?? ""
    }
}

// MARK: - 설정

struct ModelOption: Identifiable, Hashable {
    let key: String
    let label: String
    var id: String { key }

    static let defaults = [ModelOption(key: "fast", label: "빠르게"),
                           ModelOption(key: "normal", label: "보통"),
                           ModelOption(key: "deep", label: "깊게")]
}

enum ReplyVoice: String, CaseIterable, Identifiable {
    case off, voiceOnly, always
    var id: String { rawValue }
    var label: String {
        switch self {
        case .off: return "듣지 않기"
        case .voiceOnly: return "말로 물었을 때만"
        case .always: return "항상"
        }
    }
}

enum VoiceEngine: String, CaseIterable, Identifiable {
    case iphone, pc
    var id: String { rawValue }
    var label: String {
        switch self {
        case .iphone: return "아이폰 목소리 · 바로"
        case .pc: return "PC 소희 목소리 · 조금 늦음"
        }
    }
}

struct QuickChip: Identifiable {
    let title: String
    let say: String
    var id: String { title }

    static let all = [QuickChip(title: "서버 상태", say: "서버 상태 어때?"),
                      QuickChip(title: "승인 대기", say: "AI Office 승인 대기 뭐 있어?"),
                      QuickChip(title: "오늘 할 일", say: "오늘 뭐부터 하면 좋을까? 급한 것부터 3개만."),
                      QuickChip(title: "농민닷컴 현황", say: "농민닷컴 오늘 주문이랑 배송 현황 짧게 알려줘"),
                      QuickChip(title: "홍보 뭐 올려", say: "오늘 올릴 홍보물 하나 만들어서 보내줘")]
}
