# Unity XR ARCore — Embedded with Patches

이 패키지는 원래 `com.unity.xr.arcore: 6.4.2` (registry)였으나, Unity 6000.4.6f1 + AGP 9 도입 후 namespace 충돌로 빌드 실패해서 embed해 패치 중.

원본: Unity registry `com.unity.xr.arcore@6.4.2` (해시 b6ac3dd83886)

## 적용된 패치

### `Runtime/Android/unityandroidpermissions.aar` 의 `AndroidManifest.xml`
- 원본: `package="com.google.ar.core"` (arcore_client.aar와 충돌)
- 패치: `package="com.unity.xr.arcore.permissions"`
- 사유: AGP 9 manifest merger가 라이브러리 모듈마다 고유 namespace 요구. arcore_client.aar는 실제 Google ARCore SDK라 그대로 두고, Unity 측 보조 모듈인 unityandroidpermissions만 namespace 분리.

## 향후 처리

- Unity가 ARCore 6.4.x 후속 패치(예: 6.4.3)에서 이 namespace 충돌을 정식 수정하면:
  1. `Packages/manifest.json`의 `com.unity.xr.arcore` 항목을 다시 버전 문자열로 복원
  2. 이 폴더 통째로 삭제
  3. `Packages/packages-lock.json`의 `source: "embedded"` → `source: "registry"`로 복원
- Unity 메이저 업데이트 시 이 패치가 새 ARCore 버전에서도 필요한지 재확인 필요.

## 재패치 절차 (.aar 안의 manifest 수정 시)

```bash
# 1. .aar 풀기
unzip unityandroidpermissions.aar -d /tmp/aar

# 2. AndroidManifest.xml의 package 속성 변경

# 3. 재압축 (forward-slash 경로 필수 — Python 사용)
python -c "
import zipfile, os
with zipfile.ZipFile('unityandroidpermissions.aar', 'w', zipfile.ZIP_DEFLATED) as zf:
    for root, _, files in os.walk('/tmp/aar'):
        for f in files:
            full = os.path.join(root, f)
            zf.write(full, os.path.relpath(full, '/tmp/aar').replace(os.sep, '/'))
"
```
