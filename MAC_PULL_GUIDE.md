# Mac에서 woopang 프로젝트 pull 및 복구 가이드

## ⚠️ 중요: Git 저장소 위치

**이전**: `~/woopang-assets` (잘못된 구조)
**현재**: `~/woopang` (올바른 Git 저장소 루트)

Git 명령어는 반드시 **`~/woopang`** 디렉토리에서 실행하세요!

## 0. Git 저장소 확인 (처음 한 번만)

```bash
# Git 저장소 루트 확인
cd ~/woopang
git rev-parse --show-toplevel

# 결과가 /Users/yourname/woopang 이어야 합니다
# 만약 다른 경로가 나온다면 올바른 위치가 아닙니다
```

### 기존 woopang-assets에서 마이그레이션하기

만약 기존에 `~/woopang-assets`를 사용하고 있었다면:

```bash
# 1. 백업 (선택사항)
mv ~/woopang-assets ~/woopang-assets-backup

# 2. 새로 클론
cd ~
git clone https://github.com/coreaguy/woopang.git

# 3. Unity에서 새 경로(~/woopang)로 프로젝트 열기
```

## 1. Git Pull 수행

### 방법 1: 기존 작업 유지하면서 pull
```bash
cd ~/woopang  # ⚠️ woopang-assets가 아닙니다!
git stash     # 현재 변경사항 임시 저장
git pull origin main --rebase
```

### 방법 2: 깨끗한 상태로 pull (권장)
```bash
cd ~/woopang  # ⚠️ woopang-assets가 아닙니다!
git fetch origin
git reset --hard origin/main  # 로컬 변경사항 삭제하고 완전히 동기화
```

## 2. Unity 프로젝트 복구

### Step 1: Library 폴더 삭제
```bash
cd ~/woopang
rm -rf Library/
```

### Step 2: Unity에서 프로젝트 재생성
1. Unity Hub를 실행합니다
2. 기존 프로젝트를 **Unity Hub에서 완전히 제거**합니다
   - woopang-assets (옛날 경로)
   - woopang (새 경로, 있다면)
3. Unity Hub에서 "Add" 버튼을 눌러 **~/woopang 폴더**를 추가합니다
   - ⚠️ ~/woopang-assets가 아닙니다!
   - ⚠️ ~/woopang/Assets도 아닙니다!
   - ✅ ~/woopang (Git 저장소 루트)
4. 프로젝트를 엽니다 (Library 폴더가 자동으로 재생성됩니다)

### Step 3: URP 설정 확인
Unity가 완전히 로드된 후:
1. 메뉴: **Edit > Project Settings > Graphics**
2. "Scriptable Render Pipeline Settings" 항목이 **URP-HighFidelity**로 설정되어 있는지 확인
3. 만약 None으로 되어 있다면:
   - Assets/Settings/URP-HighFidelity.asset을 드래그하여 설정

## 3. 에러가 계속되는 경우

### 방법 A: URP 재설치
1. Unity Editor 메뉴: **Window > Package Manager**
2. "Universal RP" 패키지를 찾습니다
3. 우측 상단에서 "Remove" 클릭
4. 다시 "Install" 클릭하여 재설치

### 방법 B: Reimport All
1. Unity Editor 메뉴: **Assets > Reimport All**
2. 모든 에셋이 다시 import됩니다 (시간이 걸릴 수 있습니다)

## 4. Git 브랜치 확인

현재 상태 확인:
```bash
git status
git branch -a
```

올바른 브랜치로 전환:
```bash
git checkout main
git pull origin main
```

## 5. 주의사항

### Git에서 제외된 폴더들:
- `/server/` - 서버 관련 파일
- `/.claude/` - Claude Code 설정
- `/docs/` - 문서
- `/scripts_old/` - 옛날 스크립트

### Git에 포함된 폴더들:
- `Assets/` - Unity 에셋
- `ProjectSettings/` - Unity 프로젝트 설정
- `Packages/` - Unity 패키지 매니페스트

## 6. Firebase SDK 참고사항

Firebase SDK 대용량 파일들은 Git에서 제외되어 있습니다.
필요시 Unity Package Manager를 통해 재설치하세요:

1. **Window > Package Manager**
2. "Packages: Unity Registry" 선택
3. "Firebase" 검색하여 필요한 패키지 설치

## 문제가 해결되지 않을 경우

Unity 콘솔 로그를 확인하여 구체적인 에러 메시지를 찾아주세요.
