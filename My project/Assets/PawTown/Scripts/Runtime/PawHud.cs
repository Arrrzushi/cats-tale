using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Quest card, energy bar, bag + inventory slots, and a small toast line.</summary>
    public class PawHud : MonoBehaviour
    {
        [Header("Quest card")]
        public Text questTitle;
        public Text questSub;
        public RectTransform questCard;

        [Header("Energy")]
        public Image energyFill;     // Filled / Vertical / Bottom
        public Image energyTrack;

        [Header("Bag")]
        public Button bagButton;
        public GameObject inventoryPanel;
        public Image[] slotIcons;    // 5 icon images (inside the panel slots)
        public Button[] slotButtons;
        public Sprite fishSlot, canSlot, yarnSlot;
        public Text toast;

        float toastUntil, punch;
        PawGame game;
        PawOrders orders;
        PawTownDemoMover mover;

        void Start()
        {
            game = PawGame.Instance;
            orders = PawOrders.Instance;
            if (bagButton)
            {
                bagButton.onClick.AddListener(ToggleBag);
                // always on top of the joystick / camera drag zones, lifted clear of the screen edge, with a bigger tap area
                var bt = (RectTransform)bagButton.transform;
                bt.SetAsLastSibling();
                bt.anchoredPosition += new Vector2(0f, 26f);
                if (bagButton.targetGraphic) bagButton.targetGraphic.raycastPadding = new Vector4(-30f, -24f, -30f, -30f);
            }
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int k = i;
                slotButtons[i].onClick.AddListener(() => game?.UseItem(k));
            }
            if (inventoryPanel) inventoryPanel.SetActive(false);
            if (game != null)
            {
                game.Changed += RefreshBag;
                game.Penalized += (why, lost) => { Toast($"{why}  {lost}"); punch = 1f; };
                game.Eaten += kind => { Toast($"Yum! +{PawGame.EnergyOf(kind):0} energy"); punch = 1f; };
                game.Collected += (kind, stored) =>
                {
                    Toast(stored ? $"+1 {PawGame.NameOf(kind)} in your bag" : $"Yum! +{PawGame.EnergyOf(kind):0} energy");
                    punch = 1f;
                };
            }
            if (orders != null) orders.StageChanged += st =>
            {
                punch = 1f;
                if (st == PawOrders.Stage.Pickup || st == PawOrders.Stage.Deliver) Toast("Tip: tap the card to auto-walk there");
            };
            mover = FindAnyObjectByType<PawTownDemoMover>();
            // tap the quest card: the cat walks the route by itself (any stick input takes over again)
            var cardBtn = questCard ? questCard.GetComponent<Button>() : null;
            if (cardBtn && mover)
                cardBtn.onClick.AddListener(() =>
                {
                    if (orders == null || !orders.HasTarget) { PawAudio.Instance?.Click(); PawMenus.Instance?.ShowBoard(); return; }
                    bool on = !mover.AutoWalking;
                    mover.FollowOrderRoute(on);
                    PawAudio.Instance?.Click();
                    Toast(on ? "Auto-walking · touch the stick to take over" : "Auto-walk off");
                });
            RefreshBag();
            if (toast) toast.text = "";
        }

        void ToggleBag()
        {
            if (PawMenus.Instance != null) { PawAudio.Instance?.Click(); PawMenus.Instance.ShowBag(null); return; }
            if (!inventoryPanel) return;
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
            PawAudio.Instance?.Click();
        }

        Sprite lockSprite;

        void RefreshBag()
        {
            if (game == null) return;
            for (int i = 0; i < slotIcons.Length; i++)
            {
                bool has = i < game.bag.Count, locked = i >= game.bagSize;
                slotIcons[i].enabled = has || locked;
                if (slotButtons.Length > i) slotButtons[i].interactable = has;
                if (locked && !has)
                {
                    // slots you have not unlocked yet (Bigger Bag in the shop)
                    if (!lockSprite) lockSprite = Resources.Load<Sprite>("Screens/ic_lock");
                    slotIcons[i].sprite = lockSprite;
                    slotIcons[i].color = new Color(1f, 1f, 1f, 0.75f);
                    continue;
                }
                slotIcons[i].color = Color.white;
                if (has)
                {
                    var k = game.bag[i];
                    slotIcons[i].sprite = k == TreatKind.Fish ? fishSlot : k == TreatKind.Can ? canSlot : yarnSlot;
                }
            }
        }

        public void Toast(string s)
        {
            if (!toast) return;
            toast.text = s;
            toastUntil = Time.time + 2.2f;
        }

        void Update()
        {
            // quest card
            if (orders != null && questTitle)
            {
                bool none = !orders.HasTarget && orders.stage != PawOrders.Stage.Done;
                bool roam = none && game != null && game.FreeRoam;
                questTitle.text = roam ? "Free Roam" : none ? "Pick an order" : orders.Title;
                string sub = roam ? "Explore the town · tap here for a delivery" : none ? "Tap here to open the order board" : orders.Subtitle;
                if (orders.HasTarget) sub += $"  ·  {orders.DistanceToTarget:0} m";
                if (mover != null && mover.AutoWalking) sub += "  ·  auto";
                questSub.text = sub;
            }
            if (questCard)
            {
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 3f);
                questCard.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(punch * Mathf.PI));
            }

            // energy bar: green-yellow fill, flashes red when nearly empty
            if (game != null && energyFill)
            {
                energyFill.fillAmount = Mathf.Lerp(energyFill.fillAmount, game.Energy01, Time.deltaTime * 6f);
                bool low = game.Energy01 < 0.2f;
                energyFill.color = low ? Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), 0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : Color.white;
            }

            if (toast)
            {
                float a = Mathf.Clamp01((toastUntil - Time.time) / 0.4f);
                var c = toast.color; c.a = a; toast.color = c;
            }
        }
    }
}
