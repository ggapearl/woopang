package com.que.woogy;

import android.content.Intent;
import android.graphics.Canvas;
import android.graphics.ColorFilter;
import android.graphics.Paint;
import android.graphics.PixelFormat;
import android.graphics.Rect;
import android.graphics.drawable.Drawable;
import android.net.Uri;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.webkit.WebView;

import androidx.annotation.NonNull;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;

import com.getcapacitor.BridgeActivity;

/**
 * 우기 — 화면은 https://woopang.com/woogy/ (capacitor.config.json 의 server.url).
 * 알림 · 간편로그인 · 위치 · 카메라 허락은 WoogyNativePlugin(웹의 window.WoogyApp, static/native.js) 이 맡는다.
 * WebViewClient 를 바꾸지 말 것 — 원격 화면에 Capacitor 브리지를 넣고 tel:·바깥 링크를 폰 앱으로 넘기는 게 BridgeWebViewClient 다.
 */
public class MainActivity extends BridgeActivity {
    private static final int YELLOW = 0xFFFFE98A;     // 웹 머리 막대(--y3)
    private static final int WHITE = 0xFFFFFFFF;      // 웹 아래 탭 막대

    @Override
    public void onCreate(Bundle savedInstanceState) {
        registerPlugin(WoogyNativePlugin.class);
        super.onCreate(savedInstanceState);
        WoogyMessagingService.ensureChannel(this);

        // targetSdk 36(안드로이드 16)은 화면 끝까지 그리기가 강제라, 모든 버전에서 같은 방식으로 맞춘다:
        // 웹뷰를 상태바·내비게이션바·키보드만큼 안쪽으로 밀고, 그 틈은 창 배경이 채운다 — 위(상태바)는 머리 막대 노랑, 아래(내비바)는 탭 막대 흰색.
        Window window = getWindow();
        WindowCompat.setDecorFitsSystemWindows(window, false);
        WindowInsetsControllerCompat bars = WindowCompat.getInsetsController(window, window.getDecorView());
        bars.setAppearanceLightStatusBars(true);       // 밝은 바탕 → 어두운 아이콘
        bars.setAppearanceLightNavigationBars(true);

        final Backdrop backdrop = new Backdrop();
        window.setBackgroundDrawable(backdrop);
        View content = findViewById(android.R.id.content);
        ViewCompat.setOnApplyWindowInsetsListener(content, (v, insets) -> {
            Insets sys = insets.getInsets(WindowInsetsCompat.Type.systemBars() | WindowInsetsCompat.Type.displayCutout());
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            v.setPadding(sys.left, sys.top, sys.right, Math.max(sys.bottom, ime.bottom));
            backdrop.setTop(sys.top);
            return WindowInsetsCompat.CONSUMED;
        });

        open(getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        open(intent);
    }

    /** 로그인 복귀(com.que.woogy://auth) · 공유 링크 · 알림 누름 → 그 우기 화면을 연다 */
    private void open(Intent in) {
        final Uri to = WoogyLinks.target(in);
        if (to == null) return;
        // 한 번 쓴 토큰·링크가 다시 열리지 않게(화면 돌림 · 다시 그리기) Intent 에서 지운다
        in.setData(null);
        in.removeExtra("link");
        getWindow().getDecorView().post(() -> {
            if (bridge == null) return;
            WebView wv = bridge.getWebView();
            if (wv != null) wv.loadUrl(to.toString());
        });
    }

    /** 상태바 자리는 노랑 · 나머지는 흰색 — 안쪽으로 밀린 만큼(setTop)만 노랑으로 칠한다 */
    private static final class Backdrop extends Drawable {
        private final Paint top = new Paint();
        private final Paint rest = new Paint();
        private int topPx;

        Backdrop() {
            top.setColor(YELLOW);
            rest.setColor(WHITE);
        }

        void setTop(int px) {
            if (px != topPx) {
                topPx = px;
                invalidateSelf();
            }
        }

        @Override
        public void draw(@NonNull Canvas c) {
            Rect b = getBounds();
            c.drawRect(b.left, b.top, b.right, b.bottom, rest);
            c.drawRect(b.left, b.top, b.right, b.top + topPx, top);
        }

        @Override public void setAlpha(int alpha) {}
        @Override public void setColorFilter(ColorFilter cf) {}
        @Override public int getOpacity() { return PixelFormat.OPAQUE; }
    }
}
