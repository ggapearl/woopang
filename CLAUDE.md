# WOOPANG 프로젝트 작업 가이드

> Claude Code가 이 프로젝트에서 작업할 때 참고하는 가이드라인
> 새 대화 시작 시 자동으로 읽힘

---

## 1. 프로젝트 개요

- **프로젝트**: WOOPANG - AR 기반 SNS 앱 (Unity)
- **타겟**: 100만~1000만 사용자 규모
- **상태**: 개발 중 (앱서비스 활성화 전)
- **폰트**: AppleSDGothicNeoM.ttf (모든 UI에 적용)

---

## 2. 핵심 작업 원칙

### 2.1 개발 방향
```
- 단순하게 X → 사용자 친화적으로 꼼꼼하게
- 필요한 기능/로직을 충분히 갖추도록 작업
- 대규모 사용자를 고려한 안정적인 구조
- 복잡하더라도 완성도 높게
```

### 2.2 기존 코드 존중
```
⚠️ 중요: 기존에 잘 작동하는 로직은 함부로 삭제/변경 금지
- 기존 기능 안에서 추가/개선하는 방향
- 삭제가 필요하면 반드시 먼저 제안
- 수정 시 기존 기능이 초기화되지 않도록 주의
```

### 2.3 완전한 작업 수행
```
⚠️ 중요: 로직 작업 시 UI 오브젝트까지 생성하고 연결 완료해야 함

Claude가 해야 할 것:
1. 스크립트 생성/수정 (로직 구현)
2. 에디터 스크립트로 UI 오브젝트 생성 (Assets/Scripts/Editor/)
3. 컴포넌트 연결 (스크립트 → 게임오브젝트)
4. Inspector 필드 → 오브젝트 연결
5. 누락된 참조 체크 (AutoConnectFields 패턴 사용)
6. Play 모드 또는 씬 로드 시 정상 동작 확인
```

### 2.3.1 UI 자동 생성 절차 (필수 - 씬 로드 시 자동 실행)
```
🚫 절대 금지: [MenuItem] 메뉴 방식으로 UI 생성 스크립트 작성
   - 사용자가 수동으로 실행해야 하는 방식은 사용 금지
   - "WOOPANG/Setup/..." 같은 메뉴 아이템 생성 X

⚠️ 중요: 수동 메뉴 실행 X → 씬 로드 시 자동으로 UI 생성/연결

1. 로직 스크립트에 필드 추가
   [Header("=== 새 기능 ===")]
   public GameObject newPanel;
   public Button confirmButton;
   public Text messageText;

2. 에디터 스크립트에 자동 실행 트리거 3개 설정
   [InitializeOnLoad]
   public class MySetup {
       static MySetup() {
           EditorSceneManager.sceneOpened += OnSceneOpened;
           EditorApplication.delayCall += CheckAndSetupUI;
       }

       // ⭐ 스크립트 컴파일 완료 시 자동 실행 (필수!)
       [DidReloadScripts]
       private static void OnScriptsReloaded() {
           EditorApplication.delayCall += CheckAndSetupUISilent;
       }

       private static void OnSceneOpened(...) {
           EditorApplication.delayCall += CheckAndSetupUISilent;
       }
   }

   트리거 시점:
   - static 생성자: 에디터 시작 시
   - [DidReloadScripts]: 스크립트 컴파일 완료 시 ⭐
   - sceneOpened: 씬 열 때

3. 부모 오브젝트 구조 분석 후 올바른 위치에 생성
   - 부모 panel의 자식 구조 확인 (씬 파일 분석)
   - 다이얼로그/오버레이는 SetAsLastSibling()으로 맨 앞에 표시
   - 기존 UI와 충돌하지 않는 위치 선정

4. 체크 및 생성 로직 작성 (없을 때만 생성)
   private static void CheckAndSetupUISilent() {
       Manager mgr = Object.FindFirstObjectByType<Manager>();
       if (mgr == null || mgr.newPanel != null) return;
       // 생성 + 연결
       GameObject panel = CreateNewPanel(mgr.parentPanel.transform, ...);
       panel.transform.SetAsLastSibling(); // 다른 UI 위에 표시
       mgr.newPanel = panel;
       EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
   }

5. AutoConnectFields() 메서드 보완 (런타임 fallback)
   if (newPanel == null)
       newPanel = transform.Find("NewPanel")?.gameObject;

6. 씬 저장 시 변경사항 자동 반영 확인
```

