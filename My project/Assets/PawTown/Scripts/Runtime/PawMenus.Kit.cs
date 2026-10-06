using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Shared building blocks for the shop / rewards / bag screens: rewards, prices, popups, close buttons.</summary>
    public partial class PawMenus
    {
        // ------------------------------------------------------------------ rewards
        public enum RewardKind { Coins, Fish, Treat, Cosmetic, Refill, BagSlots }

        public struct Reward
        {
            public RewardKind kind;
            public int amount;
            public TreatKind treat;
            public string id;

            public static Reward Coins(int n) => new Reward { kind = RewardKind.Coins, amount = n };
            public static Reward Fish(int n) => new Reward { kind = RewardKind.Fish, amount = n };
            public static Reward Treat(TreatKind t, int n) => new Reward { kind = RewardKind.Treat, treat = t, amount = n };
            public static Reward Cosmetic(string id) => new Reward { kind = RewardKind.Cosmetic, id = id, amount = 1 };

            public string Icon => kind == RewardKind.Coins ? "ic_paw_coin" : kind == RewardKind.Fish ? "ic_fish_cookie"
                : kind == RewardKind.Treat ? (treat == TreatKind.Fish ? "it_fish" : treat == TreatKind.Can ? "it_can" : "it_yarn")
                : kind == RewardKind.Cosmetic ? (PawCosmetics.Get(id)?.icon ?? "tile_item")
                : kind == RewardKind.Refill ? "ic_bolt" : "it_backpack";

            public string Label => kind == RewardKind.Coins ? $"{amount} Coins" : kind == RewardKind.Fish ? $"{amount} Golden Fish"
                : kind == RewardKind.Treat ? $"{PawGame.NameOf(treat)} x{amount}"
                : kind == RewardKind.Cosmetic ? (PawCosmetics.Get(id)?.name ?? id)
                : kind == RewardKind.Refill ? "Full energy" : $"Bag +{amount}";

            public string Short => kind == RewardKind.Cosmetic ? "" : $"x{amount}";
        }

        /// <summary>What a treat that does not fit in the bag is paid back as: half its shop price in coins.</summary>
        static int TreatRefund(TreatKind k) => k == TreatKind.Can ? 60 : k == TreatKind.Fish ? 25 : 20;

        /// <summary>Put n treats in the bag; any that do not fit are paid back at half their shop price, so a
        /// small bag never wastes a reward or a purchase.</summary>
        string GiveTreats(TreatKind k, int n)
        {
            int put = 0;
            for (int i = 0; i < n; i++) if (game != null && game.AddToBag(k)) put++;
            int refund = (n - put) * TreatRefund(k);
            if (refund > 0) { PawProgress.AddCoins(refund); return $"+{put} {PawGame.NameOf(k)} (bag full: +{refund} coins)"; }
            return $"+{put} {PawGame.NameOf(k)}";
        }

        /// <summary>Give a reward. Treats that do not fit in the bag become coins (see GiveTreats).</summary>
        string Grant(Reward r)
        {
            switch (r.kind)
            {
                case RewardKind.Coins: PawProgress.AddCoins(r.amount); return $"+{r.amount} Paw Coins";
                case RewardKind.Fish: PawProgress.AddFish(r.amount); return $"+{r.amount} Golden Fish";
                case RewardKind.Treat: return GiveTreats(r.treat, r.amount);
                case RewardKind.Cosmetic: PawSave.Own(r.id); return $"Unlocked {PawCosmetics.Get(r.id)?.name}!";
                case RewardKind.Refill: game?.Refill(); return "Energy full!";
                case RewardKind.BagSlots:
                    if (game) game.UpgradeBag(r.amount); else PawSave.BagSize += r.amount;
                    return $"Bag +{r.amount} slots";
            }
            return "";
        }

        static bool Afford(int coins, int fish) => PawProgress.Coins >= coins && PawProgress.Fish >= fish;

        static void Pay(int coins, int fish)
        {
            if (coins > 0) PawProgress.AddCoins(-coins);
            if (fish > 0) PawProgress.AddFish(-fish);
        }

        // ------------------------------------------------------------------ test-mode store (replace with Unity IAP later)
        /// <summary>Real-money purchase. Test mode: a confirm popup, then onSuccess.</summary>
        /// <summary>Secret coupon codes that complete a real-money purchase while there is no payment gateway.</summary>
        static readonly string[] Coupons = { "MEOWMEOW", "ARUSHI", "CODE999" };

        /// <summary>Real-money checkout (no store connected): asks for a secret coupon code; a valid code redeems the
        /// item, anything else fails the transaction. The single place to swap in Unity IAP later.</summary>
        void Purchase(string what, string price, Action onSuccess)
        {
            var p = Popup("Purchase", out var panel, 600);
            Txt(panel, what, 40, Ink, new Vector2(0, 205), new Vector2(760, 56), TextAnchor.MiddleCenter).font = fontBold;
            Txt(panel, price, 32, InkSoft, new Vector2(0, 150), new Vector2(700, 44), TextAnchor.MiddleCenter);
            Txt(panel, "Do you have the secret coupon?", 32, Ink, new Vector2(0, 85), new Vector2(760, 50), TextAnchor.MiddleCenter).font = fontBold;

            // coupon field
            var box = Pill(panel, "btn_tan", new Vector2(0, 5), new Vector2(560, 90), new Color(1f, 0.97f, 0.9f));
            box.raycastTarget = true;
            var input = box.gameObject.AddComponent<InputField>();
            var text = Txt(box.transform, "", 36, Ink, new Vector2(0, 2), new Vector2(480, 70), TextAnchor.MiddleCenter);
            text.font = fontBold; text.supportRichText = false;
            text.resizeTextForBestFit = false;
            var hint = Txt(box.transform, "Enter coupon code", 30, new Color(0.45f, 0.35f, 0.28f, 0.6f), new Vector2(0, 2), new Vector2(480, 70), TextAnchor.MiddleCenter);
            hint.resizeTextForBestFit = false;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = 20;
            input.contentType = InputField.ContentType.Alphanumeric;
            input.onValidateInput += (t, i2, c) => char.ToUpperInvariant(c);
            var msg = Txt(panel, "", 26, new Color(0.8f, 0.25f, 0.2f), new Vector2(0, -75), new Vector2(720, 40), TextAnchor.MiddleCenter);
            msg.font = fontBold;

            var ok = TextButton(panel, "btn_orange", "REDEEM", new Vector2(150, -170), 110, 300);
            ok.onClick.AddListener(() =>
            {
                string code = (input.text ?? "").Trim().ToUpperInvariant();
                if (Array.IndexOf(Coupons, code) >= 0)
                {
                    Destroy(p);
                    PawAudio.Instance?.Success();
                    onSuccess();
                }
                else
                {
                    PawAudio.Instance?.Oops();
                    msg.text = code.Length == 0 ? "Enter a coupon code first." : "Transaction failed: that coupon is not valid.";
                    StartCoroutine(Shake(box.rectTransform));
                }
            });
            var no = TextButton(panel, "btn_tan", "Cancel", new Vector2(-190, -170), 100, 240, Ink);
            no.onClick.AddListener(() => { Click(); Destroy(p); Toast("Transaction cancelled"); });
            input.ActivateInputField();
        }

        IEnumerator Shake(RectTransform r)
        {
            Vector2 p0 = r.anchoredPosition;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                if (!r) yield break;
                r.anchoredPosition = p0 + new Vector2(Mathf.Sin(t * 60f) * 12f * (1f - t / 0.35f), 0);
                yield return null;
            }
            if (r) r.anchoredPosition = p0;
        }

        // ------------------------------------------------------------------ building blocks
        /// <summary>Full-screen art: cover the screen, optionally shifted sideways / zoomed / pinned to the top edge.</summary>
        Image Backdrop(Transform s, string sprite, float shiftX = 0f, float zoom = 1f, bool alignTop = false, bool kenBurns = false)
        {
            opaqueScreen = true;
            var holder = new GameObject("BG", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(s, false);
            FullBleed(holder);
            var img = new GameObject(sprite, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(holder, false);
            img.sprite = S(sprite);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            float aspect = img.sprite ? img.sprite.rect.width / img.sprite.rect.height : 16f / 9f;
            Vector2 scr = root.rect.size;
            float w = Mathf.Max(scr.x, scr.y * aspect) * zoom, h = w / aspect;
            if (h < scr.y) { h = scr.y * zoom; w = h * aspect; }
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, alignTop ? 1f : 0.5f);
            rt.pivot = new Vector2(0.5f, alignTop ? 1f : 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            float maxShift = (w - scr.x) * 0.5f;
            rt.anchoredPosition = new Vector2(Mathf.Clamp(shiftX * scr.x, -maxShift, maxShift), 0f);
            if (kenBurns) Anim(img, PawUIAnim.Mode.KenBurns, 0.6f);
            return img;
        }

        Button CloseButton(Transform parent, Vector2 anchor, Vector2 pos, Action onClose, float size = 104)
        {
            var x = Img(parent, "x_dark", Vector2.zero, size, true);
            Anchor(x.rectTransform, anchor.x, anchor.y, pos);
            var b = x.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => { Click(); onClose(); });
            StartCoroutine(PopIn(x.rectTransform, 0.3f, 0.35f));
            return b;
        }

        Button TextButton(Transform parent, string sprite, string label, Vector2 pos, float height, float width, Color? ink = null, int fontSize = 0)
        {
            var b = Img(parent, sprite, pos, height);
            b.preserveAspect = false;
            b.rectTransform.sizeDelta = new Vector2(width, height);
            Color dark = sprite == "btn_teal" ? new Color(0.1f, 0.25f, 0.22f) : sprite == "btn_orange" ? new Color(0.55f, 0.25f, 0.08f) : InkSoft;
            var t = Txt(b.transform, label, fontSize > 0 ? fontSize : Mathf.RoundToInt(height * 0.36f), ink ?? Cream, new Vector2(0, 3), new Vector2(width - 30, height), TextAnchor.MiddleCenter,
                false, ink.HasValue ? (Color?)null : dark);
            t.font = fontBold;
            Squish(b);
            return b.gameObject.AddComponent<Button>();
        }

        /// <summary>Price pill: coin or fish icon + amount; greys out when the player can't afford it.</summary>
        Button PriceButton(Transform parent, Vector2 pos, int coins, int fish, string realPrice, float width = 200, float height = 66)
        {
            bool afford = realPrice != null || Afford(coins, fish);
            var b = Img(parent, afford ? "btn_orange" : "btn_tan", pos, height);
            b.preserveAspect = false;
            b.rectTransform.sizeDelta = new Vector2(width, height);
            string label = realPrice ?? (coins > 0 ? coins.ToString() : fish.ToString());
            if (realPrice == null)
            {
                Img(b.transform, coins > 0 ? "ic_paw_coin" : "ic_fish_cookie", new Vector2(-width * 0.5f + 34, 2), height * 0.82f);
                Txt(b.transform, label, Mathf.RoundToInt(height * 0.48f), afford ? Cream : InkSoft, new Vector2(18, 3), new Vector2(width - 70, height), TextAnchor.MiddleCenter,
                    false, afford ? (Color?)new Color(0.55f, 0.25f, 0.08f) : null).font = fontBold;
            }
            else
                Txt(b.transform, label, Mathf.RoundToInt(height * 0.48f), Cream, new Vector2(width * 0.09f, 3), new Vector2(width - 40, height), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f)).font = fontBold;
            Squish(b);
            return b.gameObject.AddComponent<Button>();
        }

        /// <summary>Modal popup over the current screen (does not replace it). Returns the root to Destroy.</summary>
        GameObject Popup(string name, out Transform panel, float height = 640, string panelSprite = "panel_paw")
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            g.transform.SetParent(root, false);
            Stretch((RectTransform)g.transform);
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(g.transform, false);
            Stretch(dim.rectTransform);
            dim.sprite = white;
            dim.color = new Color(0.12f, 0.08f, 0.05f, 0.5f);
            var p = panelSprite == "panel_paw" ? Card(g.transform, "panel_plain", Vector2.zero, new Vector2(height * 1.45f, height)) : Img(g.transform, panelSprite, Vector2.zero, height);
            StartCoroutine(PopIn(p.rectTransform, 0f, 0.4f));
            panel = p.transform;
            canvas.enabled = true;
            return g;
        }

        /// <summary>Stretchable card / panel (9-slice): the visible edge is exactly the rect, corners never stretch.</summary>
        Image Card(Transform parent, string sprite, Vector2 pos, Vector2 size)
        {
            var c = Img(parent, sprite, pos, size.y);
            c.preserveAspect = false;
            c.type = Image.Type.Sliced;
            // show the corner art at a fixed on-screen size whatever the sprite resolution (panel ~70 px, card ~32 px)
            float corner = c.sprite != null && c.sprite.border.x > 0 ? c.sprite.border.x : 96f;
            c.pixelsPerUnitMultiplier = corner / (sprite == "panel_plain" ? 70f : 32f);
            c.rectTransform.sizeDelta = size;
            return c;
        }

        /// <summary>Panel title that never sits on the panel's baked badge: left of the paw badge (panel_paw),
        /// right of the cat-head badge (panel_cat, which also gets a cat face inside the badge), centred otherwise.</summary>
        Text PanelTitle(Image panel, string text, int size, string icon = null)
        {
            var r = panel.rectTransform.sizeDelta;
            string sp = panel.sprite ? panel.sprite.name : "";
            Text t;
            if (sp == "panel_cat")
            {
                var face = Img(panel.transform, icon ?? "cat_face", Vector2.zero, r.y * 0.17f);
                Anchor(face.rectTransform, 0.133f, 0.875f);
                t = Txt(panel.transform, text, size, Ink, Vector2.zero, new Vector2(r.x * 0.5f, size * 1.4f), TextAnchor.MiddleLeft);
                Anchor(t.rectTransform, 0.52f, 0.85f);
            }
            else if (sp == "panel_paw")
            {
                t = Txt(panel.transform, text, size, Ink, Vector2.zero, new Vector2(r.x * 0.33f, size * 1.4f), TextAnchor.MiddleCenter);
                Anchor(t.rectTransform, 0.25f, 0.875f);
            }
            else
            {
                float x = 0.5f;
                if (icon != null) { var ic = Img(panel.transform, icon, Vector2.zero, size * 1.6f); Anchor(ic.rectTransform, 0.5f, 0.9f, new Vector2(-text.Length * size * 0.33f - size, 0)); }
                t = Txt(panel.transform, text, size, Ink, Vector2.zero, new Vector2(r.x * 0.8f, size * 1.4f), TextAnchor.MiddleCenter);
                Anchor(t.rectTransform, x, 0.9f);
            }
            t.font = fontBold;
            return t;
        }

        /// <summary>Small reward / item tile: cream tile + icon + optional caption under it.</summary>
        Image ItemTile(Transform parent, string icon, Vector2 pos, float size, string caption = null, int captionSize = 22)
        {
            var t = Img(parent, "tile_item", pos, size);
            var ic = Img(t.transform, icon, new Vector2(0, caption != null ? 8 : 4), size * 0.62f);
            ic.name = "Icon";
            if (caption != null) Txt(t.transform, caption, captionSize, Ink, new Vector2(0, -size * 0.34f), new Vector2(size, captionSize + 6), TextAnchor.MiddleCenter).font = fontBold;
            return t;
        }

        static string Countdown(TimeSpan t)
        {
            if (t.TotalSeconds < 0) t = TimeSpan.Zero;
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        IEnumerator Tick(Text t, Func<string> f)
        {
            while (t) { t.text = f(); yield return new WaitForSecondsRealtime(1f); }
        }

        // ------------------------------------------------------------------ settings
        void ApplySettings()
        {
            var au = PawAudio.Instance;
            if (au != null) au.SetVolumes(PawSave.MusicVol, PawSave.SoundVol);
            // graphics: 0 low (30 fps, 80% resolution, no MSAA, no shadows), 1 medium (60 fps, full resolution, 4x MSAA,
            // soft shadows), 2 high (medium + sharper shadows further out + SMAA on top of MSAA for the outlines)
            int q = Mathf.Clamp(PawSave.Quality, 0, 2);
#if UNITY_WEBGL
            Application.targetFrameRate = -1;   // browsers pace frames with requestAnimationFrame; a fixed rate stutters
#else
            Application.targetFrameRate = q == 0 ? 30 : 60;
#endif
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset rp)
            {
                rp.renderScale = q == 0 ? 0.8f : 1f;
                rp.msaaSampleCount = q == 0 ? 1 : 4;
                rp.shadowDistance = q == 2 ? 70f : 50f;
            }
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional)
                    l.shadows = q == 0 ? LightShadows.None : LightShadows.Soft;   // URP sets shadow resolution on the pipeline asset
            var cam = Camera.main;
            if (cam)
            {
                cam.allowMSAA = q > 0;
                var d = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (d) { d.antialiasing = q == 2 ? UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing : UnityEngine.Rendering.Universal.AntialiasingMode.None; d.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High; }
            }
            var js = FindAnyObjectByType<PawJoystick>();
            if (js && js.pad) js.pad.localScale = Vector3.one * PawSave.JoystickSize;
        }
    }
}
