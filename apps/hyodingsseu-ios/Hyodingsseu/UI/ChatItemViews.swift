import SwiftUI

struct ChatItemView: View {
    let item: ChatItem

    var body: some View {
        switch item.kind {
        case .user(let text, let origin):
            UserBubble(text: text, origin: origin, images: item.images, time: item.time)
        case .ai(let text, let streaming, let local, let meta):
            AIMessage(text: text, streaming: streaming, local: local, meta: meta, time: item.time)
        case .tools(let list):
            VStack(alignment: .leading, spacing: 4) {
                ForEach(list) { t in ToolRow(tool: t) }
            }
        case .permission(let card):
            PermissionCardView(card: card)
        case .question(let card):
            QuestionCardView(card: card)
        case .incoming(let source, let text, let auto):
            InboxBubble(source: source, text: text, auto: auto, images: item.images, files: item.files, time: item.time)
        case .note(let text):
            Text(text)
                .font(.caption)
                .foregroundStyle(Palette.ink3)
                .multilineTextAlignment(.center)
                .frame(maxWidth: .infinity)
        case .error(let text):
            Text(text)
                .font(.subheadline)
                .foregroundStyle(Palette.pinkDeep)
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(Palette.pinkSoft)
                .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
    }
}

/// 받은 시각 — 텔레그램처럼 작게. 오늘이면 「오후 10:51」, 아니면 「9/28 오후 10:51」. 소리로 읽지 않는다 (2026-09-30)
struct StampText: View {
    let time: Date?
    var color: Color = Palette.ink3

    var body: some View {
        if let time {
            Text(Self.label(time))
                .font(.caption2)
                .monospacedDigit()
                .foregroundStyle(color)
                .accessibilityHidden(true)
        }
    }

    static func label(_ d: Date) -> String {
        let f = DateFormatter()
        f.locale = Locale(identifier: "ko_KR")
        f.dateFormat = Calendar.current.isDateInToday(d) ? "a h:mm" : "M/d a h:mm"
        return f.string(from: d)
    }
}

struct UserBubble: View {
    let text: String
    let origin: String
    var images: [URL] = []
    var time: Date? = nil

    var body: some View {
        HStack {
            Spacer(minLength: 44)
            VStack(alignment: .trailing, spacing: 2) {
                if let via {
                    Label(via, systemImage: origin == "phone" ? "paperplane" : (origin == "voice" ? "mic" : "desktopcomputer"))
                        .font(.caption2)
                        .opacity(0.8)
                }
                Text(text)
                    .textSelection(.enabled)
                AttachmentStrip(images: images, files: [])     // 대표님이 휴대폰(텔레그램)으로 보낸 사진
                StampText(time: time, color: Palette.onNavy.opacity(0.66))
            }
            .foregroundStyle(Palette.onNavy)
            .padding(.horizontal, 14)
            .padding(.vertical, 9)
            .background(Palette.navy)
            .clipShape(UnevenRoundedRectangle(topLeadingRadius: 18, bottomLeadingRadius: 18,
                                              bottomTrailingRadius: 6, topTrailingRadius: 18, style: .continuous))
        }
    }

