# Git 커밋 규칙

> 커밋/푸시 작업 시 참조

---

## 커밋·푸시는 빌드 직전에 한 번 (2026-11-09 대표님 「커밋하고 푸시하는 건 수정 끝나면 빌드하기 전에 한번만 하자」)
- 우팡 앱(Unity) 수정은 고칠 때마다 커밋·푸시하지 않는다. 대기열의 수정을 다 끝내고 검증한 뒤 **빌드하기 직전에 한 번** 커밋·푸시한다.
- 그동안 같은 파일에 다른 세션의 변경이 섞이므로, 내가 고친 덩어리(파일·줄)를 따로 적어 두었다가 마지막에 그것만 스테이징한다.
- 효딩쓰 휴대폰 화면처럼 「커밋·푸시 → 배포」가 곧 반영인 것은 CLAUDE.md 2.0.3 을 따른다.

## 의존 파일 누락 방지 (필수)
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

## 씬 파일 커밋 규칙
```
- 활성 씬 파일은 항상 커밋에 포함 (절대 빠뜨리지 말 것)
- 현재 활성 씬: woopang_0926.unity (2026-09-29 이후 — 새 디자인. 이전 woopang_0529.unity 는 참고용으로 남겨 둠)
- 프리팹만 수정해도 씬 파일에 미커밋 변경사항이 쌓일 수 있으므로 항상 같이 커밋
- 새 씬으로 갈아탈 때는 위 "현재 활성 씬" 줄을 그때 이름으로 업데이트할 것
```

## Untracked 파일 확인 (필수)
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
