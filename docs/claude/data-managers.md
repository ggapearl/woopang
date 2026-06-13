# 데이터 매니저 추가·수정 시 연결 체크리스트 (필독)

> 5개 데이터 매니저(스폰 시스템) 추가·수정 작업 시 참조.
> (CLAUDE.md 5번 섹션에서 분리 — 5개 매니저 표·공공데이터≠공공교통 경고는 CLAUDE.md에 유지)

우팡 앱은 **5개 데이터 매니저**가 AR 오브젝트를 스폰. 새 매니저 추가 또는 기존 매니저 수정 시 **아래 시스템들 모두에 연결돼 있는지 반드시 점검**. 한 곳이라도 빠지면 그 매니저 데이터만 기능 누락 (이전에 공공교통 3개 매니저가 zoom에서 빠져있던 사례).

### 5개 매니저
| 매니저 | 데이터 카테고리 | 위치 |
|--------|----------------|------|
| `DataManager` | 우팡 자체 DB (사용자 업로드 AR 오브젝트) | [Assets/Scripts/Download/DataManager.cs](../../Assets/Scripts/Download/DataManager.cs) |
| `TourAPIManager` | 공공데이터 (관광공사 API — 관광지·맛집 등) | [Assets/Scripts/Download/TourAPIManager.cs](../../Assets/Scripts/Download/TourAPIManager.cs) |
| `SubwayManager` | 공공교통 (지하철역) | [Assets/Scripts/Download/SubwayManager.cs](../../Assets/Scripts/Download/SubwayManager.cs) |
| `TrainStationManager` | 공공교통 (기차역) | [Assets/Scripts/Download/TrainStationManager.cs](../../Assets/Scripts/Download/TrainStationManager.cs) |
| `TerminalManager` | 공공교통 (버스 터미널) | [Assets/Scripts/Download/TerminalManager.cs](../../Assets/Scripts/Download/TerminalManager.cs) |

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
