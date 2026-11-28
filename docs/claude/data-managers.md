# 데이터 매니저 추가·수정 시 연결 체크리스트 (필독)

> 최종 갱신: 2026-11-27
> 6개 데이터 매니저(스폰 시스템) 추가·수정 작업 시 참조.
> (CLAUDE.md 5번 섹션에서 분리 — 매니저 표·공공데이터≠공공교통 경고는 CLAUDE.md에 유지)

우팡 앱은 **6개 데이터 매니저**가 AR 오브젝트를 스폰. 새 매니저 추가 또는 기존 매니저 수정 시 **아래 시스템들 모두에 연결돼 있는지 반드시 점검**. 한 곳이라도 빠지면 그 매니저 데이터만 기능 누락 (이전에 공공교통 3개 매니저가 zoom에서 빠져있던 사례).

### 6개 매니저
| 매니저 | 데이터 카테고리 | 위치 |
|--------|----------------|------|
| `DataManager` | 우팡 자체 DB (사용자 업로드 AR 오브젝트) | [Assets/Scripts/Places/DataManager.cs](../../Assets/Scripts/Places/DataManager.cs) |
| `TourAPIManager` | 공공데이터 (관광공사 API — 관광지·맛집 등) | [Assets/Scripts/Places/TourAPIManager.cs](../../Assets/Scripts/Places/TourAPIManager.cs) |
| `SubwayManager` | 공공교통 (지하철역) | [Assets/Scripts/Places/SubwayManager.cs](../../Assets/Scripts/Places/SubwayManager.cs) |
| `TrainStationManager` | 공공교통 (기차역) | [Assets/Scripts/Places/TrainStationManager.cs](../../Assets/Scripts/Places/TrainStationManager.cs) |
| `TerminalManager` | 공공교통 (버스 터미널) | [Assets/Scripts/Places/TerminalManager.cs](../../Assets/Scripts/Places/TerminalManager.cs) |
| `StoreManager` | 근처 가게 (소상공인 상가정보 — 서버 `/api/stores/near` 가 가까운 몇 곳만 준다, 2026-11-20 N8a) | [Assets/Scripts/Places/StoreManager.cs](../../Assets/Scripts/Places/StoreManager.cs) |

⚠️ "공공데이터(TourAPI)" ≠ "공공교통(Subway/Train/Terminal)" — 별도 카테고리. 한국어로 둘 다 "공공"이라 헷갈리기 쉬우니 작업 시 명확히 구분할 것.

⚠️ **칩 이름 주의 (2026-11-20 N8a · 2026-11-27 다시 나눔)**: 화면의 「공공데이터」 칩(`StoresToggle` · 필터 키 `stores` · 저장값 `Filter_Stores_V1`) = 근처 가게 + 관광공사 자료 + 병원·박물관·화장실 같은 기관 공공시설, 「공공기관」 칩(`PublicDataToggle` · 필터 키 `publicData`) = 기관이 올린 관공서·학교·우체국/농협만. 어느 칩인지는 `FilterManager.ChipKey(filterKey, category)` 한 곳에서 정한다(배분·목록·목록 아래 줄 개수가 모두 이것을 쓴다 — 새로 칩을 나눌 땐 여기만 고친다). 코드 이름(`PublicData`)과 화면 이름이 엇갈리니 칩을 만질 땐 키로 확인할 것.
- `TourAPIManager` 상세는 **창을 열 때** 받는다(2026-11-27): 목록을 받을 땐 가까운 곳 사진(`detailImage`)만, 창을 열면(`DoubleTap3D.OnDoubleTapEvent` → `OnInfoOpen`) 소개(`detailIntro` — 운영 시간·쉬는 날·요금·주차·체크인 …) → 공통(`detailCommon` — 개요·홈페이지) → 반려견(`detailPetTour`, 숙박 길은 뺀다) 순서로 받아 창을 다시 그린다. 받은 것은 종류별 표시(`detailDone` — `img:`/`common:`/`pet:`/`intro:`+번호)로 한 번만 묻고, 늦게 온 답은 창이 아직 같은 장소(`DoubleTap3D.InfoSource`)일 때만 그린다. 목록을 새로 받아 장소 자료가 바뀌면 `CarryDetails` 가 받아 둔 상세를 옮겨 담는다(빈 값으로 목록의 전화·주소를 덮지 않는다). 시험: PlayTest `tourintro` (내 PC 가짜 관광공사 서버 127.0.0.1:5800 필요).
- `StoreManager` 의 오브젝트 본은 관광공사 큐브(`0002_Cube_TourAPI`)를 같이 쓴다. 이 본에는 앞면 그림 부품(`ImageDisplayController`)이 없어서 **만들 때 붙인다**(`StoreManager.NewObject`) — 빼면 가게 큐브에 분류 스티커 대신 관광공사 면이 뜬다.
- 시험: PlayTest `stores` (내 PC 가짜 가게 서버 127.0.0.1:5795 필요 — 운영 서버 · 상가정보 API 에 묻지 않는다).
- 지하철 · 기차역 · 터미널 정보 창은 `FacilityInfo.Configure` 한 곳에서 채운다(2026-11-27): 매니저 `SetupObject`와 목록 사진 창 `R0926PlaceInfo.Fill`이 이것을 쓴다.
  - 지하철역은 서버(`server/transit_info.py`, 국토교통부 TAGO)가 `/api/nearby-facilities` 응답에 세 칸을 붙여 준다: `lines`(노선), `exits`(출구 한 줄), `photos`(첫차·막차·출구 안내 카드 주소 `/api/transit/subway/<역키>/<카드>.png`).
  - 처음 보는 역은 서버가 뒤에서 맞춘다. 그래서 `SubwayManager`는 다시 받은 목록으로 이미 띄운 역도 다시 채운다.
  - 기차역 · 버스터미널도 같은 `photos` 칸 하나로 온다(2026-11-27 — 앱은 고칠 것 없음): 기차역은 주요 도시 행 첫차 · 막차 카드 `/api/transit/train/<키>/times.png`(다음 평일 기준), 터미널은 고속버스 카드 `/api/transit/bus/<키>/times.png`(TAGO 자료가 사흘치뿐이라 내일 기준 · 사흘마다 새로). 서버 미리 맞추기: `server/public_data/transit_train_match.py` · `transit_bus_match.py`.
