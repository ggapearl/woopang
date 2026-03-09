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

---

*최종 업데이트: 2026-03-02*
