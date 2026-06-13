# MCP Unity 연결 (Claude ↔ Unity Editor)

> Unity Editor와 Claude를 연결해 씬/프리팹/컴포넌트를 직접 조작할 때 참조

---

## 설치 구성
- **패키지**: CoderGamester/mcp-unity (`com.gamelovers.mcp-unity`)
- **설치 경로**: `Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js`
- **Unity 버전**: 프로젝트 현재 버전 따름 (CLAUDE.md 1번 섹션 / `ProjectSettings/ProjectVersion.txt` 참조)
- **런타임**: Node.js (v22.20+), WebSocket 기반

---

## 포트 설정
- **사용 포트**: **8090** (Unity가 `[::1]:8090` IPv6 로컬에 바인딩)
- **충돌 체크**: `netstat -ano | grep ":8090"`
- WsToastNotification(Wondershare)이 `0.0.0.0:8090`에 리슨 중이지만 Unity는 **IPv6 localhost**라 분리됨 → 충돌 없음

---

## Claude Code MCP 등록 (한 번만, user scope 영구)
```bash
claude mcp add-json --scope user "mcp-unity" \
  '{"command":"node","args":["C:/woopang/Library/PackageCache/com.gamelovers.mcp-unity@<hash>/Server~/build/index.js"]}'
```
⚠️ 패키지 업데이트 시 `@<hash>` 경로가 바뀌면 재등록 필요.

---

## 사용 전 체크 (매번 Unity 열 때)
- [ ] Tools → MCP Unity → Server Window → "Start Server" Running
- [ ] `claude mcp list` → "mcp-unity: ✓ Connected"

---

## 활용 가능한 기능 (30+ 툴)
- 씬/GameObject 조회·수정 (`get_scene_info`, `get_gameobject`, `update_gameobject`)
- Transform 조작 (`set_transform`, `move/rotate/scale_gameobject`)
- 프리팹·머티리얼 관리 (`create_prefab`, `create_material`)
- 컴포넌트 속성 수정 (`update_component`)
- Unity 콘솔 조회 (`get_console_logs`)
- 스크립트 재컴파일 (`recompile_scripts`)
- 씬 저장/로드 (`save_scene`, `load_scene`)
- Editor 메뉴 실행 (`execute_menu_item`)

---

## 주의사항
- Unity Editor가 **실행 중**이어야 MCP 연결 성립
- Claude가 씬 수정해도 **Ctrl+S 수동 저장 필수** (디스크 미반영 시 git diff 미포착)
- **Play Mode 중 스크립트 편집 불가** (Unity 제약)
- 대규모 씬 전체 조회는 **토큰 소비 큼** → 특정 GameObject 경로 지정 권장
- 씬 대량 수정 전 **git commit 필수** (롤백 대비)