- 풀의 오브젝트는 맨 위가 꺼져 있다. 교통 매니저도 DataManager·StoreManager처럼 자식(Cube)의 `DoubleTap3D`·`Target`을 `GetComponentInChildren<T>(true)`로 찾는다(11-27에 맞춤. 그날 시험에선 예전 `()`도 찾았다).
  - 시험: PlayTest `transitinfo` (내 PC 시험 서버 127.0.0.1:5794 `/transit/` 카드 그림 필요).
  - 앱은 http 절대 주소를 https로 바꿔 받는다(`ApiConfig.Secure`). 그래서 시험 카드는 상대 주소로 넣고 `SubPhotoCache.EditorBase`를 시험 서버로 둔다.

### 매니저 추가·수정 시 점검할 연결 지점

| # | 시스템 | 연결 방법 | 빠뜨리면 발생하는 증상 |
|---|--------|---------|----------------------|
| 1 | `IPlaceCacheProvider` 인터페이스 구현 | `class X : MonoBehaviour, IPlaceCacheProvider` + 모든 멤버 구현 | 컴파일 에러 |
| 2 | `FilterManager.RegisterCacheProvider(this)` 호출 | `Start()` 또는 적절한 초기화 시점 | 중앙 배분 시스템 무시 → 오브젝트 스폰 안 됨 |
| 3 | `MarkCacheReady()` 헬퍼 + `CacheBecameReady` 이벤트 발행 | 캐시 채워질 때 `isCacheReady=true` 대신 `MarkCacheReady()` 호출 | 캐시 늦게 도착 시 다음 AllocationLoop tick까지 무시 → 리스트 빈 표시 |
| 4 | `PlaceListManager` Inspector 필드 + `AddTransportData` 또는 별도 추가 흐름 | PlaceListManager에 필드 추가 + `UpdateUIWithFadeIn`에 데이터 수집 분기 | 주변 리스트에서 이 매니저 항목 누락 |
| 5 | `ARObjectZoomController` Inspector 필드 + `ApplyZoomToARObjects` 호출 + 스폰 때 `ARObjectZoomController.Fit(newObj)` | Singleton fallback + `ApplyZoomToManager(thisManager.GetSpawnedObjects())` 호출 · `spawnedObjects[id] = newObj` 바로 앞(모든 스폰 경로)에 `Fit` — 줌은 **처음 크기(프리팹 맨 위) × 배율** (11-15: 예전 `Vector3.one × 배율` 이 맨 위 2·4 인 상자를 절반·4분의 1로 줄였다). 맨 위 크기를 직접 쓰지 말 것 | 핀치 zoom 시 이 매니저 오브젝트만 스케일 안 변경 · 줌 중에 새로 뜬/풀에서 다시 꺼낸 것만 크기가 다름 |
| 6 | `LoadingManager`의 visibility 토글 (예: `SetAllObjectsVisible`, `SetAllRenderersVisible`) | 해당 함수 안에서 `thisManager.Instance.SetAllObjectsVisible(...)` 호출 | AR 세션 fallback 등에서 이 매니저 오브젝트만 안 숨겨짐 |
| 7 | `Target` 컴포넌트 세팅 (`PlaceName`, `placeId`, `gpsLatitude`, `gpsLongitude`, `TargetColor`) | Full/IndicatorOnly 스폰 시점에 자식 Target 컴포넌트에 세팅 | OffScreenIndicator/PlaceListManager 매칭 실패 (인디케이터 표시 누락 또는 이름 매칭 부정확) |
| 8 | `GetSpawnedObjects()` / `GetSpawnedFullIds()` / `GetSpawnedIndicatorIds()` public 메서드 제공 | 외부에서 접근 가능한 Dictionary 반환 | zoom·visibility 등 외부 시스템 연결 불가 |
| 9 | `Singleton.Instance` 패턴 (`public static X Instance`) | 다른 매니저들과 동일 패턴 | 다른 시스템에서 Singleton fallback 못 함 → Inspector 누락 시 NullReference |
| 10 | 가까이 카드 `R0926NearModal` (2026-11-15) | `FindManagers()` 에 `FindAnyObjectByType<X>()` + `Tick()` 에 `Gather(x.GetSpawnedObjects(), Kind.Place, me)` 한 줄씩 | 2m 안에 들어와도 이 매니저 오브젝트만 화면 가운데 카드에 안 뜬다 (시험: PlayTest `nearkinds`·`nearpublic`) |

