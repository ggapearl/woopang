using System;
using UnityEngine;

/// <summary>
/// 설정 › 사람 › 사용자 오브젝트 유효시간 — 마지막 접속이 이 안인 사람만 목록 · AR 에 보인다. 기기에 저장된다.
/// 11-16 대표님 「'설정' 에서 '사용자 오브젝트 유효시간' 이라는 옵션을 걸어서 '1시간 / 6시간 / 24시간 / 일주일' 로 …
/// (기본값 일주일) 해당 값에 따라 AR화면, 목록리스트가 동일하게 발생하도록」 — 목록(PlaceListManager.AddMapFriends)과 AR(P2PManager.SyncMapFriends)이 같이 쓴다.
/// 지금 앱을 켠 사람(근처 사람 · 10분 안에 소식)은 어느 칸이든 보인다
/// </summary>
public static class R0926PeopleSettings
{
    public const int Default = 3;   // 일주일
    private static readonly int[] Hours = { 1, 6, 24, 168 };
    private const string Key = "People_Window";
    private static int window = -1;

    /// <summary>바뀌면 목록 · AR 이 바로 다시 맞춘다</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Changed = null; window = -1; }

    /// <summary>0 1시간 · 1 6시간 · 2 24시간 · 3 일주일</summary>
    public static int Window
    {
        get
        {
            if (window < 0)
            {
                try { window = Mathf.Clamp(PlayerPrefs.GetInt(Key, Default), 0, Hours.Length - 1); }
                catch (Exception) { window = Default; }
            }
            return window;
        }
        set
        {
            int v = Mathf.Clamp(value, 0, Hours.Length - 1);
            if (v == Window) return;
            window = v;
            try { PlayerPrefs.SetInt(Key, v); PlayerPrefs.Save(); } catch (Exception) { }
            Changed?.Invoke();
        }
    }

    public static double Seconds => Hours[Window] * 3600.0;

    /// <summary>그 자리를 보낸 때(유닉스 초)가 유효시간 안인가</summary>
    public static bool Fresh(double seenAt, double now) => now - seenAt <= Seconds;
}
