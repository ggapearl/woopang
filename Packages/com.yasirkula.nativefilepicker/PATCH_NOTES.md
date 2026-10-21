# yasirkula NativeFilePicker — Embedded with Patches

원본: https://github.com/yasirkula/UnityNativeFilePicker.git (해시 de7bbba0dbbd)

## 적용된 패치

### `Plugins/NativeFilePicker/Android/NativeFilePicker.aar` 의 `AndroidManifest.xml`
- 원본: `package="com.yasirkula.unity"`
- 패치: `package="com.yasirkula.unity.nativefilepicker"`

자세한 사유와 복원/재패치 절차는 `Packages/com.yasirkula.nativecamera/PATCH_NOTES.md` 참고.

### `Plugins/NativeFilePicker/iOS/NativeFilePicker.mm` — 창 띄우기가 실패하면 '사용 중'이 영원히 남던 것 (2026-10-21, WOOPANG)
- 증상: 대표님 「glb 칸을 눌러도 · 사진 칸을 눌러도 아무것도 안 뜬다」. 원본은 띄우는 동안 state 를 1 로 두고 `presentViewController` 의 완료 블록에서 0 으로 되돌린다. 루트 화면이 이미 무언가를 띄운 채면(남은 창·닫히는 중인 창) 띄우기가 조용히 실패해 완료 블록이 불리지 않고, state 1 이 앱을 다시 켤 때까지 남았다. 그동안 C# `NativeFilePicker.IsFilePickerBusy()` 가 계속 true 라 모든 호출이 창 없이 바로 null 로 끝났다.
- 패치 (`WOOPANG 패치` 로 검색):
  1. `NFPTopViewController()` — 루트에서 `presentedViewController` 를 따라 올라간 맨 위 화면(닫히는 중인 화면은 건너뜀)에서 띄운다. 아무것도 안 띄운 평소엔 루트 그대로라 동작이 같다.
  2. 띄우라고 한 시각(`filePickerShownAt`)을 적어 두고, 5초가 지나도 창이 화면에 없으면(`presentingViewController == nil`) state 1 을 푼다 → C# 쪽 1초 점검이 '취소'(null)로 받아 기다리던 쪽이 풀린다.
  3. 사용 중 판정의 `presentingViewController == UnityGetGLViewController()` 를 `!= nil` 로 — 맨 위 화면에서 띄우면 띄운 쪽이 루트가 아니어서, 창이 떠 있는데도 '끝남'으로 볼 뻔했다.
- C# 쪽 짝: `Assets/Redesign0926/Runtime/R0926PickerWait.cs` (답을 잃은 고르기를 칸을 다시 눌러 풂 — 콜백 오브젝트에 취소 답을 대신 넣는다).
- 원본을 새 버전으로 바꿀 땐 위 세 가지를 다시 적용할 것.
