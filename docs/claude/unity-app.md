# Unity 앱 개발 가이드

> Unity 앱 (WOOPANG) 작업 시 참조

---

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── Core/               # 핵심 시스템
│   ├── DM/                 # 다이렉트 메시지 관련
│   ├── Download/           # 데이터 다운로드/관리 (5개 데이터 매니저 — docs/claude/data-managers.md)
│   ├── Download_Model/     # GLB 모델 로딩 (GLBModelLoader — docs/claude/3d-generation.md)
│   ├── Editor/             # 에디터 확장 (UI 자동 생성 스크립트)
│   ├── Firebase/           # Firebase 연동
│   ├── OffScreenIndicator/ # 화면 밖 타겟 화살표 인디케이터
│   ├── P2P/                # P2P 통신
│   ├── Prefab/             # 프리팹 런타임 스크립트 (CustomARGeospatialCreatorAnchor 등)
│   ├── UI/                 # UI 관련 (LoadingManager, FilterManager 등)
│   └── upload/             # 업로드 관련
│   (그 외: Button/Config/DeviceTracker/Geoid/Session/touch/Tween/Update/Utils 등)
├── Prefabs/
│   └── DM/                 # DM 관련 프리팹
├── Resources/Fonts/
│   └── AppleSDGothicNeoM.ttf
└── Scenes/
    ├── woopang_0529.unity  # ⭐ 현재 활성 메인 씬 (2026-05-29~, git-rules.md와 동기 유지)
    ├── WP_0111.unity       # 구 메인 씬
    └── WP_0111_Android.unity
```

---

## 주요 매니저 클래스

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

## UI 자동 생성 절차 (필수 - 씬 로드 시 자동 실행)

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

### UI 자동 연결 체크리스트
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

---

## UI 사이즈 하드코딩 금지 (필수)
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

---

## 모바일 키보드 입력 미러 패턴 (필수)
```
⚠️ iOS native input toolbar와 키보드 본체 사이에 갭이 생겨
   AR 카메라/배경이 비치는 문제 해결용 패턴.
   업로드/수정 페이지의 InputField는 모두 "미러 입력바"를 통해 입력.

대상 페이지 (현재):
- CubeUploadManager.uploadPage    → nameInput, instagramIDInput
- ModelUploadManager.uploadPage   → nameInput, instagramIDInput
- CubeDataFixManager.fixUIPanel   → nameInput, instagramIDInput, descriptionInput

새 페이지 추가 시 작업:
1. UploadInputMirrorSetup.cs 의 Specs 배열에 ManagerSpec 추가
   new ManagerSpec {
       TypeName = "NewPageManager",
       PageFieldName = "uploadPage",
       InputFieldNames = new[] { "nameInput", ... }
   }
2. 그 외 코드 변경 불필요 — 자동으로 미러 생성/연결됨

핵심 컴포넌트:
- Assets/Scripts/Upload/UploadInputMirror.cs       (런타임 동작)
- Assets/Scripts/Editor/UploadInputMirrorSetup.cs  (씬 자동 생성/연결)
- Assets/Redesign0926/Runtime/R0926InputFocus.cs    (원래 칸의 분홍 테두리 + 깜빡이는 커서 — 미러 칸은 꺼져 있어 Unity 가 커서를 안 그리므로 입력줄 커서를 그대로 얹음)

디자인 일괄 수정 (권장: 프리팹 방식):
- Assets/Prefabs/Upload/UploadInputMirror.prefab 1개만 수정하면
  3개 페이지(Cube/Model/Fix) 모두 자동 반영
- 색상/사이즈/폰트 변경 시 프리팹만 수정 → 코드 수정 불필요
- 새 페이지 추가 시에도 프리팹 그대로 사용

❌ 하지 말 것:
   - Setup의 CreateMirrorPanel()에 색상/사이즈 하드코딩
   - 페이지마다 미러 디자인 따로 만들기

✅ 해야 할 것:
   - 디자인 변경은 prefab에서만
   - Setup은 Instantiate(prefab)만 호출
   - 새 페이지 추가는 Specs 배열에 한 줄만
```

---

## 하드코딩 필드 Inspector 표시 규칙 (필수)
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

---

## UI/UX 공통 규칙 — 수정 → 저장 (필수, 2026-11-17 대표님)
```
글자·메뉴·옵션 등 값을 입력해 고치는 「저장 화면」은 모두 — 앱(Unity)·웹(우기·농민닷컴) 구분 없이 —
「고쳤으면 저장을 눌러야 반영된다」를 화면이 스스로 알려 줘야 한다. 새 저장 화면을 만들 때 기본으로 넣는다.

1. 고친 묶음(섹션 카드 · 시트)의 테두리가 핑크로 바뀐다
   - 테두리 #F06595 + 바깥 링 0 0 0 3px rgba(233,30,99,.14)
   - 고치지 않은 다른 묶음은 그대로 (화면 전체가 아니라 「고친 곳」만)
