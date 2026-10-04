"""
기상청 단기예보 조회서비스(VilageFcstInfoService_2.0) — 우팡 서버 /api/weather 의 '지금 날씨'를 기상청 값으로 덮는다.

왜: 지금 /api/weather 는 Open-Meteo 예보 모델 값이라 국지적으로 내리는 비를 놓친다 (실제로 비가 오는데 '맑음').
    기상청 초단기실황(관측, 매시 정각 기준·약 40분 뒤 발표)의 강수형태(PTY)·1시간 강수량(RN1)·기온·습도·풍속으로
    '지금' 칸만 바꾸고, 3시간 후·6시간 후·내일·일출·달 등은 지금처럼 Open-Meteo 값을 쓴다.

붙이는 법 (C:\\woopang\\server, git 미추적):
  1) 이 파일을 server/ 에 복사 (weather_kma.py)
  2) .env 에 KMA_SERVICE_KEY=<공공데이터포털 일반 인증키(Decoding)>  — 코드·git 에 키를 쓰지 말 것 (CLAUDE.md 6)
     공공데이터포털에서 '기상청_단기예보 ((구)_동네예보) 조회서비스' 활용신청이 되어 있어야 한다 (TourAPI 와 별도 신청)
  3) app_improved.py 의 /api/weather 에서 응답 dict(out)을 만든 뒤, 돌려주기 직전에:
         from weather_kma import apply_kma_now
         apply_kma_now(out, lat, lon)
  4) 서버 재시작. 앱(1.2.54~)은 pty·rn1·humidity 를 읽고, 예전 앱은 바뀐 temp·code 만 본다 (필드가 늘어도 깨지지 않는다)

응답에 더해지거나 바뀌는 필드 (실황을 못 받으면 아무것도 바꾸지 않는다):
  temp      ← T1H 기온 (℃)
  humidity  ← REH 습도 (%)  — 앱 날씨판 '습도 N%'
  wind      ← WSD 풍속 (m/s)
  pty       ← PTY 강수형태 0 없음 · 1 비 · 2 비/눈 · 3 눈 · 5 빗방울 · 6 빗방울눈날림 · 7 눈날림
  rn1       ← RN1 1시간 강수량 (mm, '강수없음' → 0)
  code      ← PTY(+초단기예보 SKY·낙뢰)로 만든 WMO 코드 — 앱 그림·'비/맑음' 글자가 이걸 쓴다
"""

import json
import math
import os
import threading
import time
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

KST = timezone(timedelta(hours=9))
BASE = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0"
CACHE_SECONDS = 600          # 같은 격자(5km)는 10분 동안 다시 묻지 않는다
TIMEOUT = 4                  # 기상청이 느려도 /api/weather 가 오래 막히지 않게

_cache = {}
_lock = threading.Lock()


# ── 위경도 → 기상청 격자 (Lambert Conformal Conic, 기상청 공개 변환식) ──────────
def latlon_to_grid(lat, lon):
    RE, GRID = 6371.00877, 5.0
    SLAT1, SLAT2, OLON, OLAT = 30.0, 60.0, 126.0, 38.0
    XO, YO = 43, 136
    d = math.pi / 180.0
    re = RE / GRID
    slat1, slat2, olon, olat = SLAT1 * d, SLAT2 * d, OLON * d, OLAT * d
    sn = math.log(math.cos(slat1) / math.cos(slat2)) / math.log(
        math.tan(math.pi * 0.25 + slat2 * 0.5) / math.tan(math.pi * 0.25 + slat1 * 0.5))
    sf = math.pow(math.tan(math.pi * 0.25 + slat1 * 0.5), sn) * math.cos(slat1) / sn
    ro = re * sf / math.pow(math.tan(math.pi * 0.25 + olat * 0.5), sn)
    ra = re * sf / math.pow(math.tan(math.pi * 0.25 + lat * d * 0.5), sn)
    theta = lon * d - olon
    if theta > math.pi:
        theta -= 2.0 * math.pi
    if theta < -math.pi:
        theta += 2.0 * math.pi
    theta *= sn
    x = math.floor(ra * math.sin(theta) + XO + 0.5)
    y = math.floor(ro - ra * math.cos(theta) + YO + 0.5)
    return int(x), int(y)


def in_korea_grid(nx, ny):
    return 1 <= nx <= 149 and 1 <= ny <= 253


# ── 발표 시각 ───────────────────────────────────────────────
def ncst_base(now=None):
    """초단기실황: 매시 정각 자료가 약 40분 뒤 나온다 → 45분 전이면 한 시간 전 자료"""
    now = now or datetime.now(KST)
    t = now if now.minute >= 45 else now - timedelta(hours=1)
    return t.strftime("%Y%m%d"), t.strftime("%H00")


