package com.que.woogy;

import android.Manifest;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.location.Location;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import androidx.browser.customtabs.CustomTabsIntent;
import androidx.core.app.ActivityCompat;
import androidx.core.app.NotificationManagerCompat;

import com.getcapacitor.JSObject;
import com.getcapacitor.PermissionState;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.getcapacitor.annotation.Permission;
import com.getcapacitor.annotation.PermissionCallback;
import com.google.android.gms.location.CurrentLocationRequest;
import com.google.android.gms.location.FusedLocationProviderClient;
import com.google.android.gms.location.LocationServices;
import com.google.android.gms.location.Priority;
import com.google.firebase.messaging.FirebaseMessaging;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

/**
 * 화면(static/native.js)이 window.Capacitor.Plugins.WoogyNative 를 window.WoogyApp.call(이름, 값) 으로 부른다.
 * 아이폰 앱(WoogyNative.swift)과 같은 이름 · 같은 답 모양:
 *   pushRegister()          → {granted, token?, platform: 'android', error?}   알림 허락(처음 한 번만 묻는다) · FCM 기기 토큰
 *   auth({url})             → {ok:true} | {ok:false, canceled:true}            간편로그인 — 시스템 브라우저 창(Custom Tabs)으로 열고, 끝나면 com.que.woogy://auth 로 돌아온다
 *   openExternal({url})     → {ok}                                             브라우저 · 전화 · 문자 · 메일로
 *   location()              → {ok:true, lat, lon, acc} | {ok:false, code}      지금 위치 한 번 (code 1 허락 안 함 · 2 못 찾음 · 3 시간 초과)
 *   cameraEnsure()          → {granted, status: authorized|denied|restricted}  카메라 허락 — 안 물었으면 한 번 묻고, 정했으면 묻지 않는다
 *   openSettings()          → {ok}                                             휴대폰 설정 › 앱 › 우기 (권한을 껐을 때 안내용)
 *   info()                  → {version, build, sdk}
 * 우기 화면(https://woogy.app/woogy…)의 맨 위 화면에서 부를 때만 준다.
 */
@CapacitorPlugin(
    name = "WoogyNative",
    permissions = {
        @Permission(alias = "notifications", strings = { "android.permission.POST_NOTIFICATIONS" }),
        @Permission(alias = "camera", strings = { Manifest.permission.CAMERA }),
        @Permission(alias = "location", strings = { Manifest.permission.ACCESS_COARSE_LOCATION, Manifest.permission.ACCESS_FINE_LOCATION })
    }
)
public class WoogyNativePlugin extends Plugin {
    private static final String PREFS = "woogy_native";

    /** 간편로그인 창을 열어 둔 호출 — 돌아오면(handleOnNewIntent) 끝내고, 그냥 창을 닫고 돌아오면(handleOnResume) 취소로 끝낸다 */
    private PluginCall pendingAuth;

    // ── 안전: 우기 화면의 맨 위 화면에서 부른 것만 ─────────────────────
    // 웹뷰의 주소는 화면 스레드에서만 읽을 수 있고 플러그인은 다른 스레드에서 불리므로, 화면 스레드에 잠깐 맡겨 읽는다.
    private String currentUrl() {
        final String[] out = new String[1];
        final CountDownLatch done = new CountDownLatch(1);
        getActivity().runOnUiThread(() -> {
            try {
                out[0] = getBridge().getWebView().getUrl();
            } finally {
                done.countDown();
            }
        });
        try {
            done.await(2, TimeUnit.SECONDS);
        } catch (InterruptedException ignored) {
            Thread.currentThread().interrupt();
        }
        return out[0];
    }

    private boolean fromOwnPage(PluginCall call) {
        String url = currentUrl();
        if (url != null && WoogyLinks.isOwn(Uri.parse(url))) return true;
        call.reject("이 화면에서는 쓸 수 없어요");
        return false;
    }

