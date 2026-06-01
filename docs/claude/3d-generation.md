# 3D 콘텐츠(GLB) 생성·자동화 작업 가이드

> AR 댄스 콘텐츠(category="anim") 또는 일반 GLB 자산 만들 때 참조.
> 작성: 2026-06-01 PoC 작업 중 정리.

---

## 0. 큰 그림 — 품질 등급

| Tier | 비주얼 | 도구 | 시간/캐릭 | 비용 |
|------|--------|------|-----------|------|
| 1 | 박스/저폴리 (마인크래프트) | Blender Python(절차적) | 30초 | 0 |
| 2 | 스타일 캐주얼 게임풍 | Mixamo 기성품 + Blender export | 5분 | 0 |
| 3 | 사실적 (그럴듯한) | Hunyuan3D 2.1 로컬 | 1~5분 | 전기료 |
| 4 | 게임 출시급 | 3D 아티스트 외주 | 수일 | 외주비 |
| 5 | 실사 멤버 | MetaHuman·스캔 | 수주 | 고비용 |

→ **현재 사장님 케이스**: Tier 3까지 PoC, Tier 4부터 외주.

---

## 1. 우리 앱 통합 규약 (필수)

GLB가 우리 앱에 동작하려면:

```sql
INSERT INTO locations (
    username, name, latitude, longitude, altitude,
    color, category, status,
    folder, main_photo, sub_photos,
    model_type, model_url, model_scale, created_at
) VALUES (
    '<출처>', '<표시 이름>', <위도>, <경도>, <고도>,
    NULL, 'anim', 'approved',
    'uploads/<폴더명>',
    'uploads/<폴더명>/thumb.jpg',
    '[]',
    'custom', 'https://woopang.com/uploads/<폴더명>/<file>.glb',
    <scale>, CURRENT_TIMESTAMP
);
```

핵심:
- **`category='anim'`** ← FilterManager가 자동 Full 스폰 제외, 인디케이터 → 탭 → 다운로드 → 스폰
- **`model_type='custom'`** ← GLBModelLoader 사용
- **`model_scale`** ← GLB 절대 크기에 맞춰 조정 (보통 0.3~1.0)
- 폴더명 = `YYYYMMDD_HHMMSS_<이름>` 패턴 유지
- 썸네일 `thumb.jpg` 필수 (444×444 권장)

---

## 2. GLB 파일 자체 요구사항

| 항목 | 권장 한도 |
|------|-----------|
| 파일 크기 | 2~3MB (모바일 AR), 최대 10MB (`GLBModelLoader.maxFileSizeBytes`) |
| 폴리곤 | 5,000~15,000 tri |
| 텍스처 | 1024×1024 권장, KTX2 압축 |
| 본 수 | 30~50 권장 (Mixamo 65 표준에서 감산) |
| 애니메이션 | 첫 클립이 자동 루프 재생됨 (`GLBModelLoader.TryPlayAnimation()`) |
| 압축 | Draco 메시 + KTX2 텍스처 필수 (안 하면 8~15MB로 부풀음) |

---

## 3. Tier 1 — Blender Python 절차적 생성 (제가 코드로 직접)

### 사용 케이스
- 빠른 PoC 검증
- 외형은 단순해도 안무·메커니즘 확인용
- 박스/저폴리 캐릭

### 스크립트 위치
- `c:\tmp\make_dance_dog.py` (v1, 16큐브 오브젝트 트랜스폼 안무, 25KB)
- `c:\tmp\make_dance_dog_v2.py` (v2, Subdiv + 본 14개 + 스키닝, 48KB) ⭐

### 실행
```bash
"/c/Program Files/Blender Foundation/Blender 4.3/blender.exe" -b -P c:/tmp/make_dance_dog_v2.py
```

### 핵심 패턴 (스크립트 안)
1. `bpy.ops.mesh.primitive_cube_add` 큐브들 생성
2. `modifiers.new("Subd", type='SUBSURF')` Subdivision Surface로 부드럽게
3. `bpy.ops.object.join` 메시 하나로 통합
4. `bpy.ops.object.armature_add` → `edit_bones.new(...)` 본 추가
5. `bpy.ops.object.parent_set(type='ARMATURE_AUTO')` Auto-weight 스키닝
6. Pose mode에서 `keyframe_insert("location"/"rotation_euler")`로 안무
7. `bpy.ops.export_scene.gltf(export_draco_mesh_compression_enable=True, level=6)` GLB export

