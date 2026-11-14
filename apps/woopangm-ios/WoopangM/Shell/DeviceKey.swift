import Foundation
import Security
import UIKit

/// 비밀번호 대신 쓰는 「기기 열쇠」 — 처음 한 번 무작위 32바이트를 만들어 Keychain 에 둔다.
/// 이 아이폰에만 남는다(ThisDeviceOnly — 다른 기기로 백업·복원되지 않는다). 서버는 이 열쇠의 해시만 갖고 있다(server/admin_m.py).
/// 앱을 지웠다 깔아도 Keychain 에 남아 있으면 다시 허용하지 않아도 된다.
enum DeviceKey {
    private static let service = "com.que.woopangm"
    private static let account = "device-key"

    /// 지금 열쇠 — 없으면 만든다. 만들지 못하면 nil (Keychain 오류)
    static func current() -> String? {
        read() ?? make()
    }

    private static func read() -> String? {
        let q: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var out: CFTypeRef?
        guard SecItemCopyMatching(q as CFDictionary, &out) == errSecSuccess,
              let data = out as? Data, let s = String(data: data, encoding: .utf8), s.count >= 43 else { return nil }
        return s
    }

    private static func make() -> String? {
        var bytes = [UInt8](repeating: 0, count: 32)
        guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else { return nil }
        let key = Data(bytes).base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")          // base64url 43자 — 서버가 받는 모양
        let add: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecValueData as String: Data(key.utf8),
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        ]
        let st = SecItemAdd(add as CFDictionary, nil)
        if st == errSecDuplicateItem { return read() }
        return st == errSecSuccess ? key : nil
    }

    /// 허용 링크에 보일 기기 이름 — iOS 16 부터 기기 이름(「대표님의 iPhone」)은 앱이 못 읽어 모델·버전으로 (예: iPhone · iOS 18.1 · iPhone16,1)
    @MainActor
    static func label() -> String {
        var u = utsname()
        uname(&u)
        let size = MemoryLayout.size(ofValue: u.machine)       // 클로저 밖에서 — 안에서 u.machine 을 읽으면 겹친 접근(컴파일 오류)
        let machine = withUnsafePointer(to: &u.machine) {
            $0.withMemoryRebound(to: CChar.self, capacity: size) { String(cString: $0) }
        }
        let d = UIDevice.current
        return "\(d.model) · iOS \(d.systemVersion) · \(machine)"
    }
}
