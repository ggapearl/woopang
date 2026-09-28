package com.que.hyodingsseu;

import android.os.Bundle;

import com.getcapacitor.BridgeActivity;

/**
 * 효딩쓰 안드로이드 — 화면은 https://woopang.com/AI/desk-app/ (capacitor.config.json 의 server.url).
 * 폰에서만 되는 것(폰 목소리·진동·밖으로 링크 열기)은 DeskNativePlugin 이 맡는다.
 * WebViewClient 를 바꾸지 말 것 — 원격 화면에 Capacitor 브리지를 넣는 게 BridgeWebViewClient 다.
 */
public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        registerPlugin(DeskNativePlugin.class);
        super.onCreate(savedInstanceState);
    }
}
