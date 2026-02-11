package com.que.woopang;

import android.util.Log;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Intent;
import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import androidx.core.app.NotificationCompat;
import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;
import com.unity3d.player.UnityPlayer;
import org.json.JSONArray;
import org.json.JSONObject;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.ArrayList;

public class CustomFirebaseMessagingService extends FirebaseMessagingService {
    private static final String TAG = "CustomFCMService";
    private static final String CHANNEL_ID = "default_channel";
    private static final String PREFS_NAME = "firebase_messages";
    private static final String PENDING_MESSAGES_KEY = "pending_messages";
    
    // 🆕 알림 관리를 위한 변수들
    private static final int MAX_ACTIVE_NOTIFICATIONS = 3; // Native에서는 더 적게
    private static ArrayList<Integer> activeNotificationIds = new ArrayList<>();

    @Override
    public void onCreate() {
        super.onCreate();
        createNotificationChannel();
    }

    @Override
    public void onMessageReceived(RemoteMessage remoteMessage) {
        super.onMessageReceived(remoteMessage);
        
        String title = "";
        String body = "";
        String messageId = remoteMessage.getMessageId();
        
        String latitude = "";
        String longitude = "";
        String radius = "";
        
        if (remoteMessage.getNotification() != null) {
            title = remoteMessage.getNotification().getTitle();
            body = remoteMessage.getNotification().getBody();
        }
        
        if (remoteMessage.getData().size() > 0) {
            if ((title == null || title.isEmpty()) && remoteMessage.getData().containsKey("title")) {
                title = remoteMessage.getData().get("title");
            }
            if ((body == null || body.isEmpty()) && remoteMessage.getData().containsKey("body")) {
                body = remoteMessage.getData().get("body");
            }
            
            if (remoteMessage.getData().containsKey("latitude")) {
                latitude = remoteMessage.getData().get("latitude");
            }
            if (remoteMessage.getData().containsKey("longitude")) {
                longitude = remoteMessage.getData().get("longitude");
            }
            if (remoteMessage.getData().containsKey("radius")) {
                radius = remoteMessage.getData().get("radius");
            }
            
            if (remoteMessage.getData().containsKey("message_id")) {
                messageId = remoteMessage.getData().get("message_id");
            }
        }
        
        if (title == null || title.isEmpty()) title = "알림";
        if (body == null || body.isEmpty()) body = "새 메시지가 도착했습니다.";
        if (messageId == null || messageId.isEmpty()) {
            messageId = String.valueOf(System.currentTimeMillis());
        }
        
        String originalTitle = title;
        if (!latitude.isEmpty() && !longitude.isEmpty()) {
            try {
                double targetLat = Double.parseDouble(latitude);
                double targetLon = Double.parseDouble(longitude);
                
                double[] currentLocation = getCurrentLocation();
                if (currentLocation != null) {
                    double distance = calculateDistance(currentLocation[0], currentLocation[1], targetLat, targetLon);
                    String distanceStr = formatDistance(distance);
                    title = originalTitle + " - " + distanceStr;
                }
            } catch (Exception e) {
                // 거리 계산 실패해도 계속 진행
            }
        }
        
        boolean unitySent = tryToSendToUnity(title, body, latitude, longitude, radius);
        
        if (!unitySent) {
            saveMessageToNativeStorage(title, body, messageId, remoteMessage);
        }
        
        // 🆕 스마트 알림 표시 (제한된 개수만)
        showSmartNotification(title, body);
    }
    
    private double calculateDistance(double lat1, double lon1, double lat2, double lon2) {
        final double R = 6371000;
        
        double latDistance = Math.toRadians(lat2 - lat1);
        double lonDistance = Math.toRadians(lon2 - lon1);
        
        double a = Math.sin(latDistance / 2) * Math.sin(latDistance / 2)
                + Math.cos(Math.toRadians(lat1)) * Math.cos(Math.toRadians(lat2))
                * Math.sin(lonDistance / 2) * Math.sin(lonDistance / 2);
        
        double c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        
        return R * c;
    }
    
    private String formatDistance(double distanceInMeters) {
        if (distanceInMeters < 1000) {
            return Math.round(distanceInMeters) + "m";
        } else if (distanceInMeters < 10000) {
            return String.format("%.1fkm", distanceInMeters / 1000.0);
        } else {
            return Math.round(distanceInMeters / 1000.0) + "km";
        }
    }
    
    private double[] getCurrentLocation() {
        try {
            SharedPreferences prefs = getSharedPreferences("firebase_messages", Context.MODE_PRIVATE);
            
            String lastLatStr = prefs.getString("last_latitude", "");
            String lastLonStr = prefs.getString("last_longitude", "");
            
            if (!lastLatStr.isEmpty() && !lastLonStr.isEmpty()) {
                double lat = Double.parseDouble(lastLatStr);
                double lon = Double.parseDouble(lastLonStr);
                return new double[]{lat, lon};
            }
        } catch (Exception e) {
            // 위치 정보 없으면 null 반환
        }
        
        return null;
    }
    
    private boolean tryToSendToUnity(String title, String body, String latitude, String longitude, String radius) {
        try {
            if (UnityPlayer.currentActivity == null) {
                return false;
            }
            
            String unityMessage;
            if (!latitude.isEmpty() && !longitude.isEmpty()) {
                unityMessage = title + "|" + body + "|" + latitude + "|" + longitude + "|" + 
                              (radius.isEmpty() ? "0" : radius);
            } else {
                unityMessage = title + "|" + body;
            }
            
            UnityPlayer.UnitySendMessage("FirebaseManager", "OnNativeMessageReceived", unityMessage);
            
            Thread.sleep(100);
            return true;
            
        } catch (Exception e) {
            return false;
        }
    }
    