def fcst_base(now=None):
    """초단기예보: 매시 30분 자료가 약 45분에 나온다 → 45분 전이면 한 시간 전 30분 자료"""
    now = now or datetime.now(KST)
    t = now if now.minute >= 45 else now - timedelta(hours=1)
    return t.strftime("%Y%m%d"), t.strftime("%H30")


def _get(op, key, base_date, base_time, nx, ny, rows):
    q = urllib.parse.urlencode({
        "serviceKey": key, "pageNo": 1, "numOfRows": rows, "dataType": "JSON",
        "base_date": base_date, "base_time": base_time, "nx": nx, "ny": ny,
    })
    with urllib.request.urlopen(f"{BASE}/{op}?{q}", timeout=TIMEOUT) as r:
        data = json.loads(r.read().decode("utf-8"))
    head = data.get("response", {}).get("header", {})
    if head.get("resultCode") != "00":
        raise RuntimeError(f"{op} {head.get('resultCode')} {head.get('resultMsg')}")
    return data["response"]["body"]["items"]["item"]


def _num(v, default=None):
    """'강수없음'·'1mm 미만'·'30.0~50.0mm' 같은 글도 숫자로"""
    if v is None:
        return default
    s = str(v).strip()
    if s in ("강수없음", "", "-"):
        return 0.0
    if "미만" in s:
        return 0.5
    s = s.replace("mm", "").split("~")[0]
    try:
        return float(s)
    except ValueError:
        return default


def pty_to_wmo(pty, sky, lightning):
    """PTY/SKY → Open-Meteo WMO 코드 (앱 R0926WeatherIcon·Condition 이 읽는 값)"""
    if pty in (1, 2, 5, 6) and lightning:
        return 95
    if pty:
        return {1: 61, 2: 66, 3: 71, 4: 80, 5: 51, 6: 66, 7: 71}.get(pty, 61)
    return {1: 0, 3: 2, 4: 3}.get(sky, 3)


def fetch_kma_now(lat, lon, key=None):
    """지금 날씨(실황 + 가장 가까운 초단기예보의 하늘상태). 실패하면 None."""
    key = key or os.getenv("KMA_SERVICE_KEY")
    if not key:
        return None
    nx, ny = latlon_to_grid(lat, lon)
    if not in_korea_grid(nx, ny):
        return None

    ck = (nx, ny)
    with _lock:
        hit = _cache.get(ck)
        if hit and time.time() - hit[0] < CACHE_SECONDS:
            return hit[1]

    try:
        d, t = ncst_base()
        obs = {i["category"]: i["obsrValue"] for i in _get("getUltraSrtNcst", key, d, t, nx, ny, 10)}
    except Exception:
        return None

    sky, lgt = None, 0
    try:
        d2, t2 = fcst_base()
        items = _get("getUltraSrtFcst", key, d2, t2, nx, ny, 60)
        first = min(i["fcstDate"] + i["fcstTime"] for i in items)   # 가장 가까운 예보 시각
        for i in items:
            if i["fcstDate"] + i["fcstTime"] != first:
                continue
            if i["category"] == "SKY":
                sky = int(_num(i["fcstValue"], 3))
            elif i["category"] == "LGT":
                lgt = _num(i["fcstValue"], 0) or 0
    except Exception:
        pass   # 하늘상태만 못 받음 — 강수 여부는 실황으로 충분

    pty = int(_num(obs.get("PTY"), 0))
    now = {
        "temp": _num(obs.get("T1H")),
        "humidity": _num(obs.get("REH")),
        "wind": _num(obs.get("WSD")),
        "pty": pty,
        "rn1": _num(obs.get("RN1"), 0.0),
        "code": pty_to_wmo(pty, sky, lgt > 0),
        "kma_grid": [nx, ny],
    }
    with _lock:
        _cache[ck] = (time.time(), now)
    return now


def apply_kma_now(out, lat, lon):
    """/api/weather 응답 dict 의 '지금' 칸을 기상청 값으로 덮는다. 기상청을 못 받으면 그대로 둔다."""
    now = fetch_kma_now(float(lat), float(lon))
    if not now:
        return out
    for k, v in now.items():
        if v is not None:
            out[k] = v
    return out


if __name__ == "__main__":
    # 격자 변환 확인 (기상청 동네예보 격자표 기준값)
    assert latlon_to_grid(37.5665, 126.9780) == (60, 127), latlon_to_grid(37.5665, 126.9780)   # 서울시청
    assert latlon_to_grid(35.1796, 129.0756) == (98, 76), latlon_to_grid(35.1796, 129.0756)    # 부산시청
    print("ok", latlon_to_grid(36.6361, 126.8280))
