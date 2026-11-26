package com.que.woopang;

import android.Manifest;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.SystemClock;

/**
 * 우팡 길안내 — 홈으로 나가도 알림창 · 위쪽 알림줄에 「단골 카페까지 350m」 (11-26 대표님 10번 ② · 시안 우팡_노치_시안_1124).
 * 위치 포그라운드 서비스 + 진행 중 알림. 안내하는 동안만 돈다 — 도착(10m 안 1초) · 끝내기 · 20분이면 바로 끈다.
 * 유니티(R0926LiveGuide.cs)가 begin / end 를 부른다. 앱이 화면에 있을 때 시작한다 (안드로이드 14 는 그때만 위치 서비스를 열어 준다).
 * ⚠ 켜기 전에 Play Console 「앱 콘텐츠 → 포그라운드 서비스 권한」 신고(위치 · 영상)가 먼저다 — README.md
 */
public class WoopangGuideService extends Service implements LocationListener {
    private static final String CHANNEL = "woopang_guide";
    private static final int NOTE_ID = 0x5747;
    private static final String ACTION_START = "com.que.woopang.guide.START";
    private static final String ACTION_ARRIVED = "com.que.woopang.guide.ARRIVED";
    private static final String ACTION_END = "com.que.woopang.guide.END";
    private static final long ARRIVE_HOLD_MS = 1000, ARRIVE_SHOW_MS = 4000;

    private static volatile boolean running;

    private final Handler main = new Handler(Looper.getMainLooper());
    private final Runnable timeUp = this::stopNow;
    private LocationManager lm;
    private String name = "";
    private double lat, lon, arriveM = 10, firstDist = -1;
    private long nearSince = -1;
    private boolean arrived;
    private String sentText = "";
    private int sentProgress = -1;

    // ── 유니티가 부른다 ───────────────────────────────────────

