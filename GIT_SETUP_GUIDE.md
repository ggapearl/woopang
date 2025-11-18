# 🔧 Git 설정 및 커밋 가이드

## ❌ 발생한 오류

```
Author identity unknown

*** Please tell me who you are.

fatal: unable to auto-detect email address
```

---

## ✅ Git 사용자 설정 (필수)

### 1단계: Git 사용자 정보 설정

```bash
# 이메일 설정 (본인 이메일로 변경)
git config --global user.email "your-email@example.com"

# 이름 설정 (본인 이름으로 변경)
git config --global user.name "Your Name"
```

**예시:**
```bash
git config --global user.email "developer@woopang.com"
git config --global user.name "Woopang Developer"
```

### 2단계: 설정 확인

```bash
# 설정된 정보 확인
git config --global user.email
git config --global user.name
```

---

## 📦 T5 Edge Line 효과 커밋하기

### 파일 목록

**새로 생성된 파일:**
```
Assets/Scripts/Prefab/T5EdgeLine.shader
Assets/Scripts/Prefab/T5EdgeLine.shader.meta
Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta
```

**수정된 파일:**
```
Assets/sou/Materials/0000_Cube.mat
Assets/Scripts/Download/0000_Cube.prefab
```

### Git 커밋 명령어

```bash
# 1. Git 사용자 설정 (처음 1회만)
git config --global user.email "your-email@example.com"
git config --global user.name "Your Name"

# 2. T5 Edge Line 셰이더 추가
git add Assets/Scripts/Prefab/T5EdgeLine.shader*

# 3. T5 Glow URP 셰이더 추가
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*

# 4. 머티리얼 수정사항 추가
git add Assets/sou/Materials/0000_Cube.mat

# 5. 프리팹 수정사항 추가
git add Assets/Scripts/Download/0000_Cube.prefab

# 6. 커밋 생성
git commit -m "Add T5 edge line glow effect for AR cube

- Implement 12-edge line detection shader
- Add T5 tube lighting effect
- Add pulse animation
- Fix URP 14.0.12 compatibility
- Update cube material with edge glow"

# 7. 상태 확인
git status
```

---

## 🔄 Mac으로 동기화

### Git Remote 설정 (GitHub/GitLab 사용 시)

```bash
# GitHub 예시
git remote add origin https://github.com/your-username/woopang.git
git branch -M main
git push -u origin main
```

### Mac에서 받기

```bash
# Mac Terminal에서
cd ~/woopang
git clone https://github.com/your-username/woopang.git
# 또는 이미 클론했다면
git pull origin main
```

---

## 📝 CRLF 경고 해결

```
warning: LF will be replaced by CRLF
```

이 경고는 Windows와 Mac/Linux 간 줄바꿈 문자 차이 때문입니다.

### 해결방법 (선택사항)

```bash
# Windows에서 자동 변환 설정
git config --global core.autocrlf true

# 경고 무시 (문제없음)
# Unity 파일은 자동으로 처리됨
```

---

## 🚀 빠른 커밋 스크립트

아래 내용을 복사해서 실행하세요:

```bash
# Git 설정 (본인 정보로 수정!)
git config --global user.email "developer@woopang.com"
git config --global user.name "Woopang Dev"

# 모든 T5 관련 파일 추가
git add Assets/Scripts/Prefab/T5EdgeLine.shader*
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*
git add Assets/sou/Materials/0000_Cube.mat
git add Assets/Scripts/Download/0000_Cube.prefab

# 커밋
git commit -m "Add T5 edge line glow effect for AR cube"

# 상태 확인
git status
git log --oneline -5
```

---

## 📊 Git 상태 확인

### 커밋 확인
```bash
# 최근 커밋 보기
git log --oneline -5

# 커밋 상세 정보
git show HEAD
```

### 변경사항 확인
```bash
# Staged 파일 확인
git diff --cached

# 모든 변경사항
git status
```

---

## 💡 Git 없이 Mac 동기화

Git을 사용하지 않는 경우:

### 방법 1: USB 드라이브
```bash
# Windows에서
# 아래 폴더를 USB에 복사
Assets/Scripts/Prefab/
Assets/sou/Materials/
Assets/Scripts/Download/

# Mac에서
# USB의 파일들을 동일한 경로에 붙여넣기
```

### 방법 2: 클라우드 (Google Drive, Dropbox)
```bash
# Windows에서
# 프로젝트 폴더를 클라우드 동기화 폴더로 이동

# Mac에서
# 클라우드 앱 설치 후 자동 동기화
```

### 방법 3: 네트워크 공유
```bash
# Windows에서 폴더 공유 설정
# Mac에서 네트워크 드라이브로 접근
```

---

## ✅ 체크리스트

### Git 초기 설정
- [ ] `git config --global user.email` 설정
- [ ] `git config --global user.name` 설정
- [ ] 설정 확인 완료

### 파일 커밋
- [ ] T5EdgeLine.shader 추가
- [ ] T5EdgeGlow_URP.shader 추가
- [ ] 0000_Cube.mat 수정
- [ ] 0000_Cube.prefab 수정
- [ ] 커밋 메시지 작성
- [ ] `git status`로 확인

### Mac 동기화
- [ ] Git remote 설정 (선택)
- [ ] Git push 또는 파일 복사
- [ ] Mac에서 Unity 프로젝트 열기
- [ ] 셰이더 컴파일 확인
- [ ] T5 효과 작동 확인

---

## 🎯 최종 명령어 (복사해서 사용)

```bash
# === 1. Git 사용자 설정 (본인 정보로 수정!) ===
git config --global user.email "your@email.com"
git config --global user.name "Your Name"

# === 2. 파일 스테이징 ===
git add Assets/Scripts/Prefab/T5EdgeLine.shader
git add Assets/Scripts/Prefab/T5EdgeLine.shader.meta
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta
git add Assets/sou/Materials/0000_Cube.mat
git add Assets/Scripts/Download/0000_Cube.prefab

# === 3. 커밋 ===
git commit -m "Add T5 edge line glow effect for AR cube"

# === 4. 확인 ===
git log --oneline -1
git status
```

---

**이제 셰이더 오류도 해결되었고, Git 설정만 하면 커밋할 수 있습니다!** 🎉

---

**작성일**: 2025-11-18
**버전**: 1.0
