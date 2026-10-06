using System;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Paw Pass: 10 tiers earned with deliveries, a free row and a premium row (unlocked by purchase).</summary>
    public partial class PawMenus
    {
        static readonly Reward[] PassFree =
        {
            Reward.Coins(50), Reward.Treat(TreatKind.Fish, 2), Reward.Treat(TreatKind.Yarn, 1), Reward.Coins(100), Reward.Treat(TreatKind.Can, 1),
            Reward.Fish(2), Reward.Coins(150), Reward.Treat(TreatKind.Can, 2), Reward.Fish(3), Reward.Coins(300)
        };
        static readonly Reward[] PassPremium =
        {
            Reward.Cosmetic("fur_siamese"), Reward.Fish(5), Reward.Cosmetic("trail_blossom"), Reward.Coins(300), Reward.Cosmetic("hat_crown"),
            Reward.Fish(10), Reward.Cosmetic("trail_clover"), Reward.Coins(600), Reward.Cosmetic("fur_tortie"), Reward.Fish(20)
        };

        bool PassClaimable()
        {
            for (int t = 0; t < PawSave.PassTier; t++)
            {
                if (!PawSave.PassClaimed(t, false)) return true;
                if (PawSave.PassPremium && !PawSave.PassClaimed(t, true)) return true;
            }
            return false;
        }

        public void ShowPass(Action back)
        {
            SetPlaying(false);
            reopen = () => ShowPass(back);
            var s = NewScreen("PawPass").transform;
            Backdrop(s, "bg_pass", 0f, 1f, true, true);
            // season banner (the spring street painting has open sky up top for it)
            var ban = Img(s, "rib_orange", Vector2.zero, 150);
            ban.preserveAspect = false;
            ban.rectTransform.sizeDelta = new Vector2(820, 150);
            Anchor(ban.rectTransform, 0.5f, 1f, new Vector2(0, -95));
            var bt = Txt(ban.transform, "PAW PASS · Spring Bakery", 46, Cream, new Vector2(0, 26), new Vector2(600, 70), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.22f, 0.06f));
            bt.font = fontBold;
            StartCoroutine(Unfurl(ban.rectTransform, 0.05f));

            // season timer under the banner (banner is part of the art, pinned to the top)
            var timer = Img(s, "btn_tan", Vector2.zero, 60);
            timer.preserveAspect = false;
            timer.rectTransform.sizeDelta = new Vector2(230, 58);
            timer.color = new Color(0.4f, 0.31f, 0.25f);
            Anchor(timer.rectTransform, 1f, 1f, new Vector2(-330, -80));
            Img(timer.transform, "ic_clock", new Vector2(-80, 1), 40);
            var tt = Txt(timer.transform, "", 28, Cream, new Vector2(18, 2), new Vector2(160, 54), TextAnchor.MiddleCenter);
            tt.font = fontBold;
            StartCoroutine(Tick(tt, () => Countdown(PawSave.SeasonEnd - DateTime.Now)));

            // track panel
            var panel = Card(s, "card", new Vector2(0, -118), new Vector2(1740, 560));
            StartCoroutine(SlideIn(panel.rectTransform, new Vector2(0, -900), 0.1f));
            var p = panel.transform;

            int tier = PawSave.PassTier;
            const float x0 = -600f, dx = 142f;
            // progress line + tier nodes
            float within = (PawSave.PassXp % PawSave.PassXpPerTier) / (float)PawSave.PassXpPerTier;
            var line = Bar(p, new Vector2(x0 + 4.5f * dx, 205), new Vector2(dx * 9 + 20, 22), 0f);
            float lineTo = tier >= PawSave.PassTiers ? 1f : Mathf.Clamp01((tier - 1 + within) / 9f);
            StartCoroutine(FillTo(line, Mathf.Max(0f, lineTo), 0.5f, 0.8f));
            for (int i = 0; i < PawSave.PassTiers; i++)
            {
                bool reached = i < tier;
                var node = Img(p, reached ? "ic_paw_coin" : "tile_paw", new Vector2(x0 + i * dx, 205), 74);
                Txt(node.transform, (i + 1).ToString(), 30, reached ? Cream : Ink, new Vector2(0, -2), new Vector2(60, 50), TextAnchor.MiddleCenter, false,
                    reached ? (Color?)new Color(0.55f, 0.3f, 0.08f) : null).font = fontBold;
                if (i == Mathf.Min(tier, PawSave.PassTiers - 1))
                {
                    var cat = Img(p, "cat_face", new Vector2(x0 + i * dx, 285), 92);
                    Anim(cat, PawUIAnim.Mode.Bob, 1f);
                }
            }

            // row labels
            var fl = Img(p, "medal_bluepaw", new Vector2(-770, 70), 104);
            Txt(fl.transform, "Free", 26, Ink, new Vector2(0, -66), new Vector2(150, 34), TextAnchor.MiddleCenter).font = fontBold;
            var pl = Img(p, "medal_crown", new Vector2(-770, -128), 104);
            Txt(pl.transform, "Premium", 26, Ink, new Vector2(0, -66), new Vector2(150, 34), TextAnchor.MiddleCenter).font = fontBold;

            for (int row = 0; row < 2; row++)
            {
                bool prem = row == 1;
                var rewards = prem ? PassPremium : PassFree;
                for (int i = 0; i < PawSave.PassTiers; i++)
                {
                    var r = rewards[i];
                    int t = i;
                    var tile = ItemTile(p, r.Icon, new Vector2(x0 + i * dx, prem ? -128 : 70), 128, r.Short.Length > 0 ? r.Short : null, 22);
                    if (prem) tile.color = new Color(1f, 0.93f, 0.7f);
                    bool claimed = PawSave.PassClaimed(i, prem);
                    bool reached = i < tier;
                    bool locked = prem && !PawSave.PassPremium;
                    if (claimed) Img(tile.transform, "ic_check", new Vector2(40, 40), 46);
                    else if (locked) Img(tile.transform, "ic_lock", new Vector2(42, 40), 42);
                    else if (reached)
                    {
                        
                        var chip = TextButton(tile.transform, "btn_teal", "CLAIM", new Vector2(0, -84), 42, 116, null, 21);
                        chip.onClick.AddListener(() =>
                        {
                            PawSave.PassClaim(t, prem);
                            PawAudio.Instance?.Success();
                            Toast(Grant(r));
                            ShowPass(back);
                        });
                    }
                    if (!reached && !claimed) tile.color = prem ? new Color(0.92f, 0.86f, 0.7f) : new Color(0.9f, 0.88f, 0.84f);
                    StartCoroutine(PopIn(tile.rectTransform, 0.25f + i * 0.03f + row * 0.1f, 0.3f));
                }
            }

            // bottom: unlock or progress hint
            if (!PawSave.PassPremium)
            {
                var unlock = Img(s, "btn_orange", new Vector2(0, -435), 104);
                unlock.preserveAspect = false;
                unlock.rectTransform.sizeDelta = new Vector2(500, 104);
                Img(unlock.transform, "it_crown", new Vector2(-190, 4), 70);
                Txt(unlock.transform, "VIP · ₹299/month", 36, Cream, new Vector2(40, 4), new Vector2(380, 80), TextAnchor.MiddleCenter, false, new Color(0.55f, 0.25f, 0.08f)).font = fontBold;
                Anim(unlock, PawUIAnim.Mode.Pulse, 0.6f);
                Squish(unlock);
                unlock.gameObject.AddComponent<Button>().onClick.AddListener(() => Purchase("Paw Pass VIP (monthly)", "₹ 299 / month", () =>
                {
                    PawSave.PassPremium = true;
                    StartCoroutine(Confetti(root, 0f, 60));
                    Toast("Paw Pass VIP active for 30 days! Claim your premium rewards");
                    ShowPass(back);
                }));
                StartCoroutine(PopIn(unlock.rectTransform, 0.6f, 0.4f));
            }
            if (PawSave.PassPremium)
            {
                var vip = Txt(s, $"VIP active until {PawSave.PassPremiumUntil:d MMM}", 28, Cream, Vector2.zero, new Vector2(600, 44), TextAnchor.MiddleCenter, false, Ink);
                vip.font = fontBold;
                Anchor(vip.rectTransform, 0.5f, 0f, new Vector2(0, 130));
            }
            int toNext = tier >= PawSave.PassTiers ? 0 : PawSave.PassXpPerTier - PawSave.PassXp % PawSave.PassXpPerTier;
            var hint = Txt(s, tier >= PawSave.PassTiers ? "Season complete!" : $"Deliver orders to earn Pass XP · {toNext} XP to tier {tier + 1}", 30, Cream,
                new Vector2(0, 0), new Vector2(1200, 44), TextAnchor.MiddleCenter, false, Ink);
            Anchor(hint.rectTransform, 0.5f, 0f, new Vector2(0, PawSave.PassPremium ? 80 : 20));

            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -80), back);
        }
    }
}