### 2.3.2 UI 자동 연결 체크리스트
```
[ ] [InitializeOnLoad] + [DidReloadScripts] + sceneOpened 3개 트리거가 있는가?
[ ] 스크립트 컴파일 완료 시 자동으로 UI가 생성/연결되는가? (수동 실행 X)
[ ] 부모 오브젝트 구조를 분석해서 올바른 위치에 생성하는가?
[ ] 오버레이/다이얼로그는 SetAsLastSibling()으로 맨 앞에 표시하는가?
[ ] 이미 존재하면 생성 스킵 + 연결만 수행하는가?
[ ] AutoConnectFields로 런타임 fallback이 있는가?
[ ] AppleSDGothicNeoM 폰트가 적용되어 있는가?
[ ] EditorSceneManager.MarkSceneDirty() 호출하는가?
[ ] EditorUtility.SetDirty(manager) 호출하는가?
[ ] Play 모드에서 정상 동작하는가?
[ ] UI 사이즈가 하드코딩 되어있지 않고 Inspector에서 조절 가능한가?
```

### 2.3.3 UI 사이즈 하드코딩 금지 (필수)
```
⚠️ 중요: UI 오브젝트의 사이즈는 하드코딩 X → Inspector에서 조절 가능하게

원칙:
- 모든 UI 사이즈(width, height, padding, spacing 등)는 코드에 고정값 X
- public 필드 또는 [SerializeField]로 Inspector에서 조절 가능하게
- 기본값은 상수로 정의하되, 사용자가 덮어쓸 수 있도록

❌ 하지 말 것:
   rect.sizeDelta = new Vector2(300f, 80f);  // 직접 하드코딩
   hlg.spacing = 30f;                         // 직접 하드코딩

✅ 해야 할 것:
   // 매니저 클래스에 조절 가능한 필드 추가
   [Header("UI 사이즈 설정")]
   public Vector2 containerSize = new Vector2(300f, 80f);
   public float iconSpacing = 30f;
   public Vector2 buttonSize = new Vector2(50f, 50f);

   // 에디터 스크립트에서 해당 값 사용
   Vector2 size = manager.containerSize != Vector2.zero
                  ? manager.containerSize
                  : DEFAULT_SIZE;
   rect.sizeDelta = size;

   // 상수는 fallback용으로만 사용
   private static readonly Vector2 DEFAULT_SIZE = new Vector2(300f, 80f);

장점:
- 사용자가 Inspector에서 크기 조절 가능
- 코드 수정 없이 디자인 변경 가능
- 다양한 화면 크기에 대응 용이
```

### 2.3.4 하드코딩 필드 Inspector 표시 규칙 (필수)
```
⚠️ "하드코딩 해줘" 요청 시 반드시 적용:

1. 해당 필드의 Tooltip에 [HARDCODED] 추가
   [Tooltip("오브젝트 스폰 딜레이 (초) [HARDCODED]")]
   public float objectSpawnDelay = 0.2f;

2. CustomEditor 스크립트에서 초록색 배경 + "HARDCODED" 뱃지 표시
   - 배경색: new Color(0.18f, 0.55f, 0.34f, 0.35f)
   - 라벨색: new Color(0.3f, 0.85f, 0.5f, 1f)
   - 읽기 전용 (DisabledScope)
   - "HARDCODED" 뱃지 우측 표시

3. 이미 CustomEditor가 있으면 hardcodedFields 배열에 추가
   없으면 새로 생성 (Assets/Scripts/Editor/{클래스명}Editor.cs)

4. 참고 패턴: DoubleTap3DEditor.cs, DataManagerEditor.cs
```

### 2.4 코드 품질
```
지양:
- 불필요한 Debug.Log
- 과도한 주석
- 사용하지 않는 코드

지향:
- 깔끔하고 정리된 코드
- 필수적인 에러 로그만 유지
- 재사용 가능한 구조
```

### 2.5 적극적인 제안
```
언제든 제안 환영:
- 개선할 로직/기능
- 추가할 UI 요소
- 더 나은 사용자 경험
- 성능 최적화 방안
```

---

## 3. UI/디자인 성향

### 3.1 일반 원칙
```
- 과하지 않고 심플하게
- 느리고 부드러운 애니메이션
- 고급스럽고 프리미엄한 느낌
- 모든 상황/각도에서 동작하는 완성도
```

### 3.2 애니메이션 속도 기준
| 유형 | 선호 속도 |
|------|-----------|
| 호버 효과 | 0.2s ~ 0.4s |
| 페이드 인/아웃 | 1s ~ 5s (느리게) |
| 반복 애니메이션 | 10s ~ 25s 주기 |
| 배경 색상 움직임 | 매우 느리게 |

