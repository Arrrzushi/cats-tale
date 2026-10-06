using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// "How to Play": an open storybook. Each spread has a picture on the left page and the rules on the right page;
    /// the arrows (or a tap on a page edge) turn the page with a paper-flip animation.
    /// Art (in Resources/Screens): htp_book (open book), htp_p1..htp_p10 (page
    /// pictures). Until they exist the book uses the parchment panels and the game's own icons, so it always works.
    /// </summary>
    public partial class PawMenus
    {
        struct HowPage { public string title, art, fallback; public string[] lines; }

        static readonly HowPage[] HowPages =
        {
            new HowPage { title = "Meet Mochi!", art = "htp_p1", fallback = "cat_sit", lines = new[] {
                "Mochi is the town's fastest little courier cat.",
                "Pick up parcels, carry them across town and deliver them to the right door.",
                "Every delivery earns Paw Coins and XP. The more you level up, the bigger the town and the jobs get!" } },
            new HowPage { title = "Moving around", art = "htp_p2", fallback = "knob_paw", lines = new[] {
                "Drag the paw stick on the left to walk. Push it to the edge to run.",
                "Tap JUMP to hop over things.",
                "Drag on the right side to look around. Tap the quest card to walk there by yourself.",
                "Cat View follows right behind Mochi; Top View shows more of the town.",
#if UNITY_WEBGL || UNITY_EDITOR || UNITY_STANDALONE
                "Keyboard: WASD or arrows to walk, hold Shift to run, Space to jump, Q / E to turn the camera, V to switch view.",
#endif
            } },
            new HowPage { title = "Taking an order", art = "htp_p3", fallback = "panel_wood", lines = new[] {
                "Press START DELIVERY and choose an order on the board.",
                "Pick up: a little stack of parcels waits at the shop door under a glowing light.",
                "Deliver: walk to the house with the light. Big orders have 2, 3 or even 4 stops!",
                "VIP orders pay double." } },
            new HowPage { title = "Finding the way", art = "htp_p4", fallback = "map_frame", lines = new[] {
                "Follow the orange arrow on the ground. It shows the safe way along the sidewalks.",
                "The map in the corner turns with you: up is always straight ahead. The compass shows north.",
                "Your destination glows with a light you can see from far away.",
                "Tap the map to open the big map. The arrow can be switched off in Settings." } },
            new HowPage { title = "Energy", art = "htp_p5", fallback = "pill_energy", lines = new[] {
                "Walking uses a little energy. Running uses much more.",
                "Swimming is the most tiring of all, and jumping costs energy too.",
                "Standing still slowly refills it.",
                "More energy = faster paws! If it runs out, Mochi gets too sleepy to move." } },
            new HowPage { title = "Yummy treats", art = "htp_p6", fallback = "it_fish", lines = new[] {
                "Treats lie around town. Walk over them to put them in your bag.",
                "Fish Cookie +25 energy, Tuna Can +40, Yarn Ball +15.",
                "Tap a treat in your bag to eat it. Mochi meows happily every time!",
                "Your bag starts with 2 slots. It grows every 4 levels, or buy a Bigger Bag in the shop." } },
            new HowPage { title = "Earning rewards", art = "htp_p7", fallback = "ic_coins", lines = new[] {
                "Each delivery pays Paw Coins, plus a bonus for being quick and for not bumping into anything.",
                "Customers leave tips, and collected treats add a little extra.",
                "Daily rewards, missions and the Paw Pass give coins, Golden Fish and outfits.",
                "Level up for coins, Golden Fish every 3rd level, a bigger bag every 4th, and farther, better paid orders." } },
            new HowPage { title = "Road safety", art = "htp_p8", fallback = "ic_shield", lines = new[] {
                "Only cross roads at the zebra crossings!",
                "Walking on the road anywhere else costs 1 Golden Fish.",
                "Getting bumped by a car costs 1 Golden Fish too. Look both ways!",
                "No fish left? Then it costs 10 Paw Coins." } },
            new HowPage { title = "Splash! Swimming", art = "htp_p9", fallback = "trail_bubbles", lines = new[] {
                "Mochi can swim in the river, slowly and with a splash.",
                "Swimming drains energy very fast, so don't stay in too long.",
                "Jump, or keep walking into the bank, to climb out." } },
            new HowPage { title = "Look your best", art = "htp_p10", fallback = "tile_cat", lines = new[] {
                "Spend coins and Golden Fish in the Wardrobe on furs, hats, collars, faces and magic trails.",
                "Drag the mirror to spin Mochi all the way round.",
                "Leaving the town in the middle of a delivery cancels it, so finish your orders first. Happy delivering!" } },
        };

        int howPage;
        bool turning;

        public void ShowHowToPlay(Action back)
        {
            SetPlaying(false);
            back ??= ShowMenu;
            reopen = () => ShowHowToPlay(back);
            var s = NewScreen("HowToPlay").transform;
            Backdrop(s, "bg_office", 0f, 1.05f, false, true);
            Dim(s, 0.45f);

            // the open book
            bool hasBook = Resources.Load<Sprite>("Screens/htp_book") != null;
            var book = hasBook ? Img(s, "htp_book", new Vector2(0, -30), 860) : Card(s, "panel_plain", new Vector2(0, -20), new Vector2(1560, 880));
            StartCoroutine(PopIn(book.rectTransform, 0.05f, 0.45f));
            var b = book.rectTransform;
            float W = b.sizeDelta.x, H = b.sizeDelta.y;
            // the painted book's pages: left 13-45 %, right 53-88 % of the width, 12-80 % of the height from the top
            float pw = hasBook ? W * 0.32f : W * 0.42f, ph = hasBook ? H * 0.68f : H * 0.78f;
            float lx = hasBook ? -0.21f * W : -pw * 0.53f, rx = hasBook ? 0.205f * W : pw * 0.53f, py = hasBook ? 0.04f * H : 0f;
            if (!hasBook)   // a spine down the middle of the parchment
            {
                var spine = Img(book.transform, "card_well", Vector2.zero, ph);
                spine.preserveAspect = false; spine.rectTransform.sizeDelta = new Vector2(26, ph); spine.color = new Color(0.82f, 0.7f, 0.55f);
            }
            var left = new GameObject("LeftPage", typeof(RectTransform)).GetComponent<RectTransform>();
            left.SetParent(book.transform, false); left.anchoredPosition = new Vector2(lx, py); left.sizeDelta = new Vector2(pw, ph);
            var right = new GameObject("RightPage", typeof(RectTransform)).GetComponent<RectTransform>();
            right.SetParent(book.transform, false); right.anchoredPosition = new Vector2(rx, py); right.sizeDelta = new Vector2(pw, ph);

            FillPages(left, right, howPage);

            // ribbon title, page arrows, page number, close
            var rib = Img(s, "rib_orange", Vector2.zero, 130);
            Anchor(rib.rectTransform, 0.5f, 1f, new Vector2(0, -80));
            Txt(rib.transform, "HOW TO PLAY", 46, Cream, new Vector2(0, 22), new Vector2(420, 70), TextAnchor.MiddleCenter, false, new Color(0.5f, 0.22f, 0.06f)).font = fontBold;
            StartCoroutine(Unfurl(rib.rectTransform, 0.1f));

            var num = Txt(book.transform, "", 26, InkSoft, new Vector2(rx, py - ph * 0.47f), new Vector2(200, 40), TextAnchor.MiddleCenter);
            num.text = $"{howPage + 1} / {HowPages.Length}";
            for (int k = -1; k <= 1; k += 2)
            {
                int dir = k;
                var ar = Img(s, k < 0 ? "rb_arrow_left" : "rb_arrow", Vector2.zero, 110, true);
                Anchor(ar.rectTransform, k < 0 ? 0f : 1f, 0.5f, new Vector2(k < 0 ? 90 : -90, -20));
                ar.gameObject.AddComponent<Button>().onClick.AddListener(() => TurnPage(dir, book.transform, left, right, num));
            }
            CloseButton(s, new Vector2(1f, 1f), new Vector2(-80, -80), back);
        }

        void FillPages(RectTransform left, RectTransform right, int i)
        {
            foreach (Transform c in left) Destroy(c.gameObject);
            foreach (Transform c in right) Destroy(c.gameObject);
            var p = HowPages[i];
            float pw = left.sizeDelta.x, ph = left.sizeDelta.y;

            // left page: the picture (painted page art when it exists, otherwise a big game icon in a frame)
            bool art = Resources.Load<Sprite>("Screens/" + p.art) != null;
            if (art) Fit(Img(left, p.art, Vector2.zero, ph * 0.95f), pw * 0.98f, ph * 0.95f);
            else
            {
                var frame = Card(left, "card", new Vector2(0, 10), new Vector2(pw * 0.86f, ph * 0.8f));
                var sp = Resources.Load<Sprite>("Screens/" + p.fallback) ? p.fallback : "cat_face";
                Fit(Img(frame.transform, sp, Vector2.zero, ph * 0.5f), pw * 0.7f, ph * 0.6f);
            }

            // right page: title + rules, each line with a paw bullet
            var t = Txt(right, p.title, 46, Ink, new Vector2(0, ph * 0.4f), new Vector2(pw * 0.92f, 70), TextAnchor.MiddleCenter);
            t.font = fontBold;
            float y = ph * 0.26f, lineH = Mathf.Min(150f, (ph * 0.66f) / Mathf.Max(1, p.lines.Length));
            foreach (var line in p.lines)
            {
                Img(right, "paw_mid", new Vector2(-pw * 0.43f, y), 30);
                var l = Txt(right, line, 28, Ink, new Vector2(pw * 0.04f, y), new Vector2(pw * 0.86f, lineH - 6), TextAnchor.MiddleLeft);
                l.horizontalOverflow = HorizontalWrapMode.Wrap;
                l.resizeTextForBestFit = true; l.resizeTextMinSize = 18; l.resizeTextMaxSize = 28;   // long lines shrink instead of spilling
                y -= lineH;
            }
        }

        void TurnPage(int dir, Transform book, RectTransform left, RectTransform right, Text num)
        {
            if (turning) return;
            int next = howPage + dir;
            if (next < 0 || next >= HowPages.Length) { Click(); return; }
            // no page-flip animation: the arrows just change the spread
            PawAudio.Instance?.Click();
            howPage = next;
            FillPages(left, right, howPage);
            num.text = $"{howPage + 1} / {HowPages.Length}";
        }

        static float EaseInOut(float x) => x < 0.5f ? 2f * x * x : 1f - Mathf.Pow(-2f * x + 2f, 2f) / 2f;
    }
}
