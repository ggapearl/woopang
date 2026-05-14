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

### Unity 에디터 버전 업데이트 시 AI 워크플로우 (필독)

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
   - CLAUDE.md "embed된 패키지" 표에 행 추가
5. **빌드 재시도 후 통과 시 git 커밋·푸시** (사용자 명시적 승인 필요)
6. **공식 fix 모니터링**: PATCH_NOTES에 복원 절차 명시했으니 추후 패키지 제작자가 해당 Unity 버전 호환 패치 내면 embed 폴더 삭제 + manifest 원복

### 자주 발생하는 패턴 (참고)

| 증상 | 원인 | 해결 패턴 |
|------|------|-----------|
| `[SerializeField] is not valid on this declaration type` | 새 Roslyn 컴파일러가 auto-property attribute 위치 거부 | `[SerializeField]` → `[field: SerializeField]` |
| `Minimum supported Gradle version is X.Y.Z` | Unity Preferences가 외부 구버전 Gradle 가리킴 | Preferences > External Tools > "Use Gradle Installed with Unity" |
| `Namespace 'X' is used in multiple modules` | AGP 9+의 namespace 검증 강화 | 충돌 .aar 풀어서 AndroidManifest의 `package=` 속성 분리 후 재압축 |
| `EPERM: operation not permitted ... PackageCache` | Defender 실시간 보호가 rename 차단 | Defender 제외 폴더에 프로젝트 경로 추가 + Library 통삭 |
| `Reference 'UnityEditor.iOS.Extensions.Xcode' missing` | 안드로이드 모드에서 iOS dll의 reference 검증 실패 | `.dll.meta`에서 `validateReferences: 0` |

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

## 5. 데이터 매니저 추가·수정 시 연결 체크리스트 (필독)

우팡 앱은 **5개 데이터 매니저**가 AR 오브젝트를 스폰. 새 매니저 추가 또는 기존 매니저 수정 시 **아래 시스템들 모두에 연결돼 있는지 반드시 점검**. 한 곳이라도 빠지면 그 매니저 데이터만 기능 누락 (이전에 공공교통 3개 매니저가 zoom에서 빠져있던 사례).

### 5개 매니저
| 매니저 | 데이터 카테고리 | 위치 |
|--------|----------------|------|
| `DataManager` | 우팡 자체 DB (사용자 업로드 AR 오브젝트) | [Assets/Scripts/Download/DataManager.cs](Assets/Scripts/Download/DataManager.cs) |
| `TourAPIManager` | 공공데이터 (관광공사 API — 관광지·맛집 등) | [Assets/Scripts/Download/TourAPIManager.cs](Assets/Scripts/Download/TourAPIManager.cs) |
| `SubwayManager` | 공공교통 (지하철역) | [Assets/Scripts/Download/SubwayManager.cs](Assets/Scripts/Download/SubwayManager.cs) |
| `TrainStationManager` | 공공교통 (기차역) | [Assets/Scripts/Download/TrainStationManager.cs](Assets/Scripts/Download/TrainStationManager.cs) |
| `TerminalManager` | 공공교통 (버스 터미널) | [Assets/Scripts/Download/TerminalManager.cs](Assets/Scripts/Download/TerminalManager.cs) |

⚠️ "공공데이터(TourAPI)" ≠ "공공교통(Subway/Train/Terminal)" — 별도 카테고리. 한국어로 둘 다 "공공"이라 헷갈리기 쉬우니 작업 시 명확히 구분할 것.

### 매니저 추가·수정 시 점검할 연결 지점

