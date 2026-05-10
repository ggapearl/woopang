# yasirkula NativeCamera — Embedded with Patches

원본: https://github.com/yasirkula/UnityNativeCamera.git (해시 72f5ebba26c2)

## 적용된 패치

### `Plugins/NativeCamera/Android/NativeCamera.aar` 의 `AndroidManifest.xml`
- 원본: `package="com.yasirkula.unity"`
- 패치: `package="com.yasirkula.unity.nativecamera"`
- 사유: Unity 6000.4.6f1 + AGP 9 manifest merger가 라이브러리 모듈마다 고유 namespace 요구. yasirkula의 4개 패키지(NativeCamera, NativeGallery, NativeFilePicker, SimpleFileBrowser)가 모두 동일 namespace를 써서 충돌 발생.

## 향후 처리

yasirkula 측에서 AGP 9 호환 업데이트(각 패키지별 고유 namespace) 적용하면:
1. `Packages/manifest.json`의 항목을 다시 git URL로 복원
2. 이 폴더 통째로 삭제
3. `Packages/packages-lock.json`의 `source: "embedded"` → `source: "git"`로 복원

## 재패치 절차

`Packages/com.unity.xr.arcore/PATCH_NOTES.md` 참고 (동일 절차).
