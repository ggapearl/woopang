import Foundation

/// 웹 화면에 넣는 짧은 JS 와, 인터넷이 안 될 때 보여 줄 안내 화면.
enum ShellPages {
    /// 우기 화면이 「앱 안에서 열렸는지」 알고 폰 기능을 부르게 하는 다리 — `window.WoogiApp` (WoogiNative.swift 참고).
    /// 우기 주소(https://woopang.com/woogi…)에서만 만든다 — 결제창 같은 바깥 화면에는 없다(앱 쪽도 한 번 더 막는다).
    static var bridgeJS: String {
        """
        (function () {
          if (window.WoogiApp) return;
          var h = location.hostname;
          if (location.protocol !== 'https:' || (h !== 'woopang.com' && h !== 'www.woopang.com') ||
              !/^\\/woogi(\\/|$)/.test(location.pathname)) return;
          var mh = window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.woogi;
          if (!mh) return;
          window.WoogiApp = Object.freeze({
            platform: 'ios', version: '\(Shell.version)', build: '\(Shell.build)',
            call: function (m, a) { return mh.postMessage({ m: String(m), a: a || {} }); }
          });
        })();
        """
    }

    static func offlineHTML(retry: String, reason: String) -> String {
        func esc(_ s: String) -> String {
            s.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;")
                .replacingOccurrences(of: ">", with: "&gt;").replacingOccurrences(of: "\"", with: "&quot;")
        }
        return """
        <!doctype html><html lang="ko"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <style>
        body{margin:0;min-height:100vh;display:grid;place-items:center;background:#FFFBEA;color:#26352B;
          font:16px/1.6 -apple-system,"Apple SD Gothic Neo",sans-serif;text-align:center;padding:24px;box-sizing:border-box;
          word-break:keep-all}
        h1{font-size:20px;margin:0 0 8px}p{margin:0 0 6px;color:#52625A}small{color:#8A978F;font-size:12px}
        a{display:inline-block;margin-top:18px;padding:12px 28px;border-radius:99px;background:#2A6B42;color:#fff;
          text-decoration:none;font-weight:700}
        </style></head><body><div>
        <h1>우기에 닿지 못했어요</h1>
        <p>인터넷 연결을 확인한 뒤 다시 시도해 주세요.</p>
        <small>\(esc(reason))</small><br>
        <a href="\(esc(retry))">다시 시도</a>
        </div></body></html>
        """
    }
}
