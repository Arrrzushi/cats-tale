using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Daily Treats + Today's Missions, Pause + Settings, Loading, Full Map.</summary>
    public partial class PawMenus
    {
        // ------------------------------------------------------------------ daily + missions
        static readonly Reward[][] DailyRewards =
        {
            new[] { Reward.Coins(50) }, new[] { Reward.Treat(TreatKind.Fish, 2) }, new[] { Reward.Coins(100) }, new[] { Reward.Treat(TreatKind.Can, 1) },
            new[] { Reward.Fish(3) }, new[] { Reward.Coins(200) }, new[] { Reward.Fish(10), Reward.Cosmetic("hat_green") }
        };
        static readonly string[] MissionText = { "Deliver 3 orders", "Collect 10 treats", "Cross 5 zebra crossings safely" };
        static readonly string[] MissionIcon = { "it_catbox", "it_fish", "rb_pin" };
        static readonly Reward[] MissionReward = { Reward.Coins(100), Reward.Fish(2), Reward.Coins(80) };

        public void ShowDaily(Action back)
        {
            SetPlaying(false);
            reopen = () => ShowDaily(back);
            var s = NewScreen("Daily").transform;
            var bg = Backdrop(s, "bg_town", 0f, 1f, false, true);
            bg.color = new Color(0.85f, 0.85f, 0.85f);
            PawSave.RefreshStreak();
            int day = PawSave.DailyDay;                 // days already claimed
            bool canClaim = PawSave.DailyClaimableToday;

            // left: daily treats
            var dp = Card(s, "panel_plain", new Vector2(-445, -20), new Vector2(850, 860));
            StartCoroutine(SlideIn(dp.rectTransform, new Vector2(-900, 0), 0.05f));
            var d = dp.transform;
            Img(d, "cat_face", new Vector2(-250, 280), 76);
            Txt(d, "DAILY TREATS", 40, Ink, new Vector2(10, 292), new Vector2(400, 54), TextAnchor.MiddleCenter).font = fontBold;
            Txt(d, "Log in for 7 days!", 24, InkSoft, new Vector2(10, 252), new Vector2(400, 34), TextAnchor.MiddleCenter);
            for (int i = 0; i < 7; i++)
            {
                bool big = i == 6;
                int col = i % 3, row = i / 3;
                var pos = big ? new Vector2(0, -290) : new Vector2(-240 + col * 240, 120 - row * 200);
                var tile = Card(d, "card", pos, big ? new Vector2(660, 160) : new Vector2(196, 180));
                var r = DailyRewards[i];
                bool claimed = i < day;
                bool today = i == day && canClaim;
                var t = tile.transform;
                if (big)
                {
                    var gift = Img(t, "gift_box", new Vector2(-200, 8), 140);
                    Anim(gift, PawUIAnim.Mode.Wiggle, 1.2f);
                    Txt(t, "Day 7", 34, Ink, new Vector2(60, 38), new Vector2(300, 44), TextAnchor.MiddleCenter).font = fontBold;
                    Txt(t, "10 Golden Fish + Forest Cap", 24, InkSoft, new Vector2(60, -6), new Vector2(420, 34), TextAnchor.MiddleCenter);
                }
                else
                {
                    Img(t, r[0].Icon, new Vector2(0, 30), 92);
                    Txt(t, r[0].Short, 24, Ink, new Vector2(52, -14), new Vector2(76, 30), TextAnchor.MiddleCenter).font = fontBold;
                    if (!(i == day && canClaim)) Txt(t, $"Day {i + 1}", 26, InkSoft, new Vector2(0, -60), new Vector2(170, 34), TextAnchor.MiddleCenter).font = fontBold;
                }
                if (claimed) { Img(t, "ic_check", big ? new Vector2(300, 40) : new Vector2(65, 62), 56); tile.color = new Color(0.86f, 0.95f, 0.84f); }
                else if (today)
                {
                    
                    int di = i;
                    var claim = TextButton(t, "btn_teal", "CLAIM", big ? new Vector2(60, -52) : new Vector2(0, -62), 52, 150, null, 26);
                    claim.onClick.AddListener(() =>
                    {
                        string msg = "";
                        foreach (var rw in DailyRewards[di]) msg = Grant(rw);
                        PawSave.DailyClaim();
                        PawAudio.Instance?.Success();
                        StartCoroutine(Confetti(root, 0f, 40));
                        Toast(msg);
                        ShowDaily(back);
                    });
                }
                else tile.color = new Color(0.93f, 0.9f, 0.86f);
                StartCoroutine(PopIn(tile.rectTransform, 0.15f + i * 0.05f, 0.3f));
            }

            // right: missions
            var mp = Card(s, "panel_plain", new Vector2(470, -20), new Vector2(850, 860));
            StartCoroutine(SlideIn(mp.rectTransform, new Vector2(900, 0), 0.1f));
            var m = mp.transform;
            Img(m, "ic_book", new Vector2(-270, 285), 70);
            Txt(m, "TODAY'S MISSIONS", 40, Ink, new Vector2(20, 290), new Vector2(480, 54), TextAnchor.MiddleCenter).font = fontBold;
            for (int i = 0; i < 3; i++)
            {
                string id = PawSave.MissionIds[i];
                int prog = PawSave.Mission(id), goal = PawSave.MissionGoal[i];
                bool done = prog >= goal, claimed = PawSave.MissionClaimed(id);
                var row = Card(m, "card", new Vector2(0, 140 - i * 192), new Vector2(720, 166));
                var rt = row.transform;
                // icon in a fixed 96 box on the left; title, bar and reward all start on one left edge after a clear gap
                Fit(Img(rt, MissionIcon[i], new Vector2(-288, 0), 96), 96, 96);
                const float left = -212f;
                Txt(rt, MissionText[i], 28, Ink, new Vector2(left + 215, 42), new Vector2(430, 40), TextAnchor.MiddleLeft).font = fontBold;
                var bar = Bar(rt, new Vector2(left + 165, -8), new Vector2(330, 28), 0f);
                StartCoroutine(FillTo(bar, (float)prog / goal, 0.4f + i * 0.1f, 0.6f));
                Txt(rt, $"{prog}/{goal}", 28, Ink, new Vector2(left + 385, -8), new Vector2(90, 36), TextAnchor.MiddleLeft).font = fontBold;
                var rw = MissionReward[i];
                Fit(Img(rt, rw.Icon, new Vector2(left + 18, -50), 36), 36, 36);
                Txt(rt, "Reward " + rw.Short, 22, InkSoft, new Vector2(left + 46 + 100, -50), new Vector2(200, 30), TextAnchor.MiddleLeft);
                if (claimed) Img(rt, "ic_check", new Vector2(288, 0), 66);
                else if (done)
                {
                    var claim = TextButton(rt, "btn_teal", "CLAIM", new Vector2(285, -8), 60, 118, null, 25);
                    Anim(claim, PawUIAnim.Mode.Pulse, 1f);
                    claim.onClick.AddListener(() =>
                    {
                        PawSave.MissionClaim(id);
                        PawAudio.Instance?.Success();
                        Toast(Grant(rw));
                        ShowDaily(back);
                    });
                }
                StartCoroutine(PopIn(row.rectTransform, 0.25f + i * 0.08f, 0.35f));
            }
            var reset = Txt(m, "", 26, InkSoft, new Vector2(0, -365), new Vector2(600, 36), TextAnchor.MiddleCenter);
            StartCoroutine(Tick(reset, () => "Resets in " + Countdown(DateTime.Today.AddDays(1) - DateTime.Now)));

            CloseButton(s, new Vector2(1f, 1f), new Vector2(-70, -70), back);
        }

        // ------------------------------------------------------------------ pause + settings
        /// <summary>Settings > Edit Controls: show the play HUD under a drag-to-arrange layer; DONE goes back to Settings.</summary>
        void EditControls()
        {
            if (!hud) return;
            Clear();
            canvas.enabled = false;
            hud.enabled = true;
            if (mover) mover.enabled = false;
            PawHudEditor.Open(hud, fontBold, () => ShowPause());
        }

        public void ShowPause()
        {
            SetPlaying(false);
            reopen = ShowPause;
            var s = NewScreen("Pause").transform;
            Dim(s, 0.55f);

            var lp = Card(s, "panel_plain", new Vector2(-560, -20), new Vector2(600, 800));
            StartCoroutine(SlideIn(lp.rectTransform, new Vector2(-800, 0), 0f));
            var l = lp.transform;
            Img(l, "paw_mid", new Vector2(-185, 245), 56);
            Img(l, "paw_mid", new Vector2(185, 245), 56);
            Txt(l, "PAUSED", 60, Ink, new Vector2(0, 245), new Vector2(300, 80), TextAnchor.MiddleCenter).font = fontBold;
            var resume = TextButton(l, "btn_teal", "RESUME", new Vector2(0, 115), 110, 440);
            Img(resume.transform, "ic_play", new Vector2(-150, 3), 60);
            Anim(resume, PawUIAnim.Mode.Pulse, 0.5f);
            resume.onClick.AddListener(() => { Click(); Resume(); });
            var home = TextButton(l, "btn_tan", "HOME", new Vector2(0, -12), 100, 440, Ink);
            Img(home.transform, "rb_home", new Vector2(-150, 2), 64);
            home.onClick.AddListener(() => { Click(); ConfirmLeave(); });
            var cat = Img(l, "pillow_cat", new Vector2(0, -250), 210);
            

            var rp = Card(s, "panel_plain", new Vector2(330, -20), new Vector2(1040, 800));
            StartCoroutine(SlideIn(rp.rectTransform, new Vector2(900, 0), 0.05f));
            var r = rp.transform;
            Txt(r, "SETTINGS", 42, Ink, new Vector2(0, 322), new Vector2(400, 56), TextAnchor.MiddleCenter).font = fontBold;
            // move / resize every on-screen control
            var edit = TextButton(r, "btn_teal", "EDIT CONTROLS", new Vector2(330, 322), 66, 270, null, 24);
            edit.onClick.AddListener(() => { Click(); EditControls(); });
            float y = 235;
            SettingSlider(r, "ic_music", "Music", y, PawSave.MusicVol, v => { PawSave.MusicVol = v; ApplySettings(); }); y -= 83;
            SettingSlider(r, "ic_sound", "Sound", y, PawSave.SoundVol, v => { PawSave.SoundVol = v; ApplySettings(); }); y -= 83;
            SettingToggle(r, "ic_vibrate", "Vibration", y, PawSave.Vibration, v => { PawSave.Vibration = v; if (v) Haptic(); }); y -= 83;
            SettingToggle(r, "map_heading", "Arrow Guide", y, PawSave.ArrowGuide, v => { PawSave.ArrowGuide = v; }); y -= 83;
            var cam = FindAnyObjectByType<PawTownFollowCamera>();
            SettingChoice(r, "ic_camera", "Camera", y, new[] { "Cat View", "Top View" }, cam != null && cam.mode == PawTownFollowCamera.Mode.TopView ? 1 : 0,
                i => { if (cam) cam.SetMode(i == 0 ? PawTownFollowCamera.Mode.CatView : PawTownFollowCamera.Mode.TopView); }); y -= 83;
            SettingSlider(r, "paw_mid", "Joystick Size", y, Mathf.InverseLerp(0.7f, 1.4f, PawSave.JoystickSize), v => { PawSave.JoystickSize = Mathf.Lerp(0.7f, 1.4f, v); ApplySettings(); }); y -= 83;
            SettingChoice(r, "ic_quality", "Graphics", y, new[] { "Low", "Medium", "High" }, PawSave.Quality, i => { PawSave.Quality = i; ApplySettings(); });

            string[] links = { "Help", "Privacy", "Restore Purchases" };
            string[] li = { "ic_help", "ic_shield", "ic_restore" };
            float[] lw = { 190, 210, 340 };
            float lx = -380;
            for (int i = 0; i < 3; i++)
            {
                var b = TextButton(r, "btn_tan", "", new Vector2(lx + lw[i] * 0.5f, -330), 70, lw[i], Ink);
                Img(b.transform, li[i], new Vector2(-lw[i] * 0.5f + 40, 2), 40);
                Txt(b.transform, links[i], 24, Ink, new Vector2(20, 2), new Vector2(lw[i] - 60, 60), TextAnchor.MiddleCenter).font = fontBold;
                int k = i;
                b.onClick.AddListener(() =>
                {
                    Click();
                    if (k == 0) Info("How to play", "Drag on the left to walk, push to the edge to sprint.\nTap the ground to walk there, tap the quest card to auto-walk.\nPick up at PAW-LIVERY HQ, deliver to the door, cross at zebras!");
                    else if (k == 1) Info("Privacy", "Your progress is saved only on this device.\nNo personal data is collected.");
                    else Toast("Purchases restored (test store)");
                });
                lx += lw[i] + 22;
            }
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-70, -70), Resume);
        }

        void Info(string title, string body)
        {
            var p = Popup("Info", out var panel, 520);
            Txt(panel, title, 46, Ink, new Vector2(0, 150), new Vector2(700, 60), TextAnchor.MiddleCenter).font = fontBold;
            Txt(panel, body, 28, InkSoft, new Vector2(0, 10), new Vector2(680, 200), TextAnchor.MiddleCenter);
            var ok = TextButton(panel, "btn_orange", "OK", new Vector2(0, -160), 96, 240);
            ok.onClick.AddListener(() => { Click(); Destroy(p); });
        }

        static void Haptic()
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }

        Transform SettingRow(Transform r, string icon, string label, float y)
        {
            var row = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(r, false);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(880, 80);
            Img(row, icon, new Vector2(-400, 0), 50);
            Txt(row, label, 30, Ink, new Vector2(-245, 0), new Vector2(240, 50), TextAnchor.MiddleLeft).font = fontBold;
            return row;
        }

        void SettingSlider(Transform r, string icon, string label, float y, float value, Action<float> onChange)
        {
            var row = SettingRow(r, icon, label, y);
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(row, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = new Vector2(190, 0);
            rt.sizeDelta = new Vector2(440, 40);
            var track = Img(rt, "btn_tan", Vector2.zero, 34);
            track.preserveAspect = false;
            Stretch(track.rectTransform, 2f);
            track.color = new Color(0.8f, 0.7f, 0.58f);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            Stretch(fillArea, 6f);
            var fill = Img(fillArea, "btn_teal", Vector2.zero, 28);
            fill.preserveAspect = false;
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rt, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(20, 0); handleArea.offsetMax = new Vector2(-20, 0);
            var handle = Img(handleArea, "knob_paw", Vector2.zero, 64);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(64, 64);
            var sl = go.GetComponent<Slider>();
            sl.fillRect = fill.rectTransform;
            sl.handleRect = handle.rectTransform;
            sl.targetGraphic = handle;
            sl.transition = Selectable.Transition.None;
            sl.value = Mathf.Clamp01(value);
            sl.onValueChanged.AddListener(v => onChange(v));
        }

        void SettingToggle(Transform r, string icon, string label, float y, bool value, Action<bool> onChange)
        {
            var row = SettingRow(r, icon, label, y);
            bool v = value;
            var t = Img(row, v ? "toggle_on" : "toggle_off", new Vector2(340, 0), 64, true);
            t.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                v = !v;
                t.sprite = S(v ? "toggle_on" : "toggle_off");
                Click();
                onChange(v);
            });
        }

        void SettingChoice(Transform r, string icon, string label, float y, string[] options, int value, Action<int> onChange)
        {
            var row = SettingRow(r, icon, label, y);
            float w = 440f / options.Length;
            var imgs = new Image[options.Length];
            var txts = new Text[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                var b = Img(row, i == value ? "btn_teal" : "btn_tan", new Vector2(-30 + w * (i + 0.5f), 0), 56);
                b.preserveAspect = false;
                b.rectTransform.sizeDelta = new Vector2(w - 8, 56);
                imgs[i] = b;
                txts[i] = Txt(b.transform, options[i], 24, i == value ? Cream : Ink, new Vector2(0, 2), new Vector2(w - 12, 50), TextAnchor.MiddleCenter);
                txts[i].font = fontBold;
                Squish(b);
                b.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Click();
                    for (int j = 0; j < imgs.Length; j++) { imgs[j].sprite = S(j == k ? "btn_teal" : "btn_tan"); txts[j].color = j == k ? Cream : Ink; }
                    onChange(k);
                });
            }
        }

        // ------------------------------------------------------------------ loading
        static readonly string[] Tips =
        {
            "Tip: tap the order card to auto-walk there",
            "Tip: push the stick to the edge to sprint",
            "Tip: cross at zebra crossings to finish missions",
            "Tip: treats in your bag restore energy",
            "Tip: tap the ground to walk there by yourself",
        };

        public void ShowLoading(Action then)
        {
            SetPlaying(false);
            var s = NewScreen("Loading").transform;
            Backdrop(s, "bg_town", 0f, 1.05f, false, true);
            var road = Img(s, "btn_tan", Vector2.zero, 60);
            road.preserveAspect = false;
            road.color = new Color(0.45f, 0.42f, 0.4f, 0.0f);
            var cat = Img(s, "cat_run_box", Vector2.zero, 240);
            Anchor(cat.rectTransform, 0f, 0f, new Vector2(-200, 330));
            Anim(cat, PawUIAnim.Mode.Bob, 2f);

            var tipCard = Card(s, "panel_plain", Vector2.zero, new Vector2(460, 320));
            Anchor(tipCard.rectTransform, 1f, 0.6f, new Vector2(-330, 0));
            Img(tipCard.transform, "ic_star", new Vector2(0, 80), 50);
            var tip = Txt(tipCard.transform, Tips[UnityEngine.Random.Range(0, Tips.Length)], 30, Ink, new Vector2(0, -15), new Vector2(380, 140), TextAnchor.MiddleCenter);
            tip.font = fontBold;
            StartCoroutine(PopIn(tipCard.rectTransform, 0.2f, 0.45f));

            var barBg = Pill(s, "btn_tan", Vector2.zero, new Vector2(1000, 70));
            Anchor(barBg.rectTransform, 0.5f, 0f, new Vector2(0, 140));
            var fill = PawBarFill.Make(barBg.transform, S("btn_orange"), Color.white, 8f);
            fill.fillAmount = 0f;
            var pawL = Img(barBg.transform, "rb_paw", new Vector2(-545, 0), 110);
            var pawR = Img(barBg.transform, "paw_mid", new Vector2(555, 0), 70);
            
            var pct = Txt(barBg.transform, "0%", 30, Ink, new Vector2(0, 2), new Vector2(200, 60), TextAnchor.MiddleCenter);
            pct.font = fontBold;
            StartCoroutine(Loading(fill, pct, cat.rectTransform, tip, then));
        }

        IEnumerator Loading(Image fill, Text pct, RectTransform cat, Text tip, Action then)
        {
            var gen = FindAnyObjectByType<PawTownGenerator>();
            float t = 0f, minTime = 2.6f, maxTime = 7f, tipT = 0f;
            int tipI = 0;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                bool ready = gen == null || gen.transform.childCount > 40;
                float k = Mathf.Clamp01(t / minTime);
                if (!ready) k = Mathf.Min(k, 0.9f);
                if (fill) fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, k, Time.unscaledDeltaTime * 1.2f);
                if (pct) pct.text = $"{Mathf.RoundToInt((fill ? fill.fillAmount : k) * 100)}%";
                if (cat) cat.anchoredPosition = new Vector2(Mathf.Lerp(-200, root.rect.width + 200, (t % 4.5f) / 4.5f), 330);
                tipT += Time.unscaledDeltaTime;
                if (tipT > 2.2f && tip) { tipT = 0; tipI = (tipI + 1) % Tips.Length; tip.text = Tips[tipI]; }
                if ((fill && fill.fillAmount >= 0.999f && ready) || t > maxTime) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            then();
        }

        // ------------------------------------------------------------------ full map (uses the HUD's cat-head map, enlarged)
        bool mapOpen;
        Action mapBack;

        public void ShowMap(Action back)
        {
            var map = FindAnyObjectByType<PawMiniMap>(FindObjectsInactive.Include);
            if (map == null) { Toast("Map not available"); return; }
            SetPlaying(false);
            back ??= Resume;
            mapBack = back;
            reopen = () => ShowMap(back);
            var s = NewScreen("Map").transform;
            if (hud) hud.enabled = true;            // the map lives on the HUD canvas
            mapOpen = true;
            map.SetBig(true);
            HideHudForMap(map);

            var legend = Card(s, "panel_plain", Vector2.zero, new Vector2(270, 360));
            Anchor(legend.rectTransform, 1f, 0.5f, new Vector2(-150, -40));
            StartCoroutine(SlideIn(legend.rectTransform, new Vector2(400, 0), 0.2f));
            string[] names = { "Order", "Treats", "Shops" };
            string[] icons = { "it_catbox", "it_fish", "sq_shop" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                bool on = map.LayerOn(k);
                var chip = Card(legend.transform, on ? "card" : "card_well", new Vector2(0, 105 - i * 105), new Vector2(220, 86));
                Img(chip.transform, icons[i], new Vector2(-70, 2), 60);
                var t = Txt(chip.transform, names[i], 28, on ? Ink : InkSoft, new Vector2(25, 2), new Vector2(130, 50), TextAnchor.MiddleCenter);
                t.font = fontBold;
                Squish(chip);
                chip.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    Click();
                    bool v = !map.LayerOn(k);
                    map.SetLayer(k, v);
                    chip.sprite = S(v ? "card" : "card_well");
                    t.color = v ? Ink : InkSoft;
                });
            }
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-70, -70), CloseMap);
        }

        public void CloseMap()
        {
            if (!mapOpen) return;
            mapOpen = false;
            var map = FindAnyObjectByType<PawMiniMap>(FindObjectsInactive.Include);
            if (map) map.SetBig(false);
            RestoreHudAfterMap();
            var b = mapBack ?? Resume;
            mapBack = null;
            Clear();
            b();
        }

        public bool MapOpen => mapOpen;

        readonly List<GameObject> mapHidden = new List<GameObject>();
        GameObject mapDim;

        /// <summary>The big map lives on the HUD canvas: hide the rest of the HUD and dim the town behind it.</summary>
        void HideHudForMap(PawMiniMap map)
        {
            RestoreHudAfterMap();
            if (!hud) return;
            // every HUD widget next to the map (quest card, energy, joystick, buttons) goes away while it is big
            Transform widget = map.transform, parent = widget.parent;
            foreach (Transform c in parent)
                if (c != widget && c.gameObject.activeSelf && (map.backdrop == null || c.gameObject != map.backdrop))
                { c.gameObject.SetActive(false); mapHidden.Add(c.gameObject); }
            foreach (Transform c in hud.transform)
                if (!widget.IsChildOf(c) && c.gameObject.activeSelf) { c.gameObject.SetActive(false); mapHidden.Add(c.gameObject); }
            var tip = widget.Find("Tip");
            if (tip && tip.gameObject.activeSelf) { tip.gameObject.SetActive(false); mapHidden.Add(tip.gameObject); }
            mapDim = new GameObject("MapDim", typeof(RectTransform), typeof(Image));
            mapDim.transform.SetParent(parent, false);
            Stretch((RectTransform)mapDim.transform);
            mapDim.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.5f);
            mapDim.GetComponent<Image>().raycastTarget = false;
            mapDim.transform.SetSiblingIndex(widget.GetSiblingIndex());
        }

        void RestoreHudAfterMap()
        {
            foreach (var g in mapHidden) if (g) g.SetActive(true);
            mapHidden.Clear();
            if (mapDim) Destroy(mapDim);
            mapDim = null;
        }
    }
}
