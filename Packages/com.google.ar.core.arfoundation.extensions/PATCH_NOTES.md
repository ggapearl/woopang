# ARCore Extensions — Embedded with Patches

이 패키지는 원래 git URL로 잡혀있었으나, Unity 6 호환성 이슈로 `Packages/` 안에 embed해 직접 패치 중.

원본: https://github.com/google-ar/arcore-unity-extensions.git#arf5
해시: ad14e3f6490a1cce882e2b5e2471c9cff675c89b

## 적용된 패치

### 1. `Runtime/GeospatialCreatorRuntime/Scripts/ARGeospatialCreatorOrigin.cs:87`
- 원본: `[SerializeField]` (auto-property에 직접)
- 패치: `[field: SerializeField]` (backing field에 attribute 적용)
- 사유: Unity 6 (.NET 신컴파일러)에서 `error CS0592: Attribute 'SerializeField' is not valid on this declaration type` 발생.

### 2. `Editor/ExternalDependencyManager/Editor/Google.IOSResolver.dll.meta`
- 원본: `validateReferences: 1`
- 패치: `validateReferences: 0`
- 사유: 안드로이드 빌드 타겟 스위치 시 `UnityEditor.iOS.Extensions.Xcode` 참조 못 찾는 경고 발생. iOS 전용 도구라 안드로이드에선 사용 안 됨.

## 향후 처리

- 구글에서 Unity 6 호환 공식 fix(arf6 브랜치 등)가 나오면:
  1. `Packages/manifest.json` 다시 git URL로 복원
  2. 이 폴더 통째로 삭제
  3. `Packages/packages-lock.json` `source: "git"` 으로 복원
- ARCore Extensions 버전 올릴 일 있으면 위 두 패치를 새 버전에서도 동일하게 재적용해야 함.
