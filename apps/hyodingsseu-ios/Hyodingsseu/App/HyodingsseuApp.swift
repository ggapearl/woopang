import SwiftUI

@main
struct HyodingsseuApp: App {
    @StateObject private var store = DeskStore()

    var body: some Scene {
        WindowGroup {
            RootView()
                .environmentObject(store)
        }
    }
}

/// 짝을 지었으면 대화, 아니면 연결 화면.
struct RootView: View {
    @EnvironmentObject private var store: DeskStore
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        Group {
            if store.isPaired {
                MainView()
            } else {
                PairView()
            }
        }
        .onChange(of: scenePhase) { _, phase in
            store.scenePhaseChanged(phase)
        }
        .onAppear {
            store.scenePhaseChanged(.active)
        }
    }
}
