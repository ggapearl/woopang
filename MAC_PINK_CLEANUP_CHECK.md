# Mac에서 PINk 파일 삭제 확인 및 정리 가이드

## 🎯 목적
Windows에서 삭제한 PINk 파일들이 Mac에서도 제대로 제거되었는지 확인하고, 필요시 수동 정리

---

## 📋 확인 순서

### 1단계: Mac에서 현재 Git 상태 확인

```bash
# Mac Terminal에서
cd ~/woopang  # 또는 프로젝트 경로

# 현재 브랜치 및 커밋 확인
git status
git log --oneline -5

# Windows와 Mac의 커밋 비교
git log origin/master --oneline -5
```

**확인사항:**
- Mac의 현재 커밋이 Windows보다 오래된 것인지
- Uncommitted changes가 있는지

---

### 2단계: Pull 전에 Mac에 PINk 파일 존재 여부 확인

```bash
# PINk 관련 파일 검색
find ~/woopang/Assets -name "*PINk*" -o -name "*pink*" 2>/dev/null

# 특정 파일들 확인
ls -la ~/woopang/Assets/Scripts/Download/PINkDataManager.cs* 2>/dev/null
ls -la ~/woopang/Assets/Scripts/Prefab/PINkTap.cs* 2>/dev/null
ls -la ~/woopang/Assets/Scripts/upload/pinkUploadManager.cs* 2>/dev/null
ls -la ~/woopang/Assets/Scripts/Prefab/CustomGlowPulse_PINk.mat* 2>/dev/null
ls -la ~/woopang/Assets/Fbx/PINkMaterial.mat* 2>/dev/null
```

**만약 파일들이 존재한다면:**
- Windows와 동일한 파일들인지 확인
- Git이 추적하고 있는지 확인

---

### 3단계: Git Pull 실행

```bash
# 백업 (선택사항)
cp -r ~/woopang ~/woopang_backup_$(date +%Y%m%d)

# Pull 실행
cd ~/woopang
git pull origin master

# 또는 (충돌 방지)
git fetch origin
git merge origin/master
```

**예상되는 시나리오:**

#### 시나리오 A: 정상적으로 Pull 성공
```
Updating abc1234..def5678
Fast-forward
 Assets/Scripts/Download/PINkDataManager.cs      | 100 --------
 Assets/Scripts/Download/PINkDataManager.cs.meta |  11 -
 Assets/Scripts/Prefab/PINkTap.cs                |  50 ----
 ...
 5 files changed, 0 insertions(+), 200 deletions(-)
 delete mode 100644 Assets/Scripts/Download/PINkDataManager.cs
```
→ **이 경우 파일들이 자동으로 삭제됨**

#### 시나리오 B: Untracked files 충돌
```
error: The following untracked working tree files would be overwritten by merge:
    Assets/Scripts/Download/PINkDataManager.cs
Please move or remove them before you merge.
```
→ **Mac에 있는 파일이 Git에 추적되지 않은 파일**
→ 수동 삭제 필요

#### 시나리오 C: Modified files 충돌
```
error: Your local changes to the following files would be overwritten by merge:
    Assets/Scripts/Download/PINkDataManager.cs
Please commit your changes or stash them before you merge.
```
→ **Mac에서 파일을 수정했음**
→ Stash 또는 커밋 필요

---

### 4단계: Pull 후 PINk 파일 삭제 확인

```bash
# Assets 폴더에서 PINk 검색
find ~/woopang/Assets -name "*PINk*" 2>/dev/null
find ~/woopang/Assets -name "*pink*" 2>/dev/null

# 코드 내 PINk 참조 검색
grep -r "PINkDataManager\|PINkTap\|pinkUploadManager" ~/woopang/Assets --include="*.cs"

# 서버 코드 확인
grep -n "pink" ~/woopang/server/app_improved.py
```

**기대 결과:**
- Assets 폴더: PINk 관련 파일 0개
- 코드 내 참조: 검색 결과 없음
- 서버: "DEPRECATED" 주석 처리된 코드만 있음

---

### 5단계: 파일이 남아있는 경우 수동 삭제

#### Windows와 Mac의 파일 경로 차이 확인:

```bash
# Mac에서 전체 구조 확인
find ~/woopang/Assets/Scripts -type d

# Windows와 비교
# Windows: Assets\Scripts\Download\
# Mac:     Assets/Scripts/Download/
```

#### 수동 삭제 명령어:

```bash
# PINk 관련 파일 및 .meta 파일 삭제
rm -f ~/woopang/Assets/Scripts/Download/PINkDataManager.cs
rm -f ~/woopang/Assets/Scripts/Download/PINkDataManager.cs.meta
rm -f ~/woopang/Assets/Scripts/Prefab/PINkTap.cs
rm -f ~/woopang/Assets/Scripts/Prefab/PINkTap.cs.meta
rm -f ~/woopang/Assets/Scripts/Prefab/CustomGlowPulse_PINk.mat
rm -f ~/woopang/Assets/Scripts/Prefab/CustomGlowPulse_PINk.mat.meta
rm -f ~/woopang/Assets/Scripts/upload/pinkUploadManager.cs
rm -f ~/woopang/Assets/Scripts/upload/pinkUploadManager.cs.meta
rm -f ~/woopang/Assets/Fbx/PINkMaterial.mat
rm -f ~/woopang/Assets/Fbx/PINkMaterial.mat.meta

# 삭제 확인
find ~/woopang/Assets -name "*PINk*"
```