### 한계
- 박스 조합 → 아무리 잘해도 "스타일라이즈" 수준
- 실사 캐릭 불가 — Tier 3 필요

---

## 4. Tier 2 — Mixamo (휴머노이드 한정)

### 사용 케이스
- 사람 캐릭터 (앳하트 멤버 PoC)
- 4족 동물 ❌ Mixamo 지원 안 함

### 절차 (캐릭터당 10분)
1. https://mixamo.com Adobe 계정 무료 로그인
2. 캐릭터 선택 (Y Bot, Erika 등) 또는 **Upload Character** (자체 모델)
3. 사람 모델 업로드 시: 턱·손목·무릎·사타구니 5점 클릭 → Auto-Rigger
4. **Find Animations** → "Hip Hop Dance", "K-pop", "Samba" 등 무료 라이브러리
5. Format=**FBX Binary**, Skin=With Skin, FPS=30, Keyframe Reduction=None → Download
6. Blender import → glTF 2.0 export (Draco + animations 체크)

### Tip
- 자체 모델 업로드는 **T-pose 또는 A-pose** 권장
- Hunyuan3D 출력은 보통 호환됨

---

## 5. Tier 3 — Hunyuan3D 2.1 로컬 (사실적 — 우리 본 게임)

### 설치 위치
- 코드: `c:\hunyuan3d\Hunyuan3D-2.1\`
- venv: `c:\hunyuan3d\.venv\` (Python 3.11/3.12 + uv)
- 모델 가중치: 자동 다운로드 → `~/.cache/huggingface/hub/`

### 시스템 요구
- **NVIDIA 드라이버 580+** (CUDA 13 지원) ← 사장님 PC 현재 610.47 ✓
- **VRAM 8GB+** (Shape 모델만), 12GB+ (Paint 모델까지 풀)
- VRAM 8GB 경우 → 양자화/저메모리 모드로 Paint 실행 가능
- 디스크 ~30GB

### 설치 핵심 명령 (참고용)
```bash
cd /c/hunyuan3d
git clone https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1.git
cd Hunyuan3D-2.1
uv venv --python 3.11 .venv
.venv/Scripts/activate  # Windows
uv pip install torch==2.5.1 torchvision==0.20.1 --index-url https://download.pytorch.org/whl/cu124
uv pip install -r requirements.txt
# Custom CUDA 래스터라이저 컴파일 (MSVC build tools 필요)
cd hy3dpaint/custom_rasterizer && pip install -e . && cd ../..
```

### 모델 다운로드 (자동, ~25GB)
- Shape 모델: `tencent/Hunyuan3D-2.1` (HuggingFace)
- Paint 모델: 같은 저장소
- RealESRGAN: `wget https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth -P hy3dpaint/ckpt`

### 사용 패턴 (Python API)
```python
import sys
sys.path.insert(0, './hy3dshape')
sys.path.insert(0, './hy3dpaint')
from hy3dshape.pipelines import Hunyuan3DDiTFlowMatchingPipeline
from textureGenPipeline import Hunyuan3DPaintPipeline, Hunyuan3DPaintConfig

# 1) Shape (메시) — 이미지 → 흑백 메시
shape_pipe = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained('tencent/Hunyuan3D-2.1')
mesh = shape_pipe(image='input.png')

# 2) Paint (텍스처) — 이미지 + 메시 → 컬러 메시
paint_pipe = Hunyuan3DPaintPipeline.from_pretrained(Hunyuan3DPaintConfig(...))
textured_mesh = paint_pipe(mesh=mesh, image='input.png')

