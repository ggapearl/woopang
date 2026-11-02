using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Redesign0926
{
    /// <summary>
    /// v53: 10-27 대표님 「안드로이드는 '사진설정'을 하려 해도 '사진첩'·'갤러리'가 아니라 '내 파일'로 안내」
    ///  「아예 웹으로 이동하지 않고 앱 내에서 수정할 수 있으면 더 좋을 것 같긴 해」
    ///  — 내 프로필 '편집'이 웹 대신 여는 시트(ProfileEdit0926, R0926ProfileEdit). 프로필 창 위 어두운 바탕 + 장소 수정 카드와 같은 자리의 카드:
    ///  사진(사진첩 → 자르기) · 아이디 · 소개 · 전화번호 · 인스타그램 · 페이스북 · X · 맨 아래 '비밀번호 변경 · 계정 삭제'(웹) · 저장하기.
    ///  입력칸은 프로필 창의 키보드 위 입력줄(UploadInputMirror — 보물찾기 리뷰 칸과 같은 줄)로 쓴다. 끌어내리기·X·뒤로가기로 닫는다(바깥 누르기는 안 닫는다 — 고치던 글이 날아간다).
    /// v66: 10-25 대표님 「프로필 편집에서 glb 를 업로드하고 어떻게 보이도록 할 건지 선택」 — 사진 칸 아래 3D 모델 칸(Model0926) · 사진/3D 고르기(F_display) · 프로필 화면 둥근 사진 위 3D 칸.
    /// </summary>
    public static partial class Redesign0926Apply
    {
        private const float PeSaveH = 150f, PeSaveY = 48f, PeMsgH = 100f;

        private static void ApplyProfileEdit(Transform root, List<string> log)
        {
            var panel = Find(root, "FullProfilePanel");
            if (panel == null) { log.Add("프로필 편집: 프로필 창 없음"); return; }

            var sheet = FindOrCreate(panel.transform, "ProfileEdit0926");
            sheet.transform.SetAsLastSibling();
            Img(sheet, null, new Color(0, 0, 0, 0.6f), Image.Type.Simple).raycastTarget = true;
            SetRect(RT(sheet), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var logic = Ensure<R0926ProfileEdit>(sheet);

            // ── 카드 (장소 수정 카드와 같은 자리 — 도크 위) ──
            var card = FindOrCreate(sheet.transform, "Card0926");
            Img(card, Spr("r0926_pill"), new Color(Sheet.r, Sheet.g, Sheet.b, 1f), Image.Type.Sliced, 64f / CardRadius).raycastTarget = true;
            var crt = RT(card);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.pivot = new Vector2(0.5f, 0.5f);
            crt.offsetMin = new Vector2(UploadMargin, 390); crt.offsetMax = new Vector2(-UploadMargin, -250);
            EditorUtility.SetDirty(crt);
            Ensure<R0926SafeInset>(card).SetEdge(R0926SafeInset.Edge.Bottom, R0926SafeInset.Mode.Stretch);
            Slide(card, 260f);

            var title = FindOrCreate(card.transform, "Title0926");
            Txt(title, "프로필 편집", 56, Ink, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Row(title, -44, 84);
            Loc(title, "프로필 편집", "Edit profile", "プロフィールを編集", "编辑个人资料", "Editar perfil");

            var x = FindOrCreate(card.transform, "Close0926");
            SetRect(RT(x), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-36, -30), new Vector2(110, 110));
            Img(x, Spr("r0926_circle"), new Color(1, 1, 1, 0.12f), Image.Type.Simple).raycastTarget = true;
            var xic = FindOrCreate(x.transform, "Icon0926");
            Img(xic, Spr("r0926_i_close"), Ink, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(xic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            var xBtn = Ensure<Button>(x);
            xBtn.targetGraphic = x.GetComponent<Image>();
            ResetListeners(xBtn);
            UnityEventTools.AddPersistentListener(xBtn.onClick, logic.Close);

            // ── 칸 목록 (스크롤) ──
            var content = HuntScroll(card.transform, "Form0926", out var view, out var sr);
            var vrt = RT(view);
            vrt.offsetMin = new Vector2(0, PeSaveY + PeSaveH + 12 + PeMsgH + 8); vrt.offsetMax = new Vector2(0, -170);
            EditorUtility.SetDirty(vrt);
            var group = Ensure<CanvasGroup>(view);
            var cvl = content.GetComponent<VerticalLayoutGroup>();
            cvl.padding = new RectOffset((int)UP, (int)UP, 8, 60); cvl.spacing = 44;
            EditorUtility.SetDirty(cvl);

            var photo = PeAvatarRow(content, logic);
            var modelRow = PeModelRow(content, logic, out var modelLabel, out var modelView);
            var displayGroup = PeDisplayGroup(content, logic, out var dTabs, out var dLabels, out var removeLabel);

            var uGroup = PeGroup(content, "F_username", new[] { "아이디", "Username", "ユーザー名", "用户名", "Nombre de usuario" });
            var uIn = PeInput(uGroup, "Input", 140, new[] { "영문 소문자·숫자·._", "Lowercase letters, numbers, . and _", "英小文字・数字・._", "小写字母、数字、._", "Minúsculas, números, . y _" },
                false, 20, InputField.ContentType.Custom, TouchScreenKeyboardType.EmailAddress);
            var lockGo = FindOrCreate(uIn.transform, "Lock0926");
            lockGo.transform.SetAsLastSibling();
            Stretch(RT(lockGo));
            Img(lockGo, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
            var lockBtn = Ensure<Button>(lockGo);
            lockBtn.transition = Selectable.Transition.None;
            ResetListeners(lockBtn);
            UnityEventTools.AddPersistentListener(lockBtn.onClick, logic.OnUsernameLocked);
            var lockIc = FindOrCreate(lockGo.transform, "Icon0926");
            Img(lockIc, Spr("r0926_lock"), Muted, Image.Type.Simple).raycastTarget = false;
            SetRect(RT(lockIc), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(46, 46));
            lockGo.SetActive(false);
            var noteGo = FindOrCreate(uGroup, "Note0926");
            var note = Txt(noteGo, "", 38, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            note.horizontalOverflow = HorizontalWrapMode.Wrap; note.verticalOverflow = VerticalWrapMode.Overflow; note.lineSpacing = 1.1f;

            var bIn = PeInput(PeGroup(content, "F_bio", new[] { "소개", "Bio", "自己紹介", "简介", "Biografía" }), "Input", 260,
                new[] { "나를 한두 줄로 소개해 주세요", "Tell people about yourself in a line or two", "一言二言で自己紹介してください", "用一两句话介绍一下自己", "Preséntate en una o dos líneas" },
                true, 150, InputField.ContentType.Standard, TouchScreenKeyboardType.Default);
            var pIn = PeInput(PeGroup(content, "F_phone", new[] { "전화번호", "Phone", "電話番号", "电话号码", "Teléfono" }), "Input", 140,
                new[] { "나만 볼 수 있어요", "Only you can see this", "自分だけが見られます", "仅自己可见", "Solo tú puedes verlo" },
                false, 20, InputField.ContentType.Custom, TouchScreenKeyboardType.PhonePad);
            var iIn = PeInput(PeGroup(content, "F_instagram", new[] { "인스타그램", "Instagram", "Instagram", "Instagram", "Instagram" }), "Input", 140,
                new[] { "인스타그램 아이디", "Instagram username", "Instagramのユーザー名", "Instagram 用户名", "Usuario de Instagram" },
                false, 50, InputField.ContentType.Custom, TouchScreenKeyboardType.EmailAddress);
            var fIn = PeInput(PeGroup(content, "F_facebook", new[] { "페이스북", "Facebook", "Facebook", "Facebook", "Facebook" }), "Input", 140,
                new[] { "페이스북 아이디", "Facebook username", "Facebookのユーザー名", "Facebook 用户名", "Usuario de Facebook" },
                false, 50, InputField.ContentType.Custom, TouchScreenKeyboardType.EmailAddress);
            var xIn = PeInput(PeGroup(content, "F_x", new[] { "X", "X", "X", "X", "X" }), "Input", 140,
                new[] { "X 아이디", "X username", "Xのユーザー名", "X 用户名", "Usuario de X" },
                false, 50, InputField.ContentType.Custom, TouchScreenKeyboardType.EmailAddress);

            modelRow.SetSiblingIndex(1);       // 칸 묶음은 차례로 맨 뒤에 붙는다 — 사진 칸 바로 아래로
            displayGroup.transform.SetSiblingIndex(2);

            // 비밀번호·계정 삭제는 웹에서 (일회용 코드로 연다)
            var web = FindOrCreate(content, "Web0926");
            web.transform.SetAsLastSibling();
            Ensure<LayoutElement>(web).preferredHeight = 110;
            Img(web, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
            var webBtn = Ensure<Button>(web);
            webBtn.transition = Selectable.Transition.None;
            ResetListeners(webBtn);
            UnityEventTools.AddPersistentListener(webBtn.onClick, logic.OpenWeb);
            var wl = FindOrCreate(web.transform, "Label0926");
            Stretch(RT(wl));
            Txt(wl, "비밀번호 변경 · 계정 삭제  ›", 40, Muted, TextAnchor.MiddleLeft, FontStyle.Normal).horizontalOverflow = HorizontalWrapMode.Wrap;
            Loc(wl, "비밀번호 변경 · 계정 삭제  ›", "Change password · Delete account  ›", "パスワード変更 · アカウント削除  ›", "修改密码 · 删除账户  ›", "Cambiar contraseña · Eliminar cuenta  ›");

            // ── 아래: 안내 한 줄 + 저장하기 ──
            var msg = FindOrCreate(card.transform, "Message0926");
            SetRect(RT(msg), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, PeSaveY + PeSaveH + 12), new Vector2(-UP * 2, PeMsgH));
            var msgText = Txt(msg, "", 40, Pink, TextAnchor.MiddleCenter, FontStyle.Bold);
            msgText.horizontalOverflow = HorizontalWrapMode.Wrap; msgText.verticalOverflow = VerticalWrapMode.Overflow;
            msg.SetActive(false);

            var save = FindOrCreate(card.transform, "Save0926");
            SetRect(RT(save), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, PeSaveY), new Vector2(-UP * 2, PeSaveH));
            PillButton(save, true);
            var saveBtn = Ensure<Button>(save);
            saveBtn.targetGraphic = save.GetComponent<Image>();
            ResetListeners(saveBtn);
            UnityEventTools.AddPersistentListener(saveBtn.onClick, logic.OnSave);
            var sl = FindOrCreate(save.transform, "Label0926");
            Stretch(RT(sl));
            var saveLabel = Txt(sl, "저장하기", 46, Dark, TextAnchor.MiddleCenter, FontStyle.Bold);   // 글은 런타임이 상태마다 바꾼다 (번역 부품을 달지 않는다)

            Grab(card);

            // 끌어내리기 · X · 뒤로가기 = 닫기 (바깥 누르기는 고치던 글이 날아가 넣지 않는다)
            BackCloses(x.transform, "프로필 편집 X", log);
            string cardPath = AnimationUtility.CalculateTransformPath(card.transform, root);
            string xPath = AnimationUtility.CalculateTransformPath(x.transform, root);
            Swipe(root, cardPath, xPath, new string[0], log);
            var sd = card.GetComponent<R0926SwipeDismiss>();
            if (sd != null)
            {
                var dso = new SerializedObject(sd);
                dso.FindProperty("cancelClicks").boolValue = true;   // 저장하기·사진 위에서 끌어 내리다 놓아도 눌리지 않게
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
            Proxy(root, sd, xPath, log);

            var so = new SerializedObject(logic);
            void S(string n, Object v) { so.FindProperty(n).objectReferenceValue = v; }
            S("form", group);
            S("scroll", sr);
            S("avatar", photo);
            S("usernameInput", uIn);
            S("usernameNote", note);
            S("usernameLock", lockGo);
            S("bioInput", bIn);
            S("phoneInput", pIn);
            S("instagramInput", iIn);
            S("facebookInput", fIn);
            S("xInput", xIn);
            S("message", msgText);
            S("saveButton", saveBtn);
            S("saveLabel", saveLabel);
            S("photoCrop", FindInScene<R0926PhotoCrop>(root.gameObject));
            S("busy", FindInScene<R0926Busy>(root.gameObject));
            S("closer", sd);
            S("closeButton", xBtn);
            S("modelView", modelView);
            S("modelLabel", modelLabel);
            S("displayGroup", displayGroup);
            SetArray(so.FindProperty("displayTabs"), dTabs);
            SetArray(so.FindProperty("displayLabels"), dLabels);
            S("removeLabel", removeLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (FindInScene<R0926PhotoCrop>(root.gameObject) == null) log.Add("프로필 편집: 자르기 화면 없음");

            // 키보드 위 입력줄 — 프로필 창의 줄(보물찾기 리뷰 칸, Redesign0926ApplyHuntProfile)에 이 칸들도 넣는다
            var mirror = panel.GetComponent<UploadInputMirror>();
            if (mirror != null)
            {
                var mso = new SerializedObject(mirror);
                var src = mso.FindProperty("sourceInputs");
                var have = new HashSet<Object>();
                for (int i = 0; i < src.arraySize; i++) have.Add(src.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach (var f in new[] { uIn, bIn, pIn, iIn, fIn, xIn })
                {
                    if (have.Contains(f)) continue;
                    src.arraySize++;
                    src.GetArrayElementAtIndex(src.arraySize - 1).objectReferenceValue = f;
                }
                mso.ApplyModifiedPropertiesWithoutUndo();
            }
            else log.Add("프로필 편집: 키보드 입력줄 없음");

            // 프로필 화면 — 둥근 사진 위 3D 칸 (사진과 같은 원에 잘린다)
            var avatarMask = Find(panel.transform, "AvatarMask");
            var pm = FindInScene<ProfileManager>(root.gameObject);
            if (avatarMask != null && pm != null)
            {
                var am = FindOrCreate(avatarMask.transform, "Model0926");
                am.transform.SetAsLastSibling();
                pm.avatarModel = PeModelView(am);
                EditorUtility.SetDirty(pm);
            }
            else log.Add("프로필 편집: 프로필 화면 사진 칸 없음");

            sheet.SetActive(false);
            log.Add("profile edit ok");
        }

        // 사진 칸
        private static Image PeAvatarRow(Transform content, R0926ProfileEdit logic)
        {
            var mask = PeCircleRow(content, "Avatar0926", logic.PickPhoto, "r0926_i_cam",
                new[] { "사진 바꾸기", "Change photo", "写真を変更", "更换头像", "Cambiar foto" },
                new[] { "사진첩에서 골라 정사각형으로 잘라요", "Pick from your photos and crop it square", "写真から選んで正方形に切り抜きます", "从相册选择并裁剪为正方形", "Elige de tus fotos y recórtala en cuadrado" }, out _);
            mask.transform.parent.SetAsFirstSibling();
            var ph = FindOrCreate(mask.transform, "Photo0926");
            Stretch(RT(ph));
            var photo = Img(ph, null, new Color(1, 1, 1, 0.08f), Image.Type.Simple);
            photo.raycastTarget = false; photo.preserveAspect = false;
            return photo;
        }

        // 3D 모델 칸 — 없으면 원 안에 '3D', 있으면 모델이 돈다. 글은 런타임이 모델이 있는지 따라 바꾼다
        private static Transform PeModelRow(Transform content, R0926ProfileEdit logic, out Text label, out R0926ProfileModelView view)
        {
            var mask = PeCircleRow(content, "Model0926", logic.PickModel, "r0926_i_plus", null,
                new[] { "GLB 파일 60MB까지 · 앱에서 바로 보이도록 가볍게 줄여요", "GLB files up to 60 MB · made lighter so it shows right away", "GLBファイル60MBまで · すぐ表示できるよう軽くします", "GLB 文件，最大 60MB · 自动压缩以便立即显示", "Archivos GLB de hasta 60 MB · se aligeran para verse al instante" }, out label);
            var empty = FindOrCreate(mask.transform, "Empty0926");
            Stretch(RT(empty));
            Img(empty, null, new Color(1, 1, 1, 0.08f), Image.Type.Simple).raycastTarget = false;
            var mark = FindOrCreate(empty.transform, "Mark0926");
            Stretch(RT(mark));
            Txt(mark, "3D", 72, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            var pv = FindOrCreate(mask.transform, "Preview0926");
            pv.transform.SetAsLastSibling();
            view = PeModelView(pv);
            return mask.transform.parent;
        }

        // 둥근 칸 위 3D 그림 — 모델이 뜨기 전엔 꺼 둔다(밑의 사진이 보인다)
        private static R0926ProfileModelView PeModelView(GameObject go)
        {
            Stretch(RT(go));
            var raw = Ensure<RawImage>(go);
            raw.raycastTarget = false;
            raw.enabled = false;
            var view = Ensure<R0926ProfileModelView>(go);
            var so = new SerializedObject(view);
            so.FindProperty("view").objectReferenceValue = raw;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // 사진/3D 고르기 + 지우기 — 모델이 있을 때만 보인다
        private static GameObject PeDisplayGroup(Transform content, R0926ProfileEdit logic, out Image[] tabs, out Text[] labels, out Text removeLabel)
        {
            const float segH = 110f;
            var g = PeGroup(content, "F_display", new[] { "다른 사람에게 보이는 모습", "How others see you", "他の人への見え方", "别人看到的样子", "Cómo te ven los demás" });
            var seg = FindOrCreate(g, "Seg0926");
            Ensure<LayoutElement>(seg).preferredHeight = segH;
            Img(seg, Spr("r0926_pill"), new Color(1, 1, 1, 0.06f), Image.Type.Sliced, 64f / (segH / 2f)).raycastTarget = true;
            string[][] words =
            {
                new[] { "사진", "Photo", "写真", "照片", "Foto" },
                new[] { "3D 모델", "3D model", "3Dモデル", "3D模型", "Modelo 3D" },
            };
            tabs = new Image[2];
            labels = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                var tab = FindOrCreate(seg.transform, i == 0 ? "TabPhoto" : "TabModel");
                tabs[i] = Img(tab, Spr("r0926_pill"), new Color(1, 1, 1, 0f), Image.Type.Sliced, 64f / (segH / 2f - 6f));
                tabs[i].raycastTarget = true;
                var trt = RT(tab);
                trt.anchorMin = new Vector2(i / 2f, 0); trt.anchorMax = new Vector2((i + 1) / 2f, 1); trt.pivot = new Vector2(0.5f, 0.5f);
                trt.offsetMin = new Vector2(6, 6); trt.offsetMax = new Vector2(-6, -6);
                var lb = FindOrCreate(tab.transform, "Label0926");
                Stretch(RT(lb));
                labels[i] = Txt(lb, words[i][0], 44, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                Loc(lb, words[i][0], words[i][1], words[i][2], words[i][3], words[i][4]);
                var b = Ensure<Button>(tab);
                b.targetGraphic = tabs[i];
                b.transition = Selectable.Transition.None;
                ResetListeners(b);
                if (i == 0) UnityEventTools.AddPersistentListener(b.onClick, logic.ShowAsPhoto);
                else UnityEventTools.AddPersistentListener(b.onClick, logic.ShowAsModel);
            }

            var rm = FindOrCreate(g, "Remove0926");
            Ensure<LayoutElement>(rm).preferredHeight = 90;
            Img(rm, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
            var rmBtn = Ensure<Button>(rm);
            rmBtn.transition = Selectable.Transition.None;
            ResetListeners(rmBtn);
            UnityEventTools.AddPersistentListener(rmBtn.onClick, logic.RemoveModel);
            var rl = FindOrCreate(rm.transform, "Label0926");
            Stretch(RT(rl));
            removeLabel = Txt(rl, "3D 모델 지우기", 40, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);   // 글은 런타임이 바꾼다 (번역 부품을 달지 않는다)
            removeLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            var go = g.gameObject;
            go.SetActive(false);
            return go;
        }

        // 둥근 그림 + 분홍 테두리 + 표 · 오른쪽에 안내 두 줄. 칸 전체가 버튼 — 돌려주는 원 안에 그림을 넣는다.
        // title 이 null 이면 제목 글은 런타임이 넣는다 (번역 부품을 달지 않는다)
        private static GameObject PeCircleRow(Transform content, string name, UnityAction click, string icon, string[] title, string[] subWords, out Text titleText)
        {
            const float ms = 240f, cx = 150f;
            float ring = (ms / 2f - 4f) / 0.82f * 2f;   // 테두리 안쪽 가장자리(그림의 0.82)가 사진 가장자리를 4 덮게 (프로필 카드와 같은 셈)
            var row = FindOrCreate(content, name);
            Ensure<LayoutElement>(row).preferredHeight = 300;
            Img(row, null, new Color(1, 1, 1, 0), Image.Type.Simple).raycastTarget = true;
            var b = Ensure<Button>(row);
            b.transition = Selectable.Transition.None;
            ResetListeners(b);
            UnityEventTools.AddPersistentListener(b.onClick, click);

            var mask = FindOrCreate(row.transform, "Mask0926");
            SetRect(RT(mask), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, 0), new Vector2(ms, ms));
            Img(mask, Spr("r0926_circle"), Color.white, Image.Type.Simple).raycastTarget = false;
            Ensure<Mask>(mask).showMaskGraphic = false;

            var rg = FindOrCreate(row.transform, "Ring0926");
            SetRect(RT(rg), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx, 0), new Vector2(ring, ring));
            Img(rg, Spr("r0926_ring_big"), Pink, Image.Type.Simple).raycastTarget = false;

            var badge = FindOrCreate(row.transform, "Badge0926");
            SetRect(RT(badge), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(cx + ms * 0.36f, -ms * 0.36f), new Vector2(88, 88));
            Img(badge, Spr("r0926_circle"), Pink, Image.Type.Simple).raycastTarget = false;
            var bic = FindOrCreate(badge.transform, "Icon0926");
            SetRect(RT(bic), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
            Img(bic, Spr(icon), Dark, Image.Type.Simple).raycastTarget = false;

            var lb = FindOrCreate(row.transform, "Label0926");
            SetRect(RT(lb), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(cx + ring / 2f + 40, 6), new Vector2(-(cx + ring / 2f + 40), 70));
            titleText = Txt(lb, title != null ? title[0] : "", 50, Ink, TextAnchor.LowerLeft, FontStyle.Bold);
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (title != null) Loc(lb, title[0], title[1], title[2], title[3], title[4]);
            var sub = FindOrCreate(row.transform, "Sub0926");
            SetRect(RT(sub), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 1), new Vector2(cx + ring / 2f + 40, -6), new Vector2(-(cx + ring / 2f + 40), 110));
            var st = Txt(sub, subWords[0], 38, Muted, TextAnchor.UpperLeft, FontStyle.Normal);
            st.horizontalOverflow = HorizontalWrapMode.Wrap; st.verticalOverflow = VerticalWrapMode.Overflow;
            Loc(sub, subWords[0], subWords[1], subWords[2], subWords[3], subWords[4]);
            return mask;
        }

        // 칸 묶음 — 이름 한 줄 + 칸 (+ 안내)
        private static Transform PeGroup(Transform content, string name, string[] label)
        {
            var g = FindOrCreate(content, name);
            g.transform.SetAsLastSibling();
            var vl = Ensure<VerticalLayoutGroup>(g);
            vl.padding = new RectOffset(0, 0, 0, 0); vl.spacing = 14;
            vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            EditorUtility.SetDirty(vl);
            var lg = FindOrCreate(g.transform, "Label0926");
            lg.transform.SetAsFirstSibling();
            Ensure<LayoutElement>(lg).preferredHeight = 56;
            Txt(lg, label[0], 40, Muted, TextAnchor.MiddleLeft, FontStyle.Bold).horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc(lg, label[0], label[1], label[2], label[3], label[4]);
            return g.transform;
        }

        // 입력칸 — 키보드 위 입력줄로 쓴다(입력줄이 이 칸의 종류·줄·글자 수·키보드를 그대로 쓴다)
        private static InputField PeInput(Transform parent, string name, float h, string[] ph, bool multi, int limit, InputField.ContentType type, TouchScreenKeyboardType kb)
        {
            var g = FindOrCreate(parent, name);
            Ensure<LayoutElement>(g).preferredHeight = h;
            var img = Img(g, Spr("r0926_pill"), InputBg, Image.Type.Sliced, 64f / Mathf.Min(48f, h / 2f));
            img.raycastTarget = true;
            var f = Ensure<InputField>(g);
            TextAnchor a = multi ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            var tg = FindOrCreate(g.transform, "Text");
            tg.transform.SetSiblingIndex(0);
            Stretch(RT(tg)); RT(tg).offsetMin = new Vector2(36, 18); RT(tg).offsetMax = new Vector2(-36, -18);
            var t = Txt(tg, null, 46, Ink, a, FontStyle.Normal);
            t.supportRichText = false;
            t.horizontalOverflow = multi ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var pg = FindOrCreate(g.transform, "Placeholder");
            pg.transform.SetSiblingIndex(1);
            Stretch(RT(pg)); RT(pg).offsetMin = new Vector2(36, 18); RT(pg).offsetMax = new Vector2(-36, -18);
            var p = Txt(pg, ph[0], 42, Muted, a, FontStyle.Normal);
            p.horizontalOverflow = HorizontalWrapMode.Wrap;
            Loc(pg, ph[0], ph[1], ph[2], ph[3], ph[4]);
            f.textComponent = t; f.placeholder = p;
            f.contentType = type;
            f.lineType = multi ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            if (type == InputField.ContentType.Custom)
            {
                // 아이디·SNS 는 영문 키보드, 전화는 숫자 — 글자 거르기는 하지 않는다(예전 아이디에 대문자가 있어도 그대로 보이게)
                f.inputType = InputField.InputType.Standard;
                f.keyboardType = kb;
                f.characterValidation = InputField.CharacterValidation.None;
            }
            f.characterLimit = limit;
            f.targetGraphic = img;
            f.transition = Selectable.Transition.None;
            EditorUtility.SetDirty(f);
            return f;
        }
    }
}
