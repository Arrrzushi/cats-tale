using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Out of Energy ("Too sleepy!") and the Bag / inventory.</summary>
    public partial class PawMenus
    {
        // ------------------------------------------------------------------ energy bar (paw pill + fill + text)
        Image EnergyPill(Transform parent, Vector2 pos, float width, float value, out Text label, float ghostTo = -1f)
        {
            var pill = Img(parent, "pill_paw", pos, width * 0.26f);
            pill.preserveAspect = false;
            pill.rectTransform.sizeDelta = new Vector2(width, width * 0.25f);
            var track = pill.rectTransform;
            Image Fill(Color c, float v)
            {
                // well inside the pill, right of the paw badge; the fill is a sliced pill that never stretches
                var well = new GameObject("Well", typeof(RectTransform)).GetComponent<RectTransform>();
                well.SetParent(pill.transform, false);
                well.anchorMin = new Vector2(0.19f, 0.22f); well.anchorMax = new Vector2(0.965f, 0.78f);
                well.offsetMin = well.offsetMax = Vector2.zero;
                var f = PawBarFill.Make(well, S("btn_teal"), c, 0f);
                f.fillAmount = Mathf.Clamp01(v);
                return f;
            }
            if (ghostTo >= 0f) Anim(Fill(new Color(0.75f, 0.95f, 0.6f, 0.55f), ghostTo), PawUIAnim.Mode.Pulse, 0f);
            var fill = Fill(new Color(0.45f, 0.82f, 0.38f), value);
            label = Txt(pill.transform, "", Mathf.RoundToInt(width * 0.07f), Ink, new Vector2(width * 0.08f, 1), new Vector2(width * 0.6f, width * 0.2f), TextAnchor.MiddleCenter);
            label.font = fontBold;
            return fill;
        }

        // ------------------------------------------------------------------ out of energy
        Coroutine resting;

        public void ShowSleepy(Action back)
        {
            SetPlaying(false);
            reopen = () => ShowSleepy(back);
            var s = NewScreen("Sleepy").transform;
            Backdrop(s, "bg_sleep", 0.17f, 1.12f, false, true);

            var panel = Card(s, "panel_plain", new Vector2(420, -20), new Vector2(960, 790));
            StartCoroutine(PopIn(panel.rectTransform, 0.05f, 0.5f));
            var p = panel.transform;
            var head = Img(p, "rib_cream", new Vector2(0, 352), 160);
            Txt(head.transform, "TOO SLEEPY!", 40, Ink, new Vector2(0, 12), new Vector2(360, 64), TextAnchor.MiddleCenter).font = fontBold;
            var pillow = Img(p, "pillow_cat", new Vector2(0, 120), 170);
            

            float e = game != null ? game.Energy01 : 0f;
            var fill = EnergyPill(p, new Vector2(0, -10), 560, e, out var el);
            el.text = $"{Mathf.RoundToInt(e * 100)} / 100";

            // the four ways back to full speed
            int treatIdx = BestTreat();
            var opts = new (string icon, string title, string amount, Action act, bool ok, string why)[]
            {
                (treatIdx >= 0 ? (game.bag[treatIdx] == TreatKind.Fish ? "it_fish" : game.bag[treatIdx] == TreatKind.Can ? "it_can" : "it_yarn") : "it_fish",
                 "Eat a treat", treatIdx >= 0 ? $"+{PawGame.EnergyOf(game.bag[treatIdx]):0}" : "Bag empty",
                 () => { var k = game.bag[treatIdx]; game.UseItem(treatIdx); Toast($"Yum! +{PawGame.EnergyOf(k):0} energy"); Wake(back); }, treatIdx >= 0, "No treats in your bag"),
                ("tile_video", "Watch a video", "+50", () => StartCoroutine(FakeAd(() => { game?.AddEnergy(50f); Toast("+50 energy"); Wake(back); })), true, ""),
                ("ic_fish_cookie", "Energy Refill", "5", () =>
                {
                    if (!Afford(0, 5)) { Toast("Not enough Golden Fish"); ShowShop(2, () => ShowSleepy(back)); return; }
                    Pay(0, 5); game?.Refill(); PawAudio.Instance?.Success(); Toast("Energy full!"); Wake(back);
                }, true, ""),
                ("ic_bed", "Rest", "2:00", null, true, ""),
            };
            for (int i = 0; i < opts.Length; i++)
            {
                var o = opts[i];
                var card = Card(p, "card", new Vector2(-330 + i * 220, -225), new Vector2(196, 240));
                var c = card.transform;
                Img(c, o.icon, new Vector2(0, 50), 96);
                Txt(c, o.title, 25, Ink, new Vector2(0, -22), new Vector2(176, 34), TextAnchor.MiddleCenter).font = fontBold;
                var amt = new GameObject("Amount", typeof(RectTransform)).GetComponent<RectTransform>();
                amt.SetParent(c, false);
                amt.anchoredPosition = new Vector2(0, -78);
                Img(amt, i == 2 ? "ic_fish_cookie" : i == 3 ? "ic_clock" : "ic_bolt", new Vector2(-46, 0), 40);
                var at = Txt(amt, o.amount, 30, Ink, new Vector2(16, 0), new Vector2(120, 40), TextAnchor.MiddleCenter);
                at.font = fontBold;
                if (!o.ok) { card.color = new Color(0.85f, 0.85f, 0.85f); at.color = InkSoft; }
                Squish(card);
                var btn = card.gameObject.AddComponent<Button>();
                if (i == 3) btn.onClick.AddListener(() => { Click(); if (resting == null) resting = StartCoroutine(Rest(at, fill, el, back)); });
                else btn.onClick.AddListener(() => { if (!o.ok) { Toast(o.why); return; } Click(); o.act(); });
                StartCoroutine(PopIn(card.rectTransform, 0.3f + i * 0.08f, 0.35f));
            }
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -80), () => { StopRest(); back(); });
        }

        int BestTreat()
        {
            if (game == null) return -1;
            int best = -1; float e = 0f;
            for (int i = 0; i < game.bag.Count; i++) { float v = PawGame.EnergyOf(game.bag[i]); if (v > e) { e = v; best = i; } }
            return best;
        }

        void Wake(Action back) { StopRest(); StartCoroutine(Later(0.3f, back)); }

        void StopRest()
        {
            if (resting != null) { StopCoroutine(resting); resting = null; }
        }

        IEnumerator Rest(Text label, Image fill, Text energyText, Action back)
        {
            float total = 120f, t = 0f, start = game != null ? game.Energy01 : 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float k = t / total;
                if (label) label.text = $"{(int)((total - t) / 60)}:{(int)((total - t) % 60):00}";
                float e = Mathf.Lerp(start, 1f, k);
                if (fill) fill.fillAmount = e;
                if (energyText) energyText.text = $"{Mathf.RoundToInt(e * 100)} / 100";
                yield return null;
            }
            resting = null;
            game?.Refill();
            PawAudio.Instance?.Success();
            Toast("Well rested! Energy full");
            back();
        }

        /// <summary>Stand-in for a rewarded video ad (3 s), so the flow can be tested end to end.</summary>
        IEnumerator FakeAd(Action reward)
        {
            var p = Popup("Ad", out var panel, 420);
            Txt(panel, "Your video is playing...", 40, Ink, new Vector2(0, 60), new Vector2(700, 60), TextAnchor.MiddleCenter).font = fontBold;
            var t = Txt(panel, "3", 72, Orange, new Vector2(0, -40), new Vector2(200, 90), TextAnchor.MiddleCenter);
            t.font = fontBold;
            for (int i = 3; i > 0; i--) { if (t) t.text = i.ToString(); yield return new WaitForSecondsRealtime(1f); }
            Destroy(p);
            PawAudio.Instance?.Success();
            reward();
        }

        // ------------------------------------------------------------------ bag
        int bagSel;

        public void ShowBag(Action back)
        {
            back ??= Resume;
            SetPlaying(false);
            reopen = () => ShowBag(back);
            var s = NewScreen("Bag").transform;
            var bg = Backdrop(s, "bg_town", 0f, 1f, false, true);
            bg.color = new Color(0.85f, 0.85f, 0.85f);
            var pack = Img(s, "big_backpack", new Vector2(-640, -60), 560);
            
            StartCoroutine(PopIn(pack.rectTransform, 0.05f, 0.5f));

            var panel = Img(s, "panel_cat", new Vector2(250, -10), 760);
            panel.preserveAspect = false;
            panel.rectTransform.sizeDelta = new Vector2(1240, 760);
            StartCoroutine(SlideIn(panel.rectTransform, new Vector2(900, 0), 0.1f));
            var p = panel.transform;
            PanelTitle(panel, "Inventory", 46);
            int count = game != null ? game.bag.Count : 0, size = PawSave.BagSize;
            Txt(p, $"{count} / {size}", 30, InkSoft, new Vector2(140, 268), new Vector2(160, 50), TextAnchor.MiddleRight).font = fontBold;
            if (bagSel >= count) bagSel = count - 1;

            // slots: 5 per row, up to 9 (locked beyond the current size)
            for (int i = 0; i < PawSave.BagMax; i++)
            {
                int col = i % 5, row = i / 5;
                var pos = new Vector2(-470 + col * 140, 165 - row * 150);
                bool locked = i >= size;
                bool has = i < count;
                var well = Img(p, has ? "tile_item" : "tile_well", pos, 126);
                if (locked) { Img(well.transform, "ic_lock", Vector2.zero, 56); well.color = new Color(0.9f, 0.86f, 0.8f); }
                else if (has)
                {
                    var k = game.bag[i];
                    Img(well.transform, k == TreatKind.Fish ? "it_fish" : k == TreatKind.Can ? "it_can" : "it_yarn", new Vector2(0, 4), 84);
                    if (i == bagSel)
                    {
                        var ol = well.gameObject.AddComponent<Outline>();
                        ol.effectColor = new Color(1f, 0.7f, 0.2f);
                        ol.effectDistance = new Vector2(5, -5);
                        well.rectTransform.localScale = Vector3.one * 1.06f;
                    }
                    int idx = i;
                    Squish(well);
                    well.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); bagSel = idx; ShowBag(back); });
                }
                StartCoroutine(PopIn(well.rectTransform, 0.2f + i * 0.04f, 0.3f));
            }
            if (size < PawSave.BagMax)
            {
                var up = TextButton(p, "btn_teal", "Upgrade bag", new Vector2(-190, -150), 76, 300, null, 28);
                up.onClick.AddListener(() => { Click(); ShowShop(0, () => ShowBag(back)); });
            }

            // detail card for the selected treat
            var card = Card(p, "card", new Vector2(372, 30), new Vector2(300, 460));
            var c = card.transform;
            float energy = game != null ? game.Energy01 : 0f;
            if (count > 0 && bagSel < 0) bagSel = 0;
            if (count > 0)
            {
                var k = game.bag[bagSel];
                var big = Img(c, k == TreatKind.Fish ? "it_fish" : k == TreatKind.Can ? "it_can" : "it_yarn", new Vector2(0, 120), 170);
                
                Txt(c, PawGame.NameOf(k), 36, Ink, new Vector2(0, 5), new Vector2(310, 50), TextAnchor.MiddleCenter).font = fontBold;
                Txt(c, $"Restores {PawGame.EnergyOf(k):0} energy.", 26, InkSoft, new Vector2(0, -40), new Vector2(310, 40), TextAnchor.MiddleCenter);
                var eat = TextButton(c, "btn_teal", "EAT", new Vector2(0, -110), 76, 240);
                eat.onClick.AddListener(() =>
                {
                    if (game.Energy01 >= 0.999f) { Toast("Energy is already full"); return; }
                    game.UseItem(bagSel);
                    Toast($"Yum! +{PawGame.EnergyOf(k):0} energy");
                    ShowBag(back);
                });
                var drop = TextButton(c, "btn_tan", "DROP", new Vector2(0, -192), 70, 240, Ink);
                drop.onClick.AddListener(() => { Click(); game.DropItem(bagSel); Toast($"Dropped a {PawGame.NameOf(k)}"); ShowBag(back); });
                float ghost = Mathf.Clamp01(energy + PawGame.EnergyOf(k) / (game.maxEnergy > 0 ? game.maxEnergy : 100f));
                EnergyPill(p, new Vector2(-190, -275), 560, energy, out var el, ghost);
                el.text = $"{Mathf.RoundToInt(energy * 100)} / 100";
                var refill = Txt(p, $"Eat: +{PawGame.EnergyOf(k):0}", 28, new Color(0.3f, 0.55f, 0.25f), new Vector2(200, -275), new Vector2(220, 44), TextAnchor.MiddleLeft);
                refill.font = fontBold;
            }
            else
            {
                Img(c, "paw_light", new Vector2(0, 100), 140).color = new Color(1, 1, 1, 0.6f);
                Txt(c, "Your bag is empty", 32, Ink, new Vector2(0, -10), new Vector2(310, 50), TextAnchor.MiddleCenter).font = fontBold;
                Txt(c, "Collect treats in town\nor buy them in the shop.", 24, InkSoft, new Vector2(0, -75), new Vector2(310, 80), TextAnchor.MiddleCenter);
                var shop = TextButton(c, "btn_orange", "SHOP", new Vector2(0, -170), 76, 240);
                shop.onClick.AddListener(() => { Click(); ShowShop(0, () => ShowBag(back)); });
                EnergyPill(p, new Vector2(-190, -275), 560, energy, out var el);
                el.text = $"{Mathf.RoundToInt(energy * 100)} / 100";
            }
            StartCoroutine(PopIn(card.rectTransform, 0.3f, 0.4f));
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -80), back);
        }
    }
}