### 3.3 시각적 효과 선호
```
선호:
✓ 글로우/빛 번짐 효과
✓ 그라데이션 배경
✓ 부드러운 그림자
✓ 미묘한 패럴랙스
✓ 호버 시 자연스러운 확대/강조

비선호:
✗ 급격한 색상 변화
✗ 과도한 바운스 효과
✗ 요란한 알림음
✗ 번쩍임 효과
```

### 3.4 폰트 설정
```csharp
// 모든 UI에 적용
Font: AppleSDGothicNeoM.ttf
경로: Assets/Fonts/AppleSDGothicNeoM.ttf

// 코드에서 로드
Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
```

---

## 4. 코드 작성 스타일

### 4.1 구조 선호
```csharp
// ============================================================
// 섹션 제목 - 영어/한국어 병행
// ============================================================

// 설정값은 상단에 모아두기
[Header("설정")]
public float animationDuration = 2f;
public float fadeTime = 3f;
```

### 4.2 에러 처리
```csharp
// 조용한 실패 처리 선호 (사용자에게 불필요한 에러 노출 지양)
try {
    riskyOperation();
} catch(e) {
    Debug.LogWarning($"[클래스명] 처리 실패: {e.Message}");
    // fallback 동작
}
```

### 4.3 Null 체크 패턴
```csharp
// 필드 연결 확인 필수
if (targetField == null)
{
    Debug.LogError($"[클래스명] targetField가 null입니다! 인스펙터에서 연결 필요");
    return;
}
```

---

## 5. 체크리스트

### 작업 전 확인
```
[ ] 기존 코드 먼저 읽었는가?
[ ] 기존 기능에 영향 없는가?
[ ] 삭제할 코드가 있다면 먼저 제안했는가?
```

### 작업 중 확인
```
[ ] 모든 필드 참조가 연결되어 있는가?
[ ] null 체크가 충분한가?
[ ] AppleSDGothicNeoM 폰트를 적용했는가?
[ ] 불필요한 Debug.Log를 제거했는가?
[ ] UI 사이즈가 하드코딩 없이 Inspector에서 조절 가능한가?
```

### 작업 후 확인
```
[ ] 모든 상황에서 동작하는가?
[ ] 엣지 케이스를 고려했는가?
[ ] 추가로 개선할 점이 있는가? (있다면 제안)
```

---

## 6. 프로젝트 구조 참고

```
Assets/
├── Scripts/
│   ├── Core/          # 핵심 시스템
│   ├── DM/            # 다이렉트 메시지 관련
│   ├── Download/      # 데이터 다운로드/관리
│   ├── Editor/        # 에디터 확장
│   ├── Firebase/      # Firebase 연동
│   ├── P2P/           # P2P 통신
│   ├── UI/            # UI 관련
│   └── upload/        # 업로드 관련
├── Prefabs/
│   └── DM/            # DM 관련 프리팹
├── Fonts/
│   └── AppleSDGothicNeoM.ttf
└── Scenes/
    └── WP_0111.unity  # 메인 씬
```

---

## 7. 주요 매니저 클래스

| 클래스 | 역할 |
|--------|------|
| MessagePanelManager | DM 대화 목록 패널 |
| ChatPanelManager | 1:1 채팅방 |
| CommentManager | 댓글 패널 |
| FollowManager | 팔로워/팔로잉 목록 |
| ProfileManager | 프로필 관리 |
| LoginManager | 로그인/인증 |
| P2PManager | P2P 사용자 위치 공유 |

---

## 8. 키워드 정리

```
#꼼꼼함 #디테일 #부드러움 #심플함 #고급스러움
#모든각도 #엣지케이스 #직관적 #재사용가능
#느린애니메이션 #조용한사운드 #패럴랙스
#글로우효과 #호버피드백 #모바일대응
#대규모사용자 #안정성 #완성도
```

---

## 9. 마켓 배포 변경사항 정리

```
사용자가 "마켓에 올릴 변경사항 정리해줘" 등 요청 시:
- 반드시 한국어 + 영어 두 버전 모두 작성
- 아래 형식으로 간결하게 (한 줄에 하나씩, 글머리 기호 없이)
- 기술 용어보다 사용자 관점의 개선사항 중심으로

형식 예시:
---
[한국어]
HTTPS 보안 통신 전면 적용
DM 시스템 UI 개선
전반적인 안정성 및 버그 수정

[English]
Full HTTPS secure communication
DM system UI improvements
Overall stability and bug fixes
---
```

---

## 10. Git 커밋 규칙

