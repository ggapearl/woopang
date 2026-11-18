package com.que.woogy;

import android.Manifest;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Build;

import androidx.core.app.ActivityCompat;
import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;

import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;

import java.util.Map;

/**
 * 알림(FCM). 서버(wg_push.py)는 notification + data.link 로 보낸다.
 *  - 앱이 꺼져 있거나 뒤에 있을 때: 시스템이 알림을 직접 띄운다(채널 woogy_default · 작은 아이콘 · 색은 Manifest 의 meta-data).
 *    누르면 MainActivity 가 data.link 를 받아 그 화면을 연다(WoogyLinks.target).
 *  - 앱이 켜져 있을 때: 시스템은 띄우지 않고 여기로 온다 → 같은 모양으로 직접 띄운다.
 */
public class WoogyMessagingService extends FirebaseMessagingService {
    static final String CHANNEL = "woogy_default";

    static void ensureChannel(Context c) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return;
        NotificationManager nm = c.getSystemService(NotificationManager.class);
        if (nm == null || nm.getNotificationChannel(CHANNEL) != null) return;
        NotificationChannel ch = new NotificationChannel(CHANNEL, c.getString(R.string.notif_channel_name), NotificationManager.IMPORTANCE_HIGH);
        ch.setDescription(c.getString(R.string.notif_channel_desc));
        nm.createNotificationChannel(ch);
    }

    @Override
    public void onMessageReceived(RemoteMessage m) {
        Map<String, String> data = m.getData();
        String title = m.getNotification() != null ? m.getNotification().getTitle() : data.get("title");
        String body = m.getNotification() != null ? m.getNotification().getBody() : data.get("body");
        if ((title == null || title.isEmpty()) && (body == null || body.isEmpty())) return;
        if (Build.VERSION.SDK_INT >= 33
                && ActivityCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) return;
        ensureChannel(this);

        Intent open = new Intent(this, MainActivity.class).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        String link = data.get("link");
        if (link != null && !link.isEmpty()) open.putExtra("link", link);
        int id = (int) (System.currentTimeMillis() & 0x7fffffff);
        PendingIntent pi = PendingIntent.getActivity(this, id, open, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);

        NotificationCompat.Builder b = new NotificationCompat.Builder(this, CHANNEL)
                .setSmallIcon(R.drawable.ic_stat_woogy)
                .setColor(getColor(R.color.colorAccent))
                .setContentTitle(title == null ? "" : title)
                .setContentText(body == null ? "" : body)
                .setStyle(new NotificationCompat.BigTextStyle().bigText(body == null ? "" : body))
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setDefaults(NotificationCompat.DEFAULT_ALL)
                .setAutoCancel(true)
                .setContentIntent(pi);
        NotificationManagerCompat.from(this).notify(id, b.build());
    }

    /** 새 기기 토큰 — 서버로는 로그인한 화면이 다음에 알림을 켤 때(pushRegister) 올린다. 토큰은 앱을 지웠다 깔 때 말고는 거의 바뀌지 않는다. */
    @Override
    public void onNewToken(String token) {
        super.onNewToken(token);
    }
}