    private void saveMessageToNativeStorage(String title, String body, String messageId, RemoteMessage remoteMessage) {
        try {
            SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
            
            String existingMessagesJson = prefs.getString(PENDING_MESSAGES_KEY, "[]");
            JSONArray messagesArray = new JSONArray(existingMessagesJson);
            
            for (int i = 0; i < messagesArray.length(); i++) {
                JSONObject existingMessage = messagesArray.getJSONObject(i);
                if (messageId.equals(existingMessage.optString("messageId", ""))) {
                    return;
                }
            }
            
            JSONObject newMessage = new JSONObject();
            newMessage.put("title", title);
            newMessage.put("body", body);
            newMessage.put("messageId", messageId);
            newMessage.put("timestampString", getCurrentTimestamp());
            newMessage.put("receivedAtString", getCurrentTimestamp());
            newMessage.put("isRead", false);
            newMessage.put("message", "[" + title + "] " + body);
            
            if (remoteMessage.getData().containsKey("latitude")) {
                newMessage.put("messageLat", Float.parseFloat(remoteMessage.getData().get("latitude")));
                newMessage.put("messageLon", Float.parseFloat(remoteMessage.getData().get("longitude")));
                newMessage.put("messageRadius", Float.parseFloat(remoteMessage.getData().getOrDefault("radius", "0")));
            } else {
                newMessage.put("messageLat", 0.0f);
                newMessage.put("messageLon", 0.0f);
                newMessage.put("messageRadius", 0.0f);
            }
            newMessage.put("currentDistance", "");
            
            JSONArray newMessagesArray = new JSONArray();
            newMessagesArray.put(newMessage);
            
            int maxMessages = 30;
            for (int i = 0; i < Math.min(messagesArray.length(), maxMessages - 1); i++) {
                newMessagesArray.put(messagesArray.getJSONObject(i));
            }
            
            SharedPreferences.Editor editor = prefs.edit();
            editor.putString(PENDING_MESSAGES_KEY, newMessagesArray.toString());
            editor.apply();
            
        } catch (Exception e) {
            Log.e(TAG, "Failed to save message to Native storage: " + e.getMessage());
        }
    }
    
    private String getCurrentTimestamp() {
        SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd HH:mm:ss", Locale.getDefault());
        return sdf.format(new Date());
    }
    
    @Override
    public void onNewToken(String token) {
        super.onNewToken(token);
        
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        SharedPreferences.Editor editor = prefs.edit();
        editor.putString("FCMToken", token);
        editor.apply();
        
        try {
            UnityPlayer.UnitySendMessage("FirebaseManager", "OnNativeTokenReceived", token);
        } catch (Exception e) {
            Log.e(TAG, "Failed to send token to Unity: " + e.getMessage());
        }
    }
    
    // 🆕 스마트 알림 표시 (제한된 개수만)
    private void showSmartNotification(String title, String body) {
        try {
            NotificationManager notificationManager = 
                (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        
            // 🆕 오래된 알림 정리
            manageActiveNotifications();
        
            Intent intent = new Intent(this, com.unity3d.player.UnityPlayerActivity.class);
            intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        
            int notificationId = (int) System.currentTimeMillis();
            PendingIntent pendingIntent = PendingIntent.getActivity(
                this, 
                notificationId,
                intent, 
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
            );
        
            NotificationCompat.Builder notificationBuilder = 
                new NotificationCompat.Builder(this, CHANNEL_ID)
                    .setSmallIcon(android.R.drawable.ic_dialog_info)
                    .setContentTitle(title)
                    .setContentText(body)
                    .setAutoCancel(true)
                    .setPriority(NotificationCompat.PRIORITY_HIGH)
                    .setDefaults(NotificationCompat.DEFAULT_ALL)
                    .setContentIntent(pendingIntent)
                    // 🆕 자동 만료 시간 설정 (2분)
                    .setTimeoutAfter(120000) // 2분 = 120초 = 120000ms
                    // 🆕 알림 그룹화
                    .setGroup("woopang_messages");
        
            notificationManager.notify(notificationId, notificationBuilder.build());
            
            // 🆕 활성 알림 ID 추가
            activeNotificationIds.add(notificationId);
        
        } catch (Exception e) {
            Log.e(TAG, "Failed to show smart notification: " + e.getMessage());
        }
    }

    // 🆕 활성 알림 관리
    private void manageActiveNotifications() {
        // 최대 개수 초과 시 오래된 알림 삭제
        while (activeNotificationIds.size() >= MAX_ACTIVE_NOTIFICATIONS) {
            Integer oldestId = activeNotificationIds.get(0);
            
            NotificationManager notificationManager = 
                (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
            notificationManager.cancel(oldestId);
            
            activeNotificationIds.remove(0);
        }
    }

    private void createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            CharSequence name = "Default Channel";
            String description = "Default notification channel for Firebase messages";
            int importance = NotificationManager.IMPORTANCE_HIGH;
            
            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, name, importance);
            channel.setDescription(description);
            channel.enableLights(true);
            channel.enableVibration(true);
            
            NotificationManager notificationManager = getSystemService(NotificationManager.class);
            notificationManager.createNotificationChannel(channel);
        }
    }
}