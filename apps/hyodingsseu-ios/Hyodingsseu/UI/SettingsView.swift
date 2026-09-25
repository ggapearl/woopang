import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var store: DeskStore
    @State var confirmUnpair = false

    var body: some View {
        Form {
            Section {
                Picker("언제", selection: $store.replyVoice) {
                    ForEach(ReplyVoice.allCases) { v in Text(v.label).tag(v) }
                }
                Picker("목소리", selection: $store.voiceEngine) {
                    ForEach(VoiceEngine.allCases) { v in Text(v.label).tag(v) }
                }
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Text("말 빠르기")
                        Spacer()
                        Text(String(format: "%.2f×", store.speed))
                            .monospacedDigit()
                            .foregroundStyle(Palette.ink2)
                    }
                    Slider(value: $store.speed, in: 0.8...1.6, step: 0.05, onEditingChanged: { editing in
                        if !editing { store.commitSpeed() }
                    })
                    .tint(Palette.pink)
                }
                Button("들어 보기") { store.testVoice() }
            } header: {
                Text("답을 소리로 듣기")
            } footer: {
                Text("말 빠르기는 PC 스피커·텔레그램 음성 메시지의 소희 목소리에도 함께 적용돼요.")
            }

            Section {
                Picker("생각의 깊이", selection: Binding(get: { store.model }, set: { store.setModel($0) })) {
                    ForEach(store.models) { m in Text(m.label).tag(m.key) }
                }
            } header: {
                Text("효딩쓰")
            } footer: {
                Text("깊게는 Opus 로 오래 생각합니다. 구독 사용량을 많이 씁니다.")
            }

            Section {
                LabeledContent("붙은 곳", value: store.host == "brain" ? "PC 뒤의 두뇌" : "PC 효딩쓰 창")
                LabeledContent("서버") {
                    Text(store.api.base).font(.footnote).foregroundStyle(Palette.ink2).lineLimit(2)
                }
                Button("이 아이폰 연결 끊기", role: .destructive) { confirmUnpair = true }
            } header: {
                Text("연결")
            } footer: {
                Text("끊으면 이 아이폰의 열쇠가 PC 에서도 지워집니다. 다시 쓰려면 PC 창 ☰ 에서 새 코드를 받으세요.")
            }

            Section {
                Text("효딩쓰 1.0 · QUE. ENT · 대표님 개인용")
                    .font(.footnote)
                    .foregroundStyle(Palette.ink3)
            }
        }
        .navigationTitle("설정")
        .navigationBarTitleDisplayMode(.inline)
        .toolbarBackground(Palette.bar, for: .navigationBar)
        .toolbarBackground(.visible, for: .navigationBar)
        .toolbarColorScheme(.dark, for: .navigationBar)
        .confirmationDialog("이 아이폰의 연결을 끊을까요?", isPresented: $confirmUnpair, titleVisibility: .visible) {
            Button("연결 끊기", role: .destructive) {
                store.unpair(message: "연결을 끊었습니다. 다시 쓰려면 PC 창에서 새 코드를 받아 주세요.")
            }
        }
    }
}
