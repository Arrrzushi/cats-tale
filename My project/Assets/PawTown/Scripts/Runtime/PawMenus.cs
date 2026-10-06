using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// The out-of-play screens, built at runtime from the Cat's Tale art (Resources/Screens) and animated:
    /// Title -> Main Menu (office) -> Order Board -> [play] -> Order Delivered -> (Level Up) -> Order Board ...
    /// All motion uses unscaled time. Text is real UI text (Fredoka), so it stays sharp at any resolution.
    /// </summary>
    public partial class PawMenus : MonoBehaviour
    {
        public static PawMenus Instance { get; private set; }
        public bool showTitleOnStart = true;

        // palette
        static readonly Color Ink = new Color32(0x4A, 0x33, 0x27, 0xFF);
        static readonly Color InkSoft = new Color32(0x7A, 0x5A, 0x44, 0xFF);
        static readonly Color Cream = new Color32(0xFF, 0xF6, 0xE6, 0xFF);
        static readonly Color Orange = new Color32(0xE8, 0x7A, 0x2C, 0xFF);
        static readonly Color TagRed = new Color32(0xD9, 0x4F, 0x45, 0xFF);

        Canvas canvas;
        RectTransform root;
        Font font, fontBold;
        Sprite white;
        PawOrders orders;
        PawGame game;
        PawTownDemoMover mover;
        Canvas hud;
        GameObject current;
        readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        List<PawOrders.Offer> offers;
        int selected;
        int pendingLevels;
        float orderAcceptedAt;

        // ------------------------------------------------------------------ lifecycle
        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator Start()
        {
            font = Resources.Load<Font>("Fonts/Fredoka-600");
            fontBold = Resources.Load<Font>("Fonts/Fredoka-700");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fontBold == null) fontBold = font;
            var tex = Texture2D.whiteTexture;
            white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            var go = new GameObject("Menus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<PawCanvasFit>().publishSafeArea = true;
            root = (RectTransform)go.transform;

            yield return null;   // let the other systems Awake / Start
            orders = PawOrders.Instance;
            game = PawGame.Instance;
            mover = FindAnyObjectByType<PawTownDemoMover>();
            if (mover) { petStart = mover.transform.position; petStartRot = mover.transform.rotation; petStartSet = true; }
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (c.name == "HUD") hud = c;
            if (hud && hud.GetComponent<CanvasScaler>() && !hud.GetComponent<PawCanvasFit>()) hud.gameObject.AddComponent<PawCanvasFit>();
            if (orders != null)
            {
                orders.waitForAccept = true;
                orders.Delivered_ += OnDelivered;
            }
            AddHudHomeButton();
            AddHudRunButton();
            if (hud) StartCoroutine(Later(0.1f, () => PawHudLayout.Apply(hud)));   // after the joystick has found its home
            if (game != null) game.Exhausted += () => { if (current == null) ShowSleepy(Resume); };
            if (mover != null)
            {
                if (mover.GetComponent<PawCosmetics>() == null) mover.gameObject.AddComponent<PawCosmetics>();
                if (FindAnyObjectByType<PawMissions>() == null) new GameObject("Missions").AddComponent<PawMissions>().pet = mover.transform;
            }
            var mm = FindAnyObjectByType<PawMiniMap>(FindObjectsInactive.Include);
            if (mm) mm.shopIcon = S("sq_shop");
            ApplySettings();
            if (showTitleOnStart) ShowLoading(ShowTitle); else SetPlaying(true);
        }

        void SetPlaying(bool playing)
        {
            AudioListener.pause = !playing;          // town sounds (traffic, ambience, steps) stop in menus; music keeps going
            if (mover) mover.enabled = playing;
            if (hud) hud.enabled = playing;
            canvas.enabled = !playing || current != null;
        }

        void Clear()
        {
            if (current) Destroy(current);
            current = null;
        }

        Action reopen;
        /// <summary>Re-open whatever full screen is showing (used as the "back" of popups opened from it).</summary>
        void Reopen() { if (reopen != null) reopen(); else ShowMenu(); }

        // A screen rebuilt in place (claiming, equipping, switching a tab) must not replay its entrance: everything
        // started while it is being built just snaps to where it ends. Only a real change of screen animates in.
        int calmFrame = -1;
        bool Calm => calmFrame == Time.frameCount;

        // full-screen menus paint over the whole town: stop rendering it behind them (big saving in menus)
        bool opaqueScreen;
        Camera mainCam;
        int savedMask; CameraClearFlags savedClear; bool townHidden;
        Vector3 petStart; Quaternion petStartRot; bool petStartSet;

        void LateUpdate()
        {
            if (!mainCam) { mainCam = Camera.main; if (!mainCam) return; }
            bool hide = current != null && opaqueScreen && !mapOpen;
            if (hide == townHidden) return;
            townHidden = hide;
            // draw nothing (just a clear) instead of switching the camera off: same saving, no "no cameras" screen
            if (hide) { savedMask = mainCam.cullingMask; savedClear = mainCam.clearFlags; mainCam.cullingMask = 0; mainCam.clearFlags = CameraClearFlags.SolidColor; }
            else { mainCam.cullingMask = savedMask; mainCam.clearFlags = savedClear; }
        }

        GameObject NewScreen(string name)
        {
            opaqueScreen = false;
            if (current != null && current.name == name) calmFrame = Time.frameCount;
            Clear();
            var g = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            g.transform.SetParent(root, false);
            Stretch((RectTransform)g.transform);
            var si = PawCanvasFit.SafeInsets;
            ((RectTransform)g.transform).offsetMin = new Vector2(si.x, si.y);
            ((RectTransform)g.transform).offsetMax = new Vector2(-si.z, -si.w);
            current = g;
            canvas.enabled = true;
            StartCoroutine(Fade(g.GetComponent<CanvasGroup>(), 0f, 1f, 0f, 0.25f));
            return g;
        }

        // ------------------------------------------------------------------ 1. title
        public void ShowTitle()
        {
            SetPlaying(false);
            reopen = ShowTitle;
            var s = NewScreen("Title").transform;
            var bg = Background(s, "bg_title", true);
            // the cats are painted into the art; the blank shop sign gets the shop name (image space, moves with the art)
            var sign = Txt(bg.transform, "PAW POST", 40, new Color(0.42f, 0.27f, 0.16f), Vector2.zero, new Vector2(300, 60), TextAnchor.MiddleCenter);
            sign.font = fontBold;
            Anchor(sign.rectTransform, 0.742f, 0.466f);
            sign.rectTransform.localRotation = Quaternion.Euler(0, 0, -1.5f);
            StartCoroutine(PopIn(sign.rectTransform, 0.9f, 0.4f));

            // logo plaque
            var logo = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            logo.SetParent(s, false);
            logo.anchorMin = logo.anchorMax = new Vector2(0.27f, 0.72f);
            logo.sizeDelta = new Vector2(760, 420);
            var plaque = Img(logo, "panel_paw", Vector2.zero, 400);
            var t1 = Txt(plaque.transform, "CAT'S TALE", 100, Ink, new Vector2(0, 40), new Vector2(720, 150), TextAnchor.MiddleCenter, true);
            t1.font = fontBold;
            var rib = Img(logo, "rib_orange", new Vector2(0, -105), 120);
            // the arched ribbon's band sits in its upper two thirds (about 17% of the height above centre)
            Txt(rib.transform, "Pawlivery", 48, Cream, new Vector2(0, 21), new Vector2(300, 66), TextAnchor.MiddleCenter, false, Ink).font = fontBold;
            
            StartCoroutine(Drop(logo, 0.15f, 0.8f));
            StartCoroutine(Unfurl(rib.rectTransform, 0.75f));

            // tap to start
            var tap = Img(s, "btn_tan", new Vector2(0, 110), 120);
            Anchor(tap.rectTransform, 0.2f, 0f, new Vector2(0, 120));   // bottom left, clear of the parcel stack
            var paw = Img(tap.transform, "rb_paw", new Vector2(-190, 0), 140);
            Txt(tap.transform, "TAP TO START", 44, Ink, new Vector2(55, 2), new Vector2(300, 80), TextAnchor.MiddleCenter).font = fontBold;
            Anim(tap, PawUIAnim.Mode.Pulse, 1f);
            StartCoroutine(PopIn(tap.rectTransform, 1.0f, 0.5f));
            

            // corner buttons + version
            var gear = Img(s, "rb_gear", Vector2.zero, 110, true);
            Anchor(gear.rectTransform, 1f, 1f, new Vector2(-90, -90));
            Squish(gear);
            gear.gameObject.AddComponent<Button>().onClick.AddListener(() => { PawAudio.Instance?.ToggleMute(); Click(); Toast(PawAudio.Instance != null && PawAudio.Instance.Muted ? "Sound off" : "Sound on"); });
            var ver = Txt(s, "v0.3", 24, Cream, Vector2.zero, new Vector2(200, 40), TextAnchor.LowerRight, false, Ink);
            Anchor(ver.rectTransform, 1f, 0f, new Vector2(-110, 30));

            // falling paw sparkles
            StartCoroutine(Sparkles(s, 2.5f));

            // tap anywhere
            var hit = Hit(s);
            hit.onClick.AddListener(() => { Click(); ShowMenu(); });
            gear.transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ 2. main menu
        public void ShowMenu()
        {
            SetPlaying(false);
            reopen = ShowMenu;
            var s = NewScreen("MainMenu").transform;
            Background(s, "bg_office", true);   // the cat on the rug is part of the art

            TopBar(s, 0f);

            // left column
            string[] ln = { "Daily Rewards", "Missions", "Paw Pass", "How to Play" };
            Action[] la = { () => ShowDaily(ShowMenu), () => ShowDaily(ShowMenu), () => ShowPass(ShowMenu), () => ShowHowToPlay(ShowMenu) };
            bool[] ld = { PawSave.DailyClaimableToday, PawSave.AnyMissionClaimable(), PassClaimable(), false };
            string[] li = { "rb_star", "rb_pin", "rb_paw", "ic_book" };
            for (int i = 0; i < ln.Length; i++)
            {
                var plate = Img(s, "btn_tan", Vector2.zero, 92);
                Anchor(plate.rectTransform, 0f, 0.5f, new Vector2(250, 210 - i * 140));
                var ic = Img(plate.transform, li[i], new Vector2(-120, 0), 128);
                Txt(plate.transform, ln[i], 26, Ink, new Vector2(42, 2), new Vector2(190, 70), TextAnchor.MiddleCenter).font = fontBold;
                Squish(plate);
                var act = la[i];
                plate.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); act(); });
                StartCoroutine(SlideIn(plate.rectTransform, new Vector2(-500, 0), 0.2f + i * 0.08f));
                if (ld[i]) Dot(ic.transform);
            }
            // right column
            string[] rn = { "Shop", "Wardrobe", "Bag" };
            string[] ri = { "sq_shop", "tile_cat", "sq_bag" };
            Action[] ra = { () => ShowShop(0, ShowMenu), () => ShowWardrobe(ShowMenu), () => ShowBag(ShowMenu) };
            for (int i = 0; i < rn.Length; i++)
            {
                var b = Img(s, ri[i], Vector2.zero, 128);
                Anchor(b.rectTransform, 1f, 0.5f, new Vector2(-130, 170 - i * 165));   // three buttons, centred like the left column
                Txt(b.transform, rn[i], 28, Cream, new Vector2(0, -78), new Vector2(220, 40), TextAnchor.MiddleCenter, false, Ink);
                Squish(b);
                var act = ra[i];
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); act(); });
                StartCoroutine(SlideIn(b.rectTransform, new Vector2(500, 0), 0.2f + i * 0.08f));
                if (i == 0 && PawSave.BundleAvailable) Dot(b.transform);
            }

            // start delivery
            var start = Img(s, "btn_orange", Vector2.zero, 170);
            Anchor(start.rectTransform, 0.5f, 0f, new Vector2(0, 120));
            Txt(start.transform, "START DELIVERY", 56, Cream, new Vector2(40, 4), new Vector2(520, 100), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f));
            Anim(start, PawUIAnim.Mode.Pulse, 0.7f);
            Squish(start);
            start.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); ShowBoard(); });
            StartCoroutine(PopIn(start.rectTransform, 0.45f, 0.5f));

            var roam = Img(s, "btn_teal", Vector2.zero, 100);
            Anchor(roam.rectTransform, 1f, 0f, new Vector2(-230, 110));
            Txt(roam.transform, "FREE ROAM", 36, Cream, new Vector2(0, 3), new Vector2(280, 70), TextAnchor.MiddleCenter, false, new Color(0.1f, 0.25f, 0.22f));
            Squish(roam);
            roam.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); StartFreeRoam(); });
            StartCoroutine(PopIn(roam.rectTransform, 0.6f, 0.4f));
        }

        void TopBar(Transform s, float delay)
        {
            // level + xp: text and bar share one left edge, to the right of the cat badge painted on the pill
            var lv = Img(s, "pill_cat", Vector2.zero, 104);
            Anchor(lv.rectTransform, 0f, 1f, new Vector2(230, -76));
            const float contentX = 42f, contentW = 250f;
            Txt(lv.transform, $"Lv {PawProgress.Level}", 34, Ink, new Vector2(contentX, 20), new Vector2(contentW, 44), TextAnchor.MiddleLeft).font = fontBold;
            int need = PawProgress.XpToNext(PawProgress.Level);
            Bar(lv.transform, new Vector2(contentX, -20), new Vector2(contentW, 30), (float)PawProgress.Xp / need);
            Txt(lv.transform, $"{PawProgress.Xp}/{need} XP", 19, Cream, new Vector2(contentX, -19), new Vector2(contentW, 30), TextAnchor.MiddleCenter, false, Ink);
            StartCoroutine(SlideIn(lv.rectTransform, new Vector2(0, 200), delay));
            // coins + golden fish, in a self-spacing row (they can never overlap, whatever the screen width)
            var row = CounterRow(s, 40f);   // energy only exists while playing, so it is not shown on menus
            // "back" must be the screen showing now (captured at tap time), not whatever is open when X is pressed:
            // by then that is the shop itself, and X would just reopen it
            Counter(row, "ic_paw_coin", PawProgress.Coins, delay + 0.05f, () => { var here = reopen ?? ShowMenu; ShowShop(1, here); });
            Counter(row, "ic_fish_cookie", PawProgress.Fish, delay + 0.1f, () => { var here = reopen ?? ShowMenu; ShowShop(2, here); });
        }

        /// <summary>Top-right row for currency counters: lays its children out right to left with fixed spacing.</summary>
        RectTransform CounterRow(Transform s, float rightPad)
        {
            var row = new GameObject("Counters", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            row.SetParent(s, false);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 1f);
            row.anchoredPosition = new Vector2(-rightPad, -34);
            row.sizeDelta = new Vector2(700, 84);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleRight;
            h.spacing = 26f;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return row;
        }

        void Counter(Transform row, string icon, int value, float delay, Action onPlus = null)
        {
            // [icon][  number  ][+] inside a fixed-size pill; the icon hangs a little off the left end like the art
            var holder = new GameObject("Counter", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            holder.SetParent(row, false);
            holder.sizeDelta = new Vector2(260, 84);
            var p = Pill(holder, "btn_tan", new Vector2(14, 0), new Vector2(246, 70));
            Img(holder, icon, new Vector2(-96, 0), 84);
            var t = Txt(p.transform, "0", 34, Ink, new Vector2(-14, 2), new Vector2(150, 56), TextAnchor.MiddleCenter);
            t.font = fontBold;
            var plus = Pill(p.transform, "btn_teal", new Vector2(92, 0), new Vector2(44, 44));
            Txt(plus.transform, "+", 34, Cream, new Vector2(0, 2), new Vector2(44, 44), TextAnchor.MiddleCenter).font = fontBold;
            if (onPlus != null)
            {
                p.raycastTarget = true;
                Squish(p);
                p.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); onPlus(); });
            }
            else plus.gameObject.SetActive(false);
            StartCoroutine(CountUp(t, 0, value, delay + 0.2f, 0.8f, ""));
        }

        // ------------------------------------------------------------------ 3. order board
        public void ShowBoard()
        {
            SetPlaying(false);
            reopen = ShowBoard;
            var s = NewScreen("OrderBoard").transform;
            var bg = Background(s, "bg_board", true);   // the HQ notice-board room
            bg.color = new Color(0.86f, 0.86f, 0.86f);

            var board = Img(s, "panel_wood", new Vector2(0, -10), 880);
            StartCoroutine(Drop(board.rectTransform, 0f, 0.7f));
            var rib = Img(s, "rib_orange", new Vector2(0, 420), 128);
            var rt = Txt(rib.transform, "ORDER BOARD", 46, Cream, new Vector2(0, 22), new Vector2(330, 70), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.22f, 0.06f));
            rt.font = fontBold;
            StartCoroutine(Unfurl(rib.rectTransform, 0.45f));

            var back = Img(s, "rb_arrow_left", Vector2.zero, 110, true);
            Anchor(back.rectTransform, 0f, 1f, new Vector2(90, -235));
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); ShowMenu(); });

            offers = orders != null ? orders.MakeOffers(4) : new List<PawOrders.Offer>();
            selected = 0;
            var cards = new List<Image>();
            Vector2[] pos = { new Vector2(-285, 165), new Vector2(285, 165), new Vector2(-285, -205), new Vector2(285, -205) };
            if (offers.Count == 0)
                Txt(board.transform, "The town is still waking up...\nTry again in a second!", 44, Ink, Vector2.zero, new Vector2(900, 200), TextAnchor.MiddleCenter);
            for (int i = 0; i < offers.Count; i++)
            {
                var card = OrderCard(s, offers[i], pos[i]);
                cards.Add(card);
                int k = i;
                card.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); Select(cards, k); });
                StartCoroutine(Deal(card.rectTransform, 0.5f + i * 0.14f, (i % 2 == 0 ? -1 : 1) * 14f));
            }
            if (cards.Count > 0) StartCoroutine(Later(0.5f + cards.Count * 0.14f + 0.35f, () => Select(cards, 0)));

            var accept = Img(s, "btn_orange", new Vector2(0, -470), 140);
            var at = Txt(accept.transform, "ACCEPT ORDER", 50, Cream, new Vector2(30, 4), new Vector2(440, 90), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f));
            Squish(accept);
            Anim(accept, PawUIAnim.Mode.Pulse, 0.5f);
            accept.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (offers.Count == 0) { ShowBoard(); return; }
                Click();
                AcceptSelected();
            });
            StartCoroutine(PopIn(accept.rectTransform, 1.1f, 0.45f));
            TopBar(s, 0.2f);
        }

        Image OrderCard(Transform s, PawOrders.Offer o, Vector2 pos)
        {
            var card = Img(s, "panel_paw", pos, 350);
            var c = card.transform;
            var portrait = Img(c, "portrait_" + o.customer, new Vector2(-165, 10), 165);
            var name = Txt(c, o.dest.Length > 0 ? char.ToUpper(o.dest[0]) + o.dest.Substring(1) : o.dest, 34, Ink, new Vector2(75, 85), new Vector2(300, 90), TextAnchor.MiddleLeft);
            name.font = fontBold;
            Img(c, "pin_red", new Vector2(-55, 22), 40);
            Txt(c, $"{o.distance:0} m", 30, InkSoft, new Vector2(72, 22), new Vector2(200, 40), TextAnchor.MiddleLeft);
            Img(c, "ic_paw_coin", new Vector2(-55, -28), 44);
            Txt(c, $"+ {o.reward}", 34, Ink, new Vector2(60, -28), new Vector2(200, 44), TextAnchor.MiddleLeft).font = fontBold;
            for (int i = 0; i < 3; i++)
            {
                var st = Img(c, "ic_star", new Vector2(-50 + i * 44, -78), 40);
                if (i >= o.stars) st.color = new Color(0.55f, 0.48f, 0.42f, 0.55f);
            }
            for (int i = 0; i < o.tags.Length; i++)
            {
                var tg = Img(c, "btn_tan", new Vector2(-130 + i * 150, -132), 46);
                tg.color = new Color(1f, 0.86f, 0.82f);
                Txt(tg.transform, o.tags[i], 22, TagRed, new Vector2(0, 2), new Vector2(140, 40), TextAnchor.MiddleCenter).font = fontBold;
            }
            if (o.vip)
            {
                var v = Img(c, "rib_star", new Vector2(175, 140), 74);
                v.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
                Txt(v.transform, "VIP", 32, Cream, new Vector2(22, 6), new Vector2(160, 50), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.22f, 0.06f)).font = fontBold;
                
            }
            portrait.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -3);
            return card;
        }

        void Select(List<Image> cards, int k)
        {
            selected = k;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (!c) continue;
                bool on = i == k;
                StartCoroutine(ScaleTo(c.rectTransform, on ? 1.07f : 0.97f, 0.18f));
                var ol = c.GetComponent<Outline>();
                if (on && ol == null) { ol = c.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(1f, 0.72f, 0.2f, 0.9f); ol.effectDistance = new Vector2(6, -6); }
                if (!on && ol != null) Destroy(ol);
                c.color = on ? Color.white : new Color(0.93f, 0.93f, 0.93f);
            }
        }

        void AcceptSelected()
        {
            if (orders == null || offers == null || offers.Count == 0) return;
            var o = offers[Mathf.Clamp(selected, 0, offers.Count - 1)];
            orders.Accept(o);
            if (game != null) game.FreeRoam = false;   // a delivery brings the treats back
            game?.ResetOrderStats();
            orderAcceptedAt = Time.time;
            Clear();
            SetPlaying(true);
            Toast($"Order accepted! Pick it up at PAW-LIVERY HQ");
        }

        /// <summary>Free Roam: just explore. Any order in progress is dropped and treats are cleared until the next delivery.</summary>
        public void StartFreeRoam()
        {
            orders?.Cancel();
            if (game != null) game.FreeRoam = true;
            Resume();
        }

        /// <summary>Pause > Home: leaving the town ends the run. Warn first; leaving cancels the order and the next
        /// start is fresh (full energy, back at HQ, no order).</summary>
        void ConfirmLeave()
        {
            bool onOrder = orders != null && (orders.HasTarget || orders.Current != null);
            var p = Popup("Leave", out var panel, 520);
            Txt(panel, "Leave the town?", 46, Ink, new Vector2(0, 150), new Vector2(700, 60), TextAnchor.MiddleCenter).font = fontBold;
            Txt(panel, onOrder ? "Your delivery will be cancelled and you won't be paid for it."
                               : "Your trip ends here and your energy resets.", 30, InkSoft, new Vector2(0, 50), new Vector2(680, 110), TextAnchor.MiddleCenter);
            Txt(panel, "Next time you start fresh at Paw-livery HQ.", 24, InkSoft, new Vector2(0, -35), new Vector2(680, 40), TextAnchor.MiddleCenter);
            var stay = TextButton(panel, "btn_teal", "STAY", new Vector2(-170, -150), 100, 260);
            stay.onClick.AddListener(() => { Click(); Destroy(p); });
            var leave = TextButton(panel, "btn_orange", "LEAVE", new Vector2(170, -150), 100, 260);
            leave.onClick.AddListener(() => { Click(); Destroy(p); FreshStart(); ShowMenu(); });
        }

        void FreshStart()
        {
            orders?.Cancel();
            game?.ResetRun();
            if (mover && petStartSet)
            {
                var cc = mover.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                mover.transform.SetPositionAndRotation(petStart, petStartRot);
                if (cc) cc.enabled = true;
            }
        }

        public void Resume()
        {
            Clear();
            SetPlaying(true);
        }

        // ------------------------------------------------------------------ 4. order delivered
        void OnDelivered(float seconds, PawOrders.Offer o)
        {
            StartCoroutine(Later(1.1f, () => ShowDelivered(seconds, o)));
        }

        public void ShowDelivered(float seconds, PawOrders.Offer o)
        {
            SetPlaying(false);
            int basePay = o != null ? o.reward : 30;
            int bumps = game != null ? game.Bumps : 0;
            int treats = game != null ? game.TreatsCollected : 0;
            float par = o != null ? 25f + o.distance / 1.3f : 90f;
            int timeBonus = seconds < par ? Mathf.RoundToInt(10 + 20 * (1f - seconds / par)) : 0;
            int noBumps = bumps == 0 ? 8 : 0;
            int tip = 5 + (o != null && o.vip ? 15 : UnityEngine.Random.Range(0, 10));
            int treatPay = treats;
            int total = basePay + timeBonus + noBumps + tip + treatPay;
            int stars = 1 + (timeBonus > 0 ? 1 : 0) + (bumps == 0 ? 1 : 0);
            int xpGain = 60 + (o != null ? o.stars * 25 : 25) + stars * 10;
            int lv0 = PawProgress.Level, xp0 = PawProgress.Xp;
            PawProgress.AddCoins(total);
            PawProgress.CountDelivery();
            PawSave.MissionAdd("deliver");
            PawSave.PassXp = PawSave.PassXp + 40 + stars * 10;
            pendingLevels = PawProgress.AddXp(xpGain);
            levelUpChips = pendingLevels > 0 ? GrantLevelRewards(lv0, PawProgress.Level) : null;

            var s = NewScreen("Delivered").transform;
            Dim(s, 0.45f);
            var panel = Img(s, "panel_cat", new Vector2(0, 10), 740);
            StartCoroutine(PopIn(panel.rectTransform, 0.05f, 0.55f));
            var p = panel.transform;
            var badge = Img(p, "cat_face_smile", Vector2.zero, 118);      // fill the panel's cat-head badge
            Anchor(badge.rectTransform, 0.133f, 0.88f);
            
            var rib = Img(s, "rib_star", new Vector2(0, 360), 150);
            Txt(rib.transform, "ORDER DELIVERED!", 52, Cream, new Vector2(40, 10), new Vector2(660, 80), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.22f, 0.06f)).font = fontBold;
            StartCoroutine(Unfurl(rib.rectTransform, 0.35f));
            for (int i = 0; i < 3; i++)
            {
                var st = Img(s, "ic_star", new Vector2(-120 + i * 120, 245 + (i == 1 ? 18 : 0)), i == 1 ? 120 : 100);
                if (i >= stars) st.color = new Color(0.45f, 0.38f, 0.33f, 0.6f);
                bool lit = i < stars;
                StartCoroutine(PopIn(st.rectTransform, 0.7f + i * 0.22f, 0.4f, () => { if (lit) PawAudio.Instance?.Success(); }));
            }
            var pic = Img(p, "pic_handoff", new Vector2(-260, -35), 400);
            pic.gameObject.AddComponent<Outline>().effectColor = new Color(0.86f, 0.75f, 0.58f);
            pic.GetComponent<Outline>().effectDistance = new Vector2(8, -8);
            StartCoroutine(PopIn(pic.rectTransform, 0.4f, 0.45f));
            

            (string icon, string label, int v)[] rows =
            {
                ("ic_paw_coin", "Base Pay", basePay), ("ic_stopwatch", "Time Bonus", timeBonus),
                ("ic_star", "No Bumps Bonus", noBumps), ("ic_gift", "Tip", tip), ("ic_fish_cookie", "Treats Collected", treatPay)
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(p, false);
                row.anchoredPosition = new Vector2(200, 150 - i * 60);
                row.sizeDelta = new Vector2(440, 56);
                Img(row, rows[i].icon, new Vector2(-195, 0), 46);
                Txt(row, rows[i].label, 30, Ink, new Vector2(-30, 0), new Vector2(260, 50), TextAnchor.MiddleLeft);
                Img(row, "ic_paw_coin", new Vector2(130, 0), 34);
                var vt = Txt(row, "+0", 32, Ink, new Vector2(215, 0), new Vector2(110, 50), TextAnchor.MiddleLeft);
                vt.font = fontBold;
                StartCoroutine(SlideIn(row, new Vector2(80, 0), 1.2f + i * 0.12f));
                StartCoroutine(CountUp(vt, 0, rows[i].v, 1.3f + i * 0.12f, 0.6f, "+"));
            }
            // xp bar
            var xpRow = new GameObject("XP", typeof(RectTransform)).GetComponent<RectTransform>();
            xpRow.SetParent(p, false);
            xpRow.anchoredPosition = new Vector2(200, -175);
            xpRow.sizeDelta = new Vector2(440, 50);
            Txt(xpRow, "XP", 30, Ink, new Vector2(-195, 0), new Vector2(60, 44), TextAnchor.MiddleLeft).font = fontBold;
            int need0 = PawProgress.XpToNext(lv0);
            var fill = Bar(xpRow, new Vector2(0, 0), new Vector2(260, 34), (float)xp0 / need0);
            var xt = Txt(xpRow, $"+{xpGain} XP", 26, Ink, new Vector2(215, 0), new Vector2(150, 40), TextAnchor.MiddleLeft);
            xt.font = fontBold;
            float to = pendingLevels > 0 ? 1f : (float)PawProgress.Xp / PawProgress.XpToNext(PawProgress.Level);
            StartCoroutine(FillTo(fill, to, 2.0f, 0.9f));
            StartCoroutine(Confetti(s, 0.9f, 60));

            // buttons
            var x2 = Img(s, "btn_teal", new Vector2(-300, -430), 130);
            var x2t = Txt(x2.transform, "x2 REWARDS", 44, Cream, new Vector2(0, 4), new Vector2(380, 80), TextAnchor.MiddleCenter, false, new Color(0.1f, 0.25f, 0.22f));
            Squish(x2);
            bool claimed = false;
            x2.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (claimed) return;
                claimed = true;
                // the double is paid only after a rewarded video has played to the end
                StartCoroutine(FakeAd(() =>
                {
                    PawProgress.AddCoins(total);
                    if (x2t) x2t.text = "CLAIMED!";
                    if (x2) x2.color = new Color(0.8f, 0.8f, 0.8f);
                    if (s) StartCoroutine(Confetti(s, 0f, 30));
                }));
            });
            var next = Img(s, "btn_orange", new Vector2(300, -430), 130);
            Txt(next.transform, "NEXT ORDER", 44, Cream, new Vector2(30, 4), new Vector2(380, 80), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f));
            Squish(next);
            Anim(next, PawUIAnim.Mode.Pulse, 0.5f);
            next.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); AfterResults(ShowBoard); });
            var home = Img(s, "rb_home", new Vector2(0, -430), 110, true);
            home.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); AfterResults(ShowMenu); });
            StartCoroutine(PopIn(x2.rectTransform, 2.0f, 0.4f));
            StartCoroutine(PopIn(home.rectTransform, 2.1f, 0.4f));
            StartCoroutine(PopIn(next.rectTransform, 2.2f, 0.4f));
        }

        void AfterResults(Action then)
        {
            if (pendingLevels > 0) { pendingLevels = 0; ShowLevelUp(then); }
            else then();
        }

        // ------------------------------------------------------------------ 5. level up
        public void ShowLevelUp(Action then)
        {
            SetPlaying(false);
            var s = NewScreen("LevelUp").transform;
            Dim(s, 0.5f);
            var art = Img(s, "bg_levelup", new Vector2(-330, 10), 860);
            art.gameObject.AddComponent<Outline>().effectColor = new Color(1f, 0.95f, 0.85f);
            art.GetComponent<Outline>().effectDistance = new Vector2(10, -10);
            StartCoroutine(PopIn(art.rectTransform, 0.05f, 0.6f));
            
            // text on the art's own blank banner
            var lt = Txt(art.transform, "LEVEL UP!", 64, Cream, Vector2.zero, new Vector2(560, 90), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f));
            lt.font = fontBold;
            Anchor(lt.rectTransform, 0.5f, 0.885f);
            var lvp = Img(art.transform, "btn_tan", Vector2.zero, 80);
            Anchor(lvp.rectTransform, 0.5f, 0.76f);
            Txt(lvp.transform, $"Level {PawProgress.Level}", 42, Ink, new Vector2(0, 3), new Vector2(260, 60), TextAnchor.MiddleCenter).font = fontBold;
            StartCoroutine(PopIn(lvp.rectTransform, 0.6f, 0.45f));

            var panel = Img(s, "panel_unlock", new Vector2(450, -20), 560);
            StartCoroutine(SlideIn(panel.rectTransform, new Vector2(700, 0), 0.4f));
            Txt(panel.transform, "New Unlocks!", 44, Ink, Vector2.zero, new Vector2(520, 70), TextAnchor.MiddleCenter).font = fontBold;
            Anchor(panel.transform.GetChild(panel.transform.childCount - 1) as RectTransform, 0.5f, 0.86f);
            string[] labels = levelUpChips ?? LevelChips(PawProgress.Level, 0, 0, false, false);
            float[] lx = { 0.205f, 0.5f, 0.79f };
            for (int i = 0; i < 3; i++)
            {
                // label chip sits over the little dot under each tile
                var chip = Img(panel.transform, "btn_tan", Vector2.zero, 60);
                chip.rectTransform.sizeDelta = new Vector2(186, 68);
                chip.preserveAspect = false;
                Anchor(chip.rectTransform, lx[i], 0.3f);
                Txt(chip.transform, labels[i], 24, Ink, new Vector2(0, 1), new Vector2(170, 60), TextAnchor.MiddleCenter).font = fontBold;
                StartCoroutine(PopIn(chip.rectTransform, 0.9f + i * 0.15f, 0.35f));
            }
            var aw = Txt(panel.transform, "AWESOME!", 40, Cream, Vector2.zero, new Vector2(360, 70), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f));
            aw.font = fontBold;
            Anchor(aw.rectTransform, 0.5f, 0.155f);
            var btn = new GameObject("AwesomeHit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            btn.transform.SetParent(panel.transform, false);
            btn.color = new Color(1, 1, 1, 0);
            Anchor(btn.rectTransform, 0.5f, 0.155f);
            btn.rectTransform.sizeDelta = new Vector2(400, 100);
            btn.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); then(); });
            Anim(aw, PawUIAnim.Mode.Pulse, 1f);
            PawAudio.Instance?.Success();
            StartCoroutine(Confetti(s, 0.2f, 90));
            StartCoroutine(Confetti(s, 1.2f, 50));
        }

        string[] levelUpChips;

        /// <summary>Level-up rewards are real and granted the moment the level is reached (before the screen shows):
        /// coins every level, Golden Fish every 3rd level, +1 bag slot every 4th level. The chips show what was given
        /// plus what genuinely changed in the job board (more stops per order, farther and better paid orders).</summary>
        string[] GrantLevelRewards(int from, int to)
        {
            int coins = 0, fish = 0;
            bool bag = false, stops = false;
            for (int lv = from + 1; lv <= to; lv++)
            {
                coins += 40 + 10 * lv;
                if (lv % 3 == 0) fish += 2;
                if (lv % 4 == 0 && PawSave.BagSize < PawSave.BagMax) { bag = true; if (game) game.UpgradeBag(1); else PawSave.BagSize++; }
                if (PawOrders.MaxStopsFor(lv) > PawOrders.MaxStopsFor(lv - 1)) stops = true;
            }
            if (coins > 0) PawProgress.AddCoins(coins);
            if (fish > 0) PawProgress.AddFish(fish);
            return LevelChips(to, coins, fish, bag, stops);
        }

        static string[] LevelChips(int level, int coins, int fish, bool bag, bool stops)
        {
            string c1 = coins > 0 ? $"+{coins} Paw\nCoins" : "Farther\norders";
            string c2 = fish > 0 ? $"+{fish} Golden\nFish" : "Pay\n+6%";
            string c3 = stops ? $"{PawOrders.MaxStopsFor(level)}-stop\norders!" : bag ? "Bag\n+1 slot" : "Farther\norders";
            if (c1 == c3) c3 = "Pay\n+6%";
            return new[] { c1, c2, c3 };
        }

        // ------------------------------------------------------------------ HUD home button
        /// <summary>RUN: hold to run (that way with the stick, straight ahead without it), let go to walk again.</summary>
        void AddHudRunButton()
        {
            if (!hud || hud.transform.Find("RunButton")) return;
            var b = new GameObject("RunButton", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            b.transform.SetParent(hud.transform, false);
            var sp = Resources.Load<Sprite>("Screens/btn_run");
            b.sprite = sp ? sp : S("rb_paw");
            b.preserveAspect = true;
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(1, 0);
            b.rectTransform.anchoredPosition = new Vector2(-430, 150);
            b.rectTransform.sizeDelta = new Vector2(170, 170);
            if (!sp)
            {
                var t = Txt(b.transform, "RUN", 34, Cream, new Vector2(0, -60), new Vector2(160, 44), TextAnchor.MiddleCenter, false, Ink);
                t.font = fontBold;
            }
            b.gameObject.AddComponent<PawActionButton>().action = PawActionButton.Action.Run;
        }

        void AddHudHomeButton()
        {
            if (!hud) return;
            var b = new GameObject("HomeButton", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            b.transform.SetParent(hud.transform, false);
            b.sprite = S("rb_gear"); b.preserveAspect = true;
            b.name = "PauseButton";
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0, 1);
            b.rectTransform.anchoredPosition = new Vector2(710, -80);
            b.rectTransform.sizeDelta = new Vector2(100, 100);
            Squish(b);
            b.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); ShowPause(); });
        }

        // ------------------------------------------------------------------ toast
        void Toast(string msg)
        {
            var t = Txt(root, msg, 38, Cream, Vector2.zero, new Vector2(1200, 70), TextAnchor.MiddleCenter, false, Ink);
            Anchor(t.rectTransform, 0.5f, 0.82f);
            t.raycastTarget = false;
            StartCoroutine(ToastLife(t));
        }

        IEnumerator ToastLife(Text t)
        {
            var rt = t.rectTransform;
            yield return Tween(0f, 0.25f, k => rt.localScale = Vector3.one * EaseOutBack(k));
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Tween(0f, 0.3f, k => { var c = t.color; c.a = 1f - k; t.color = c; });
            Destroy(t.gameObject);
        }

        // ------------------------------------------------------------------ building blocks
        Sprite S(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (!cache.TryGetValue(n, out var sp)) cache[n] = sp = Resources.Load<Sprite>("Screens/" + n);
            if (sp == null) Debug.LogWarning("[PawMenus] missing sprite " + n);
            return sp;
        }

        Image Img(Transform parent, string sprite, Vector2 pos, float height, bool squish = false)
        {
            var go = new GameObject(sprite ?? "Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            var sp = S(sprite);
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchoredPosition = pos;
            float aspect = sp != null ? sp.rect.width / sp.rect.height : 1f;
            rt.sizeDelta = new Vector2(height * aspect, height);
            if (sprite != null && sprite.StartsWith("btn_") && sp != null && sp.border.x > 0)
            {
                // pill buttons stretch in the middle only, so the rounded paw-print ends never squash
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
                go.AddComponent<PawPill>();
            }
            if (squish) Squish(img);
            return img;
        }

        /// <summary>Full-screen layer inside a (safe-area) screen: reaches back out under the notch to the real edges.</summary>
        static void FullBleed(RectTransform r)
        {
            Stretch(r);
            var si = PawCanvasFit.SafeInsets;
            r.offsetMin = new Vector2(-si.x, -si.y);
            r.offsetMax = new Vector2(si.z, si.w);
        }

        Image Background(Transform s, string sprite, bool kenBurns)
        {
            opaqueScreen = true;
            var holder = new GameObject("BG", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(s, false);
            FullBleed(holder);
            var img = new GameObject(sprite, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter)).GetComponent<Image>();
            img.transform.SetParent(holder, false);
            img.sprite = S(sprite);
            img.raycastTarget = false;
            var f = img.GetComponent<AspectRatioFitter>();
            f.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            f.aspectRatio = img.sprite != null ? img.sprite.rect.width / img.sprite.rect.height : 16f / 9f;
            if (kenBurns) Anim(img, PawUIAnim.Mode.KenBurns, 1f);
            return img;
        }

        void Dim(Transform s, float a)
        {
            var d = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            d.transform.SetParent(s, false);
            FullBleed(d.rectTransform);
            d.sprite = white;
            d.color = new Color(0.12f, 0.08f, 0.05f, a);
        }

        Text Txt(Transform parent, string s, int size, Color c, Vector2 pos, Vector2 box, TextAnchor anchor, bool shadow = false, Color? outline = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 0.9f;
            // never overflow the box: shrink (down to ~55%) instead of spilling past a border
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.55f));
            var rt = t.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            if (outline.HasValue)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = outline.Value;
                o.effectDistance = new Vector2(3, -3);
            }
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(1f, 0.85f, 0.6f, 0.9f);
                sh.effectDistance = new Vector2(0, -5);
            }
            return t;
        }

        /// <summary>Rounded progress bar (sliced, never stretched). Returns the Filled image whose fillAmount drives it.</summary>
        Image Bar(Transform parent, Vector2 pos, Vector2 size, float value)
        {
            var track = Pill(parent, "btn_tan", pos, size, new Color(0.78f, 0.68f, 0.55f));
            var fill = PawBarFill.Make(track.transform, S("btn_teal"), new Color(0.55f, 0.85f, 0.4f), Mathf.Max(2f, size.y * 0.1f));
            fill.fillAmount = Mathf.Clamp01(value);
            return fill;
        }

        /// <summary>Shrinks an image (keeping its shape) so it fits inside a w x h box: icons never spill into neighbours.</summary>
        static Image Fit(Image img, float w, float h)
        {
            var sz = img.rectTransform.sizeDelta;
            float k = Mathf.Min(1f, Mathf.Min(w / Mathf.Max(1f, sz.x), h / Mathf.Max(1f, sz.y)));
            img.rectTransform.sizeDelta = sz * k;
            return img;
        }

        /// <summary>A pill-shaped sliced image of an exact size (rounded ends keep their shape).</summary>
        Image Pill(Transform parent, string sprite, Vector2 pos, Vector2 size, Color? color = null)
        {
            var img = new GameObject(sprite, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = S(sprite);
            img.raycastTarget = false;
            if (color.HasValue) img.color = color.Value;
            if (img.sprite && img.sprite.border.x > 0) { img.type = Image.Type.Sliced; img.gameObject.AddComponent<PawPill>(); }
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            return img;
        }

        Button Hit(Transform s)
        {
            var h = new GameObject("Hit", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            h.transform.SetParent(s, false);
            Stretch(h.rectTransform);
            h.color = new Color(1, 1, 1, 0);
            return h.gameObject.AddComponent<Button>();
        }

        void Dot(Transform parent)
        {
            var d = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            d.transform.SetParent(parent, false);
            d.sprite = S("dot_red");
            d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.86f, 0.86f);
            d.rectTransform.sizeDelta = new Vector2(36, 36);
            Anim(d, PawUIAnim.Mode.Pulse, 3f);
        }

        static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform r, float x, float y, Vector2 pos = default)
        {
            r.anchorMin = r.anchorMax = new Vector2(x, y);
            r.anchoredPosition = pos;
        }

        static void Anim(Component c, PawUIAnim.Mode m, float amount)
        {
            var a = c.GetComponent<PawUIAnim>();
            if (a == null) a = c.gameObject.AddComponent<PawUIAnim>();
            a.mode = m; a.amount = amount;
        }

        static void Squish(Component c)
        {
            var a = c.GetComponent<PawUIAnim>();
            if (a == null) a = c.gameObject.AddComponent<PawUIAnim>();
            a.squish = true;
            var g = c.GetComponent<Graphic>();
            if (g) g.raycastTarget = true;
        }

        static void Click() => PawAudio.Instance?.Click();

        // ------------------------------------------------------------------ tweens (unscaled)
        static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        static IEnumerator Tween(float delay, float dur, Action<float> f)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            while (t < dur) { t += Time.unscaledDeltaTime; f(Mathf.Clamp01(t / dur)); yield return null; }
            f(1f);
        }

        IEnumerator Later(float delay, Action a) { yield return new WaitForSecondsRealtime(delay); a(); }

        static void Rebase(RectTransform r) { var a = r ? r.GetComponent<PawUIAnim>() : null; if (a) a.Rebase(); }

        IEnumerator PopIn(RectTransform r, float delay, float dur, Action onStart = null)
        {
            var anim = r.GetComponent<PawUIAnim>();
            if (anim) anim.enabled = false;
            if (Calm) { onStart?.Invoke(); if (anim) { anim.enabled = true; anim.Rebase(); } yield break; }
            Vector3 s = r.localScale; r.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            onStart?.Invoke();
            yield return Tween(0f, dur, k => { if (r) r.localScale = s * EaseOutBack(k); });
            if (anim) { anim.enabled = true; anim.Rebase(); }
        }

        IEnumerator Drop(RectTransform r, float delay, float dur)
        {
            var anim = r.GetComponent<PawUIAnim>();
            if (anim) anim.enabled = false;
            if (Calm) { if (anim) { anim.enabled = true; anim.Rebase(); } yield break; }
            Vector2 p = r.anchoredPosition;
            r.anchoredPosition = p + new Vector2(0, 900);
            yield return Tween(delay, dur, k => { if (r) r.anchoredPosition = Vector2.LerpUnclamped(p + new Vector2(0, 900), p, EaseOutBounce(k)); });
            if (anim) { anim.enabled = true; anim.Rebase(); }
        }

        IEnumerator SlideIn(RectTransform r, Vector2 from, float delay)
        {
            var anim = r.GetComponent<PawUIAnim>();
            if (anim) anim.enabled = false;
            if (Calm) { if (anim) { anim.enabled = true; anim.Rebase(); } yield break; }
            Vector2 p = r.anchoredPosition;
            r.anchoredPosition = p + from;
            yield return Tween(delay, 0.5f, k => { if (r) r.anchoredPosition = Vector2.LerpUnclamped(p + from, p, EaseOutBack(k)); });
            if (anim) { anim.enabled = true; anim.Rebase(); }
        }

        IEnumerator Deal(RectTransform r, float delay, float rot)
        {
            if (Calm) { r.localRotation = Quaternion.Euler(0, 0, rot * 0.08f); yield break; }
            Vector2 p = r.anchoredPosition;
            r.anchoredPosition = p + new Vector2(0, -900);
            r.localRotation = Quaternion.Euler(0, 0, rot * 3f);
            yield return Tween(delay, 0.55f, k =>
            {
                if (!r) return;
                float e = EaseOutBack(k);
                r.anchoredPosition = Vector2.LerpUnclamped(p + new Vector2(0, -900), p, e);
                r.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(rot * 3f, rot * 0.08f, e));
            });
            PawAudio.Instance?.Click();
        }

        IEnumerator Unfurl(RectTransform r, float delay)
        {
            if (Calm) yield break;
            Vector3 s = r.localScale;
            r.localScale = new Vector3(0f, s.y, 1f);
            yield return Tween(delay, 0.55f, k => { if (r) r.localScale = new Vector3(s.x * EaseOutBack(k), s.y, 1f); });
        }

        IEnumerator ScaleTo(RectTransform r, float to, float dur)
        {
            float from = r.localScale.x;
            yield return Tween(0f, dur, k => { if (r) r.localScale = Vector3.one * Mathf.Lerp(from, to, EaseOutCubic(k)); });
        }

        IEnumerator Fade(CanvasGroup g, float a, float b, float delay, float dur)
        {
            if (Calm) { g.alpha = b; yield break; }
            g.alpha = a;
            yield return Tween(delay, dur, k => { if (g) g.alpha = Mathf.Lerp(a, b, k); });
        }

        IEnumerator CountUp(Text t, int from, int to, float delay, float dur, string prefix)
        {
            if (Calm) { t.text = prefix + to; yield break; }
            t.text = prefix + from;
            yield return Tween(delay, dur, k => { if (t) t.text = prefix + Mathf.RoundToInt(Mathf.Lerp(from, to, EaseOutCubic(k))); });
        }

        IEnumerator FillTo(Image fill, float to, float delay, float dur)
        {
            if (Calm) { fill.fillAmount = to; yield break; }
            float from = fill.fillAmount;
            yield return Tween(delay, dur, k => { if (fill) fill.fillAmount = Mathf.Lerp(from, to, EaseOutCubic(k)); });
        }

        // ------------------------------------------------------------------ particles
        static readonly string[] ConfettiSprites = { "ic_star", "paw_mid", "paw_light", "ic_paw_coin" };
        static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.78f, 0.25f), new Color(0.98f, 0.55f, 0.6f), new Color(0.45f, 0.8f, 0.55f), new Color(0.4f, 0.7f, 0.95f), new Color(1f, 0.6f, 0.25f)
        };

        IEnumerator Confetti(Transform s, float delay, int n)
        {
            yield return new WaitForSecondsRealtime(delay);
            for (int i = 0; i < n; i++)
            {
                if (!s) yield break;
                bool sprite = UnityEngine.Random.value < 0.35f;
                var img = new GameObject("Confetti", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(s, false);
                img.raycastTarget = false;
                if (sprite)
                {
                    img.sprite = S(ConfettiSprites[UnityEngine.Random.Range(0, ConfettiSprites.Length)]);
                    img.preserveAspect = true;
                    img.rectTransform.sizeDelta = Vector2.one * UnityEngine.Random.Range(34f, 56f);
                }
                else
                {
                    img.sprite = white;
                    img.color = ConfettiColors[UnityEngine.Random.Range(0, ConfettiColors.Length)];
                    img.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(14f, 22f), UnityEngine.Random.Range(22f, 34f));
                }
                img.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-200f, 200f), UnityEngine.Random.Range(-60f, 120f));
                var a = img.gameObject.AddComponent<PawUIAnim>();
                a.mode = PawUIAnim.Mode.Confetti;
                a.velocity = new Vector2(UnityEngine.Random.Range(-900f, 900f), UnityEngine.Random.Range(500f, 1300f));
                a.spin = UnityEngine.Random.Range(-400f, 400f);
                a.life = UnityEngine.Random.Range(1.8f, 2.8f);
                if (i % 6 == 5) yield return null;
            }
        }

        IEnumerator Sparkles(Transform s, float every)
        {
            while (s)
            {
                var p = new GameObject("Sparkle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                p.transform.SetParent(s, false);
                p.sprite = S(UnityEngine.Random.value < 0.5f ? "paw_light" : "ic_star");
                p.preserveAspect = true;
                p.raycastTarget = false;
                p.color = new Color(1, 1, 1, 0.85f);
                p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(UnityEngine.Random.Range(0.05f, 0.95f), 1.05f);
                p.rectTransform.sizeDelta = Vector2.one * UnityEngine.Random.Range(30f, 50f);
                var a = p.gameObject.AddComponent<PawUIAnim>();
                a.mode = PawUIAnim.Mode.Confetti;
                a.velocity = new Vector2(UnityEngine.Random.Range(-60f, 60f), -120f);
                a.spin = UnityEngine.Random.Range(-60f, 60f);
                a.life = 5f;
                yield return new WaitForSecondsRealtime(every * UnityEngine.Random.Range(0.5f, 1.2f));
            }
        }
    }
}
