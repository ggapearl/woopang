# 안드로이드 백그라운드 길안내 (준비만 · 1.2.71 에는 꺼 둠)

11-26 대표님 10번 ② 「위에 노치관련 수정안 확인했어. 1,2번 둘다 진행해」 — 시안 `C:\woopang-app\mockups\우팡_노치_시안_1124.html` 「바꾸는 안 2」.
홈으로 나가거나 화면을 꺼도 알림창 · 위쪽 알림줄에 「단골 카페까지 350m · 북동쪽」 + 진행 막대. 아이폰은 같은 일을 실시간 현황(Live Activity)으로 한다 (`Assets/Plugins/iOS/WoopangLiveGuide.swift`).

## 왜 꺼 두었나
위치 **포그라운드 서비스**(FOREGROUND_SERVICE_LOCATION)가 든 AAB 를 처음 올리면 Play Console 이 **「포그라운드 서비스 권한」 신고**를 요구한다 —
무슨 기능인지 설명 + **기능이 보이는 영상 링크**(유튜브 일부 공개 등). 신고 전에 올리면 출시가 막힌다. 대표님 계정으로만 할 수 있어 코드만 준비해 두었다.

## 동작
- 안내를 시작할 때(앱이 화면에 있을 때) 서비스가 뜨고, 홈으로 나가도 GPS · 네트워크 위치로 2초 · 3m 마다 거리를 잰다.
- 도착(10m 안 1초) → 「도착했어요」 4초 뒤 닫힘 · 「안내 끝내기」 버튼 · 앱에서 끝내기 · **20분**이면 바로 끈다. 최근 앱에서 지우면 같이 끝.
- 알림 채널 「길안내」 — 소리 · 진동 없음(IMPORTANCE_LOW). 작은 아이콘은 `ic_stat_woopang`(WoopangNotify.androidlib).
- 위치 허락이 없거나 서비스가 매니페스트에 없으면 아무것도 하지 않는다(`begin` 이 false).

## 켜는 순서
1. 이 폴더의 `WoopangGuideService.java` 를 `Assets/Plugins/Android/` 로 옮긴다.
2. `AndroidManifest_snippet.xml` 의 권한 두 줄과 `<service>` 를 `Assets/Plugins/Android/AndroidManifest.xml` 에 넣는다.
3. Player Settings → Android → Scripting Define Symbols 에 `WOOPANG_GUIDE_SERVICE` 추가 (`R0926LiveGuide.cs` 의 안드로이드 부분이 켜진다).
4. 로컬 빌드를 폰에 깔아 영상을 찍는다: 장소 고르기 → 길안내 → 홈으로 → 알림창에 남은 거리 → 걸어가 「도착했어요」.
5. **Play Console → 앱 콘텐츠 → 포그라운드 서비스 권한** 에 「위치 — 사용자가 시작한 길안내(내비게이션)」로 신고 + 영상 링크. 승인되면 그 AAB 를 올린다.

## 확인
- 컴파일: Unity 의 OpenJDK `javac` + `SDK/platforms/android-35/android.jar` 로 경고(옛 API 분기)만 · 오류 0 (11-27).
- 시험: 에디터 플레이 시험 `liveguide` 는 C# 쪽 시작 · 도착 · 끝 호출 순서만 본다 — 서비스 자체는 실기기에서.