### 새 매니저 추가 워크플로우

1. **`IPlaceCacheProvider` 구현** — 인터페이스 멤버 모두 구현 (caches, spawn/despawn, IsCacheReady 등)
2. **`MarkCacheReady()` 헬퍼 추가** — 다른 4개 매니저 동일 패턴 복사
3. **Singleton 패턴** — `public static X Instance` 추가
4. **`Start()`에서 `FilterManager.RegisterCacheProvider(this)` 호출**
5. **Inspector에서 prefab 연결** — IndicatorOnly prefab + Full prefab
6. **외부 시스템들에 추가** (체크리스트 4·5·6번):
   - `PlaceListManager` 필드 + 데이터 수집 흐름
   - `ARObjectZoomController` 필드 + `ApplyZoomToManager` 호출 + 스폰 경로마다 `ARObjectZoomController.Fit(newObj)` (시험: PlayTest `zoombase`)
   - `LoadingManager`의 visibility 토글 함수들
   - 가까이 카드 `R0926NearModal` 의 `FindManagers()` · `Tick()` (체크리스트 10번)
7. **`Target` 컴포넌트 세팅 누락 점검** (`placeId` 포함)
8. **빌드 + 실기기 테스트** — 핀치 zoom, list panel, fallback 모드 모두 검증

### "공공교통 zoom 누락" 같은 실수 방지

원인: 새 매니저(Subway/Train/Terminal) 추가 시 zoom 컨트롤러 측 업데이트 누락. 이런 누락 방지하려면:
- 새 매니저 추가 시 **이 체크리스트 6개 시스템 모두 grep**으로 다른 매니저(예: DataManager) 참조 위치 다 찾고, 같은 위치에 새 매니저 참조 추가
- 예: `grep -rn "DataManager" Assets/Scripts/` 결과를 보고 각 위치에서 새 매니저도 같이 처리해야 하는지 판단
- 또는 **공공교통 매니저 3개를 묶어서 `ITransportManager` 같은 추상화** (장기적 리팩토링)

### 매니저가 아닌 스폰 — 보물찾기 AR 보물 (`R0926HuntTreasure`, 2026-10-12)

보물찾기 금빛 상자는 데이터 매니저가 아니다(캐시·배분 없이 서버 `/near` 가 준 자리에 하나씩). 그래도 아래 시스템을 고칠 땐 **이 호출도 같이 유지**할 것:

| 시스템 | 보물 쪽 호출 |
|--------|-------------|
| 줌 (`ARObjectZoomController.ApplyZoomToARObjects`) | `R0926HuntTreasure.SpawnedObjects` 도 같은 배율 — 띄울 때 맨 위 1 로 두고 `ARObjectZoomController.Fit(go)` |
| 백그라운드 복귀·fallback (`LoadingManager`) | `SetObjectsActive` · `SetRenderersVisible` · `RetryFailedAnchors` — 복귀 순서는 ar-object-spawn.md 그대로 |
| 하늘 날씨 '가까운 오브젝트' (`R0926SkyWeather`) | `SpawnedObjects` 를 함께 센다 |
| 가까이 카드 (`R0926NearModal`) | `SpawnedObjects` 를 `Kind.Hunt` 로 함께 모은다 — 카드에서 두 번 탭 = `TapFromModal` |
| `Target` | `PlaceName`(게임 제목) · `placeId = hunt_<id>` · 금색 · `OnIndicatorTapped` = 판정 |

**일부러 안 넣은 것**: 1~4번(`IPlaceCacheProvider` · `FilterManager` · `PlaceListManager`) — 목록·지도에 뜨면 보물 위치가 드러난다. `HiddenPlaces`(X 숨기기)·`R0926DistanceFade` 도 연결하지 않는다. 상세는 [plans/treasure-hunt.md](plans/treasure-hunt.md) 5절.
