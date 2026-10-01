import Foundation

/// 웹 화면에 넣는 짧은 JS 와, 인터넷이 안 될 때 보여 줄 안내 화면.
enum ShellPages {
    /// 안드로이드(Capacitor)와 같은 이름 `window.Capacitor.Plugins.DeskNative` 을 만들어 준다 — 웹(app.js)은 손대지 않아도 된다.
    static let bridgeJS = """
    (function () {
      if (window.Capacitor && window.Capacitor.Plugins && window.Capacitor.Plugins.DeskNative) return;
      var listeners = {};
      function call(m, a) { return window.webkit.messageHandlers.deskNative.postMessage({ m: m, a: a || {} }); }
      window.__deskNativeEmit = function (ev, data) {
        (listeners[ev] || []).slice().forEach(function (f) { try { f(data || {}); } catch (e) {} });
      };
      window.Capacitor = {
        isNativePlatform: function () { return true; },
        getPlatform: function () { return 'ios'; },
        Plugins: { DeskNative: {
          haptic: function (a) { return call('haptic', a); },
          openExternal: function (a) { return call('openExternal', a); },
          deviceName: function () { return call('deviceName'); },
          speak: function (a) { return call('speak', a); },
          stop: function () { return call('stop'); },
          setBars: function (a) { return call('setBars', a); },
          addListener: function (ev, cb) {
            (listeners[ev] = listeners[ev] || []).push(cb);
            return Promise.resolve({ remove: function () {
              listeners[ev] = (listeners[ev] || []).filter(function (f) { return f !== cb; });
            } });
          }
        } }
      };
    })();
    """

    static func offlineHTML(retry: String, reason: String) -> String {
        let why = reason.replacingOccurrences(of: "&", with: "&amp;").replacingOccurrences(of: "<", with: "&lt;")
        return """
        <!doctype html><html lang="ko"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <style>
        body{margin:0;min-height:100vh;display:grid;place-items:center;background:#1B2A6B;color:#F3F4FF;
          font:16px/1.6 -apple-system,"Apple SD Gothic Neo",sans-serif;text-align:center;padding:24px;box-sizing:border-box}
        h1{font-size:20px;margin:0 0 8px}p{margin:0 0 6px;color:#AEB8E8}small{color:#8F99CC;font-size:12px}
        a{display:inline-block;margin-top:18px;padding:12px 28px;border-radius:99px;background:#E91E63;color:#fff;
          text-decoration:none;font-weight:700}
        </style></head><body><div>
        <h1>효딩쓰에 닿지 못했어요</h1>
        <p>인터넷 연결을 확인한 뒤 다시 시도해 주세요.</p>
        <small>\(why)</small><br>
        <a href="\(retry)">다시 시도</a>
        </div></body></html>
        """
    }
}
