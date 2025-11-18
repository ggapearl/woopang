# Mac Unity 셰이더 인식 문제 해결

## 문제 상황
- GitHub에서 pull 성공
- 파일 내용 확인 완료 (GUID 모두 일치)
- Unity Inspector에서 여전히 "Unlit/Texture" 표시

## 원인
Unity의 Library 캐시가 업데이트되지 않아서 새 셰이더를 인식하지 못함

---

## 해결 방법 (순서대로 시도)

### 방법 1: Unity에서 Asset 강제 재임포트 (가장 간단)

1. Unity 프로젝트 열기
2. 메뉴: **Assets > Reimport All**
3. 재임포트 완료 대기 (수 분 소요 가능)
4. 0000_Cube.mat 파일 다시 확인

### 방법 2: 셰이더 파일 개별 재임포트

1. Unity Project 창에서 찾기:
   - `Assets/Scripts/Prefab/T5EdgeLine.shader`
2. 우클릭 > **Reimport**
3. 0000_Cube.mat 파일 Inspector 확인

### 방법 3: Library 폴더 삭제 (강력한 방법)

```bash
# Mac Terminal에서
cd ~/woopang  # 또는 프로젝트 경로

# Unity 완전히 종료 확인!
# Unity 앱이 실행 중이면 안됨

# Library 폴더 삭제
rm -rf Library/

# Unity 프로젝트 다시 열기
open -a Unity
```

**주의사항:**
- Unity가 **완전히 종료**된 상태에서만 실행
- Library 폴더 삭제 후 Unity가 모든 에셋을 다시 임포트 (시간 소요)
- Temp, Logs 폴더는 삭제하지 않아도 됨

### 방법 4: 프로젝트 재시작

1. Unity 완전히 종료
2. Mac 재부팅 (선택사항)
3. Unity Hub에서 프로젝트 다시 열기

---

## 확인 방법

### Unity Inspector에서 확인:

1. Project 창에서 `0000_Cube.mat` 선택
2. Inspector에서 Shader 이름 확인:
   - **수정 전**: "Unlit/Texture"
   - **수정 후**: "Universal Render Pipeline/T5EdgeLine"

### 셰이더 파라미터 확인:

셰이더가 제대로 인식되면 Inspector에서 다음 파라미터들이 보여야 함:
```
- Base Color
- Edge Color
- Edge Intensity
- Edge Sharpness
- Edge Width
- Tube Glow
- Inner Glow
- Min Glow
- Glow Pulse Speed
- Base Map
```

---

## 추가 문제 해결

### Console 오류 확인:

Unity Console (Cmd+Shift+C)에서 셰이더 관련 오류 확인:
- 빨간색 오류가 있으면 셰이더 컴파일 실패
- 노란색 경고는 무시 가능

### URP 설정 확인:

1. Edit > Project Settings > Graphics
2. Scriptable Render Pipeline Settings에 URP Asset이 설정되어 있는지 확인

### 파일 권한 확인:

```bash
# Mac Terminal에서
cd ~/woopang/Assets/Scripts/Prefab

# 파일 권한 확인
ls -la T5EdgeLine.shader*

# 권한이 이상하면 수정
chmod 644 T5EdgeLine.shader
chmod 644 T5EdgeLine.shader.meta
```

---

## 최종 확인 체크리스트

### GitHub Pull 확인:
- [ ] `git pull origin main` 성공
- [ ] T5EdgeLine.shader 파일 존재
- [ ] T5EdgeLine.shader.meta 파일 존재
- [ ] 0000_Cube.mat 파일 존재
- [ ] 0000_Cube.mat.meta 파일 존재

### 파일 내용 확인 (Terminal):
```bash
# 셰이더 GUID
grep "guid:" Assets/Scripts/Prefab/T5EdgeLine.shader.meta
# 출력: guid: f6789012345678901abcdef06789abcd

# 머티리얼 셰이더 참조
grep "m_Shader:" Assets/sou/Materials/0000_Cube.mat
# 출력: guid: f6789012345678901abcdef06789abcd
```

### Unity 확인:
- [ ] Console에 오류 없음
- [ ] T5EdgeLine.shader가 Project 창에서 보임
- [ ] 0000_Cube.mat의 Shader가 "Universal Render Pipeline/T5EdgeLine"로 표시
- [ ] Inspector에서 T5 파라미터들 보임

---

## 작동 확인

셰이더가 제대로 적용되면:
1. Scene 뷰에서 큐브 모서리에 밝은 선이 보여야 함
2. Edge Intensity를 높이면 빛이 더 밝아짐
3. Edge Width를 조절하면 선 두께 변경
4. 런타임에서 펄스 애니메이션 작동

---

**작성일**: 2025-11-18
**상태**: Unity Library 캐시 문제 해결 가이드
