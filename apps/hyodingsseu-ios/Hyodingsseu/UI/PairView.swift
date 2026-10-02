import SwiftUI

/// 처음 한 번 — PC 창에서 받은 6자리 코드로 짝을 짓는다.
struct PairView: View {
    @EnvironmentObject var store: DeskStore
    @State var code = ""
    @State var busy = false
    @State var error: String?
    @State var showServer = false
    @State var server = DeskAPI.defaultBase
    @FocusState var focused: Bool

    var body: some View {
        ScrollView {
            VStack(spacing: 18) {
                OrbView(state: busy ? "thinking" : "idle", size: 78)
                    .padding(.top, 56)
                Text("효딩쓰와 연결")
                    .font(.title2.weight(.bold))
                    .foregroundStyle(Palette.ink)
                Text("PC 효딩쓰 창의 ☰ → 휴대폰 앱 →\n「연결 코드 받기」를 누르고 6자리 숫자를 넣어 주세요.")
                    .font(.subheadline)
                    .foregroundStyle(Palette.ink2)
                    .multilineTextAlignment(.center)

                TextField("000000", text: $code)
                    .keyboardType(.numberPad)
                    .textContentType(.oneTimeCode)
                    .font(.system(size: 34, weight: .bold, design: .monospaced))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(Palette.navy)
                    .focused($focused)
                    .padding(.vertical, 12)
                    .background(Palette.surface)
                    .overlay(RoundedRectangle(cornerRadius: 14, style: .continuous)
                        .stroke(focused ? Palette.navy : Palette.line, lineWidth: 1.5))
                    .clipShape(RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .onChange(of: code) { _, v in
                        let digits = String(v.filter { $0.isNumber }.prefix(6))
                        if digits != v { code = digits }
                        if digits.count == 6 { pair() }
                    }

                if let error {
                    Text(error)
                        .font(.footnote)
                        .foregroundStyle(Palette.pinkDeep)
                        .multilineTextAlignment(.center)
                } else if let m = store.pairMessage {
                    Text(m)
                        .font(.footnote)
                        .foregroundStyle(Palette.ink2)
                        .multilineTextAlignment(.center)
                }

                Button(action: pair) {
                    if busy {
                        ProgressView().tint(Palette.onNavy)
                    } else {
                        Text("연결하기")
                    }
                }
                .buttonStyle(PrimaryButtonStyle(wide: true))
                .disabled(code.count != 6 || busy)

                DisclosureGroup("서버 주소", isExpanded: $showServer) {
                    TextField("https://…", text: $server)
                        .keyboardType(.URL)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .font(.footnote)
                        .padding(.top, 6)
                }
                .font(.footnote)
                .foregroundStyle(Palette.ink2)
                .padding(.top, 12)
            }
            .padding(.horizontal, 24)
        }
        .background(Palette.ground.ignoresSafeArea())
        .onAppear {
            server = store.api.base
            focused = true
        }
    }

    private func pair() {
        guard code.count == 6, !busy else { return }
        busy = true
        error = nil
        store.api.base = server
        let entered = code
        Task {
            let err = await store.pair(code: entered)
            busy = false
            if let err {
                error = err
                code = ""
            }
        }
    }
}
