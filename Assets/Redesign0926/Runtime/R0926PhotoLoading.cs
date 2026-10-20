using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사진을 고른 뒤 불러오는 동안 그 칸에 — 빛이 흐르는 빈 칸 + 도는 분홍 원호.
/// 10-20 대표님 「메인·서브 사진 올릴 때(특히 서브) 사진이 올라가는 동안 그 섹션에 로딩스피너나 스켈레톤」.
/// 대표 칸은 그 칸을 덮고, 추가 사진은 그리드 맨 뒤에 불러올 장수만큼 — 한 장씩 들어올 때마다 하나씩 사진으로 바뀐다.
/// 빈 칸 모양(둥근 칸 · 빛 띠 · 원호)은 Redesign0926Apply3 가 만들어 둔 본을 복제한다
/// </summary>
public class R0926PhotoLoading : MonoBehaviour
{
    [SerializeField] private GameObject mainSkeleton;   // 대표 사진 칸을 덮는 빈 칸 (꺼 둔 채)
    [SerializeField] private GameObject subTemplate;    // 추가 사진 빈 칸 본 (꺼 둔 채)
    [SerializeField] private Transform subGrid;         // 추가 사진 그리드

    private readonly List<GameObject> subs = new List<GameObject>();
    private readonly List<RectTransform> spins = new List<RectTransform>();
    private RectTransform mainSpin;

    public bool MainShowing => mainSkeleton != null && mainSkeleton.activeSelf;
    public int SubShowing { get; private set; }

    public void SetMain(bool on)
    {
        if (mainSkeleton == null || mainSkeleton.activeSelf == on) return;
        if (mainSpin == null) mainSpin = mainSkeleton.transform.Find("Spin0926") as RectTransform;
        mainSkeleton.SetActive(on);
    }

    public void SetSub(int count)
    {
        if (subGrid == null || subTemplate == null) return;
        count = Mathf.Max(0, count);
        while (subs.Count < count)
        {
            var go = Instantiate(subTemplate, subGrid, false);
            go.name = "SubLoading0926_" + subs.Count;
            subs.Add(go);
            spins.Add(go.transform.Find("Spin0926") as RectTransform);
        }
        for (int i = 0; i < subs.Count; i++)
        {
            bool on = i < count;
            if (on) subs[i].transform.SetAsLastSibling();   // 이미 들어온 사진 뒤에
            if (subs[i].activeSelf != on) subs[i].SetActive(on);
        }
        SubShowing = count;
    }

    private void LateUpdate()
    {
        var turn = new Vector3(0f, 0f, -Time.unscaledTime * 400f);   // 상태 알약 원호와 같은 빠르기
        if (mainSpin != null && MainShowing) mainSpin.localEulerAngles = turn;
        for (int i = 0; i < SubShowing && i < spins.Count; i++)
            if (spins[i] != null) spins[i].localEulerAngles = turn;
    }
}