    public static boolean begin(Context ctx, String name, double lat, double lon, double arriveM, double maxSec) {
        if (!declared(ctx) || !locationAllowed(ctx)) return false;
        Intent i = new Intent(ctx, WoopangGuideService.class).setAction(ACTION_START)
                .putExtra("name", name == null ? "" : name).putExtra("lat", lat).putExtra("lon", lon)
                .putExtra("arrive", arriveM).putExtra("max", maxSec);
        try {
            if (Build.VERSION.SDK_INT >= 26) ctx.startForegroundService(i);
            else ctx.startService(i);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    public static void end(Context ctx, boolean arrived) {
        if (!running) return;
        try {
            if (arrived) ctx.startService(new Intent(ctx, WoopangGuideService.class).setAction(ACTION_ARRIVED));
            else ctx.stopService(new Intent(ctx, WoopangGuideService.class));
        } catch (Exception ignored) {
        }
    }

    // 매니페스트에 서비스 · 위치 포그라운드 권한이 들어 있을 때만 (README 의 순서대로 켜기 전에는 아무것도 하지 않는다)
    private static boolean declared(Context ctx) {
        try {
            ctx.getPackageManager().getServiceInfo(new ComponentName(ctx, WoopangGuideService.class), 0);
        } catch (PackageManager.NameNotFoundException e) {
            return false;
        }
        return Build.VERSION.SDK_INT < 34 || ctx.checkSelfPermission("android.permission.FOREGROUND_SERVICE_LOCATION") == PackageManager.PERMISSION_GRANTED;
    }

    private static boolean locationAllowed(Context ctx) {
        if (Build.VERSION.SDK_INT < 23) return true;
        return ctx.checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED
                || ctx.checkSelfPermission(Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED;
    }

    // ── 서비스 ─────────────────────────────────────────────

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String action = intent != null ? intent.getAction() : null;
        if (ACTION_END.equals(action)) {
            stopNow();
            return START_NOT_STICKY;
        }
        if (ACTION_ARRIVED.equals(action)) {
            if (running) showArrived();
            return START_NOT_STICKY;
        }
        if (intent == null || !ACTION_START.equals(action)) {
            stopNow();
            return START_NOT_STICKY;
        }

        name = intent.getStringExtra("name");
        if (name == null || name.isEmpty()) name = "우팡 길안내";
        lat = intent.getDoubleExtra("lat", 0);
        lon = intent.getDoubleExtra("lon", 0);
        arriveM = Math.max(1, intent.getDoubleExtra("arrive", 10));
        double maxSec = intent.getDoubleExtra("max", 1200);
        firstDist = -1;
        nearSince = -1;
        arrived = false;
        sentText = "";
        sentProgress = -1;

        channel();
        Notification n = note(name + "까지 …", "우팡 길안내 · 누르면 앱으로", -1);
        try {
            if (Build.VERSION.SDK_INT >= 29) startForeground(NOTE_ID, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION);
            else startForeground(NOTE_ID, n);
        } catch (Exception e) {   // 안드로이드 14 — 앱이 화면에 없거나 위치 허락이 없으면 거절된다
            stopNow();
            return START_NOT_STICKY;
        }
        running = true;
        main.removeCallbacks(timeUp);
        main.postDelayed(timeUp, (long) (Math.max(60, maxSec) * 1000));
        startLocation();
        return START_NOT_STICKY;
    }

    @Override
    public void onTaskRemoved(Intent rootIntent) {
        stopNow();   // 앱을 최근 앱에서 밀어 지우면 같이 끝
    }

    @Override
    public void onDestroy() {
        running = false;
        main.removeCallbacksAndMessages(null);
        stopLocation();
        if (Build.VERSION.SDK_INT >= 24) stopForeground(STOP_FOREGROUND_REMOVE);
        else stopForeground(true);
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    private void stopNow() {
        running = false;
        stopSelf();
    }

    // ── 위치 ─────────────────────────────────────────────

    private void startLocation() {
        stopLocation();
        lm = (LocationManager) getSystemService(Context.LOCATION_SERVICE);
        if (lm == null) return;
        try {
            if (lm.isProviderEnabled(LocationManager.GPS_PROVIDER))
                lm.requestLocationUpdates(LocationManager.GPS_PROVIDER, 2000, 3, this, Looper.getMainLooper());
            if (lm.isProviderEnabled(LocationManager.NETWORK_PROVIDER))
                lm.requestLocationUpdates(LocationManager.NETWORK_PROVIDER, 2000, 3, this, Looper.getMainLooper());
            Location last = lm.getLastKnownLocation(LocationManager.GPS_PROVIDER);
            if (last != null && SystemClock.elapsedRealtimeNanos() - last.getElapsedRealtimeNanos() < 30_000_000_000L) onLocationChanged(last);
        } catch (SecurityException ignored) {
        }
    }

    private void stopLocation() {
        if (lm != null) {
            try {
                lm.removeUpdates(this);
            } catch (Exception ignored) {
            }
        }
        lm = null;
    }

    @Override
    public void onLocationChanged(Location l) {
        if (!running || arrived || l == null) return;
        float[] r = new float[2];
        Location.distanceBetween(l.getLatitude(), l.getLongitude(), lat, lon, r);
        double d = r[0];
        if (firstDist < 0) firstDist = Math.max(d, 1);
        long now = SystemClock.elapsedRealtime();
        if (d <= arriveM) {
            if (nearSince < 0) nearSince = now;
            if (now - nearSince >= ARRIVE_HOLD_MS) {
                showArrived();
                return;
            }
        } else nearSince = -1;
        int progress = (int) Math.round(Math.max(0, Math.min(1, 1 - d / firstDist)) * 100);
        String text = name + "까지 " + dist(d) + " · " + compass(r[1]);
        if (text.equals(sentText) && progress == sentProgress) return;
        sentText = text;
        sentProgress = progress;
        notifyNote(note(text, "우팡 길안내 · 누르면 앱으로", progress));
    }

    @Override public void onStatusChanged(String provider, int status, Bundle extras) {}
    @Override public void onProviderEnabled(String provider) {}
    @Override public void onProviderDisabled(String provider) {}

    private void showArrived() {
        if (arrived) return;
        arrived = true;
        stopLocation();
        notifyNote(note("도착했어요", name, 100));
        main.removeCallbacks(timeUp);
        main.postDelayed(timeUp, ARRIVE_SHOW_MS);   // 「도착했어요」를 4초 보이고 닫는다 (아이폰과 같게)
    }

    // ── 알림 ─────────────────────────────────────────────

    private void channel() {
        if (Build.VERSION.SDK_INT < 26) return;
        NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null || nm.getNotificationChannel(CHANNEL) != null) return;
        NotificationChannel c = new NotificationChannel(CHANNEL, "길안내", NotificationManager.IMPORTANCE_LOW);
        c.setDescription("길안내 중 남은 거리");
        c.setShowBadge(false);
        c.setSound(null, null);
        c.enableVibration(false);
        nm.createNotificationChannel(c);
    }

    private Notification note(String title, String text, int progress) {
        Notification.Builder b = Build.VERSION.SDK_INT >= 26 ? new Notification.Builder(this, CHANNEL) : new Notification.Builder(this);
        int icon = getResources().getIdentifier("ic_stat_woopang", "drawable", getPackageName());
        b.setSmallIcon(icon != 0 ? icon : getApplicationInfo().icon)
                .setContentTitle(title)
                .setContentText(text)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .setShowWhen(false)
                .setColor(0xFFE95383)
                .setCategory(Notification.CATEGORY_NAVIGATION);
        if (progress >= 0) b.setProgress(100, progress, false);
        Intent open = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (open != null) {
            open.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
            b.setContentIntent(PendingIntent.getActivity(this, 0, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE));
        }
        if (!arrived) {
            Intent stop = new Intent(this, WoopangGuideService.class).setAction(ACTION_END);
            b.addAction(new Notification.Action.Builder(null, "안내 끝내기",
                    PendingIntent.getService(this, 1, stop, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE)).build());
        }
        if (Build.VERSION.SDK_INT >= 31) b.setForegroundServiceBehavior(Notification.FOREGROUND_SERVICE_IMMEDIATE);
        return b.build();
    }

    private void notifyNote(Notification n) {
        NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) nm.notify(NOTE_ID, n);
    }

    // R0926Notch.Dist 와 같은 표기
    private static String dist(double m) {
        if (m < 100) return Math.round(m) + "m";
        if (m < 995) return (Math.round(m / 10) * 10) + "m";
        if (m < 9950) return String.format(java.util.Locale.US, "%.1fkm", m / 1000);
        return Math.round(m / 1000) + "km";
    }

    private static String compass(float bearing) {
        String[] w = {"북쪽", "북동쪽", "동쪽", "남동쪽", "남쪽", "남서쪽", "서쪽", "북서쪽"};
        int i = (int) Math.floor(((bearing % 360 + 360) % 360 + 22.5) / 45) % 8;
        return w[i];
    }
}
