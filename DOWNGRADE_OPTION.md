# ARFoundation 다운그레이드 옵션 (비권장)

## ⚠️ 경고
이 방법은 Unity 6와 호환성 문제를 일으킬 수 있습니다.
**ARCore Extensions 업그레이드를 먼저 시도하는 것을 강력히 권장합니다.**

---

## 다운그레이드 단계

### 1. Packages/manifest.json 수정

현재:
```json
{
  "dependencies": {
    "com.unity.xr.arcore": "5.1.6",
    "com.unity.xr.arkit": "5.1.6",
    // ARFoundation은 명시적으로 없음 (자동 설치됨)
  }
}
```

다음으로 변경:
```json
{
  "dependencies": {
    "com.unity.xr.arfoundation": "4.1.5",
    "com.unity.xr.arcore": "4.1.5",
    "com.unity.xr.arkit": "4.1.5",
    // ... 나머지 동일
  }
}
```

### 2. 패키지 캐시 삭제

```bash
cd /Users/pdnom/Desktop/woopang
rm -rf Library/PackageCache
```

### 3. Unity 재시작

Unity를 완전히 종료했다가 다시 실행

### 4. 문제 발생 가능성

다음 에러가 발생할 수 있습니다:

```
Error: Package 'com.unity.render-pipelines.universal' has dependency on
'com.unity.xr.arfoundation@6.0.0' but version '4.1.5' was found
```

**이 경우:**
- Unity 6는 ARFoundation 6.x를 권장
- ARFoundation 4.x는 Unity 2020 LTS용
- 강제 다운그레이드 시 URP, Input System 등 충돌 가능

---

## 🎯 결론

**ARCore Extensions arf6 업그레이드가 유일한 안전한 해결책입니다.**

Unity 6 + ARFoundation 6.3.1 환경에서는:
- ARCore Extensions도 6.x 호환 버전 필요
- 다운그레이드는 더 많은 문제를 일으킴
