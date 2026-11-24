# 출시 기록 (Release Log)

빌드를 스토어에 올릴 때마다 **맨 위에 새 항목을 추가**한다.
목적은 자랑이 아니라 **추적**이다 — "이 빌드가 어느 소스냐", "그때 뭘 알고도 넘어갔냐"에
답할 수 있어야 다음 회차에 원인을 찾는다.

## 적는 규칙
- 업로드 **직후** 적는다. 나중에 적으면 buildNumber를 잊는다.
- 실패한 업로드도 적는다. 실패 이유가 다음 빌드의 체크리스트가 된다.
- 미해결 항목을 **반드시** 남긴다. 알고도 넘긴 것과 몰랐던 것은 다르다.
- 비밀번호·키 실값은 적지 않는다 (CLAUDE.md 6.1 규칙 3번).

---

## 1.2.50 — (작성 중)

| 항목 | iOS | Android |
|---|---|---|
| 버전 | 1.2.50 | 1.2.50 |
| 빌드번호 | | |
| 커밋 | 91fb6cf | 91fb6cf |
| 업로드 일시 | | |
| 처리 결과 | | |
| 심사 제출 | | |

- **Unity**: 6000.4.6f1
- **Xcode**: (맥북에서 확인)
- **aps-environment**: development → production 변경 여부 =
- **targetSdk**: 36 (2026-08-31 Play 요구사항 대응)

### 이번 회차 주요 변경
- AR 3D 캐릭터(dance_anim) 신규 — 큐브 더블탭 → GLB 다운로드 → 안무 재생, 스와이프 회전
- 공중화장실 전국 51,994건 추가 (`toilet` 카테고리)
- AR 세션 복구 안내 / 화면 밖 표시기 10 → 30
- GLB 색상 미반영 버그 수정 (materials 키 정규식 탐색)
- targetSdk 35 → 36, 미사용 에셋 214MB 정리

### 알고도 넘긴 것 (다음 회차 확인 대상)
- 화장실이 전체 DB의 **62%** — 도심에서 화면이 보라색 큐브로 덮이는지 실기기 확인 필요.
  심하면 `FilterManager.filterPublicData` 기본값을 끔으로 조정.
  되돌릴 땐 `username='DATA.GO.KR' AND category='toilet'` 로 이번 작업분만 골라낼 수 있다.
- **태블릿(sw>=600dp)에서 세로 고정이 무시됨** — targetSdk 36의 동작 변경.
  폰은 영향 없음. 필요하면 매니페스트 `PROPERTY_COMPAT_ALLOW_RESTRICTED_RESIZABILITY`
  로 API 37까지 유예 가능.
- **Android 개발자 인증** — `com.que.woopang` 등록 상태 확인 미완.
  2026-09-30 까지 미등록 시 Play에서 삭제. 설치 50회 미만은 선착순 등록이라 자동 보장 아님.
- **`Info.plist` / `UnityAppController.mm` 이 저장소에 없다.**
  맥북 `/Users/pdnom/Desktop/` 에만 존재하며 `PostProcessBuildUtils` 가 복사해 쓴다.
  그 맥을 새로 세팅하면 조용히 잘못된 빌드가 나온다. 저장소로 옮기는 게 맞다.

---

## 1.2.48 — 2026-05-20 (추정)

git 이력으로 복원한 항목이라 **업로드 여부는 미확인**.
`bundleVersion 1.2.48` + `AndroidBundleVersionCode 260520002` 가 커밋 `229d35a` 에 기록됨.

## 1.2.43 — 2026-05-13
커밋 `6a41aaa`. 상세 미기록.

## 1.2.42 — 2026-05-11
커밋 `82c3acf`. Unity 6000.4.6f1 + AGP 9 호환 작업.
