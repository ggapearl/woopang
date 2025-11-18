# Windows ↔ Mac 동기화 명령어

## 현재 상황
- Mac: PINk 파일 삭제 및 커밋 완료
- Windows: 아직 Mac의 변경사항을 받지 않음

---

## Mac에서 실행 (먼저)

```bash
# 1. Push (Mac → GitHub)
git push origin main

# 2. 확인
git log --oneline -3
```

---

## Windows에서 실행 (그 다음)

```bash
# 1. Mac의 변경사항 가져오기
git pull origin master

# 2. 새로 만든 가이드 파일들 추가
git add MAC_PINK_CLEANUP_CHECK.md
git add MAC_UNITY_REFRESH_GUIDE.md

# 3. 커밋
git commit -m "Add Mac synchronization guides"

# 4. Push
git push origin master

# 5. 확인
git log --oneline -5
```

---

## 최종 확인

### Windows에서:
```bash
# PINk 파일 없는지 확인
find Assets -name "*PINk*" 2>/dev/null | wc -l
# 출력: 0
```

### Mac에서:
```bash
# 최신 상태로 Pull
git pull origin main

# PINk 파일 없는지 확인
find Assets -name "*PINk*"
# 출력: (없음)

# 가이드 파일 있는지 확인
ls -la MAC_*.md
```

---

## 브랜치 이름 통일

현재:
- Windows: `master` 브랜치
- Mac: `main` 브랜치

### 통일 방법 (Windows에서):

```bash
# master → main으로 변경
git branch -m master main

# Remote 브랜치도 변경
git push -u origin main

# 기존 master 브랜치 삭제 (GitHub에서)
git push origin --delete master
```

그러면 앞으로:
- Windows: `git push origin main`
- Mac: `git push origin main`

---

**작성일**: 2025-11-18
