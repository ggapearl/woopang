using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 목록 시트 제목줄 오른쪽 '목록 | 지도 | 설정'. 세 쪽이 옆으로 나란히 있고, 탭을 누르거나 옆으로 밀면 미끄러져 넘어간다.
/// 지도·설정 쪽은 보일 때만 켠다(지도가 켜지고 꺼질 때 하던 일은 R0926FriendsMap 그대로). 시트를 닫았다 열어도 마지막 쪽을 유지한다.
/// 옆으로 밀기는 지도·슬라이더처럼 스스로 끄는 것 위에서 시작하면 건드리지 않는다 (세로 목록 위에서는 옆으로 밀 때만 넘겨받는다).
/// </summary>
public class R0926SheetModes : MonoBehaviour
{
    [Tooltip("탭 순서대로 — 켜고 끄는 패널 (목록 칸은 비워 둔다)")]
    [SerializeField] private GameObject[] panels;
    [Tooltip("탭 순서대로 — 옆으로 미끄러지는 쪽 (목록 쪽 포함)")]
    [SerializeField] private RectTransform[] pages;
    [Tooltip("쪽들이 잘려 보이는 틀 (시트)")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Image[] tabs;
    [SerializeField] private Text[] labels;

    private static readonly Color On = new Color(0.953f, 0.961f, 0.965f);
    private static readonly Color Off = new Color(0.549f, 0.584f, 0.616f);
    private const float Gap = 40f;
    private const float DecidePx = 14f;

    private int current;
    private float pos;                 // 지금 보이는 쪽 (소수 = 넘어가는 중)
    private float[] baseX;
    private Canvas root;

    private enum Phase { Idle, Pending, Dragging, Ignored }
    private Phase phase;
    private Pointer tracked;
    private Vector2 startPos, lastPos;
    private float startPagePos, lastTime, velocity;
    private ScrollRect scroll, frozen;
    private static readonly List<RaycastResult> hits = new List<RaycastResult>();

    public int Current => current;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>(true);
        root = c != null ? c.rootCanvas : null;
        CacheBase();
        pos = current;
    }

    private void CacheBase()
    {
        if (baseX != null || pages == null) return;
        baseX = new float[pages.Length];
        for (int i = 0; i < pages.Length; i++) baseX[i] = pages[i] != null ? pages[i].anchoredPosition.x : 0f;
    }

    public void Show(int index)
    {
        int n = tabs != null ? tabs.Length : 1;
        current = Mathf.Clamp(index, 0, n - 1);
        if (!Application.isPlaying || !isActiveAndEnabled) pos = current;   // 에디터 캡처·닫힌 시트는 바로
        Layout();
    }

    private void OnDisable()
    {
        EndDrag();
        pos = current;
        Layout();
    }

    private void Update()
    {
        Drag();
        if (phase != Phase.Dragging && pos != current)
        {
            pos = Mathf.Lerp(pos, current, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (Mathf.Abs(pos - current) < 0.002f) pos = current;
        }
        Layout();
    }

    private float Step => (viewport != null ? viewport.rect.width : 1440f) + Gap;

    private void Layout()
    {
        CacheBase();
        if (pages != null && pages.Length > 0)
        {
            float step = Step;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;
                var p = pages[i].anchoredPosition;
                float x = baseX[i] + (i - pos) * step;
                if (!Mathf.Approximately(p.x, x)) pages[i].anchoredPosition = new Vector2(x, p.y);
            }
        }
        if (panels != null)
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                bool vis = Mathf.Abs(i - pos) < 0.999f;
                if (panels[i].activeSelf != vis) panels[i].SetActive(vis);
            }
        if (tabs != null)
            for (int i = 0; i < tabs.Length; i++)
            {
                float k = Mathf.Clamp01(1f - Mathf.Abs(pos - i));
                if (tabs[i] != null) tabs[i].color = new Color(1f, 1f, 1f, 0.14f * k);
                if (labels != null && i < labels.Length && labels[i] != null) labels[i].color = Color.Lerp(Off, On, k);
            }
    }

    // ── 옆으로 밀기 ──
    private void Drag()
    {
        if (frozen != null && phase != Phase.Dragging) { frozen.enabled = true; frozen = null; }
        if (viewport == null || pages == null || pages.Length < 2) return;

        if (phase == Phase.Idle)
        {
            tracked = null;
            foreach (var d in InputSystem.devices)
                if (d is Pointer pp && d.enabled && pp.press.wasPressedThisFrame) { tracked = pp; break; }
            if (tracked != null) Begin(tracked.position.ReadValue());
            return;
        }
        if (tracked == null || !tracked.added || !tracked.press.isPressed)
        {
            if (phase == Phase.Dragging) Release();
            phase = Phase.Idle;
            return;
        }
        Vector2 now = tracked.position.ReadValue();
        if (phase == Phase.Pending)
        {
            Vector2 d = now - startPos;
            if (d.magnitude < DecidePx) return;
            if (Mathf.Abs(d.x) < Mathf.Abs(d.y) * 1.3f) { phase = Phase.Ignored; return; }
            if (scroll != null) { frozen = scroll; frozen.enabled = false; }
            phase = Phase.Dragging;
            startPos = lastPos = now;
            startPagePos = pos;
            lastTime = Time.unscaledTime;
            velocity = 0f;
        }
        if (phase == Phase.Dragging)
        {
            float scale = root != null ? root.scaleFactor : 1f;
            float p = startPagePos - (now.x - startPos.x) / scale / Step;
            float max = pages.Length - 1;
            if (p < 0f) p *= 0.3f;                       // 끝에서는 살짝만 끌려온다
            else if (p > max) p = max + (p - max) * 0.3f;
            float dt = Time.unscaledTime - lastTime;
            if (dt > 0.0001f) velocity = Mathf.Lerp(velocity, (now.x - lastPos.x) / scale / dt, 0.5f);
            lastPos = now;
            lastTime = Time.unscaledTime;
            pos = p;
        }
    }

    private void Begin(Vector2 at)
    {
        phase = Phase.Ignored;
        var es = EventSystem.current;
        if (es == null) return;
        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = at }, hits);
        if (hits.Count == 0 || !hits[0].gameObject.transform.IsChildOf(viewport)) return;
        scroll = null;
        var handler = ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject);
        if (handler != null && handler.transform.IsChildOf(viewport))
        {
            var sr = handler.GetComponent<ScrollRect>();
            if (sr == null || sr.horizontal) return;    // 지도·슬라이더·가로 목록 — 그쪽 끌기
            scroll = sr;
        }
        startPos = at;
        phase = Phase.Pending;
    }

    private void Release()
    {
        float moved = pos - startPagePos;
        int target = current;
        if (moved > 0.18f || velocity < -1400f) target = current + 1;
        else if (moved < -0.18f || velocity > 1400f) target = current - 1;
        current = Mathf.Clamp(target, 0, pages.Length - 1);
        EndDrag();
    }

    private void EndDrag()
    {
        if (frozen != null) { frozen.enabled = true; frozen = null; }
        phase = Phase.Idle;
    }
}
