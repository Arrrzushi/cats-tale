using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Wardrobe: live 3D preview of the real cat on a turntable + Fur / Hats / Collars / Trails item grid.</summary>
    public partial class PawMenus
    {
        const int PreviewLayer = 31;
        Camera previewCam;
        RenderTexture previewRT;
        readonly Dictionary<GameObject, int> savedLayers = new Dictionary<GameObject, int>();
        Quaternion savedPetRot;
        bool previewing;
        PawDragSpin spin;
        Vector3 savedPetPos;
        string wardrobeSel;
        PawCosmetics.Slot wardrobeSlot = PawCosmetics.Slot.Fur;
        int wardrobePage;

        static readonly string[] SlotNames = { "Fur", "Hats", "Collars", "Trails", "Face" };
        static readonly string[] SlotIcons = { "fur_orange", "hat_blue", "col_bow", "trail_flower", "face_heart" };

        public void ShowWardrobe(Action back) => ShowWardrobe(back, wardrobeSlot);

        void ShowWardrobe(Action back, PawCosmetics.Slot slot)
        {
            SetPlaying(false);
            wardrobeSlot = slot;
            reopen = () => ShowWardrobe(back, wardrobeSlot);
            var s = NewScreen("Wardrobe").transform;
            var bg = Backdrop(s, "bg_wardrobe", -0.2f, 1.08f);
            Dim(s, 0.08f);
            StartPreview(s);

            // item panel (right)
            var panel = Card(s, "panel_plain", new Vector2(520, -40), new Vector2(840, 800));
            StartCoroutine(SlideIn(panel.rectTransform, new Vector2(900, 0), 0.05f));
            var p = panel.transform;
            PanelTitle(panel, "WARDROBE", 42);

            // slot tabs (left edge of the panel)
            for (int i = 0; i < SlotNames.Length; i++)
            {
                var sl = (PawCosmetics.Slot)i;
                bool on = sl == slot;
                var t = Img(p, on ? "btn_orange" : "btn_tan", new Vector2(-280, 222 - i * 98), 82);
                t.preserveAspect = false;
                t.rectTransform.sizeDelta = new Vector2(190, 82);
                Img(t.transform, SlotIcons[i], new Vector2(-58, 2), 58);
                Txt(t.transform, SlotNames[i], 26, on ? Cream : Ink, new Vector2(26, 2), new Vector2(120, 56), TextAnchor.MiddleCenter, false,
                    on ? (Color?)new Color(0.55f, 0.25f, 0.08f) : null).font = fontBold;
                Squish(t);
                t.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); wardrobeSel = null; wardrobePage = 0; ShowWardrobe(back, sl); });
            }

            // item grid
            var items = PawCosmetics.Catalog.FindAll(c => c.slot == slot);
            string equipped = PawCosmetics.EquippedId(slot);
            if (wardrobeSel == null || PawCosmetics.Get(wardrobeSel)?.slot != slot) wardrobeSel = equipped;
            // up to 9 items: 3 columns at full size; more: pages of 12 in a 4 x 3 grid with arrows
            bool dense = items.Count > 9;
            const int perPage = 12;
            int pages = dense ? (items.Count + perPage - 1) / perPage : 1;
            int selIdx = items.FindIndex(c => c.id == wardrobeSel);
            if (wardrobePage < 0 || wardrobePage >= pages) wardrobePage = dense && selIdx >= 0 ? selIdx / perPage : 0;
            var grid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(p, false);
            grid.localScale = Vector3.one * (dense ? 0.84f : 1f);
            if (pages > 1)
            {
                var lab = Txt(p, $"{wardrobePage + 1} / {pages}", 28, Ink, new Vector2(103, -198), new Vector2(160, 50), TextAnchor.MiddleCenter);
                lab.font = fontBold;
                for (int k = -1; k <= 1; k += 2)
                {
                    int dir = k;
                    var ar = Img(p, k < 0 ? "rb_arrow_left" : "rb_arrow", new Vector2(103 + k * 130, -198), 62, true);
                    ar.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        Click();
                        wardrobePage = (wardrobePage + dir + pages) % pages;
                        ShowWardrobe(back, slot);
                    });
                }
            }
            int first = dense ? wardrobePage * perPage : 0, last = dense ? Mathf.Min(items.Count, first + perPage) : items.Count;
            for (int i = first; i < last; i++)
            {
                var it = items[i];
                int cols = dense ? 4 : 3, col = (i - first) % cols, row = (i - first) / cols;
                var pos = dense ? new Vector2(-104 + col * 138, 214 - row * 142) / 0.84f : new Vector2(-50 + col * 158, 210 - row * 162);
                var tile = Img(grid, "tile_item", pos, 146);
                var ic = Img(tile.transform, it.icon == "tile_item" ? "x_light" : it.icon, new Vector2(0, 6), it.icon == "tile_item" ? 70 : 104);
                if (it.rarity != PawCosmetics.Rarity.Common)
                    Img(tile.transform, it.rarity == PawCosmetics.Rarity.Rare ? "frame_rare" : it.rarity == PawCosmetics.Rarity.Epic ? "frame_epic" : "frame_legend", Vector2.zero, 148);
                bool owned = PawSave.Owns(it.id);
                if (it.id == equipped) Img(tile.transform, "ic_check", new Vector2(48, -48), 46);
                else if (!owned)
                {
                    Img(tile.transform, "ic_lock", new Vector2(52, -46), 44);
                    ic.color = new Color(1f, 1f, 1f, 0.75f);
                    string price = it.coins > 0 ? it.coins.ToString() : it.fish > 0 ? it.fish.ToString() : "Pass";
                    var pr = Img(tile.transform, "btn_tan", new Vector2(-10, -60), 34);
                    pr.preserveAspect = false;
                    pr.rectTransform.sizeDelta = new Vector2(96, 34);
                    if (it.coins > 0 || it.fish > 0) Img(pr.transform, it.coins > 0 ? "ic_paw_coin" : "ic_fish_cookie", new Vector2(-34, 1), 30);
                    Txt(pr.transform, price, 20, Ink, new Vector2(it.coins > 0 || it.fish > 0 ? 12 : 0, 1), new Vector2(70, 30), TextAnchor.MiddleCenter).font = fontBold;
                }
                if (PawSave.IsNew(it.id)) Img(tile.transform, "tag_new", new Vector2(40, 60), 34);
                if (it.id == wardrobeSel)
                {
                    var ol = tile.gameObject.AddComponent<Outline>();
                    ol.effectColor = new Color(1f, 0.7f, 0.2f);
                    ol.effectDistance = new Vector2(5, -5);
                    tile.rectTransform.localScale = Vector3.one * 1.06f;
                }
                Squish(tile);
                tile.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Click();
                    wardrobeSel = it.id;
                    PawSave.Seen(it.id);
                    PreviewItem(it);
                    ShowWardrobe(back, slot);
                });
                StartCoroutine(PopIn(tile.rectTransform, 0.1f + (i - first) * 0.04f, 0.3f));
            }

            // selected item: name + action
            var sel = PawCosmetics.Get(wardrobeSel) ?? PawCosmetics.Get(equipped);
            var infoY = -315f;
            Txt(p, sel.name, 32, Ink, new Vector2(-150, infoY), new Vector2(380, 50), TextAnchor.MiddleLeft).font = fontBold;
            Txt(p, sel.rarity.ToString(), 22, sel.rarity == PawCosmetics.Rarity.Common ? InkSoft : sel.rarity == PawCosmetics.Rarity.Rare ? new Color(0.25f, 0.5f, 0.85f)
                : sel.rarity == PawCosmetics.Rarity.Epic ? new Color(0.55f, 0.3f, 0.8f) : new Color(0.85f, 0.55f, 0.1f), new Vector2(-150, infoY - 34), new Vector2(380, 30), TextAnchor.MiddleLeft);
            if (sel.id == equipped)
                TextButton(p, "btn_tan", "EQUIPPED", new Vector2(215, infoY - 10), 90, 300, Ink);
            else if (PawSave.Owns(sel.id))
            {
                var eq = TextButton(p, "btn_orange", "EQUIP", new Vector2(215, infoY - 10), 90, 300);
                Anim(eq, PawUIAnim.Mode.Pulse, 0.5f);
                eq.onClick.AddListener(() => { PawSave.Equip(sel.slot.ToString(), sel.id); PawCosmetics.Instance?.ClearPreview(); PawAudio.Instance?.Success(); Toast($"{sel.name} equipped!"); ShowWardrobe(back, slot); });
            }
            else if (sel.coins > 0 || sel.fish > 0)
            {
                var buy = PriceButton(p, new Vector2(215, infoY - 10), sel.coins, sel.fish, null, 300, 90);
                buy.onClick.AddListener(() =>
                {
                    if (!Afford(sel.coins, sel.fish)) { Toast(sel.coins > 0 ? "Not enough Paw Coins" : "Not enough Golden Fish"); return; }
                    Pay(sel.coins, sel.fish);
                    PawSave.Own(sel.id);
                    PawSave.Seen(sel.id);
                    PawSave.Equip(sel.slot.ToString(), sel.id);
                    PawCosmetics.Instance?.ClearPreview();
                    PawAudio.Instance?.Success();
                    Toast($"{sel.name} unlocked and equipped!");
                    ShowWardrobe(back, slot);
                });
            }
            else
            {
                var pass = TextButton(p, "btn_teal", "PAW PASS", new Vector2(215, infoY - 10), 90, 300);
                pass.onClick.AddListener(() => { Click(); ShowPass(() => ShowWardrobe(back, slot)); });
            }

            CurrencyBar(s, 0.1f);
            CloseButton(s, new Vector2(0f, 1f), new Vector2(80, -80), () => { StopPreview(); back(); });
        }

        // ------------------------------------------------------------------ live preview
        void PreviewItem(PawCosmetics.Item it) => PawCosmetics.Instance?.SetPreview(it.slot, it.id);

        void StartPreview(Transform s)
        {
            if (mover == null) return;
            var pet = mover.transform;
            if (!previewing)
            {
                previewing = true;
                savedPetRot = pet.rotation;
                savedPetPos = pet.position;
                savedLayers.Clear();
                foreach (var r in pet.GetComponentsInChildren<Renderer>(true)) { savedLayers[r.gameObject] = r.gameObject.layer; r.gameObject.layer = PreviewLayer; }
                previewRT = new RenderTexture(1100, 840, 24, RenderTextureFormat.ARGB32) { name = "WardrobePreview", antiAliasing = 4 };
                var go = new GameObject("PreviewCam");
                previewCam = go.AddComponent<Camera>();
                previewCam.clearFlags = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = new Color(0, 0, 0, 0);
                previewCam.cullingMask = 1 << PreviewLayer;
                previewCam.fieldOfView = 24f;
                previewCam.nearClipPlane = 0.05f;
                previewCam.targetTexture = previewRT;
                var d = previewCam.GetUniversalAdditionalCameraData();
                d.renderPostProcessing = false;
                d.renderShadows = false;
                StartCoroutine(Turntable());
            }
            if (wardrobeSel != null && PawCosmetics.Get(wardrobeSel) != null) PreviewItem(PawCosmetics.Get(wardrobeSel));
            // a cat-head "mirror" over the pedestal: cream glow + the live 3D cat inside the window
            var mirror = new GameObject("Mirror", typeof(RectTransform)).GetComponent<RectTransform>();
            mirror.SetParent(s, false);
            mirror.anchoredPosition = new Vector2(-300, -95);
            mirror.sizeDelta = new Vector2(940, 730);
            var win = Img(mirror, "map_mask", Vector2.zero, 636);
            win.preserveAspect = false; Stretch(win.rectTransform);
            win.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            win.color = new Color(1f, 0.95f, 0.86f);
            var glow = Img(win.transform, "medal_paw", new Vector2(0, -10), 620);
            glow.color = new Color(1f, 0.85f, 0.6f, 0.22f);
            Anim(glow, PawUIAnim.Mode.Spin, 0.2f);
            var view = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            view.transform.SetParent(win.transform, false);
            view.texture = previewRT;
            view.raycastTarget = true;                       // drag on the preview to spin her all the way round
            spin = view.gameObject.AddComponent<PawDragSpin>();
            view.rectTransform.anchoredPosition = new Vector2(0, -60);
            view.rectTransform.sizeDelta = new Vector2(820, 626);
            var frame = Img(mirror, "map_frame", Vector2.zero, 636);
            frame.preserveAspect = false; Stretch(frame.rectTransform);
            var tag = Img(mirror, "btn_tan", new Vector2(0, -345), 60);
            tag.preserveAspect = false; tag.rectTransform.sizeDelta = new Vector2(240, 60);
            Txt(tag.transform, "Preview", 28, Ink, new Vector2(0, 2), new Vector2(220, 50), TextAnchor.MiddleCenter).font = fontBold;
            StartCoroutine(PopIn(mirror, 0.1f, 0.5f));
        }

        IEnumerator Turntable()
        {
            var pet = mover.transform;
            var anim = pet.GetComponentInChildren<Animator>();
            Transform head = null;
            foreach (var t in pet.GetComponentsInChildren<Transform>()) if (t.name == "Head") { head = t; break; }
            float a = 0f, zoom = 0f, lap = 0f, walkK = 0f;
            Vector3 front = savedPetRot * Vector3.forward;
            while (previewing && previewCam && current != null && current.name == "Wardrobe")
            {
                float dt = Time.unscaledDeltaTime;
                // Hats and Face are small: ease the camera in towards the head; other tabs show the whole cat
                bool close = head && (wardrobeSlot == PawCosmetics.Slot.Hat || wardrobeSlot == PawCosmetics.Slot.Face);
                zoom = Mathf.MoveTowards(zoom, close ? 1f : 0f, dt * 2.5f);
                float k = Mathf.SmoothStep(0f, 1f, zoom);

                // Trails tab: she trots a little circle on the pedestal so the trail draws behind her
                bool trot = wardrobeSlot == PawCosmetics.Slot.Trail;
                walkK = Mathf.MoveTowards(walkK, trot ? 1f : 0f, dt * 2f);
                lap += dt * 1.6f * walkK;
                const float R = 0.55f;
                Vector3 circle = new Vector3(Mathf.Sin(lap), 0f, Mathf.Cos(lap) - 1f) * R;
                pet.position = savedPetPos + savedPetRot * circle * walkK;
                if (anim) { anim.SetFloat("Speed", 1.2f * walkK); anim.SetBool("Grounded", true); anim.SetFloat("AnimSpeed", 1f); }

                // turning: your drag wins; when idle for 2 s she drifts back to a gentle sway
                bool user = spin && (spin.dragging || Time.unscaledTime - spin.lastTouch < 2f);
                if (!user && spin) spin.yaw = Mathf.MoveTowardsAngle(spin.yaw, 0f, dt * 60f);
                a += dt * 25f;
                float sway = user ? 0f : Mathf.Lerp(28f, wardrobeSlot == PawCosmetics.Slot.Face ? 10f : 22f, k) * (1f - walkK);
                float bias = Mathf.Lerp(-20f, -6f, k) * (1f - walkK);
                float yaw = (spin ? spin.yaw : 0f) + Mathf.Sin(a * Mathf.Deg2Rad) * sway + bias;
                Quaternion face = trot ? Quaternion.LookRotation(savedPetRot * new Vector3(Mathf.Cos(lap), 0f, -Mathf.Sin(lap))) : savedPetRot;
                pet.rotation = Quaternion.Slerp(savedPetRot, face, walkK) * Quaternion.Euler(0, walkK > 0.5f ? 0f : yaw, 0);

                // camera: whole cat (pulled back a little on Trails to fit the circle), or a head shot that still shows
                // the whole face (Face) and room above the ears (Hats)
                Vector3 centre = savedPetPos + (savedPetRot * new Vector3(0, 0, -R)) * walkK;
                float far = Mathf.Lerp(3.4f, 4.3f, walkK);
                Vector3 farPos = centre + front * far + Vector3.up * (0.9f + 0.6f * walkK), farLook = centre + Vector3.up * 0.36f;
                bool hatTab = wardrobeSlot == PawCosmetics.Slot.Hat;
                Vector3 focus = head ? head.position + Vector3.up * (hatTab ? 0.3f : 0.02f) : farLook;
                Vector3 nearPos = focus + front * (hatTab ? 2.1f : 2.25f) + Vector3.up * (hatTab ? 0.25f : 0.12f);
                previewCam.transform.position = Vector3.Lerp(farPos, nearPos, k);
                previewCam.transform.LookAt(Vector3.Lerp(farLook, focus, k));
                yield return null;
            }
            StopPreview();
        }

        void StopPreview()
        {
            if (!previewing) return;
            previewing = false;
            if (mover)
            {
                mover.transform.rotation = savedPetRot;
                mover.transform.position = savedPetPos;
                var an = mover.GetComponentInChildren<Animator>();
                if (an) an.SetFloat("Speed", 0f);
                foreach (var kv in savedLayers) if (kv.Key) kv.Key.layer = kv.Value;
            }
            savedLayers.Clear();
            if (previewCam) Destroy(previewCam.gameObject);
            if (previewRT) { previewRT.Release(); Destroy(previewRT); }
            PawCosmetics.Instance?.ClearPreview();   // back to what is actually equipped
        }
    }
}