### 10.1 의존 파일 누락 방지 (필수)
```
⚠️ 중요: 커밋 시 의존 관계가 있는 파일을 반드시 함께 포함

실제 발생한 사례:
- CommentManager.cs에서 MobileKeyboardHandler.DismissKeyboardOnly() 호출
- CommentManager.cs만 커밋하고 MobileKeyboardHandler.cs는 누락
- iOS 빌드 시 "does not contain a definition for 'DismissKeyboardOnly'" 에러 발생
- 로컬에서는 두 파일 모두 있으므로 에러가 안 나지만, 빌드 서버는 커밋된 버전만 사용

커밋 전 체크리스트:
[ ] 수정한 파일에서 호출하는 메서드가 다른 파일에 있는가?
[ ] 그 파일도 함께 커밋에 포함했는가?
[ ] git diff HEAD --name-only로 스테이징된 파일 목록 재확인
[ ] 새로 추가한 public 메서드/클래스를 참조하는 파일이 있다면 함께 커밋
```

### 10.2 씬 파일 커밋 규칙
```
- WP_0111.unity 씬 파일은 항상 커밋에 포함 (절대 빠뜨리지 말 것)
- 프리팹만 수정해도 씬 파일에 미커밋 변경사항이 쌓일 수 있으므로 항상 같이 커밋
```

### 10.3 Untracked 파일 확인 (필수)
```
⚠️ 중요: 커밋 전 반드시 untracked 파일을 확인하고 포함

新규 스크립트 추가 시 발생하는 문제:
- Assets/Scripts/ 폴더에 새 파일을 생성했지만 git add에서 누락
- 예: CubeTouchRotator.cs, 에디터 스크립트들
- 다른 컴퓨터에서 pull 시 파일 부재 → 씬의 컴포넌트 참조 끊김

커밋 전 절차:
[ ] git status로 전체 변경사항 확인
[ ] Untracked files에 새로 추가한 스크립트가 있는가?
[ ] Assets/ 폴더 내 모든 변경사항 확인 (Assets/Scripts/, Assets/Editor/ 등)
[ ] git add Assets/로 폴더 전체 추가 (수정 + untracked 파일 모두)
[ ] git status로 다시 확인
[ ] git commit → git push

올바른 커밋 순서:
1. 코드 작성/수정 완료
2. git status  # 전체 확인
3. git add Assets/  # Assets 폴더 전체 추가
4. git status  # 스테이징 재확인
5. git commit -m "..."
6. git push
```

---

## 11. 마케팅 / 캠페인 핵심 로직

### 11.1 "Look Up" 캠페인 컨셉
```
핵심 메시지: 다른 SNS는 고개를 숙이게 만들지만, WOOPANG은 고개를 들게 만든다.

- AR 오브젝트가 하늘/높은 곳에도 배치됨 → 터치하려면 허리를 펴고 고개를 들어야 함
- 자연스럽게 건강한 자세를 유도하는 "Healthy Social" 앱
- 기존 SNS (Instagram, Facebook, TikTok, Google Maps)는 모두 고개를 숙이고 스크롤
- WOOPANG은 현실 공간을 걸어다니며 AR로 세상을 탐험 → 자연스러운 운동 + 바깥 활동
```

### 11.2 차별화 포인트 (vs 기존 SNS)
```
WOOPANG만의 강점:
- 실제 근처에 있는 사람만 팔로우 가능 → 진짜 만남, 가짜 없음
- 가게에 들어가지 않아도 AR로 인테리어/정보 확인 가능
- 화장실 분리 여부, 애견동반 가능 여부 등 실용 정보 제공
- 검색 없이 주변을 둘러보면 정보가 보임 (AR 오버레이)
- 모든 정보가 위치 기반 → 가짜 리뷰/봇 계정 원천 차단

기존 SNS 단점 (비교 포인트):
- Instagram/Facebook: 가짜 계정, 봇, 조작된 리뷰, 중독성 스크롤
- Google Maps: 가짜/매수 리뷰, 실제와 다른 사진, 들어가봐야 아는 정보
- TikTok: 무한 스크롤 중독, 비현실적 콘텐츠, 실제 연결 부재
- 공통: 항상 고개 숙임, 실내에서만 사용, 실제 만남 없음
```

### 11.3 광고 영상 기본 구조 (타임라인)
```
[0-6초]   오프닝: 사람들이 고개 숙이고 핸드폰 보는 일상 (어둡고 우울한 톤)
[6-8초]   전환: "But what if..." 텍스트 또는 나레이션
[8-8.5초] Instagram 아이콘 + 고개 숙인 사람 (빠른 컷)
[8.5-9초] Facebook 아이콘 + 고개 숙인 사람 (빠른 컷)
[9-9.5초] TikTok/Google Maps 아이콘 + 고개 숙인 사람 (빠른 컷)
[9.5-14초] WOOPANG 앱 사용 장면: 고개 들고 하늘 보며 AR 탐험 (밝고 활기찬 톤)
[14-15초] WOOPANG 로고 + 슬로건 "Look Up. Live Real."
```