    /// 이 아이폰에서 보낸 건 표시하지 않고, 다른 곳에서 온 것만 어디서인지 붙인다.
    private var via: String? {
        switch origin {
        case "typed": return "PC 에서"
        case "voice": return "PC 에서 말로"
        case "phone": return "텔레그램"
        default: return nil
        }
    }
}

struct AIMessage: View {
    @EnvironmentObject var store: DeskStore
    let text: String
    let streaming: Bool
    let local: Bool
    let meta: String?
    var time: Date? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 6) {
                Circle().fill(Palette.pink).frame(width: 7, height: 7)
                Text(store.name).font(.caption).foregroundStyle(Palette.ink3)
                if !streaming { StampText(time: time) }
                if local {
                    Text("로컬 LLM")
                        .font(.caption2)
                        .foregroundStyle(Palette.navy)
                        .padding(.horizontal, 7)
                        .padding(.vertical, 1)
                        .background(Palette.navySoft)
                        .clipShape(Capsule())
                }
                Spacer(minLength: 0)
                if !streaming && !text.isEmpty {
                    Button { store.readAloud(text) } label: {
                        Label("읽기", systemImage: "speaker.wave.2")
                            .font(.caption2.weight(.semibold))
                            .foregroundStyle(Palette.ink2)
                            .padding(.horizontal, 8)
                            .padding(.vertical, 3)
                            .overlay(Capsule().stroke(Palette.line, lineWidth: 1))
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("이 답을 소리로 읽기")
                }
            }
            if streaming {
                Text(text + " ▍")
                    .foregroundStyle(Palette.ink)
            } else {
                MarkdownText(text: text)
            }
            if let meta {
                Text(meta).font(.caption2).foregroundStyle(Palette.ink3).monospacedDigit()
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// 마크다운을 가볍게 — 제목은 굵게, 목록은 •, 굵게·기울임·코드·링크는 그대로 살린다.
struct MarkdownText: View {
    let text: String

    var body: some View {
        Text(Self.render(text))
            .foregroundStyle(Palette.ink)
            .tint(Palette.navy)
            .textSelection(.enabled)
    }

    static func render(_ source: String) -> AttributedString {
        var lines: [String] = []
        var inCode = false
        for raw in source.components(separatedBy: "\n") {
            if raw.trimmingCharacters(in: .whitespaces).hasPrefix("```") {
                inCode.toggle()
                continue
            }
            if inCode {
                lines.append(raw)
                continue
            }
            let line: String
            if let r = raw.range(of: "^#{1,4}\\s+", options: .regularExpression) {
                line = "**" + String(raw[r.upperBound...]) + "**"
            } else if let r = raw.range(of: "^\\s*[-*+]\\s+", options: .regularExpression) {
                line = "• " + String(raw[r.upperBound...])
            } else {
                line = raw
            }
            lines.append(DocLinks.linkify(line))
        }
        let md = lines.joined(separator: "\n")
        let opts = AttributedString.MarkdownParsingOptions(interpretedSyntax: .inlineOnlyPreservingWhitespace)
        return (try? AttributedString(markdown: md, options: opts)) ?? AttributedString(source)
    }
}

/// 답 속 PC 문서 경로(C:\que-desk\…) → 누를 수 있는 hyodoc:// 링크 (2026-09-28).
/// 누르면 DeskStore.handleLink 가 PC 에 서명 링크(30일)를 받아 사파리로 연다 — 안드로이드 앱과 같은 동작.
enum DocLinks {
    static let scheme = "hyodoc"
    private static let pattern = try! NSRegularExpression(pattern: #"`?([Cc]:[\\/]+que-desk(?:[\\/][^\s`'"<>|*?\])]*)?)`?"#)

    static func linkify(_ line: String) -> String {
        let ns = line as NSString
        var out = line
        for m in pattern.matches(in: line, range: NSRange(location: 0, length: ns.length)).reversed() {
            let path = ns.substring(with: m.range(at: 1))
            var comps = URLComponents()
            comps.scheme = scheme
            comps.host = "open"
            comps.queryItems = [URLQueryItem(name: "p", value: path)]
            guard let url = comps.url?.absoluteString, let r = Range(m.range, in: out) else { continue }
            // 마크다운 글자 안의 \ 는 \\ 로 써야 그대로 보인다
            out.replaceSubrange(r, with: "[" + path.replacingOccurrences(of: "\\", with: "\\\\") + "](" + url + ")")
        }
        return out
    }
}

/// 휴대폰으로 보낸 그림·파일, 대표님이 보낸 사진 — 누르면 사파리에서 (PC 가 서명한 30일 링크)
struct AttachmentStrip: View {
    let images: [URL]
    let files: [FileLink]

    var body: some View {
        if !images.isEmpty || !files.isEmpty {
            VStack(alignment: .leading, spacing: 6) {
                if !images.isEmpty {
                    ScrollView(.horizontal, showsIndicators: false) {
                        HStack(spacing: 6) {
                            ForEach(images, id: \.self) { u in
                                Link(destination: u) {
                                    AsyncImage(url: u) { phase in
                                        if let img = phase.image {
                                            img.resizable().scaledToFill()
                                        } else {
                                            Palette.line
                                        }
                                    }
                                    .frame(width: 84, height: 84)
                                    .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                                }
                            }
                        }
                    }
                }
                ForEach(files, id: \.self) { f in
                    Link("📄 " + f.name, destination: f.url)
                        .font(.caption)
                }
            }
            .padding(.top, 4)
        }
    }
}

struct ToolRow: View {
    let tool: ToolEntry

    var body: some View {
        HStack(spacing: 8) {
            Group {
                switch tool.status {
                case .ok:
                    Image(systemName: "checkmark")
                        .font(.caption2.weight(.bold))
                        .foregroundStyle(Palette.ok)
                case .fail:
                    Circle().fill(Palette.pink).frame(width: 7, height: 7)
                case .running:
                    ProgressView().controlSize(.mini)
                }
            }
            .frame(width: 14, height: 14)
            Text(tool.label)
                .font(.footnote.weight(.semibold))
                .foregroundStyle(Palette.ink)
                .fixedSize()
            Text(tool.detail)
                .font(.footnote)
                .foregroundStyle(Palette.ink3)
                .lineLimit(1)
                .truncationMode(.tail)
        }
        .padding(.leading, tool.sub ? 16 : 0)
    }
}

struct InboxBubble: View {
    let source: String
    let text: String
    let auto: Bool
    var images: [URL] = []
    var files: [FileLink] = []
    var time: Date? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(source)
                .font(.caption2.weight(.bold))
                .foregroundStyle(auto ? Palette.pinkDeep : Palette.navy)
            if !text.isEmpty {
                Text(text)
                    .font(.subheadline)
                    .foregroundStyle(Palette.ink)
            }
            AttachmentStrip(images: images, files: files)       // 「📱 휴대폰으로 보냄」의 카드뉴스 그림·파일
            StampText(time: time)
                .frame(maxWidth: .infinity, alignment: .trailing)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 8)
        .background(auto ? Palette.pinkSoft : Palette.navySoft)
        .clipShape(UnevenRoundedRectangle(topLeadingRadius: 14, bottomLeadingRadius: 6,
                                          bottomTrailingRadius: 14, topTrailingRadius: 14, style: .continuous))
        .padding(.trailing, 28)
    }
}

// MARK: - 허락·선택 카드

func closedLabel(_ decision: String) -> String {
    ["allow": "허락했습니다", "always": "이번 대화에선 계속 허락했습니다", "deny": "거절했습니다",
     "timeout": "답이 없어 하지 않았습니다", "answer": "답했습니다"][decision] ?? "닫혔습니다"
}

struct PermissionCardView: View {
    @EnvironmentObject var store: DeskStore
    let card: PermCard

    var body: some View {
        let open = card.closed == nil
        VStack(alignment: .leading, spacing: 6) {
            Text(card.closed.map(closedLabel) ?? "허락이 필요합니다")
                .font(.caption.weight(.bold))
                .tracking(0.5)
                .foregroundStyle(open ? Palette.pink : Palette.ink3)
            Text(card.title)
                .font(open ? Font.headline : Font.subheadline.weight(.semibold))
                .foregroundStyle(open ? Palette.ink : Palette.ink2)
            if open {
                if !card.note.isEmpty {
                    Text(card.note).font(.footnote).foregroundStyle(Palette.ink2)
                }
                if !card.body.isEmpty {
                    ScrollView {
                        Text(card.body)
                            .font(.system(.caption, design: .monospaced))
                            .foregroundStyle(Palette.ink)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(10)
                    }
                    .frame(maxHeight: 180)
                    .background(Palette.surface2)
                    .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
                }
                HStack(spacing: 6) {
                    Button("허락") { store.decide(card.id, "allow") }
                        .buttonStyle(PrimaryButtonStyle())
                    if card.canAlways {
                        Button("계속 허락") { store.decide(card.id, "always") }
                            .buttonStyle(GhostButtonStyle())
                    }
                    Button("거절") { store.decide(card.id, "deny") }
                        .buttonStyle(NoButtonStyle())
                }
                .padding(.top, 4)
                Text("아래에 다르게 하라고 적으셔도 됩니다.")
                    .font(.caption2)
                    .foregroundStyle(Palette.ink3)
            }
        }
        .padding(open ? 14 : 10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(open ? Palette.surface : Color.clear)
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous)
            .stroke(open ? Palette.pinkLine : Palette.line, lineWidth: 1.5))
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }
}

struct QuestionCardView: View {
    @EnvironmentObject var store: DeskStore
    let card: QuestionCard
    @State var picks: [String: [String]] = [:]

