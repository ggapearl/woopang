# 우팡 이모지 (N-U · 2026-11-02)

- 그림: **Microsoft Fluent Emoji 3D** — MIT (`LICENSE-FluentEmoji.txt`). 토스페이스는 라이선스가 변형·재배포를 막아 쓰지 않았다.
- 1,595개. 순서는 Unicode `emoji-test.txt`(v18.0) 의 fully-qualified 순서를 iOS 키보드 분류로 묶었다 —
  스마일리 및 사람 · 동물 및 자연 · 음식 및 음료 · 활동 · 여행 및 장소 · 사물 · 기호(+깃발 8). 피부색은 기본 하나.
- 런타임 자료 `Resources/Emoji/`
  - `emoji_p0~6.png` — 키패드용 2048² (16×16칸 · 128px · 그림 124px) · 밉맵 없음
  - `emoji_s.png` — 글 속용 4096×2048 (64×32칸 · 64px · 그림 60px) · 밉맵
  - `emoji_index.txt` — 줄 i = 글자 U+E000+i 의 진짜 코드포인트 ; 분류(1~7)
  - 가져오기 설정은 `Editor/R0926EmojiImport.cs` (안드로이드·iOS ASTC 6x6)
- 폭 글꼴 `Assets/Resources/Fonts/WoopangEmoji.ttf` — U+E000~E7FF 를 빈 글리프 1.18em 으로. 본문 글꼴 두 벌(AppleSDGothicNeoM)의
  대체 글꼴(fontNames 두 번째 · fontReferences)이라 예전 방식 Text 가 그 글자의 자리를 잡는다 → `R0926EmojiText` 가 그 자리에 그림을 얹는다.

## 다시 만들기 (`build~/` — 유니티는 `~` 로 끝나는 폴더를 가져오지 않는다)
내려받은 파일은 각자 빈 폴더에, 스크립트는 다른 폴더에서 `python -E -P` 로.
1. `https://api.github.com/repos/microsoft/fluentui-emoji/git/trees/main?recursive=1` → `tree.json`
2. `fetch_meta.py tree.json meta/` — 폴더마다 metadata.json
3. `https://unicode.org/Public/emoji/latest/emoji-test.txt`
4. `plan_set.py meta/ emoji-test.txt tree.json set_rows.json` — 3D PNG 경로 + 순서
5. `fetch_png.py set_rows.json png/`
6. `build_emoji.py set_rows.json png/ Assets/Resources/Fonts/AppleSDGothicNeoM.ttf out/` → 위 파일들
