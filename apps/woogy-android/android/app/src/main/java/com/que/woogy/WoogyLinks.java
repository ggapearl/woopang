package com.que.woogy;

import android.content.Intent;
import android.net.Uri;

/**
 * 앱이 여는 주소를 가려내는 곳 — iOS 앱(Shell.swift)의 isOwn · authExchange · shareLink 와 같은 규칙.
 * 우기 주소가 아닌 것은 화면에 불러오지 않는다.
 */
final class WoogyLinks {
    static final String SCHEME = "com.que.woogy";

    private WoogyLinks() {}

    /** https://woopang.com/woogy… 만 우기 화면이다 */
    static boolean isOwn(Uri u) {
        if (u == null || !"https".equalsIgnoreCase(u.getScheme())) return false;
        String h = u.getHost() == null ? "" : u.getHost().toLowerCase();
        if (!h.equals("woopang.com") && !h.equals("www.woopang.com")) return false;
        String p = u.getPath() == null ? "" : u.getPath();
        return p.equals("/woogy") || p.startsWith("/woogy/");
    }

    /** com.que.woogy://auth?t=<1회용 토큰> → https://woopang.com/woogy/auth/app?t=<토큰> (서버가 토큰을 로그인으로 바꾼다 — 5분 · 한 번만) */
    static Uri authExchange(Uri u) {
        if (u == null || !SCHEME.equalsIgnoreCase(u.getScheme()) || !"auth".equalsIgnoreCase(u.getHost())) return null;
        String t = u.getQueryParameter("t");
        if (t == null || t.length() < 16 || t.length() > 200 || !t.matches("[A-Za-z0-9_-]+")) return null;
        return Uri.parse("https://woopang.com/woogy/auth/app").buildUpon().appendQueryParameter("t", t).build();
    }

    /** 공유 카드 링크 https://woopang.com/woogy/c/S<16진 10자리> → https://woopang.com/woogy/#share=<번호> */
    static Uri shareLink(Uri u) {
        if (!isOwn(u)) return null;
        String[] p = u.getPath().split("/");                // ["", "woogy", "c", "S…"]
        if (p.length != 4 || !p[2].equals("c")) return null;
        String no = p[3];
        if (!no.matches("S[0-9A-F]{10}")) return null;
        return Uri.parse("https://woopang.com/woogy/#share=" + no);
    }

    /** 앱이 받은 Intent(로그인 복귀 · 공유 링크 · 알림 누름)가 열어야 할 우기 화면 — 없으면 null */
    static Uri target(Intent in) {
        if (in == null) return null;
        Uri d = in.getData();
        Uri a = authExchange(d);
        if (a != null) return a;
        Uri s = shareLink(d);
        if (s != null) return s;
        String link = in.getStringExtra("link");              // FCM 알림을 눌렀을 때 시스템이 실어 준다(서버 data.link)
        if (link != null && !link.isEmpty()) {
            Uri l = Uri.parse(link);
            if (isOwn(l)) return l;
        }
        return null;
    }
}