| # | 시스템 | 연결 방법 | 빠뜨리면 발생하는 증상 |
|---|--------|---------|----------------------|
| 1 | `IPlaceCacheProvider` 인터페이스 구현 | `class X : MonoBehaviour, IPlaceCacheProvider` + 모든 멤버 구현 | 컴파일 에러 |
| 2 | `FilterManager.RegisterCacheProvider(this)` 호출 | `Start()` 또는 적절한 초기화 시점 | 중앙 배분 시스템 무시 → 오브젝트 스폰 안 됨 |
| 3 | `MarkCacheReady()` 헬퍼 + `CacheBecameReady` 이벤트 발행 | 캐시 채워질 때 `isCacheReady=true` 대신 `MarkCacheReady()` 호출 | 캐시 늦게 도착 시 다음 AllocationLoop tick까지 무시 → 리스트 빈 표시 |
| 4 | `PlaceListManager` Inspector 필드 + `AddTransportData` 또는 별도 추가 흐름 | PlaceListManager에 필드 추가 + `UpdateUIWithFadeIn`에 데이터 수집 분기 | 주변 리스트에서 이 매니저 항목 누락 |
| 5 | `ARObjectZoomController` Inspector 필드 + `ApplyZoomToARObjects` 호출 | Singleton fallback + `ApplyZoomToManager(thisManager.GetSpawnedObjects())` 호출 | 핀치 zoom 시 이 매니저 오브젝트만 스케일 안 변경 |
| 6 | `LoadingManager`의 visibility 토글 (예: `SetAllObjectsVisible`, `SetAllRenderersVisible`) | 해당 함수 안에서 `thisManager.Instance.SetAllObjectsVisible(...)` 호출 | AR 세션 fallback 등에서 이 매니저 오브젝트만 안 숨겨짐 |
| 7 | `Target` 컴포넌트 세팅 (`PlaceName`, `placeId`, `gpsLatitude`, `gpsLongitude`, `TargetColor`) | Full/IndicatorOnly 스폰 시점에 자식 Target 컴포넌트에 세팅 | OffScreenIndicator/PlaceListManager 매칭 실패 (인디케이터 표시 누락 또는 이름 매칭 부정확) |
| 8 | `GetSpawnedObjects()` / `GetSpawnedFullIds()` / `GetSpawnedIndicatorIds()` public 메서드 제공 | 외부에서 접근 가능한 Dictionary 반환 | zoom·visibility 등 외부 시스템 연결 불가 |
| 9 | `Singleton.Instance` 패턴 (`public static X Instance`) | 다른 매니저들과 동일 패턴 | 다른 시스템에서 Singleton fallback 못 함 → Inspector 누락 시 NullReference |

### 새 매니저 추가 워크플로우

1. **`IPlaceCacheProvider` 구현** — 인터페이스 멤버 모두 구현 (caches, spawn/despawn, IsCacheReady 등)
2. **`MarkCacheReady()` 헬퍼 추가** — 다른 4개 매니저 동일 패턴 복사
3. **Singleton 패턴** — `public static X Instance` 추가
4. **`Start()`에서 `FilterManager.RegisterCacheProvider(this)` 호출**
5. **Inspector에서 prefab 연결** — IndicatorOnly prefab + Full prefab
6. **외부 시스템들에 추가** (체크리스트 4·5·6번):
   - `PlaceListManager` 필드 + 데이터 수집 흐름
   - `ARObjectZoomController` 필드 + `ApplyZoomToManager` 호출
   - `LoadingManager`의 visibility 토글 함수들
7. **`Target` 컴포넌트 세팅 누락 점검** (`placeId` 포함)
8. **빌드 + 실기기 테스트** — 핀치 zoom, list panel, fallback 모드 모두 검증

### "공공교통 zoom 누락" 같은 실수 방지

원인: 새 매니저(Subway/Train/Terminal) 추가 시 zoom 컨트롤러 측 업데이트 누락. 이런 누락 방지하려면:
- 새 매니저 추가 시 **이 체크리스트 6개 시스템 모두 grep**으로 다른 매니저(예: DataManager) 참조 위치 다 찾고, 같은 위치에 새 매니저 참조 추가
- 예: `grep -rn "DataManager" Assets/Scripts/` 결과를 보고 각 위치에서 새 매니저도 같이 처리해야 하는지 판단
- 또는 **공공교통 매니저 3개를 묶어서 `ITransportManager` 같은 추상화** (장기적 리팩토링)

---

*최종 업데이트: 2026-05-14 (5번 섹션 추가: 데이터 매니저 연결 체크리스트 — 공공교통 zoom 누락 사례 방지)*
