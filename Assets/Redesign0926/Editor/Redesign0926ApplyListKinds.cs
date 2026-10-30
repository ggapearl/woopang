using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// 목록의 사람·보물찾기 줄 (2026-10-13 대표님: "게임을 참석한 사람도 목록에서 게임이벤트가 보이도록 … 보물모양 이모지가 앞에 있고 …
    /// '팔로잉' 을 눌러도 해당 내용이 바뀌거나 변하지 않는데 … 사람의 경우 간단한 사람 이모지를 써서 '핑크색 유지'").
    ///  · 사람 필터(P2PUserFilterPanel)가 FilterManager 오브젝트와 사람 칩(P2PUserToggle)에 둘 붙어 있었다 — 한 번 누르면 둘이 따로 돌고
    ///    칩은 그중 하나만 읽었다. 앱을 켤 때부터 켜져 있는 FilterManager 쪽 하나만 남긴다 (칩 쪽은 목록을 처음 열 때에야 깨어났다)
    ///  · 목록 줄 앞 그림 — 사람 모양(핑크) · 금빛 상자 · 축제마다 그림 · 보호소 강아지 · 장소는 분류 그림 (R0926PlaceRows)
    ///  · 지도의 보물찾기 핀·원 (10-13 대표님: "'지도' 에서도 보물찾기 이벤트의 경우에는 보일 수 있도록") — R0926FriendsMap
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private static void ApplyListKinds(Transform root, List<string> log)
        {
            var scene = root.gameObject.scene;
            var panels = new List<P2PUserFilterPanel>();
            P2PUserFilterPanel keep = null;
            foreach (var p in Object.FindObjectsByType<P2PUserFilterPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (p.gameObject.scene != scene) continue;
                panels.Add(p);
                if (keep == null && p.GetComponent<FilterManager>() != null) keep = p;
            }
            if (keep == null && panels.Count > 0) keep = panels[0];
            foreach (var p in panels)
            {
                if (p == keep) continue;
                var so = new SerializedObject(keep);
                var toggle = so.FindProperty("p2pUserToggle");
                if (toggle.objectReferenceValue == null)
                {
                    toggle.objectReferenceValue = p.GetToggle();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                log.Add("사람 필터 중복 제거: " + p.gameObject.name + " (남긴 것: " + keep.gameObject.name + ")");
                Object.DestroyImmediate(p);
            }
            if (keep != null) EditorUtility.SetDirty(keep);
            else log.Add("사람 필터(P2PUserFilterPanel) 없음");

            var rows = root.GetComponentInChildren<R0926PlaceRows>(true);
            if (rows != null)
            {
                var rso = new SerializedObject(rows);
                rso.FindProperty("personSprite").objectReferenceValue = Spr("r0926_i_user");
                rso.FindProperty("huntSprite").objectReferenceValue = Spr("r0926_i_treasure");
                rso.FindProperty("huntLockSprite").objectReferenceValue = Spr("r0926_i_treasure_lock");
                // 보물찾기 줄 — 금빛 테두리 · 지나가는 빛 띠 · 상자 옆 별 (10-27 대표님 「살짝 하이라이트, 반짝임」)
                rso.FindProperty("huntRingSprite").objectReferenceValue = Spr("r0926_pill_ring");
                rso.FindProperty("shineSprite").objectReferenceValue = Spr("r0926_shine");
                rso.FindProperty("twinkleSprite").objectReferenceValue = Spr("r0926_re_spark");
                // 축제·보호소 줄 그림 — 서버가 축제마다 고른 이름(r0926_f_<이름>) · 보호소는 shelter (10-23 대표님)
                var kinds = new List<Sprite>();
                foreach (var file in System.IO.Directory.GetFiles(SpriteDir, "r0926_f_*.png"))   // 막 바꿔 들인 그림은 FindAssets 색인에 아직 없다
                {
                    var s = Spr(System.IO.Path.GetFileNameWithoutExtension(file));
                    if (s != null) kinds.Add(s);
                }
                SetArray(rso.FindProperty("kindIcons"), kinds.ToArray());
                // 장소 줄 분류 그림 — 필터 칩·올리기 칩과 같은 r0926_c_<분류> (10-27 대표님 「'목록' 에서도 동그라미가 아닌 이모지」)
                var cats = new List<Sprite>();
                foreach (var file in System.IO.Directory.GetFiles(SpriteDir, "r0926_c_*.png"))
                {
                    var s = Spr(System.IO.Path.GetFileNameWithoutExtension(file));
                    if (s != null) cats.Add(s);
                }
                SetArray(rso.FindProperty("catIcons"), cats.ToArray());
                rso.ApplyModifiedPropertiesWithoutUndo();
                if (kinds.Count == 0) log.Add("축제 그림(r0926_f_*) 없음");
                if (cats.Count == 0) log.Add("분류 그림(r0926_c_*) 없음");
            }
            else log.Add("목록 줄(R0926PlaceRows) 없음");

            var map = root.GetComponentInChildren<R0926FriendsMap>(true);
            var mso = map != null ? new SerializedObject(map) : null;
            var pins = mso != null ? mso.FindProperty("pinLayer").objectReferenceValue as RectTransform : null;
            if (pins == null) { log.Add("지도 핀 자리(R0926FriendsMap.pinLayer) 없음"); return; }
            mso.FindProperty("huntPinTemplate").objectReferenceValue = BuildHuntPinTemplate(pins);
            mso.FindProperty("huntAreaTemplate").objectReferenceValue = BuildHuntAreaTemplate(pins);
            mso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 지도의 보물찾기 — 친구 핀과 같은 틀(240×184 · 원 가운데가 그 자리) · 금빛 테두리 원 안에 상자, 아래에 게임 이름(금빛)
        private static GameObject BuildHuntPinTemplate(Transform pins)
        {
            var pin = FindOrCreate(pins, "HuntPinTemplate0926");
            SetRect(RT(pin), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(240, 184));
            Img(pin, null, new Color(0, 0, 0, 0), Image.Type.Simple).raycastTarget = true;
            Ensure<Button>(pin).transition = Selectable.Transition.None;
            var center = new Vector2(0.5f, 0.5f);
            var ring = FindOrCreate(pin.transform, "Ring");
            SetRect(RT(ring), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(116, 116));
            Img(ring, Spr("r0926_circle"), HuntGold, Image.Type.Simple).raycastTarget = false;
            var face = FindOrCreate(ring.transform, "Face");
            SetRect(RT(face), center, center, center, Vector2.zero, new Vector2(102, 102));
            Img(face, Spr("r0926_circle"), Dark, Image.Type.Simple).raycastTarget = false;
            var icon = FindOrCreate(face.transform, "Icon");
            SetRect(RT(icon), center, center, center, new Vector2(0, 2), new Vector2(84, 84));
            var ii = Img(icon, Spr("r0926_i_treasure"), Color.white, Image.Type.Simple);
            ii.raycastTarget = false; ii.preserveAspect = true;
            // 비밀번호를 건 보물 — 테 오른쪽 아래 작은 자물쇠 배지 (R0926FriendsMap 이 켠다)
            var lk = FindOrCreate(ring.transform, "Lock");
            lk.transform.SetAsLastSibling();
            SetRect(RT(lk), center, center, center, new Vector2(40, -40), new Vector2(48, 48));
            Img(lk, Spr("r0926_circle"), HuntGold, Image.Type.Simple).raycastTarget = false;
            var lf = FindOrCreate(lk.transform, "Face");
            SetRect(RT(lf), center, center, center, Vector2.zero, new Vector2(40, 40));
            Img(lf, Spr("r0926_circle"), Dark, Image.Type.Simple).raycastTarget = false;
            var lg = FindOrCreate(lf.transform, "Glyph");
            SetRect(RT(lg), center, center, center, new Vector2(0, 1), new Vector2(26, 26));
            var lgi = Img(lg, Spr("r0926_lock"), HuntGold, Image.Type.Simple);
            lgi.raycastTarget = false; lgi.preserveAspect = true;
            lk.SetActive(false);
            var name = FindOrCreate(pin.transform, "Name");
            SetRect(RT(name), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(150, 52));
            Img(name, Spr("r0926_pill"), new Color(0.055f, 0.067f, 0.078f, 0.88f), Image.Type.Sliced, 64f / 26f).raycastTarget = false;
            var nh = Ensure<HorizontalLayoutGroup>(name);
            nh.padding = new RectOffset(20, 20, 0, 0); nh.childAlignment = TextAnchor.MiddleCenter;
            nh.childControlWidth = true; nh.childControlHeight = true; nh.childForceExpandWidth = false; nh.childForceExpandHeight = true;
            Ensure<ContentSizeFitter>(name).horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var nt = FindOrCreate(name.transform, "Text");
            Txt(nt, "보물찾기", 34, HuntGold, TextAnchor.MiddleCenter, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            pin.SetActive(false);
            return pin;
        }

        // 대략 공개 게임의 '이 안 어딘가' 원 — 크기는 실행 때 지도 배율로 (R0926FriendsMap.LayoutHunts)
        private static GameObject BuildHuntAreaTemplate(Transform pins)
        {
            var area = FindOrCreate(pins, "HuntAreaTemplate0926");
            area.transform.SetAsFirstSibling();
            SetRect(RT(area), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
            Img(area, Spr("r0926_circle"), new Color(HuntGold.r, HuntGold.g, HuntGold.b, 0.14f), Image.Type.Simple).raycastTarget = false;
            area.SetActive(false);
            return area;
        }
    }
}
