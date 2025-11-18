# 📚 Git 기초 개념 및 사용법

## 🎯 Git이란?

파일의 변경사항을 추적하고 관리하는 **버전 관리 시스템**입니다.
- 코드의 히스토리를 저장
- 여러 사람과 협업
- 이전 버전으로 되돌리기
- Windows와 Mac 간 동기화

---

## 📖 Git 주요 개념

### 1️⃣ Working Directory (작업 디렉토리)
- 현재 컴퓨터의 파일들
- Unity에서 수정한 파일들
- **상태**: 아직 Git이 추적하지 않음

### 2️⃣ Staging Area (스테이징 영역)
- 커밋할 준비가 된 파일들
- `git add` 명령으로 추가
- **상태**: 커밋 대기 중

### 3️⃣ Repository (저장소)
- 커밋된 히스토리
- `git commit` 명령으로 저장
- **상태**: 영구적으로 저장됨

### 4️⃣ Remote (원격 저장소)
- GitHub, GitLab 등의 클라우드
- `git push` 명령으로 업로드
- **상태**: 다른 컴퓨터와 공유 가능

---

## 🔄 Git 워크플로우

```
[Working Directory]
    ↓ git add
[Staging Area]
    ↓ git commit
[Local Repository]
    ↓ git push
[Remote Repository (GitHub/GitLab)]
    ↓ git pull (Mac에서)
[Mac의 Working Directory]
```

---

## 💻 Git 주요 명령어

### 📝 git add
**역할**: 파일을 Staging Area에 추가

```bash
# 특정 파일 추가
git add filename.txt

# 여러 파일 추가 (와일드카드)
git add *.shader

# 모든 변경사항 추가
git add .

# 특정 폴더의 모든 파일
git add Assets/Scripts/
```

**예시:**
```bash
# T5 셰이더만 추가
git add Assets/Scripts/Prefab/T5EdgeLine.shader
git add Assets/Scripts/Prefab/T5EdgeLine.shader.meta
```

### 💾 git commit
**역할**: Staging Area의 파일들을 로컬 저장소에 영구 저장

```bash
# 커밋 메시지와 함께 저장
git commit -m "설명 메시지"

# 상세한 커밋 메시지
git commit -m "제목

- 변경사항 1
- 변경사항 2
- 변경사항 3"
```

**커밋 = 스냅샷**
- 특정 시점의 프로젝트 상태를 저장
- 언제든 이 시점으로 되돌릴 수 있음
- 각 커밋은 고유 ID(해시)를 가짐

**예시:**
```bash
git commit -m "Add T5 edge line glow effect

- Implement 12-edge detection shader
- Add tube lighting effect
- Fix URP compatibility"
```

### 📤 git push
**역할**: 로컬 저장소 → 원격 저장소(GitHub/GitLab)

```bash
# 기본 푸시
git push origin main

# 처음 푸시 (upstream 설정)
git push -u origin main
```

### 📥 git pull
**역할**: 원격 저장소 → 로컬 저장소 (Mac에서 사용)

```bash
# Mac에서 Windows의 변경사항 받기
git pull origin main
```

### 📊 git status
**역할**: 현재 상태 확인

```bash
git status
```

**출력 예시:**
```
Changes not staged for commit:  ← 수정했지만 add 안 함
  modified:   Assets/sou/Materials/0000_Cube.mat

Untracked files:  ← 새 파일 (Git이 모름)
  Assets/Scripts/Prefab/T5EdgeLine.shader
```

### 📜 git log
**역할**: 커밋 히스토리 확인

```bash
# 간단히 보기
git log --oneline

# 최근 5개만
git log --oneline -5

# 그래프로 보기
git log --oneline --graph
```

---

## 🎯 현재 상황 분석

### 현재 Git 상태:
```
Changes not staged for commit:
  - Assets/Scripts/Prefab/T5EdgeGlow_URP.shader (수정됨)
  - Assets/sou/Materials/0000_Cube.mat (수정됨)

Staged for commit:
  - Assets/Scripts/Prefab/T5EdgeLine.shader (add 완료)
  - Assets/Scripts/Prefab/T5EdgeLine.shader.meta (add 완료)
```

### 문제:
- `T5EdgeLine.shader`만 add했음
- 다른 수정 파일들은 add 안 됨
- 커밋에 포함되지 않음!

---

## ✅ 해야 할 작업

### 1단계: 모든 T5 관련 파일 추가

```bash
# T5EdgeLine 셰이더 (이미 add됨)
# git add Assets/Scripts/Prefab/T5EdgeLine.shader*  (완료)

# T5EdgeGlow_URP 셰이더 추가
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta

# 0000_Cube 머티리얼 추가
git add Assets/sou/Materials/0000_Cube.mat

# 0000_Cube 프리팹 추가
git add Assets/Scripts/Download/0000_Cube.prefab
```

### 2단계: 상태 확인

```bash
git status
```

**확인사항:**
- "Changes to be committed:" 섹션에 모든 파일이 있는지
- 초록색으로 표시되는지

### 3단계: 커밋

