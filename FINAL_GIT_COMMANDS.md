# 🚀 최종 Git 커밋 명령어 모음

## ✅ 지금 실행할 명령어

아래 명령어를 **순서대로** 복사해서 실행하세요!

---

## 📦 단계별 가이드

### 1️⃣ T5 효과 + PINk 제거 모두 커밋

```bash
# === Git 사용자 설정 확인 (이미 완료) ===
git config --global user.name
git config --global user.email

# === 모든 변경사항 스테이징 ===
# T5 셰이더 추가
git add Assets/Scripts/Prefab/T5EdgeLine.shader
git add Assets/Scripts/Prefab/T5EdgeLine.shader.meta
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader.meta

# 머티리얼 업데이트
git add Assets/sou/Materials/0000_Cube.mat

# 프리팹 업데이트
git add Assets/Scripts/Download/0000_Cube.prefab

# PINk 파일 삭제 추가
git add -A

# 서버 수정사항
git add server/app_improved.py

# 문서 추가
git add GIT_BASICS_GUIDE.md
git add GIT_SETUP_GUIDE.md
git add MAC_SYNC_GUIDE.md
git add T5_EDGE_LINE_GUIDE.md
git add SHADER_FIX_SUMMARY.md
git add CLEANUP_SUMMARY.md
git add FINAL_GIT_COMMANDS.md
```

### 2️⃣ 상태 확인

```bash
git status
```

**확인사항:**
- "Changes to be committed:" 섹션에 파일들이 초록색으로 표시
- T5 셰이더, 머티리얼, 프리팹 포함
- PINk 파일들 deleted로 표시
- server/app_improved.py modified로 표시

### 3️⃣ 커밋 생성

```bash
git commit -m "Add T5 edge line glow effect and remove PINk feature

Major Changes:
- Add T5EdgeLine shader for 12-edge detection
- Add T5EdgeGlow_URP shader (URP 14.0.12 compatible)
- Update 0000_Cube material with T5 glow parameters
- Fix shadow caster compilation errors

PINk Feature Removal:
- Delete PINkDataManager.cs
- Delete PINkTap.cs
- Delete pinkUploadManager.cs
- Delete PINk materials
- Deprecate /pinks API endpoint in server

Documentation:
- Add Git basics guide
- Add Mac sync guide
- Add T5 edge line guide
- Add shader fix summary
- Add cleanup summary"
```

### 4️⃣ 커밋 확인

```bash
# 최근 커밋 보기
git log --oneline -1

# 커밋 상세 정보
git show HEAD --stat

# 현재 상태 (clean이어야 함)
git status
```

---

## 🎯 간단 버전 (한번에 실행)

```bash
# 모든 파일 추가
git add Assets/Scripts/Prefab/T5EdgeLine.shader*
git add Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*
git add Assets/sou/Materials/0000_Cube.mat
git add Assets/Scripts/Download/0000_Cube.prefab
git add server/app_improved.py
git add GIT_*.md MAC_SYNC_GUIDE.md T5_*.md SHADER_FIX_SUMMARY.md CLEANUP_SUMMARY.md FINAL_GIT_COMMANDS.md
git add -A

# 커밋
git commit -m "Add T5 edge line glow effect and remove PINk feature"

# 확인
git log --oneline -1
git status
```

---

## 📤 GitHub에 푸시 (선택사항)

### GitHub 저장소가 있는 경우:

```bash
# Remote 추가 (처음 한번만)
git remote add origin https://github.com/pdnom/woopang.git

# 푸시
git push -u origin main

# 또는 (이미 upstream 설정된 경우)
git push
```

### GitHub 저장소 만들기:
1. GitHub.com 접속
2. 로그인
3. New Repository 클릭
4. Repository name: `woopang`
5. Private 선택 (권장)
6. Create repository
7. 위의 명령어로 푸시

---

## 💻 Mac에서 받기

### 방법 1: Git Clone (처음)
```bash
# Mac Terminal에서
cd ~/Documents
git clone https://github.com/pdnom/woopang.git
cd woopang

# Unity 실행
open -a Unity
```

### 방법 2: Git Pull (이미 있는 경우)
```bash
# Mac Terminal에서
cd ~/woopang
git pull origin main

# Unity 프로젝트 다시 열기
```

### 방법 3: 파일 복사 (Git 없이)
```bash
# Windows에서 USB로 복사
복사할 파일:
- Assets/Scripts/Prefab/T5EdgeLine.shader*
- Assets/Scripts/Prefab/T5EdgeGlow_URP.shader*
- Assets/sou/Materials/0000_Cube.mat
- Assets/Scripts/Download/0000_Cube.prefab
- server/app_improved.py

# Mac에서 동일한 경로에 붙여넣기
```

---

## ❓ 문제 해결

### "nothing to commit" 오류
```bash
# 변경사항 확인
git status

# Untracked files가 있다면
git add .
git commit -m "Add all files"
```

### "failed to push" 오류
```bash
# 먼저 pull
git pull origin main --allow-unrelated-histories

# 충돌 해결 후 다시 push
git push origin main
```

### .gitignore 설정
```bash
# .gitignore 파일에 추가 (Unity 임시 파일 제외)
echo "Library/" >> .gitignore
echo "Temp/" >> .gitignore
echo "Logs/" >> .gitignore
echo "*.log" >> .gitignore

git add .gitignore
git commit -m "Add .gitignore for Unity"
```

---

## 📋 체크리스트

### 커밋 전:
- [ ] `git status`로 변경사항 확인
- [ ] `git add`로 필요한 파일 스테이징
- [ ] `git status`로 스테이징 확인 (초록색)

### 커밋 후:
- [ ] `git log --oneline -1`로 커밋 확인
- [ ] `git status`로 clean 확인
- [ ] (선택) `git push`로 원격 저장소에 업로드

### Mac 동기화:
- [ ] Git pull 또는 파일 복사
- [ ] Unity 프로젝트 열기
- [ ] Console에서 오류 확인
- [ ] T5 효과 작동 확인
- [ ] PINk 관련 파일 삭제 확인

---

## 🎓 Git 명령어 요약

| 명령어 | 설명 | 사용 시점 |
|--------|------|-----------|
| `git add <file>` | 파일 스테이징 | 변경사항을 커밋에 포함시키고 싶을 때 |
| `git add -A` | 모든 변경사항 스테이징 | 삭제된 파일 포함 모든 변경사항 |
| `git commit -m "msg"` | 커밋 생성 | 변경사항을 영구 저장 |
| `git push` | 원격에 업로드 | Mac과 공유하고 싶을 때 |
| `git pull` | 원격에서 다운로드 | Mac에서 Windows 작업 받을 때 |
| `git status` | 상태 확인 | 현재 변경사항 확인 |
| `git log` | 히스토리 확인 | 커밋 내역 보기 |

---

## 🚀 지금 바로 실행!

```bash
# === 복사해서 실행하세요! ===

# 모든 파일 추가
git add -A

# 커밋
git commit -m "Add T5 edge line glow effect and remove PINk feature

- Add T5EdgeLine shader for 12-edge detection
- Update cube material with T5 parameters
- Remove PINk related files
- Update server API"

# 확인
git log --oneline -1
git status

# 성공 메시지 확인!
```

---

**작성일**: 2025-11-18
**버전**: Final
