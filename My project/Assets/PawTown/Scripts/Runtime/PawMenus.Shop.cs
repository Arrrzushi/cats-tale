using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Treat Shop (Treats / Coins / Golden Fish / Bundles) and the Starter Bundle offer.</summary>
    public partial class PawMenus
    {
        class ShopItem
        {
            public string name, desc, icon;
            public int coins, fish;
            public string realPrice;                 // real-money items (test-mode store)
            public Func<bool> canBuy;                // extra condition (bag space, max bag size...)
            public string cantText;
            public Action give;
            public Action open;                      // opens its own screen instead of buying from the card
        }

        List<ShopItem> ShopTab(int tab, Action back)
        {
            var l = new List<ShopItem>();
            Func<bool> space = () => game == null || game.bag.Count < game.bagSize;
            switch (tab)
            {
                case 0:   // treats
                    l.Add(new ShopItem { name = "Fish Cookie", desc = "+25 Energy", icon = "it_fish", coins = 50, canBuy = space, cantText = "Your bag is full!", give = () => game.AddToBag(TreatKind.Fish) });
                    l.Add(new ShopItem { name = "Tuna Can", desc = "+40 Energy", icon = "it_can", coins = 120, canBuy = space, cantText = "Your bag is full!", give = () => game.AddToBag(TreatKind.Can) });
                    l.Add(new ShopItem { name = "Yarn Ball", desc = "+15 Energy", icon = "it_yarn", coins = 40, canBuy = space, cantText = "Your bag is full!", give = () => game.AddToBag(TreatKind.Yarn) });
                    // packs never need free slots: what does not fit is paid back in coins (GiveTreats)
                    l.Add(new ShopItem { name = "Snack Box x3", desc = "Cookie, tuna, yarn", icon = "it_snackbox", fish = 3,
                        give = () => { GiveTreats(TreatKind.Fish, 1); GiveTreats(TreatKind.Can, 1); GiveTreats(TreatKind.Yarn, 1); } });
                    l.Add(new ShopItem { name = "Energy Refill", desc = "Instant full energy", icon = "ic_bolt", fish = 5,
                        canBuy = () => game == null || game.Energy01 < 0.99f, cantText = "Energy is already full", give = () => game?.Refill() });
                    l.Add(new ShopItem { name = "Bigger Bag", desc = $"{PawSave.BagSize} -> {Mathf.Min(PawSave.BagMax, PawSave.BagSize + 2)} slots", icon = "it_backpack", fish = 20,
                        canBuy = () => PawSave.BagSize < PawSave.BagMax, cantText = "Your bag is the biggest size", give = () => { if (game) game.UpgradeBag(2); else PawSave.BagSize += 2; } });
                    break;
                case 1:   // coins (for golden fish)
                    l.Add(new ShopItem { name = "Coin Pouch", desc = "+300 Paw Coins", icon = "ic_paw_coin", fish = 5, give = () => PawProgress.AddCoins(300) });
                    l.Add(new ShopItem { name = "Coin Stack", desc = "+800 Paw Coins", icon = "it_coin", fish = 12, give = () => PawProgress.AddCoins(800) });
                    l.Add(new ShopItem { name = "Coin Chest", desc = "+2000 Paw Coins", icon = "ic_coins", fish = 25, give = () => PawProgress.AddCoins(2000) });
                    break;
                case 2:   // golden fish (real money, test mode)
                    l.Add(new ShopItem { name = "Fish Snack", desc = "+20 Golden Fish", icon = "ic_fish_cookie", realPrice = "₹ 79", give = () => PawProgress.AddFish(20) });
                    l.Add(new ShopItem { name = "Fish Basket", desc = "+60 Golden Fish", icon = "it_catcookie", realPrice = "₹ 199", give = () => PawProgress.AddFish(60) });
                    l.Add(new ShopItem { name = "Fish Feast", desc = "+150 Golden Fish", icon = "it_catbox", realPrice = "₹ 449", give = () => PawProgress.AddFish(150) });
                    break;
                case 3:   // bundles
                    if (PawSave.BundleAvailable)
                        l.Add(new ShopItem { name = "Starter Bundle", desc = "Best value, once only", icon = "gift_box", realPrice = "₹ 249", open = () => ShowBundle(() => ShowShop(3, back)) });
                    l.Add(new ShopItem { name = "Picnic Pack", desc = "3 cookies + 2 yarn", icon = "ic_gift", fish = 3,
                        give = () => { GiveTreats(TreatKind.Fish, 3); GiveTreats(TreatKind.Yarn, 2); } });
                    l.Add(new ShopItem { name = "Lunch Box", desc = "2 tuna + 300 coins", icon = "it_snackbox", fish = 7,
                        give = () => { GiveTreats(TreatKind.Can, 2); PawProgress.AddCoins(300); } });
                    l.Add(new ShopItem { name = "Treasure Crate", desc = "+45 Fish, +1500 coins", icon = "ic_coins", realPrice = "₹ 149",
                        give = () => { PawProgress.AddFish(45); PawProgress.AddCoins(1500); } });
                    break;
            }
            return l;
        }

        static readonly string[] ShopTabs = { "Treats", "Coins", "Golden Fish", "Bundles" };

        public void ShowShop(int tab, Action back)
        {
            SetPlaying(false);
            reopen = () => ShowShop(tab, back);
            var s = NewScreen("Shop").transform;
            var bg = Backdrop(s, "bg_shop", 0f, 1f, false, true);
            // the market stall painting has an open counter: the goods sit on a parchment board in front of it
            var board = Card(s, "panel_plain", new Vector2(60, -60), new Vector2(1320, 790));
            StartCoroutine(SlideIn(board.rectTransform, new Vector2(0, -900), 0.05f));
            var b = board.transform;

            // tabs along the top of the board (positions in board space)
            for (int i = 0; i < ShopTabs.Length; i++)
            {
                int k = i;
                var t = Img(b, i == tab ? "btn_orange" : "btn_tan", Vector2.zero, 64);
                t.preserveAspect = false;
                t.rectTransform.sizeDelta = new Vector2(250, 64);
                Anchor(t.rectTransform, 0.14f + i * 0.24f, 0.885f);
                var tt = Txt(t.transform, ShopTabs[i], 28, i == tab ? Cream : Ink, new Vector2(0, 2), new Vector2(240, 60), TextAnchor.MiddleCenter, false,
                    i == tab ? (Color?)new Color(0.55f, 0.25f, 0.08f) : null);
                tt.font = fontBold;
                Squish(t);
                t.gameObject.AddComponent<Button>().onClick.AddListener(() => { Click(); ShowShop(k, back); });
                StartCoroutine(PopIn(t.rectTransform, 0.05f + i * 0.05f, 0.3f));
            }

            var items = ShopTab(tab, back);
            bool wide = items.Count > 3;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int col = i % 3, row = i / 3;
                var card = Card(b, "card", Vector2.zero, wide ? new Vector2(290, 160) : new Vector2(280, 320));
                Anchor(card.rectTransform, 0.2f + col * 0.3f, wide ? 0.6f - row * 0.31f : 0.43f);
                var c = card.transform;
                if (wide)
                {
                    // icon on the left, name / effect / price stacked on the right
                    var ic = Img(c, it.icon, new Vector2(-88, 4), 92);
                    
                    Txt(c, it.name, 25, Ink, new Vector2(48, 48), new Vector2(170, 34), TextAnchor.MiddleCenter).font = fontBold;
                    Txt(c, it.desc, 19, InkSoft, new Vector2(48, 14), new Vector2(170, 28), TextAnchor.MiddleCenter);
                    var pb = PriceButton(c, new Vector2(48, -38), it.coins, it.fish, it.realPrice, 164, 52);
                    pb.onClick.AddListener(() => BuyItem(it, tab, back));
                }
                else
                {
                    Txt(c, it.name, 30, Ink, new Vector2(0, 120), new Vector2(250, 44), TextAnchor.MiddleCenter).font = fontBold;
                    var ic = Img(c, it.icon, new Vector2(0, 28), 130);
                    
                    Txt(c, it.desc, 23, InkSoft, new Vector2(0, -68), new Vector2(250, 34), TextAnchor.MiddleCenter);
                    var pb = PriceButton(c, new Vector2(0, -118), it.coins, it.fish, it.realPrice, 200, 60);
                    pb.onClick.AddListener(() => BuyItem(it, tab, back));
                }
                StartCoroutine(PopIn(card.rectTransform, 0.15f + i * 0.07f, 0.35f));
            }

            CurrencyBar(s, 0.1f);
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -205), back);
            var st = Img(s, "cat_sit", Vector2.zero, 230);
            Anchor(st.rectTransform, 0f, 0f, new Vector2(150, 150));
            
        }

        void BuyItem(ShopItem it, int tab, Action back)
        {
            if (it.open != null) { Click(); it.open(); return; }
            if (it.canBuy != null && !it.canBuy()) { Toast(it.cantText); return; }
            if (it.realPrice != null)
            {
                Purchase(it.name, it.realPrice, () => { it.give(); Toast($"{it.name}: {it.desc}"); ShowShop(tab, back); });
                return;
            }
            if (!Afford(it.coins, it.fish)) { Toast(it.coins > 0 ? "Not enough Paw Coins" : "Not enough Golden Fish"); return; }
            Pay(it.coins, it.fish);
            it.give();
            PawAudio.Instance?.Success();
            Toast($"Bought {it.name}!");
            ShowShop(tab, back);
        }

        /// <summary>Coins + golden fish counters along the top right (used on shop-like screens).</summary>
        void CurrencyBar(Transform s, float delay)
        {
            var row = CounterRow(s, 170f);   // clear of the close button in the corner
            Counter(row, "ic_paw_coin", PawProgress.Coins, delay);
            Counter(row, "ic_fish_cookie", PawProgress.Fish, delay + 0.05f);
        }

        // ------------------------------------------------------------------ starter bundle
        static readonly Reward[] BundleContents =
        {
            // worth ~110 Golden Fish at pack prices for ₹249 (about 2.3 ₹ per fish vs 3 to 4 in the fish packs) + an exclusive cap
            Reward.Fish(80), Reward.Coins(2500), new Reward { kind = RewardKind.BagSlots, amount = 2 }, Reward.Cosmetic("hat_blue")
        };

        public void ShowBundle(Action back)
        {
            SetPlaying(false);
            reopen = () => ShowBundle(back);
            var s = NewScreen("Bundle").transform;
            Backdrop(s, "bg_bundle", 0f, 1f, false, true);
            Dim(s, 0.25f);

            // left: the gift pile, assembled from the parts
            var pile = new GameObject("Pile", typeof(RectTransform)).GetComponent<RectTransform>();
            pile.SetParent(s, false);
            pile.anchoredPosition = new Vector2(-430, -40);
            pile.sizeDelta = new Vector2(600, 600);
            var glow = Img(pile, "medal_paw", new Vector2(0, 20), 560);
            glow.color = new Color(1f, 1f, 1f, 0.18f);
            Anim(glow, PawUIAnim.Mode.Spin, 0.3f);
            var gift = Img(pile, "gift_box", new Vector2(0, -40), 360);
            Img(pile, "cap_blue", new Vector2(150, 110), 190);
            Img(pile, "it_can", new Vector2(-170, -120), 150);
            Img(pile, "it_fish", new Vector2(-180, 90), 150);
            Img(pile, "ic_coins", new Vector2(170, -150), 140);
            Img(pile, "ic_fish_cookie", new Vector2(10, 190), 120);
            StartCoroutine(PopIn(pile, 0.1f, 0.6f));
            var rib = Img(s, "rib_red", new Vector2(-470, 330), 150);
            rib.rectTransform.localRotation = Quaternion.Euler(0, 0, 8);
            Txt(rib.transform, "BEST VALUE", 48, Cream, new Vector2(0, 10), new Vector2(460, 70), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.12f, 0.08f)).font = fontBold;
            StartCoroutine(Unfurl(rib.rectTransform, 0.4f));

            // right: what's inside
            var panel = Card(s, "panel_plain", new Vector2(400, 0), new Vector2(980, 760));
            StartCoroutine(SlideIn(panel.rectTransform, new Vector2(800, 0), 0.15f));
            var p = panel.transform;
            var head = Img(p, "rib_cream", new Vector2(0, 345), 150);
            Txt(head.transform, "Starter Bundle", 34, Ink, new Vector2(0, 10), new Vector2(330, 60), TextAnchor.MiddleCenter).font = fontBold;
            for (int i = 0; i < BundleContents.Length; i++)
            {
                var r = BundleContents[i];
                float y = 195 - i * 78;
                Img(p, r.Icon, new Vector2(-280, y), 64);
                Txt(p, r.kind == RewardKind.Cosmetic ? "Delivery Cap" : r.kind == RewardKind.Treat ? PawGame.NameOf(r.treat) + "s" : r.kind == RewardKind.Coins ? "Paw Coins"
                    : r.kind == RewardKind.BagSlots ? "Bigger Bag" : "Golden Fish",
                    32, Ink, new Vector2(-30, y), new Vector2(380, 50), TextAnchor.MiddleLeft);
                Txt(p, r.kind == RewardKind.Cosmetic ? "(Exclusive)" : r.kind == RewardKind.BagSlots ? $"+{r.amount} slots" : $"x{r.amount}", 30, InkSoft, new Vector2(230, y), new Vector2(200, 50), TextAnchor.MiddleRight).font = fontBold;
            }
            var buy = PriceButton(p, new Vector2(0, -235), 0, 0, "₹ 249", 360, 96);
            Anim(buy, PawUIAnim.Mode.Pulse, 0.6f);
            buy.onClick.AddListener(() => Purchase("Starter Bundle", "₹ 249", () =>
            {
                foreach (var r in BundleContents) Grant(r);
                PawSave.BundleBought = true;
                PawSave.Equip("Hat", "hat_blue");
                Toast("Bundle unlocked! Delivery Cap equipped");
                back();
            }));
            var timerBg = Img(p, "btn_tan", new Vector2(0, -322), 64);
            timerBg.preserveAspect = false;
            timerBg.rectTransform.sizeDelta = new Vector2(330, 60);
            timerBg.color = new Color(0.4f, 0.31f, 0.25f);
            Img(timerBg.transform, "ic_clock", new Vector2(-120, 1), 44);
            var tt = Txt(timerBg.transform, "", 30, Cream, new Vector2(20, 2), new Vector2(240, 56), TextAnchor.MiddleCenter);
            tt.font = fontBold;
            StartCoroutine(Tick(tt, () => Countdown(PawSave.BundleEnd - DateTime.Now)));

            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -80), back);
            StartCoroutine(Confetti(s, 0.6f, 30));
        }
    }
}
