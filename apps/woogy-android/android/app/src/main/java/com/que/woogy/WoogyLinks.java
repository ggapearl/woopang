package com.que.woogy;

import android.content.Intent;
import android.net.Uri;

/**
 * 앱이 여는 주소를 가려내는 곳 — iOS 앱(Shell.swift)의 isOwn · authExchange · shareLink 와 같은 규칙.
 * 우기 주소가 아닌 것은 화면에 불러오지 않는다.
 */
final class WoogyLinks {
    static final String SCHEME = "com.que.woogy";
    /** 우기 전용 도메인(2026-11-22 ~). 옛 주소 woopang.com 은 이미 퍼진 공유 링크가 열리도록 같이 받는다. */
    static final String HOST = "woogy.app";

    private WoogyLinks() {}

    /** https://woogy.app/woogy… (옛 woopang.com/woogy… 도)만 우기 화면이다 */
    static boolean isOwn(Uri u) {
        if (u == null || !"https".equalsIgnoreCase(u.getScheme())) return false;
        String h = u.getHost() == null ? "" : u.getHost().toLowerCase();
        if (!h.equals(HOST) && !h.equals("www." + HOST) && !h.equals("woopang.com") && !h.equals("www.woopang.com")) return false;
        String p = u.getPath() == null ? "" : u.getPath();
        return p.equals("/woogy") || p.startsWith("/woogy/");
    }

    /** com.que.woogy://auth?t=<1회용 토큰> → https://woogy.app/woogy/auth/app?t=<토큰> (서버가 토큰을 로그인으로 바꾼다 — 5분 · 한 번만) */
    static Uri authExchange(Uri u) {
        if (u == null || !SCHEME.equalsIgnoreCase(u.getScheme()) || !"auth".equalsIgnoreCase(u.getHost())) return null;
        String t = u.getQueryParameter("t");
        if (t == null || t.length() < 16 || t.length() > 200 || !t.matches("[A-Za-z0-9_-]+")) return null;
        return Uri.parse("https://" + HOST + "/woogy/auth/app").buildUpon().appendQueryParameter("t", t).build();
    }

    /** 공유 카드 링크 https://woogy.app/woogy/c/S<16진 10자리> (옛 woopang.com 도) → https://woogy.app/woogy/#share=<번호> */
    static Uri shareLink(Uri u) {
        String no;
        boolean help = false;
        if (u != null && SCHEME.equalsIgnoreCase(u.getScheme())) {
            // 공유 화면의 「앱 열기」 — com.que.woogy://c/S<…> (웹의 intent:// 주소가 이 모양으로 앱을 부른다)
            // 「도와주기」 는 뒤에 ?help=1 — 웹 앱이 그 카드를 오른쪽으로 민 것처럼 바로 결제 창을 연다
            String[] p = u.getPath() == null ? new String[0] : u.getPath().split("/");      // ["", "S…"]
            if (!"c".equalsIgnoreCase(u.getHost()) || p.length != 2) return null;
            no = p[1];
            help = "1".equals(u.getQueryParameter("help"));
        } else {
            if (!isOwn(u)) return null;
            String[] p = u.getPath().split("/");            // ["", "woogy", "c", "S…"]
            if (p.length != 4 || !p[2].equals("c")) return null;
            no = p[3];
        }
        if (!no.matches("S[0-9A-F]{10}")) return null;
        return Uri.parse("https://" + HOST + "/woogy/#share=" + no + (help ? "&help=1" : ""));
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