---

## 12. 공공 장소 데이터 일괄 등록 가이드

### 12.1 카테고리 분류
```
⚠️ 모든 공공데이터 색상: NULL (DB에서 color 컬럼 비워둠, 앱 기본색 white로 처리)
⚠️ 공공데이터 카테고리는 publicData 토글로 필터링 (TourAPI와 동일)
⚠️ 공공화장실(toilet)만 별도 카테고리 토글 (보라색)

[publicData 토글 — 공공데이터]
gov        - 시청, 군청, 구청, 경찰서, 소방서, 읍면동 행정복지센터
edu        - 대학교, 도서관, 교육청, 교육지원청
park       - 산 정상(고도 필수), 강/호수, 국립공원, 해수욕장, 공원
utility    - 농협, 우체국
landmark   - 역사 유적지, 다리, 지역 특화 명소, 온천
medical    - 보건소, 종합병원, 보건지소
culture    - 박물관, 미술관, 문화예술회관
sport      - 종합운동장, 체육관
religious  - 주요 사찰, 성당/성지, 향교/서원 (역사적 건축물)
welfare    - 종합사회복지관, 노인복지관, 장애인복지관

[별도 카테고리 토글]
toilet     - 공공화장실 (보라색, CategoryFilterState.Toilet)

[기존 카테고리 토글 — 사용자 데이터]
shop       - 샵/매장 (파란색)
food       - 음식점 (주황색)
cafe       - 카페 (핑크색)

[기존 별도 토글]
hub(터미널) - 고속도로 휴게소는 terminal 토글에 포함 (이미 추가 완료)
```

### 12.2 이미지 파일 규칙 (필수)
```
⚠️ 중요: 폴더 내에는 아래 파일명만 허용. 다른 파일은 저장하지 않는다.

허용 파일명:
   main.jpg      ← 로고 또는 대표 이미지 (필수, 최소 1장)
   sub_01.jpg     ← 보조 사진 1 (선택)
   sub_02.jpg     ← 보조 사진 2 (선택)
   sub_03.jpg     ← 보조 사진 3 (선택)
   sub_04.jpg     ← 보조 사진 4 (선택, 최대)

규칙:
- 최소 1장(main.jpg), 최대 5장(main + sub 4장)
- 원본 파일명(logo.png, visual_1.jpg 등)으로 저장 금지
- 다운로드 후 반드시 main.jpg / sub_XX.jpg로 변환하여 저장
- 불필요한 원본 파일은 삭제

main.jpg 1:1 비율 규칙:
- main.jpg는 반드시 가로세로 1:1 비율이어야 함
- 최종 저장 크기: 444x444px (JPEG, quality=95)

방법 1: 고해상도 로고/사진이 있는 경우
  from PIL import Image
  logo = Image.open('원본.png')
  size = max(logo.size)
  canvas = Image.new('RGB', (size, size), (255, 255, 255))
  canvas.paste(logo, ((size-logo.width)//2, (size-logo.height)//2))
  canvas = canvas.resize((444, 444), Image.LANCZOS)
  canvas.save('main.jpg', 'JPEG', quality=95)

방법 2: 로고가 저해상도(250px 미만)인 경우 → 상급기관 심볼마크 SVG 활용
  ⚠️ 저해상도 로고를 확대하면 픽셀화 발생 → SVG 벡터 활용 필수
  1. 나무위키/공식사이트에서 상급기관 SVG 로고 검색
  2. SVG에서 심볼 path만 추출 (fill 색상으로 구분: #e6280f, #004799, #0f9e33 등)
  3. 심볼만 포함하는 새 SVG 생성 (흰색 배경, 적절한 viewBox)
  4. Chrome Headless로 800x800 PNG 렌더링:
     chrome --headless --disable-gpu --screenshot=output.png --window-size=800,800 render.html
  5. Pillow로 444x444 JPEG 변환:
     img = Image.open('output.png').convert('RGB')
     img = img.resize((444, 444), Image.LANCZOS)
     img.save('main.jpg', 'JPEG', quality=95)
  6. 임시 파일(svg, html, png) 정리

방법 3: 로고를 구할 수 없는 경우
  → sub 사진(건물/내부) 중 대표적인 것을 1:1 중앙 크롭하여 main.jpg로 사용
  from PIL import Image
  img = Image.open('sub_01.jpg')
  w, h = img.size
  size = min(w, h)
  left, top = (w - size) // 2, (h - size) // 2
  cropped = img.crop((left, top, left + size, top + size))
  cropped = cropped.resize((444, 444), Image.LANCZOS)
  cropped.save('main.jpg', 'JPEG', quality=95)
```