```bash
git commit -m "Add T5 edge line glow effect for AR cube

- Add T5EdgeLine shader for 12-edge detection
- Add T5EdgeGlow_URP shader (URP compatible)
- Update 0000_Cube material with T5 glow
- Fix shadow caster compilation errors
- Update cube prefab material reference"
```

### 4단계: 확인

```bash
# 커밋 확인
git log --oneline -1

# 현재 상태 (clean이어야 함)
git status
```

---

## 🔄 Unity에서 변경한 내용은?

### Unity에서 수정 시 자동으로 반영되는 것:
✅ **Material 파라미터 변경**
- Unity Inspector에서 값 조절
- `.mat` 파일이 자동으로 업데이트됨
- Git이 변경사항 감지

### 예시 (당신의 경우):
```yaml
# 0000_Cube.mat 파일 내용 변경됨
_EdgeIntensity: 4.4      # Unity에서 조절한 값
_EdgeSharpness: 4.4      # Unity에서 조절한 값
_EdgeWidth: 0.044        # Unity에서 조절한 값
_BaseColor: {r: 0.867...} # Unity에서 조절한 값
```

### Git 워크플로우:

1. **Unity에서 수정** → `.mat` 파일 자동 변경
2. **Git이 감지** → `git status`로 확인 가능
3. **수동으로 add** → `git add Assets/sou/Materials/0000_Cube.mat`
4. **커밋** → `git commit -m "Adjust T5 glow parameters"`
5. **푸시** → `git push` (Mac과 공유)

### ⚠️ 중요:
**Unity에서 변경한 내용은 자동으로 커밋되지 않습니다!**
- 직접 `git add` 해야 함
- 직접 `git commit` 해야 함

---

## 📦 완전한 커밋 예시

```bash
# === 1. 모든 T5 파일 스테이징 ===
git add Assets/Scripts/Prefab/T5EdgeLine.shader
git add Assets/Scripts/Prefab/T5EdgeLine.shader.meta
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta
git add Assets/sou/Materials/0000_Cube.mat
git add Assets/Scripts/Download/0000_Cube.prefab

# === 2. 상태 확인 ===
git status
# 출력: 6 files to be committed (초록색)

# === 3. 커밋 ===
git commit -m "Add T5 edge line glow effect for AR cube"

# === 4. 확인 ===
git log --oneline -1
# 출력: abc1234 Add T5 edge line glow effect for AR cube
```

---

## 🚀 Mac으로 공유하기

### GitHub 사용 시:

```bash
# 1. GitHub에 저장소 생성
# (웹에서 github.com → New Repository)

# 2. Remote 추가
git remote add origin https://github.com/pdnom/woopang.git

# 3. 푸시
git push -u origin main

# 4. Mac에서 받기
# Mac Terminal에서:
git clone https://github.com/pdnom/woopang.git
# 또는
cd ~/woopang
git pull origin main
```

### GitHub 없이 공유 (USB/클라우드):

```bash
# Windows에서 압축
# T5 관련 파일들만:
Assets/Scripts/Prefab/T5EdgeLine.shader*
Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*
Assets/sou/Materials/0000_Cube.mat
Assets/Scripts/Download/0000_Cube.prefab

# Mac에서 압축 해제 후 같은 경로에 붙여넣기
```

---

## 💡 유용한 Git 팁

### 1. 모든 변경사항 한번에 커밋
```bash
# 주의: 신중하게 사용!
git add .
git commit -m "Update all files"
```

### 2. 커밋 메시지 수정 (방금 한 커밋)
```bash
git commit --amend -m "새로운 메시지"
```

### 3. 파일 unstage (add 취소)
```bash
git restore --staged filename.txt
```

### 4. 변경사항 되돌리기 (주의!)
```bash
# 작업 디렉토리 변경사항 버리기
git restore filename.txt
```

### 5. .gitignore 활용
```bash
# .gitignore 파일에 추가
Library/
Temp/
*.log
```

---

## 📋 체크리스트

### T5 효과 커밋하기:
- [ ] `git add` 로 모든 T5 파일 스테이징
- [ ] `git status`로 확인 (초록색)
- [ ] `git commit -m "메시지"`로 커밋
- [ ] `git log`로 커밋 확인
- [ ] (선택) `git push`로 GitHub에 업로드

### Unity 변경사항 추적:
- [ ] Unity에서 파라미터 조절
- [ ] `git status`로 변경 확인
- [ ] 변경된 `.mat` 파일 `git add`
- [ ] 커밋 및 푸시

---

## 🎓 요약

| 명령어 | 역할 | 예시 |
|--------|------|------|
| `git add` | Staging Area에 추가 | `git add file.txt` |
| `git commit` | 로컬 저장소에 저장 | `git commit -m "메시지"` |
| `git push` | 원격 저장소에 업로드 | `git push origin main` |
| `git pull` | 원격에서 다운로드 | `git pull origin main` |
| `git status` | 현재 상태 확인 | `git status` |
| `git log` | 커밋 히스토리 | `git log --oneline` |

---

**이제 Git의 기본을 이해하셨나요?** 🎉

**다음 단계**: T5 관련 파일들을 모두 add → commit → (선택) push!

---

**작성일**: 2025-11-18
**버전**: 1.0