    var body: some View {
        let open = card.closed == nil
        VStack(alignment: .leading, spacing: 8) {
            Text(card.closed.map(closedLabel) ?? "골라 주세요")
                .font(.caption.weight(.bold))
                .tracking(0.5)
                .foregroundStyle(open ? Palette.navy : Palette.ink3)
            ForEach(Array(card.questions.enumerated()), id: \.offset) { _, q in
                Text(q.question)
                    .font(open ? Font.headline : Font.subheadline.weight(.semibold))
                    .foregroundStyle(open ? Palette.ink : Palette.ink2)
                if open {
                    ForEach(Array(q.options.enumerated()), id: \.offset) { _, o in
                        optionButton(q, o)
                    }
                }
            }
            if open {
                HStack(spacing: 6) {
                    if needsConfirm {
                        Button("이대로 답하기") { submit() }
                            .buttonStyle(PrimaryButtonStyle())
                    }
                    Button("답하지 않기") { store.decide(card.id, "deny") }
                        .buttonStyle(NoButtonStyle())
                }
            }
        }
        .padding(open ? 14 : 10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(open ? Palette.surface : Color.clear)
        .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous)
            .stroke(open ? Palette.navy : Palette.line, lineWidth: 1.5))
        .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
    }

    private func optionButton(_ q: Question, _ o: QuestionOption) -> some View {
        let on = picks[q.question, default: []].contains(o.label)
        return Button { pick(q, o) } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(o.label).font(.subheadline.weight(.semibold)).foregroundStyle(Palette.ink)
                if !o.desc.isEmpty {
                    Text(o.desc).font(.caption).foregroundStyle(Palette.ink2)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(10)
            .background(on ? Palette.navySoft : Palette.surface)
            .overlay(RoundedRectangle(cornerRadius: 10, style: .continuous)
                .stroke(on ? Palette.navy : Palette.line, lineWidth: 1.5))
            .clipShape(RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
        .buttonStyle(.plain)
    }

    private var needsConfirm: Bool {
        card.questions.count > 1 || (card.questions.first?.multi ?? false)
    }

    private func pick(_ q: Question, _ o: QuestionOption) {
        if q.multi {
            var cur = picks[q.question, default: []]
            if let i = cur.firstIndex(of: o.label) { cur.remove(at: i) } else { cur.append(o.label) }
            picks[q.question] = cur
        } else {
            picks[q.question] = [o.label]
            if !needsConfirm { submit() }
        }
    }

    private func submit() {
        var answers: [String: String] = [:]
        for (k, v) in picks { answers[k] = v.joined(separator: ", ") }
        store.answer(card.id, answers)
    }
}