### 12.2.1 공공데이터 이미지 자동 생성 규칙 (필수)
```
⚠️ 중요: 공공데이터 INSERT 시 이미지를 반드시 함께 생성해야 함
   main_photo, folder, sub_photos가 비어있으면 앱에서 오브젝트 생성 실패

카테고리별 이미지 생성 전략:
┌─────────────┬──────────────────────────────────────────────┐
│ 카테고리    │ 이미지 생성 방법                             │
├─────────────┼──────────────────────────────────────────────┤
│ 주요 거점   │ 공식 사이트에서 로고/사진 수집               │
│ (landmark,  │ → main.jpg (444x444) + sub_01~04.jpg         │
│  culture,   │                                              │
│  religious) │                                              │
├─────────────┼──────────────────────────────────────────────┤
│ 일반 공공   │ 카테고리별 심볼 아이콘 자동 생성 (Pillow)    │
│ (gov, edu,  │ → main.jpg = sub_01.jpg (동일 파일)          │
│  medical,   │ 실제 사진 구할 수 없는 경우 심볼로 대체      │
│  sport,     │                                              │
│  welfare,   │                                              │
│  utility)   │                                              │
├─────────────┼──────────────────────────────────────────────┤
│ toilet      │ SVG 심볼 아이콘 직접 생성                    │
│ (공공화장실)│ → _shared_toilet/ 공용 폴더 1개 사용         │
│             │ → 모든 화장실이 동일 폴더/파일 공유          │
│             │ → 각 폴더에 파일 복제 불필요                 │
└─────────────┴──────────────────────────────────────────────┘

sub 사진 규칙:
- 주요 거점(박물관, 종합운동장, 랜드마크 등): 실제 사진 sub_01~04 수집
- 일반 공공(행정복지센터, 보건소 등): main.jpg를 sub_01.jpg로 복제 (동일 파일)
- 화장실: 절대 실사 sub 없음 → main.jpg = sub_01.jpg 동일 SVG 아이콘

카테고리별 심볼 아이콘 색상:
- gov:       (45, 85, 130)  — 파란 계열 (관공서)
- medical:   (40, 110, 80)  — 녹색 계열 (보건)
- edu:       (55, 90, 140)  — 파란 계열 (교육)
- sport:     (50, 120, 90)  — 녹색 계열 (체육)
- religious: (100, 75, 55)  — 갈색 계열 (전통)
- landmark:  (70, 130, 180) — 하늘색 계열 (명소)
- toilet:    (88, 55, 140)  — 보라색 계열 (화장실)

생성 스크립트: server/generate_public_icons.py
실행: python server/generate_public_icons.py
```

### 12.3 출처 추적 (username 필드)
```
⚠️ 중요: username 필드에 이미지 출처를 기록한다. 'admin' 사용 금지.

출처 표기 규칙:
- 공식 홈페이지: 도메인 대문자 (예: YESAN.GO.KR, CNE.GO.KR)
- Wikimedia Commons: WIKIMEDIA
- 공공누리: GONURI
- 공공데이터포털: DATA.GO.KR
- 직접 촬영: WOOPANG (자체 촬영)

DB INSERT 시:
   username = '{출처}'    ← 'admin' 대신 실제 이미지 출처 기록
```

