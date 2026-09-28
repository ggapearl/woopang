package com.que.hyodingsseu;

import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.view.HapticFeedbackConstants;
import android.view.View;
import android.view.Window;

import androidx.core.view.WindowInsetsControllerCompat;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * 화면(web/app.js)이 window.Capacitor.Plugins.DeskNative 로 부른다.
 * 아이폰 앱의 Speaker(아이폰 목소리) · Haptics 에 해당하는 것.
 */
@CapacitorPlugin(name = "DeskNative")
public class DeskNativePlugin extends Plugin {
    private TextToSpeech tts;
    private volatile boolean ttsReady = false;
    private volatile String ttsError = null;
    private volatile String lastUtterance = null;

    @Override
    public void load() {
        tts = new TextToSpeech(getContext(), status -> {
            if (status != TextToSpeech.SUCCESS) {
                ttsError = "폰 목소리를 켤 수 없어요";
                return;
            }
            int r = tts.setLanguage(Locale.KOREAN);
            if (r == TextToSpeech.LANG_MISSING_DATA || r == TextToSpeech.LANG_NOT_SUPPORTED) {
                ttsError = "이 폰에 한국어 목소리가 없어요";
                return;
            }
            tts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                @Override
                public void onStart(String id) {}

                @Override
                public void onDone(String id) {
                    finished(id);
                }

                @Override
                public void onError(String id) {
                    finished(id);
                }

                @Override
                public void onStop(String id, boolean interrupted) {
                    finished(id);
                }
            });
            ttsReady = true;
        });
    }

    private void finished(String id) {
        // 긴 글은 여러 조각으로 나눠 읽는다 — 마지막 조각이 끝났을 때만 알린다
        if (id != null && id.equals(lastUtterance)) {
            notifyListeners("speechDone", new JSObject());
        }
    }

    /** 폰 목소리로 읽기 — { text, rate } (rate 1.0 이 보통) */
    @PluginMethod
    public void speak(PluginCall call) {
        String text = call.getString("text", "");
        if (!ttsReady) {
            call.reject(ttsError != null ? ttsError : "폰 목소리를 준비하는 중이에요");
            return;
        }
        if (text == null || text.trim().isEmpty()) {
            call.reject("읽을 글이 없어요");
            return;
        }
        float rate = call.getFloat("rate", 1.0f);
        tts.setSpeechRate(Math.max(0.5f, Math.min(2.0f, rate)));
        List<String> parts = split(text, Math.min(3500, TextToSpeech.getMaxSpeechInputLength() - 100));
        String base = "u" + System.nanoTime() + "-";
        lastUtterance = base + (parts.size() - 1);
        for (int i = 0; i < parts.size(); i++) {
            tts.speak(parts.get(i), i == 0 ? TextToSpeech.QUEUE_FLUSH : TextToSpeech.QUEUE_ADD, null, base + i);
        }
        call.resolve();
    }

    @PluginMethod
    public void stop(PluginCall call) {
        lastUtterance = null;
        if (tts != null) tts.stop();
        call.resolve();
    }

    /** 가벼운 진동 — { kind: "tap" | "alert" } (폰의 터치 진동 설정을 따른다) */
    @PluginMethod
    public void haptic(PluginCall call) {
        String kind = call.getString("kind", "tap");
        View v = getBridge().getWebView();
        getActivity().runOnUiThread(() -> {
            int c = HapticFeedbackConstants.KEYBOARD_TAP;
            if ("alert".equals(kind)) {
                c = Build.VERSION.SDK_INT >= Build.VERSION_CODES.R ? HapticFeedbackConstants.REJECT : HapticFeedbackConstants.LONG_PRESS;
            }
            v.performHapticFeedback(c);
        });
        call.resolve();
    }

    /** 답 속 링크는 앱 밖(브라우저·해당 앱)에서 연다 — 앱 웹뷰는 효딩쓰 화면만 띄운다 */
    @PluginMethod
    public void openExternal(PluginCall call) {
        String url = call.getString("url", "");
        Uri uri = Uri.parse(url);
        String scheme = uri.getScheme() == null ? "" : uri.getScheme().toLowerCase(Locale.ROOT);
        if (!(scheme.equals("https") || scheme.equals("http") || scheme.equals("mailto") || scheme.equals("tel"))) {
            call.reject("열 수 없는 주소예요");
            return;
        }
        try {
            Intent i = new Intent(Intent.ACTION_VIEW, uri);
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            getActivity().startActivity(i);
            call.resolve();
        } catch (Exception e) {
            call.reject("열 앱이 없어요");
        }
    }

    /** 짝 지을 때 PC 에 보여 줄 이름 — 폰 설정의 기기 이름(없으면 모델명) */
    @PluginMethod
    public void deviceName(PluginCall call) {
        String name = null;
        try {
            name = Settings.Global.getString(getContext().getContentResolver(), Settings.Global.DEVICE_NAME);
        } catch (Exception ignored) {
        }
        if (name == null || name.trim().isEmpty()) name = Build.MODEL;
        JSObject r = new JSObject();
        r.put("name", name);
        r.put("model", Build.MANUFACTURER + " " + Build.MODEL);
        call.resolve(r);
    }

    /** 상태바·내비게이션 막대 색 — 화면에서 라이트/다크를 바꿀 때 { status, nav, lightNav } */
    @PluginMethod
    public void setBars(PluginCall call) {
        String status = call.getString("status");
        String nav = call.getString("nav");
        boolean lightNav = Boolean.TRUE.equals(call.getBoolean("lightNav", false));
        getActivity().runOnUiThread(() -> {
            try {
                Window w = getActivity().getWindow();
                if (status != null) w.setStatusBarColor(Color.parseColor(status));
                if (nav != null) w.setNavigationBarColor(Color.parseColor(nav));
                WindowInsetsControllerCompat c = new WindowInsetsControllerCompat(w, w.getDecorView());
                c.setAppearanceLightStatusBars(false);
                c.setAppearanceLightNavigationBars(lightNav);
            } catch (IllegalArgumentException ignored) {
            }
        });
        call.resolve();
    }

    @Override
    protected void handleOnDestroy() {
        if (tts != null) {
            tts.stop();
            tts.shutdown();
            tts = null;
        }
    }

    /** 문장 끝(. ! ? 줄바꿈)에서 끊어 max 글자 이하 조각으로 */
    private static List<String> split(String text, int max) {
        List<String> out = new ArrayList<>();
        StringBuilder cur = new StringBuilder();
        StringBuilder sentence = new StringBuilder();
        for (int i = 0; i < text.length(); i++) {
            char ch = text.charAt(i);
            sentence.append(ch);
            if (".!?。\n".indexOf(ch) >= 0 || sentence.length() >= max) {
                if (cur.length() + sentence.length() > max && cur.length() > 0) {
                    out.add(cur.toString());
                    cur.setLength(0);
                }
                cur.append(sentence);
                sentence.setLength(0);
            }
        }
        if (cur.length() + sentence.length() > max && cur.length() > 0) {
            out.add(cur.toString());
            cur.setLength(0);
        }
        cur.append(sentence);
        if (cur.length() > 0) out.add(cur.toString());
        return out;
    }
}