textured_mesh.export('output.glb')
```

### 함정
- `compile_mesh_painter.sh`는 Bash 스크립트 — Windows는 별도 처리 필요 (또는 wsl/git-bash)
- `custom_rasterizer`는 MSVC Build Tools 또는 Visual Studio C++ workload 필요
- 첫 실행 시 모델 다운로드로 20~60분 소요
- 8GB VRAM은 Paint 모델 풀 옵션엔 빠듯 — `low_vram_mode=True` 또는 양자화 옵션 사용

---

## 6. 관절 애니메이션 추가 (Hunyuan3D 정적 메시 → 움직임)

### 휴머노이드 (사람·앳하트 멤버)
→ **Mixamo 워크플로**가 사실상 표준. 위 Tier 2 절차 사용.

### 4족 동물 (강아지·마스티프 등)
| 옵션 | 무료 | 자동도 |
|------|------|--------|
| [Anything World](https://www.anything.world/) | 제한 무료 | ★★★ (어떤 생물도 자동) |
| Blender + [Rigify](https://docs.blender.org/manual/en/latest/addons/rigging/rigify/index.html) | ✓ | ★ (메타리그 + 본 위치 수동 조정) |
| [Mootion](https://www.mootion.com/) | 무료 티어 | ★★ |
| [3D AI Studio Auto-Rig](https://www.3daistudio.com/Tools/RiggingTool) | 유료 | ★★★ |
| AutoRig Pro (Blender) | $45 | ★★ |

### Blender Python으로 자동 리깅 (제가 가능)
- 사장님 케이스: Tier 3 메시 + Tier 1 v2 본 구조 합치기
- 핵심: Hunyuan3D 출력 메시에 **수동 본 위치 자동 추정**(머리·꼬리·다리)
- 정확도 보장 안 됨 — 결과 확인 후 수정 필요할 수 있음

---

## 7. 음악 → 안무 (K-POP 자동 안무)

### 도구
| 도구 | 라이선스 |
|------|----------|
| [EDGE (Stanford)](https://edge-dance.github.io/) | 오픈소스 — 자체 호스팅 가능 |
| [MVNT (한국)](https://docs.mvnt.world/ecosystem/mvnt-ai/dance-generation) ⭐ | 상업, K-POP 특화, 안무가 IP 시스템 포함 |
| [DeepMotion SayMotion](https://www.deepmotion.com/saymotion) | 무료 크레딧, 텍스트→3D |

### 한계
- Music-to-dance 출력은 **표준 휴머노이드 본 구조** (Mixamo/MetaHuman 호환)
- Hunyuan3D 출력 메시에 직접 적용 어려움 — 리타게팅 필요 (Blender / Cascadeur)

---

## 8. 미리보기 (GLB 즉시 확인)

| 도구 | 특징 |
|------|------|
| **[gltf-viewer.donmccurdy.com](https://gltf-viewer.donmccurdy.com/)** ⭐ | 드래그앤드롭, 애니 자동 재생 |
| [3dviewer.net](https://3dviewer.net/) | UI 깔끔 |
| [modelviewer.dev/editor](https://modelviewer.dev/editor/) | Google 공식, AR 보기 가능 |
| Unity Editor | Assets에 드래그 → Hierarchy → Play |

---

## 9. 자동 업로드 파이프 (PoC)

생성 후 우리 서버에 올릴 때:

```python
# Python 의사코드
import shutil, psycopg2
from datetime import datetime
from pathlib import Path

def upload_glb(glb_path, name, lat, lon, alt=0, scale=1.0):
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    folder = f"{ts}_{name.replace(' ', '_')}"
    dst = Path(r"C:\woopang\server\uploads") / folder
    dst.mkdir(exist_ok=True)
    shutil.copy(glb_path, dst / "model.glb")
    # thumb 생성 (Blender 렌더 또는 PIL)
    # ...
    # DB INSERT — c:\tmp\insert_dance_dog.py 패턴 참고
    # ...
```

기존 참고 스크립트: `c:\tmp\insert_dance_dog.py`

---

## 9-1. DB 접속 함정 (필수)

⚠️ **`.env`의 `DB_USER`/`DB_NAME`은 실제 서버 코드와 다름!**

- `.env` 표면: `DB_USER=woopang_user` / `DB_NAME=woopang_db`
- **실제 서버 사용**: `user='postgres'` / `database='ar_database'` (하드코딩, `.env`에서는 `DB_PASSWORD`만 가져옴)
- 출처: [app_improved.py:945-951](../../server/app_improved.py#L945) / [p2p_server.py:56-62](../../server/p2p_server.py#L56)

올바른 connect 예:
```python
conn = psycopg2.connect(
    host='210.105.65.145', port=5432,
    database='ar_database', user='postgres',
    password=os.getenv('DB_PASSWORD'),  # .env에서
    client_encoding='UTF8',
)
```

⚠️ **`locations.id`에 자동 증가 시퀀스 미연결.** INSERT 시 명시적으로 `MAX(id)+1` 지정 필요:
```python
cur.execute("SELECT MAX(id) FROM locations;")
next_id = cur.fetchone()[0] + 1
cur.execute("INSERT INTO locations (id, ...) VALUES (%s, ...)", (next_id, ...))
```

⚠️ **인코딩**: 윈도우 Korean CP949 환경 → `PYTHONIOENCODING=utf-8` + `PGCLIENTENCODING=UTF8` 필수. 또한 `psycopg2.connect(client_encoding='UTF8')`.

## 9-2. 재사용 가능한 INSERT 스니펫

`category='anim'` 행 추가 표준 패턴:
```python
import os, psycopg2
from pathlib import Path

