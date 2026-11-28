using System;
using System.Collections.Generic;

[System.Serializable]
public class FacilityData
{
    public string type { get; set; }
    public string name { get; set; }
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string address { get; set; }
    public string extra_info { get; set; }
    public float altitude { get; set; }
    public string main_photo { get; set; }

    // 지하철역 — 서버가 국토교통부 TAGO 역과 맞춘 것만 붙는다 (11-27 23 · 24번 · server/transit_info.py). 없으면 null
    public List<string> lines { get; set; }    // 노선 「1호선」 「2호선」
    public string exits { get; set; }          // 「1번 서울시립미술관 · 2번 국립현대미술관 …」 한 줄
    public List<string> photos { get; set; }   // 첫차·막차 · 출구 안내 카드 그림 주소 (서버가 그린다 — 기차역 · 버스터미널도 이 칸으로 · 11-27 저녁)
}
