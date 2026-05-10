# WOOPANG 프로젝트 작업 가이드

## 1. 프로젝트 개요
- **프로젝트**: WOOPANG - AR 기반 SNS 앱 (Unity 6000.4.6f1)
- **회사명**: 쾌엔터테인먼트 (QUE. ENT)
- **타겟**: 100만~1000만 사용자 규모, 모바일 (iOS/Android)
- **상태**: 개발 중 (앱서비스 활성화 전)

### 회사명 표기 규칙 (필수)
- 본문/상단: **"QUE. ENT"** (한국어/영어 모두)
- 푸터(저작권): **"쾌엔터테인먼트"** (한국어), **"KWAE Entertainment"** (영어)
- ❌ 금지: "쾌ENT", "KWAE ENT", "QUE Entertainment"

---

## 2. 핵심 작업 원칙

### 2.1 기존 코드 존중 (중요)
- 기존에 잘 작동하는 로직은 함부로 삭제/변경 금지
- 삭제가 필요하면 **반드시 먼저 제안**
- 수정 시 기존 기능이 초기화되지 않도록 주의

### 2.2 완전한 작업 수행
로직 작업 시 UI 오브젝트까지 생성하고 연결 완료해야 함:
1. 스크립트 생성/수정
2. 에디터 스크립트로 UI 오브젝트 생성 (`Assets/Scripts/Editor/`)
3. 컴포넌트 연결, Inspector 필드 연결
4. 누락된 참조 체크 (AutoConnectFields 패턴)

### 2.3 코드 품질
- 지양: 불필요한 Debug.Log, 과도한 주석, 미사용 코드
- 지향: 깔끔한 코드, 필수 에러 로그만, 재사용 가능 구조
- 폰트: `AppleSDGothicNeoM.ttf` (모든 UI)

---

## 3. 작업별 상세 가이드 (해당 작업 시 반드시 읽을 것)

| 작업 유형 | 참조 파일 |
|-----------|-----------|
| Unity 앱 개발 (UI/에디터/프리팹/매니저/스타일) | [docs/claude/unity-app.md](docs/claude/unity-app.md) |
| **AR 오브젝트 스폰 / 백그라운드 복귀 / 가시성 (필독)** | **[docs/claude/ar-object-spawn.md](docs/claude/ar-object-spawn.md)** |
| Git 커밋/푸시 | [docs/claude/git-rules.md](docs/claude/git-rules.md) |
| 마케팅/캠페인/광고 | [docs/claude/marketing.md](docs/claude/marketing.md) |
| 공공데이터 DB INSERT | [docs/claude/public-data.md](docs/claude/public-data.md) |
| 기획안/제안서 HTML | [docs/claude/proposals.md](docs/claude/proposals.md) |
| **앳하트(AtHeart) 콘텐츠 / 촬영구성안 / 멤버 분석** | **[docs/claude/atheart-contents.md](docs/claude/atheart-contents.md)** |
| 서버 포트/Nginx/배포 | [docs/claude/server-infra.md](docs/claude/server-infra.md) |
| MCP Unity (Claude ↔ Unity Editor) | [docs/claude/mcp-unity.md](docs/claude/mcp-unity.md) |

⚠️ 해당 작업을 할 때만 관련 파일을 읽을 것. 불필요한 파일은 읽지 않는다.

---

## 4. Unity 버전 업그레이드 / 새 머신 셋업 체크리스트

Unity 에디터 버전을 올리거나, 다른 머신(Mac/Windows)에서 프로젝트 처음 열 때:

1. `Library/`, `Temp/`, `obj/`, `Logs/` 폴더는 캐시 — 문제 발생 시 통삭 가능 (`Assets/`, `ProjectSettings/`, `Packages/`는 절대 삭제 금지)
2. Windows Defender 실시간 보호가 `PackageCache` rename을 막을 수 있음 → `C:\woopang` 폴더를 Defender 제외 경로로 등록
3. Unity Preferences > External Tools > Gradle은 **"Gradle Installed with Unity"** 사용 (외부 Gradle 경로 비우기). Unity 6000.4.6f1은 Gradle 9.1.0 / AGP 9 사용.

### embed된 패키지 (Unity 6000.4.6f1 + AGP 9 호환 패치)

이 패키지들은 원래 registry/git URL로 잡혀있었으나 namespace/문법 충돌로 빌드 실패해서 `Packages/`에 embed해 패치 중. **절대 원래 URL로 되돌리지 말 것**. 공식 fix 나오면 각 PATCH_NOTES 참고해서 복원.

| 패키지 | 패치 사유 | PATCH_NOTES |
|--------|-----------|-------------|
| `com.google.ar.core.arfoundation.extensions` | Unity 6 컴파일러에서 `[SerializeField]` on property 거부 | [link](Packages/com.google.ar.core.arfoundation.extensions/PATCH_NOTES.md) |
| `com.unity.xr.arcore` | AGP 9에서 `unityandroidpermissions.aar`의 namespace가 `arcore_client.aar`와 충돌 | [link](Packages/com.unity.xr.arcore/PATCH_NOTES.md) |
| `com.yasirkula.nativecamera` | AGP 9 namespace 충돌 (4개 yasirkula 패키지 동일 namespace) | [link](Packages/com.yasirkula.nativecamera/PATCH_NOTES.md) |
| `com.yasirkula.nativegallery` | 동상 | [link](Packages/com.yasirkula.nativegallery/PATCH_NOTES.md) |
| `com.yasirkula.nativefilepicker` | 동상 | [link](Packages/com.yasirkula.nativefilepicker/PATCH_NOTES.md) |
| `com.yasirkula.simplefilebrowser` | 동상 | [link](Packages/com.yasirkula.simplefilebrowser/PATCH_NOTES.md) |

### Android Native 라이브러리 패치 시 주의 (.aar 재압축)

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

---

*최종 업데이트: 2026-05-11 (Unity 6000.4.6f1 AGP 9 namespace 패치 + embed 패키지 5개 추가)*