# .env에서 DB_PASSWORD만 가져오기 (DB_USER/DB_NAME 무시)
for line in Path(r'C:\woopang\server\.env').read_text(encoding='utf-8').splitlines():
    if line.startswith('DB_PASSWORD='):
        db_pwd = line.split('=', 1)[1].strip()
        break

conn = psycopg2.connect(
    host='210.105.65.145', port=5432,
    database='ar_database', user='postgres',
    password=db_pwd, client_encoding='UTF8',
)
cur = conn.cursor()
cur.execute("SELECT MAX(id) FROM locations;")
next_id = cur.fetchone()[0] + 1

cur.execute("""
INSERT INTO locations (
    id, username, name, latitude, longitude, altitude,
    pet_friendly, separate_restroom, instagram_id,
    color, category, status, folder, main_photo, sub_photos,
    model_type, model_url, model_scale, created_at
) VALUES (
    %s, 'WOOPANG', %s, %s, %s, %s, FALSE, FALSE, '',
    NULL, 'anim', 'approved', %s, %s, '[]',
    'custom', %s, %s, CURRENT_TIMESTAMP
) RETURNING id;
""", (next_id, name, lat, lon, alt, folder, thumb_path, glb_url, scale))
new_id = cur.fetchone()[0]
conn.commit()
print(f'INSERT id={new_id}')
```

## 10. 함정 / 주의사항

1. **Python 버전** — Hunyuan3D는 3.10/3.11 테스트됨. 3.13/3.14는 일부 wheel 미지원.
2. **MSVC Build Tools** — Hunyuan3D custom CUDA 래스터라이저 컴파일에 필요.
3. **드라이버** — CUDA 13 wheel 사용 시 NVIDIA 580+ 필수.
4. **VRAM 8GB** — Paint 모델 풀 옵션 무리. `low_vram_mode=True` 필수.
5. **Mixamo는 4족 불가** — 강아지에는 Anything World 또는 Blender 수작업.
6. **K-pop 음악→안무** — MVNT는 한국 회사 직접 컨택 필요. EDGE는 오픈소스지만 운영 부담.
7. **저작권**: K-pop 안무는 한국 저작권법상 보호 — MVNT처럼 IP 클리어된 솔루션 권장.
8. **사용자 UGC 향후**: 서버 GPU + 큐 + 모더레이션 시스템 — 별도 큰 작업.

---

## 11. 우리 앱 코드 참조 (이미 작업한 부분)

| 파일 | 역할 |
|------|------|
| [GLBModelLoader.cs](../../Assets/Scripts/Download_Model/GLBModelLoader.cs) | GLB 다운·인스턴스화·애니 자동 재생 (`TryPlayAnimation()`) |
| [DataManager.cs](../../Assets/Scripts/Download/DataManager.cs) | `category='anim'` 인디케이터 탭 → DanceAnimController |
| [FilterManager.cs](../../Assets/Scripts/UI/FilterManager.cs) | anim 카테고리 자동 Full 스폰 제외 + Full 보존 |
| [DanceAnimController.cs](../../Assets/Scripts/UI/DanceAnimController.cs) | 확인 패널·다운로드·스폰·자동 디스폰 (100m/3분) |
| [Editor/DanceAnimPanelSetup.cs](../../Assets/Scripts/Editor/DanceAnimPanelSetup.cs) | 패널 UI 자동 생성 (씬에) |

---

*최종 업데이트: 2026-06-01 (Hunyuan3D 2.1 로컬 설치 진행 중)*
