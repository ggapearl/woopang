# Unity 버전 업그레이드 / 새 머신 셋업 / 빌드 에러 가이드

> Unity 에디터 버전 업그레이드, 새 머신(Mac/Windows) 셋업, 안드로이드 빌드 에러, `Packages/` embed 패치 작업 시 참조.
> (CLAUDE.md 4번 섹션에서 분리 — 핵심 요약은 CLAUDE.md에 유지)

---

## 새 머신 셋업 / 버전 업그레이드 체크리스트

1. `Library/`, `Temp/`, `obj/`, `Logs/` 폴더는 캐시 — 문제 발생 시 통삭 가능 (`Assets/`, `ProjectSettings/`, `Packages/`는 절대 삭제 금지)
2. Windows Defender 실시간 보호가 `PackageCache` rename을 막을 수 있음 → `C:\woopang` 폴더를 Defender 제외 경로로 등록
3. Unity Preferences > External Tools > Gradle은 **"Gradle Installed with Unity"** 사용 (외부 Gradle 경로 비우기). Unity 6000.4.6f1은 Gradle 9.1.0 / AGP 9 사용.

---

## embed된 패키지 (Unity 6000.4.6f1 + AGP 9 호환 패치)

이 패키지들은 원래 registry/git URL로 잡혀있었으나 namespace/문법 충돌로 빌드 실패해서 `Packages/`에 embed해 패치 중. **절대 원래 URL로 되돌리지 말 것**. 공식 fix 나오면 각 PATCH_NOTES 참고해서 복원.

| 패키지 | 패치 사유 | PATCH_NOTES |
|--------|-----------|-------------|
| `com.google.ar.core.arfoundation.extensions` | Unity 6 컴파일러에서 `[SerializeField]` on property 거부 | [link](../../Packages/com.google.ar.core.arfoundation.extensions/PATCH_NOTES.md) |
| `com.unity.xr.arcore` | AGP 9에서 `unityandroidpermissions.aar`의 namespace가 `arcore_client.aar`와 충돌 | [link](../../Packages/com.unity.xr.arcore/PATCH_NOTES.md) |
| `com.yasirkula.nativecamera` | AGP 9 namespace 충돌 (4개 yasirkula 패키지 동일 namespace) | [link](../../Packages/com.yasirkula.nativecamera/PATCH_NOTES.md) |
| `com.yasirkula.nativegallery` | 동상 | [link](../../Packages/com.yasirkula.nativegallery/PATCH_NOTES.md) |
| `com.yasirkula.nativefilepicker` | 동상 | [link](../../Packages/com.yasirkula.nativefilepicker/PATCH_NOTES.md) |
| `com.yasirkula.simplefilebrowser` | 동상 | [link](../../Packages/com.yasirkula.simplefilebrowser/PATCH_NOTES.md) |

---

## Unity 에디터 버전 업데이트 시 AI 워크플로우 (필독)

사용자가 새 Unity 버전(예: 6000.4.6f1 → 6000.5.x, 6000.4.6f1 → 6001.x)을 설치하고 프로젝트를 열 때 발생하는 컴파일/빌드 에러는 대부분 외부 패키지의 새 컴파일러 / AGP / Gradle 호환성 누락이 원인. **사용자가 "Unity 업데이트했어"라고 고지하면 AI는 다음 절차를 자동으로 수행:**

1. **현재 버전 확인**: `ProjectSettings/ProjectVersion.txt` 읽어 새 버전 확인 후 CLAUDE.md 1번 섹션의 Unity 버전 표기 업데이트
2. **첫 빌드 시도 권장**: 사용자에게 안드로이드 빌드 1회 시도 요청 → 로그 받기
3. **로그 분석 + 외부 검색**: 각 에러를 분류:
   - **컴파일 에러** (예: `error CS0592 Attribute 'SerializeField' is not valid`): 외부 패키지 코드가 새 컴파일러에 부적합 → 해당 패키지를 embed하고 패치
   - **Gradle/AGP 에러** (예: `Minimum supported Gradle version`, `Namespace 'X' is used in multiple modules`): Unity Preferences External Tools 점검 + .aar manifest namespace 분리
   - **Manifest merger 충돌**: 패키지 자체의 AndroidManifest.xml 또는 Unity 자동 생성 mainTemplate/launcherTemplate 검토
4. **패치 적용**:
   - 패키지를 `Packages/` 안에 embed (git URL/registry → `file:패키지명`)
   - `manifest.json`, `packages-lock.json` 동기화
   - 해당 패키지에 `PATCH_NOTES.md` 추가 (원본 해시 / 패치 사유 / 공식 fix 시 복원 절차 / .aar 재압축은 Python zipfile + forward-slash 필수)
   - 이 문서 "embed된 패키지" 표에 행 추가
5. **빌드 재시도 후 통과 시 git 커밋·푸시** (사용자 명시적 승인 필요)
6. **공식 fix 모니터링**: PATCH_NOTES에 복원 절차 명시했으니 추후 패키지 제작자가 해당 Unity 버전 호환 패치 내면 embed 폴더 삭제 + manifest 원복

---

## 자주 발생하는 패턴 (참고)

| 증상 | 원인 | 해결 패턴 |
|------|------|-----------|
| `[SerializeField] is not valid on this declaration type` | 새 Roslyn 컴파일러가 auto-property attribute 위치 거부 | `[SerializeField]` → `[field: SerializeField]` |
| `Minimum supported Gradle version is X.Y.Z` | Unity Preferences가 외부 구버전 Gradle 가리킴 | Preferences > External Tools > "Use Gradle Installed with Unity" |
| `Namespace 'X' is used in multiple modules` | AGP 9+의 namespace 검증 강화 | 충돌 .aar 풀어서 AndroidManifest의 `package=` 속성 분리 후 재압축 |
| `EPERM: operation not permitted ... PackageCache` | Defender 실시간 보호가 rename 차단 | Defender 제외 폴더에 프로젝트 경로 추가 + Library 통삭 |
| `Reference 'UnityEditor.iOS.Extensions.Xcode' missing` | 안드로이드 모드에서 iOS dll의 reference 검증 실패 | `.dll.meta`에서 `validateReferences: 0` |

---

## Android Native 라이브러리 패치 시 주의 (.aar 재압축)

.aar 안의 AndroidManifest.xml을 수정 후 재압축할 때 **PowerShell의 `Compress-Archive`/`ZipFile.CreateFromDirectory`는 Windows 경로 구분자(`\`)를 그대로 넣어 Android 빌드가 못 읽음**. 반드시 Python `zipfile` 모듈 또는 zip CLI 사용:

```python
import zipfile, os
with zipfile.ZipFile(dst_aar, 'w', zipfile.ZIP_DEFLATED) as zf:
    for root, _, files in os.walk(src_dir):
        for f in files:
            full = os.path.join(root, f)
            arc = os.path.relpath(full, src_dir).replace(os.sep, '/')
            zf.write(full, arc)
```
