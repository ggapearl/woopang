import Foundation

/// 인터넷이 안 될 때 보여 줄 안내 화면 (관리자 화면과 같은 어두운 색)
enum ShellPages {
    static func offlineHTML(retry: String, reason: String) -> String {
        let esc: (String) -> String = {
            $0.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;")
                .replacingOccurrences(of: "\"", with: "&quot;")
        }
        return """
        <!doctype html><html lang="ko"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <style>
        body{margin:0;min-height:100vh;display:grid;place-items:center;background:#14171A;color:#F3F5F6;
          font:16px/1.6 -apple-system,"Apple SD Gothic Neo",sans-serif;text-align:center;padding:24px;box-sizing:border-box;word-break:keep-all}
        h1{font-size:20px;margin:0 0 8px}p{margin:0 0 6px;color:#8C959D}small{color:#6E777F;font-size:12px}
        a{display:inline-block;margin-top:18px;padding:12px 28px;border-radius:99px;background:#E95383;color:#fff;
          text-decoration:none;font-weight:700}
        </style></head><body><div>
        <h1>우팡 관리자에 닿지 못했어요</h1>
        <p>인터넷 연결을 확인한 뒤 다시 시도해 주세요.</p>
        <small>\(esc(reason))</small><br>
        <a href="\(esc(retry))">다시 시도</a>
        </div></body></html>
        """
    }
}