    private SharedPreferences prefs() {
        return getContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    // ── 알림 ───────────────────────────────────────────────────────────
    @PluginMethod
    public void pushRegister(PluginCall call) {
        if (!fromOwnPage(call)) return;
        WoogyMessagingService.ensureChannel(getContext());
        if (Build.VERSION.SDK_INT >= 33 && getPermissionState("notifications") != PermissionState.GRANTED) {
            // 아이폰과 같게 — 한 번 물었으면 다시 묻지 않고 지금 상태만 돌려준다
            if (prefs().getBoolean("asked_notifications", false)) {
                call.resolve(pushReply(false, null, null));
                return;
            }
            prefs().edit().putBoolean("asked_notifications", true).apply();
            requestPermissionForAlias("notifications", call, "pushPermResult");
            return;
        }
        fetchToken(call);
    }

    @PermissionCallback
    private void pushPermResult(PluginCall call) {
        if (getPermissionState("notifications") == PermissionState.GRANTED) fetchToken(call);
        else call.resolve(pushReply(false, null, null));
    }

    private void fetchToken(PluginCall call) {
        if (!NotificationManagerCompat.from(getContext()).areNotificationsEnabled()) {
            call.resolve(pushReply(false, null, null));
            return;
        }
        try {
            FirebaseMessaging.getInstance().getToken().addOnCompleteListener(task -> {
                if (task.isSuccessful() && task.getResult() != null) {
                    call.resolve(pushReply(true, task.getResult(), null));
                } else {
                    Exception e = task.getException();
                    call.resolve(pushReply(true, null, e == null ? "기기 토큰을 받지 못했어요" : e.getClass().getSimpleName()));
                }
            });
        } catch (IllegalStateException e) {
            // google-services.json 이 없는 빌드 — 알림함만 쓴다
            call.resolve(pushReply(true, null, "firebase-not-configured"));
        }
    }

    private static JSObject pushReply(boolean granted, String token, String error) {
        JSObject r = new JSObject();
        r.put("granted", granted);
        r.put("platform", "android");
        if (token != null) r.put("token", token);
        if (error != null) r.put("error", error);
        return r;
    }

    // ── 간편로그인 ─────────────────────────────────────────────────────
    @PluginMethod
    public void auth(PluginCall call) {
        if (!fromOwnPage(call)) return;
        String s = call.getString("url", "");
        Uri url = Uri.parse(s == null ? "" : s);
        if (!WoogyLinks.isOwn(url)) {
            call.reject("로그인 주소가 잘못됐어요");
            return;
        }
        if (pendingAuth != null) finishAuth(false);
        pendingAuth = call;
        call.setKeepAlive(true);
        try {
            CustomTabsIntent tab = new CustomTabsIntent.Builder().setShowTitle(true).build();
            tab.launchUrl(getActivity(), url);
        } catch (Exception e) {
            pendingAuth = null;
            call.setKeepAlive(false);
            call.reject("로그인 창을 열 수 없어요");
        }
    }

    /** ok=true: 서버가 토큰을 들려 돌려보냄(화면 열기는 MainActivity) · false: 창을 닫고 돌아옴(취소) */
    private void finishAuth(boolean ok) {
        PluginCall c = pendingAuth;
        pendingAuth = null;
        if (c == null) return;
        JSObject r = new JSObject();
        r.put("ok", ok);
        if (!ok) r.put("canceled", true);
        c.resolve(r);
    }

    @Override
    protected void handleOnNewIntent(Intent intent) {
        super.handleOnNewIntent(intent);
        if (pendingAuth != null && WoogyLinks.authExchange(intent == null ? null : intent.getData()) != null) finishAuth(true);
    }

    @Override
    protected void handleOnResume() {
        super.handleOnResume();
        // 로그인 창을 그냥 닫고 돌아왔다 — 복귀 주소가 왔다면 handleOnNewIntent 가 먼저(onNewIntent → onResume 순서) 끝냈다
        if (pendingAuth != null) finishAuth(false);
    }

    // ── 밖으로 열기 · 설정 ─────────────────────────────────────────────
    @PluginMethod
    public void openExternal(PluginCall call) {
        if (!fromOwnPage(call)) return;
        Uri u = Uri.parse(call.getString("url", ""));
        String sc = u.getScheme() == null ? "" : u.getScheme().toLowerCase();
        if (!sc.equals("https") && !sc.equals("http") && !sc.equals("tel") && !sc.equals("sms") && !sc.equals("mailto")) {
            call.reject("열 수 없는 주소예요");
            return;
        }
        JSObject r = new JSObject();
        try {
            getActivity().startActivity(new Intent(sc.equals("tel") ? Intent.ACTION_DIAL : Intent.ACTION_VIEW, u));
            r.put("ok", true);
        } catch (Exception e) {
            r.put("ok", false);
        }
        call.resolve(r);
    }

    @PluginMethod
    public void openSettings(PluginCall call) {
        if (!fromOwnPage(call)) return;
        JSObject r = new JSObject();
        try {
            Intent i = new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:" + getContext().getPackageName()));
            getActivity().startActivity(i);
            r.put("ok", true);
        } catch (Exception e) {
            r.put("ok", false);
        }
        call.resolve(r);
    }

    // ── 카메라 ─────────────────────────────────────────────────────────
    @PluginMethod
    public void cameraEnsure(PluginCall call) {
        if (!fromOwnPage(call)) return;
        if (cameraGranted()) {
            call.resolve(cameraReply(true, "authorized"));
            return;
        }
        // 아이폰과 같게 — 안 물었으면 한 번 묻고, 이미 물었으면 묻지 않고 결과만 알려 준다(꺼져 있으면 웹이 설정 안내 문구를 보인다)
        if (prefs().getBoolean("asked_camera", false)) {
            call.resolve(cameraReply(false, "denied"));
            return;
        }
        prefs().edit().putBoolean("asked_camera", true).apply();
        requestPermissionForAlias("camera", call, "cameraPermResult");
    }

    @PermissionCallback
    private void cameraPermResult(PluginCall call) {
        boolean ok = cameraGranted();
        call.resolve(cameraReply(ok, ok ? "authorized" : "denied"));
    }

    private boolean cameraGranted() {
        return ActivityCompat.checkSelfPermission(getContext(), Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
    }

    private static JSObject cameraReply(boolean granted, String status) {
        JSObject r = new JSObject();
        r.put("granted", granted);
        r.put("status", status);
        return r;
    }

    // ── 위치 ───────────────────────────────────────────────────────────
    @PluginMethod
    public void location(PluginCall call) {
        if (!fromOwnPage(call)) return;
        if (!locationGranted()) {
            requestPermissionForAlias("location", call, "locationPermResult");
            return;
        }
        findLocation(call);
    }

    @PermissionCallback
    private void locationPermResult(PluginCall call) {
        if (locationGranted()) findLocation(call);
        else call.resolve(locReply(null, 1));
    }

    private boolean locationGranted() {
        Context c = getContext();
        return ActivityCompat.checkSelfPermission(c, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED
            || ActivityCompat.checkSelfPermission(c, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED;
    }

    @SuppressWarnings("MissingPermission")
    private void findLocation(PluginCall call) {
        boolean fine = ActivityCompat.checkSelfPermission(getContext(), Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED;
        FusedLocationProviderClient client = LocationServices.getFusedLocationProviderClient(getContext());
        CurrentLocationRequest req = new CurrentLocationRequest.Builder()
            .setPriority(fine ? Priority.PRIORITY_HIGH_ACCURACY : Priority.PRIORITY_BALANCED_POWER_ACCURACY)
            .setDurationMillis(15000)
            .setMaxUpdateAgeMillis(60000)
            .build();
        try {
            client.getCurrentLocation(req, null)
                .addOnSuccessListener(loc -> {
                    if (loc != null) {
                        call.resolve(locReply(loc, 0));
                        return;
                    }
                    // 방금 값이 없으면 마지막으로 알려진 위치로 한 번 더
                    client.getLastLocation()
                        .addOnSuccessListener(last -> call.resolve(last != null ? locReply(last, 0) : locReply(null, 2)))
                        .addOnFailureListener(e -> call.resolve(locReply(null, 2)));
                })
                .addOnFailureListener(e -> call.resolve(locReply(null, 2)));
        } catch (Exception e) {
            call.resolve(locReply(null, 2));
        }
    }

    private static JSObject locReply(Location loc, int code) {
        JSObject r = new JSObject();
        if (loc != null) {
            r.put("ok", true);
            r.put("lat", loc.getLatitude());
            r.put("lon", loc.getLongitude());
            r.put("acc", loc.hasAccuracy() ? (double) loc.getAccuracy() : 0d);
        } else {
            r.put("ok", false);
            r.put("code", code);
        }
        return r;
    }

    // ── 앱 정보 ────────────────────────────────────────────────────────
    @PluginMethod
    public void info(PluginCall call) {
        JSObject r = new JSObject();
        r.put("version", BuildConfig.VERSION_NAME);
        r.put("build", BuildConfig.VERSION_CODE);
        r.put("sdk", Build.VERSION.SDK_INT);
        call.resolve(r);
    }
}