### 12.4 DB INSERT 절차
```
1. 장소 정보 수집
   - 이름, 주소, 좌표(위도/경도/고도), 카테고리
   - 공식 홈페이지에서 로고/건물사진 다운로드

2. 이미지 저장 구조 (필수)
   uploads/
   └── {YYYYMMDD}_{장소명}/
       ├── main.jpg      ← 로고 또는 대표 이미지 (1:1 비율 필수)
       ├── sub_01.jpg     ← 내부/외관 사진 1
       └── sub_02.jpg     ← 내부/외관 사진 2
   ※ 위 파일 외 다른 파일 저장 금지

3. DB INSERT 양식
   INSERT INTO locations (
       username, name, latitude, longitude, altitude,
       pet_friendly, separate_restroom, instagram_id,
       color, category, status, folder, main_photo, sub_photos,
       model_type, model_scale, created_at
   ) VALUES (
       '{출처}', '{이름}', {위도}, {경도}, {고도},
       {pet_friendly}, {separate_restroom}, '',
       '{색상}', '{카테고리}', 'approved',
       'uploads/{폴더명}', 'uploads/{폴더명}/main.jpg',
       '["uploads/{폴더명}/sub_01.jpg","uploads/{폴더명}/sub_02.jpg"]',
       'cube', 1.0, CURRENT_TIMESTAMP
   )

4. 색상 규칙
   gov (시청/군청/구청/경찰서/소방서/행정복지센터): color='blue'
   toilet (공공화장실): color='purple'
   나머지 (edu/park/medical/culture/sport/utility/landmark/religious/welfare): color=NULL
   ※ color='white' 사용 금지 → NULL로 설정

5. 토글 규칙 (pet_friendly / separate_restroom)
   ⚠️ 중요: DB INSERT 시 반드시 적용

   separate_restroom (화장실분리):
   - 한국 공공기관 전체: true (gov/medical/edu/culture/sport/welfare/utility)
   - 일본 공공기관: false (기본값, 화장실 분리가 보편적이지 않음)
   - 공원/산/해수욕장: false (별도 공중화장실 등록으로 대체)

   pet_friendly (애견동반):
   - 반려견 놀이터/전용 공간: true
   - 한강공원/근린공원/체육공원/산책로/숲길: true (일반적으로 가능)
   - 공원이지만 반려견 금지인 곳: false (예: 신주쿠교엔)
   - 국립공원/자연휴양림: true (외부 산책로 가능)
   - 공공기관(gov/medical/edu 등): false

6. 지하철/기차역/터미널 등록 규칙
   ⚠️ 중요: 지하철역, 기차역, 터미널은 locations 테이블이 아닌 public_facilities 테이블에 관리
   - public_facilities 테이블 (type: subway/train/terminal/airport)
   - SubwayManager, TrainStationManager, TerminalManager가 이 테이블에서 데이터 조회
   - 해당 데이터는 각 매니저의 카테고리 토글로 표시/숨김 제어
   - DB 추가 작업 시 해당 지역 역/터미널 데이터도 public_facilities에 업데이트
   - 기존 main_photo는 수정 금지 (sub 사진 추가는 가능)
   - 위치 이동/폐역/신설역 등 최신 정보를 검색하여 반영
   - 역 근처 공중화장실은 locations 테이블에 toilet 카테고리로 등록 가능
```

### 12.5 이미지 소스 우선순위
```
1순위: 해당 기관 공식 홈페이지 (go.kr 등) - 공공기관 로고/배너
2순위: 공공누리 1유형 (출처 표기만으로 상업적 이용 가능)
3순위: 공공데이터포털 (data.go.kr)
4순위: Wikimedia Commons CC0 / Public Domain
⚠️ 주의: 저작권 미확인 이미지 사용 금지
⚠️ 주의: 서비스 전 반드시 라이선스 재확인
```

### 12.6 삭제 시 FK 순서 (필수)
```
DELETE 순서 (반드시 이 순서대로):
1. comment_likes  (WHERE comment_id IN (SELECT id FROM comments WHERE location_id = %s))
2. comments       (WHERE location_id = %s)
3. location_likes (WHERE location_id = %s)
4. fix_requests   (WHERE target_id = %s)
5. locations_fix  (WHERE target_id = %s)
6. locations      (WHERE id = %s)
```

### 12.7 충청도 데이터 규모 예측
```
충청남도: 약 870건 (15 시군, ~155 읍면동)
충청북도: 약 670건 (11 시군, ~130 읍면동)
합계: 약 1,540건

대부분 Gov(행정복지센터)가 차지 (~380건/전체의 25%)
Utility(농협/우체국)가 두 번째 (~234건)
```

---

## 13. 기획안 / 제안서 (HTML 페이퍼) 작업 가이드

### 13.1 기본 원칙
```
- 모든 기획안/제안서는 단일 HTML 파일로 작성 (외부 의존성 최소화)
- 최종 산출물은 PDF 배포 → 인쇄 시 레이아웃이 깨지지 않아야 함
- 서버 라우팅도 함께 등록하여 웹에서도 열람 가능하게 (woopang.com/경로)
- 파일 위치: documents/ 폴더
```

### 13.2 디자인 톤
```
- 기획안별 컨셉에 맞는 색감 사용 (다크/라이트 자유)
- 폰트: Noto Sans KR (웹), 다국어 지원 필수
- 분량: 스와핑게임 기획안 수준 (10+ 섹션, 에피소드 20개, 수익/제작/글로벌 전략 포함)
- 부족한 내용은 인터넷 검색으로 트렌드/데이터 보충하여 채울 것
```

