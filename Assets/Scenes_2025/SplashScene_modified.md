씬 수정 내용:

1. SplashVideoRawImage -> LogoImage로 이름 변경
2. RawImage 컴포넌트 -> Image 컴포넌트로 변경 (GUID: fe87c0e1cc204ed48ad3b37840f39efc)
3. SplashVideoPlayer -> SplashController로 이름 변경
4. VideoPlayer 컴포넌트 제거
5. SplashScreen 스크립트 추가 (GUID: ef74af8e343d91c4ba12e312ca776a32)
   - splashImage: LogoImage 참조
   - waitTime: 5초
   - fadeDuration: 1초
   - nextSceneName: MainScene

Unity에서 직접 수정하는 것이 더 안전합니다.