2. 같은 묶음의 저장 버튼이 핑크(#E91E63, 흰 글자)가 되고 펄스(숨 쉬듯 퍼지는 링)
   - 1.5s ease-out infinite, 링 rgba(233,30,99,.45) → 0 (9px 까지 퍼짐)
   - prefers-reduced-motion 이면 펄스만 끈다 (색은 유지)
3. 저장되면 두 표시가 모두 사라진다(화면이 다시 그려지며 초기화). 저장이 안 되면(오류·검증 실패) 그대로 둔다
4. 시트·모달을 다시 열면 깨끗한 상태로 (열 때 고침 표시를 0으로)
5. 모양·움직임의 기준 = 농민닷컴 판매자센터 상품관리 (`C:\nongmin-app\server\templates\seller_product_edit.html`
   의 `.pe-sec.dirty` · `.save-btn` · `saveBtnPulse`, JS `peMark`). 같은 색 · 같은 움직임으로 맞춘다.
```

**우기 웹앱(`C:\woogy-app`, `server/static/app.js` · `app.css`) 구현 패턴**
- 마크업: 고치는 묶음에 `data-edit="그룹"`, 그 묶음의 저장 버튼에 `data-save="그룹"` (그룹 이름은 `ss`·`acct`·`mi`·`pn` 처럼 짧게)
- 입력(`input`/`change`)은 위임 이벤트가 `markEdit()` 로 자동 처리 → 묶음에 `.dirty`, 같은 그룹 `data-save` 버튼에 `.dirty`
- 값을 바꾸는 단추(휴무 · 주소 고르기 · 이모티콘 고르기)는 `data-mark="1"`, 값이 아닌 입력(검색 칸)은 `data-noedit`
- 상태는 `S.dirty[그룹]` — 화면이 다시 그려져도 `edCls('그룹')` 으로 유지, 화면·시트를 처음 열 때 0 으로
- 새 저장 화면을 만들면 `tests/ui_check.js` 에 「고치면 핑크 · 저장 버튼 펄스」 점검을 한 줄 더한다

**Unity 앱(우팡)에 적용할 때**: 고친 영역에 `Outline`(#F06595) 을 켜고, 저장 버튼은 색을 #E91E63 로 바꾸며 스케일 1 → 1.06 으로
느리게 숨 쉬게 한다(1.5초 주기, 급격한 번쩍임 금지 — 위 「UI / 디자인 성향」). 저장하면 둘 다 원래대로.

### 알림 팝업(토스트)은 한 가지로 (2026-11-17 대표님)
일반 모드 · 가맹점 모드 구분 없이 **하나의 모양**: 약간 짙은 파스텔 초록 배경 + 흰 글자 + 노란 테두리, 화면 맨 위 로고 · 알림 종이 있는
**머리줄 안에서만** 뜬다(그 줄 아래 경계를 넘지 않고, 아이폰 노치는 침범하지 않음. 기부카드 탭에서는 지역 · 범위 칸을 가린다).
글자는 14px 이상, **알림은 짧게**(긴 사연 · 안내는 알림에 쓰지 않고 「내 활동」 같은 화면에서 길게 본다).
우기 웹앱은 `toast()` 가 서버 글까지 한 문장 40자 안으로 줄여 준다(`toastShort`).

---

## 마켓 배포 변경사항 정리
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

## UI / 디자인 성향

### 일반 원칙
- 과하지 않고 심플하게
- 느리고 부드러운 애니메이션
- 고급스럽고 프리미엄한 느낌
- 모든 상황/각도에서 동작하는 완성도

### 애니메이션 속도 기준
| 유형 | 선호 속도 |
|------|-----------|
| 호버 효과 | 0.2s ~ 0.4s |
| 페이드 인/아웃 | 1s ~ 5s (느리게) |
| 반복 애니메이션 | 10s ~ 25s 주기 |
| 배경 색상 움직임 | 매우 느리게 |

### 시각적 효과 선호
- ✓ 글로우/빛 번짐, 그라데이션 배경, 부드러운 그림자, 미묘한 패럴랙스, 호버 시 자연스러운 확대
- ✗ 급격한 색상 변화, 과도한 바운스, 요란한 알림음, 번쩍임

### 폰트 로드
```csharp
Font customFont = Resources.Load<Font>("Fonts/AppleSDGothicNeoM");
```

---

## 코드 작성 스타일

### 구조 선호
```csharp
// ============================================================
// 섹션 제목 - 영어/한국어 병행
// ============================================================

[Header("설정")]
public float animationDuration = 2f;
```

### 에러 처리 (조용한 실패 선호)
```csharp
try {
    riskyOperation();
} catch (System.Exception e) {
    Debug.LogWarning($"[클래스명] 처리 실패: {e.Message}");
    // fallback 동작
}
```

### Null 체크 (필드 연결 확인 필수)
```csharp
if (targetField == null)
{
    Debug.LogError($"[클래스명] targetField가 null입니다! 인스펙터에서 연결 필요");
    return;
}
```

---

## 작업 체크리스트

### 작업 전
- [ ] 기존 코드 먼저 읽었는가?
- [ ] 기존 기능에 영향 없는가?
- [ ] 삭제할 코드는 먼저 제안했는가?

### 작업 중
- [ ] 모든 필드 참조가 연결되어 있는가?
- [ ] null 체크가 충분한가?
- [ ] AppleSDGothicNeoM 폰트를 적용했는가?
- [ ] 불필요한 Debug.Log를 제거했는가?
- [ ] UI 사이즈가 하드코딩 없이 Inspector에서 조절 가능한가?