### 13.3 PDF 인쇄 대응 (필수)
```
⚠️ 중요: @media print 스타일 반드시 포함

필수 규칙:
- 다크 배경 유지: -webkit-print-color-adjust: exact; print-color-adjust: exact;
- 애니메이션 비활성: animation: none !important;
- 그림자/글로우 단순화 (인쇄 시 깨짐 방지)
- page-break-inside: avoid; (섹션 중간 잘림 방지)
- page-break-before: always; (주요 섹션마다 페이지 넘김)
- 배경색/배경이미지 인쇄 허용 설정 포함

@media print 기본 템플릿:
@media print {
    * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
    body { background: #0a0a0a !important; }
    .section { page-break-inside: avoid; page-break-before: always; }
    .animated, .particle, .shimmer { animation: none !important; display: none !important; }
    .no-print { display: none !important; }
}
```

### 13.4 다국어 지원 (필수)
```
⚠️ 중요: 글로벌 OTT/플랫폼 대상 제안서는 반드시 다국어 전환 포함

기본 언어 세트: 한국어(KO) / 영어(EN) / 일본어(JA) / 중국어(ZH) / 스페인어(ES)

구현 방식:
- 상단 중앙에 언어 선택 버튼 배치 (🇰🇷 🇺🇸 🇯🇵 🇨🇳 🇪🇸)
- data-lang 속성으로 텍스트 관리
- JavaScript로 즉시 전환 (페이지 리로드 X)
- 기본값: 영어 (EN) — 글로벌 플랫폼 대상이므로

예시:
<span data-lang="ko">프로그램 개요</span>
<span data-lang="en">Program Overview</span>
<span data-lang="ja">番組概要</span>
```

### 13.5 시각 효과 (웹 전용, 인쇄 시 비활성)
```
웹 열람 시 적용할 효과:
- 계절감 파티클 (꽃잎, 눈, 낙엽 등 — CSS/JS 애니메이션)
- 배경 쉬머/펄스 효과 (은은한 빛 움직임)
- 스크롤 연동 페이드인 (IntersectionObserver)
- 호버 시 카드 글로우 효과

모든 효과에 .animated 또는 .web-only 클래스 부여
→ @media print에서 일괄 숨김 처리
```

### 13.6 파비콘 (자동 생성 필수)
```
⚠️ 기획안 작업 시 반드시 아래 3곳 모두 처리:

1. SVG 파비콘 파일 생성
   - 파일명: {프로젝트명}_favicon.svg
   - 위치: documents/ 폴더 (HTML과 같은 경로)
   - 디자인: 기획안 분위기/색감에 맞는 심볼 + 그라디언트 + 글로우

2. HTML <head>에 연결
   <link rel="icon" type="image/svg+xml" href="{프로젝트명}_favicon.svg">

3. 서버 라우트 등록 (app_improved.py)
   @app.route('/plan/{프로젝트명}_favicon.svg')
   @app.route('/{프로젝트명}_favicon.svg')  ← 레거시 경로 호환
   def {프로젝트명}_favicon():
       return send_from_directory(r'C:\woopang\documents', '{프로젝트명}_favicon.svg', mimetype='image/svg+xml')

4. 북마크 페이지 파비콘 매핑 (bookmark.html)
   bookmark.html의 getFaviconUrl() 함수 내 planFavicons 객체에 추가:
   '{프로젝트명}': '/plan/{프로젝트명}_favicon.svg'
```

### 13.7 서버 라우팅 연동
```
기획안 접속 경로: /plan/{프로젝트명}

기획안 추가 시 수정할 파일 3개:
1. app_improved.py — HTML 라우트 + 파비콘 라우트 추가
2. bookmark.html — planFavicons 객체에 파비콘 경로 추가
3. documents/ — HTML 파일 + SVG 파비콘 파일 생성

현재 등록된 기획안:
- /plan/swapping → documents/swapping_game_proposal.html + swapping_favicon.svg
- /plan/dogting  → documents/dogting_proposal.html + dogting_favicon.svg
```

### 13.8 하단 푸터 (고정 정보)
```
⚠️ 모든 기획안에 동일하게 적용:

© 2019. 쾌엔터테인먼트. All rights reserved. CONFIDENTIAL.
기획자 : 김영훈 | 연락처 : +82 10.4444.4395

다국어 번역 시에도 기획자명/연락처 포함:
- EN: Producer : Young-Hoon Kim | Contact : +82 10.4444.4395
- JA: 企画者 : キム・ヨンフン | 連絡先 : +82 10.4444.4395
- ZH: 策划人 : 金英勋 | 联系方式 : +82 10.4444.4395
- ES: Productor : Young-Hoon Kim | Contacto : +82 10.4444.4395
```

---

*최종 업데이트: 2026-03-22 (기획안 파비콘 3곳 처리 체크리스트 추가, 서버+북마크+로컬 경로 정리)*