---

## 🔍 Windows vs Mac 파일 경로 차이

### 경로 표기법:
- **Windows**: `C:\woopang\Assets\Scripts\Download\file.cs`
- **Mac**: `/Users/username/woopang/Assets/Scripts/Download/file.cs`

### Git에서의 경로 (동일):
- `Assets/Scripts/Download/file.cs` (슬래시 `/` 사용)

### 대소문자 구분:
- **Windows**: 대소문자 구분 안함 (`pink` = `PINk` = `PINK`)
- **Mac**: 대소문자 구분함 (`pink` ≠ `PINk`)

**주의:** Mac에서 파일명이 조금 다르게 저장되어 있을 수 있음!

---

## 🚨 충돌 해결 방법

### Case 1: Untracked files 충돌 시

```bash
# 1. 충돌 파일 확인
git status

# 2. 해당 파일들 삭제
rm Assets/Scripts/Download/PINkDataManager.cs
rm Assets/Scripts/Download/PINkDataManager.cs.meta

# 3. 다시 Pull
git pull origin master
```

### Case 2: Modified files 충돌 시

```bash
# 방법 A: 변경사항 버리기 (주의!)
git checkout -- Assets/Scripts/Download/PINkDataManager.cs
git pull origin master

# 방법 B: 변경사항 임시 저장
git stash
git pull origin master
git stash drop  # 변경사항 영구 삭제
```

### Case 3: .meta 파일 GUID 충돌

```bash
# Mac에서 생성된 .meta 파일 삭제
find ~/woopang/Assets -name "*.meta" -newer ~/woopang/.git/FETCH_HEAD -delete

# Windows에서 온 .meta 파일로 덮어쓰기
git checkout origin/master -- Assets/sou/Materials/0000_Cube.mat.meta

# Unity 재시작 후 Library 재생성
rm -rf ~/woopang/Library
open -a Unity  # 프로젝트 열기
```

---

## 📊 체크리스트

### Pull 전:
- [ ] Mac에서 Git 상태 확인 (`git status`)
- [ ] 현재 커밋 확인 (`git log --oneline -5`)
- [ ] PINk 파일 존재 여부 확인
- [ ] Uncommitted changes 확인

### Pull 실행:
- [ ] `git pull origin master` 실행
- [ ] 충돌 없이 성공했는지 확인
- [ ] 충돌 발생 시 위 가이드 참고

### Pull 후:
- [ ] PINk 파일 삭제 확인 (`find ~/woopang/Assets -name "*PINk*"`)
- [ ] 코드 참조 확인 (`grep -r "PINk" ~/woopang/Assets`)
- [ ] 서버 코드 확인 (`grep "pink" ~/woopang/server/app_improved.py`)
- [ ] Unity Console 오류 확인
- [ ] T5 셰이더 적용 확인

### Unity 확인:
- [ ] Unity 프로젝트 열기
- [ ] Console에서 Missing Script 경고 확인
- [ ] 0000_Cube.mat 셰이더 확인
- [ ] Library 재생성 (필요시)

---

## 💡 권장 워크플로우

### 앞으로 Windows ↔ Mac 동기화 시:

1. **작업 전 동기화:**
```bash
# 작업 시작 전 항상 Pull
git pull origin master
```

2. **작업 중 커밋:**
```bash
# 의미있는 단위로 커밋
git add -A
git commit -m "작업 내용"
```

3. **작업 후 푸시:**
```bash
# 하루 작업 끝날 때 Push
git push origin master
```

4. **다른 기기에서 작업 시작:**
```bash
# 다시 Pull
git pull origin master
```

### .gitignore 설정 (권장):

```bash
# Mac에서 생성
cat >> ~/woopang/.gitignore << 'EOF'
# Unity generated
Library/
Temp/
Logs/
*.log

# OS generated
.DS_Store
Thumbs.db
EOF

git add .gitignore
git commit -m "Add .gitignore for Unity and OS files"
git push origin master
```

---

## 🎯 최종 확인 명령어

Mac Terminal에서 실행:

```bash
cd ~/woopang

# === 1. Git 상태 ===
echo "=== Git Status ==="
git status

# === 2. PINk 파일 검색 ===
echo -e "\n=== PINk Files ==="
find Assets -name "*PINk*" 2>/dev/null | wc -l
# 출력: 0 (파일 없음)

# === 3. 코드 참조 검색 ===
echo -e "\n=== Code References ==="
grep -r "PINkDataManager\|PINkTap\|pinkUploadManager" Assets --include="*.cs" | wc -l
# 출력: 0 (참조 없음)

# === 4. 서버 코드 ===
echo -e "\n=== Server Code ==="
grep -n "pink" server/app_improved.py
# 출력: DEPRECATED 주석만

# === 5. T5 셰이더 ===
echo -e "\n=== T5 Shader ==="
ls -la Assets/Scripts/Prefab/T5EdgeLine.shader*
# 출력: 파일 존재 확인
```

---

**작성일**: 2025-11-18
**목적**: Mac에서 PINk 제거 확인 및 Windows-Mac 동기화 가이드
