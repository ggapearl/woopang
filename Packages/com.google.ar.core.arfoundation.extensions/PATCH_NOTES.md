# ARCore Extensions — Embedded with Patches

이 패키지는 원래 git URL로 잡혀있었으나, Unity 6 호환성 이슈로 `Packages/` 안에 embed해 직접 패치 중.

원본: https://github.com/google-ar/arcore-unity-extensions.git#arf5
해시: ad14e3f6490a1cce882e2b5e2471c9cff675c89b

## 적용된 패치

### 1. `Runtime/GeospatialCreatorRuntime/Scripts/ARGeospatialCreatorOrigin.cs:87`
- 원본: `[SerializeField]` (auto-property에 직접)
- 패치: `[field: SerializeField]` (backing field에 attribute 적용)
- 사유: Unity 6 (.NET 신컴파일러)에서 `error CS0592: Attribute 'SerializeField' is not valid on this declaration type` 발생.

### 2. `Editor/ExternalDependencyManager/Editor/Google.IOSResolver.dll.meta`
- 원본: `validateReferences: 1`
- 패치: `validateReferences: 0`
- 사유: 안드로이드 빌드 타겟 스위치 시 `UnityEditor.iOS.Extensions.Xcode` 참조 못 찾는 경고 발생. iOS 전용 도구라 안드로이드에선 사용 안 됨.

### 3. `Runtime/Scripts/ARCoreExtensions.cs` — `ReportGeospatialCreatorPlatformWithDelay()` (2026-11-02)
- 원본: 세션 핸들을 60프레임 기다린 뒤 핸들이 0이어도 `EarthApi.ReportGeospatialCreatorPlatform` 호출
- 패치: 기다린 뒤에도 핸들이 0이면 `yield break` (주석 `WOOPANG patch`)
- 사유: 에디터(안드로이드 타겟) 플레이마다 `DllNotFoundException: arcore_sdk_c`. 기기에서도 핸들 0으로 네이티브를 부를 이유가 없다 — 핸들이 있으면 원본과 같이 동작.

### 4. `Runtime/Scripts/ARCoreExtensions.cs` — `OnEnable()` (2026-11-02)
- 패치: `No active ARCoreSessionSubsystem` / `No active ARCoreCameraSubsystem` LogError 를 `#if !UNITY_EDITOR` 로 감쌈
- 사유: 에디터엔 ARCore 로더가 없어 플레이마다 빨간 줄 두 개. 구독·동작은 그대로(원래도 null 이면 건너뜀).

### 5. `Editor/Scripts/Internal/ExternalDependencyResolverHelper.cs` — `EnableAnalyticsReporting()` (2026-11-02)
- 패치: 끌 때의 `Disabling Analytics Reporting in External Dependency Manager.` 로그만 뺌 (끄는 동작은 그대로)
- 사유: `[PostProcessScene]` 라 도메인 리로드 뒤 플레이할 때마다 찍혔다.

## 향후 처리

- 구글에서 Unity 6 호환 공식 fix(arf6 브랜치 등)가 나오면:
  1. `Packages/manifest.json` 다시 git URL로 복원
  2. 이 폴더 통째로 삭제
  3. `Packages/packages-lock.json` `source: "git"` 으로 복원
- ARCore Extensions 버전 올릴 일 있으면 위 패치들을 새 버전에서도 동일하게 재적용해야 함.
