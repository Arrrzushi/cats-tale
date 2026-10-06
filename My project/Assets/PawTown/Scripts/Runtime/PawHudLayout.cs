using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// Player-editable HUD: every control (joystick, jump, run, bag, camera view, sound, pause, quest card, energy,
    /// map) can be dragged to a new spot and made bigger or smaller. Saved per control in PlayerPrefs
    /// ("PawTown.Hud.&lt;name&gt;" = x,y,scale). Settings > Edit Controls opens the editor.
    /// </summary>
    public static class PawHudLayout
    {
        public static readonly string[] Editable =
            { "JoystickZone/Pad", "JumpButton", "RunButton", "BagButton", "ViewButton", "SoundButton", "PauseButton", "QuestCard", "EnergyTrack", "MiniMap" };
        public static readonly string[] Labels =
            { "Joystick", "Jump", "Run", "Bag", "Camera", "Sound", "Pause", "Quest", "Energy", "Map" };

        static readonly Dictionary<string, (Vector2 pos, Vector3 scale)> defaults = new Dictionary<string, (Vector2, Vector3)>();
        const string K = "PawTown.Hud.";

        public static RectTransform Find(Canvas hud, string path) => hud ? hud.transform.Find(path) as RectTransform : null;

        /// <summary>Remember the built-in layout, then apply the player's saved one.</summary>
        public static void Apply(Canvas hud)
        {
            foreach (var n in Editable)
            {
                var r = Find(hud, n);
                if (!r) continue;
                if (!defaults.ContainsKey(n)) defaults[n] = (r.anchoredPosition, r.localScale);
                string v = PlayerPrefs.GetString(K + n, "");
                var parts = v.Split(',');
                if (parts.Length == 3 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s))
                {
                    r.anchoredPosition = new Vector2(x, y);
                    r.localScale = Vector3.one * s;
                }
            }
            UnityEngine.Object.FindAnyObjectByType<PawJoystick>()?.Rehome();
        }

        public static void Save(Canvas hud)
        {
            foreach (var n in Editable)
            {
                var r = Find(hud, n);
                if (!r) continue;
                var c = System.Globalization.CultureInfo.InvariantCulture;
                PlayerPrefs.SetString(K + n, $"{r.anchoredPosition.x.ToString(c)},{r.anchoredPosition.y.ToString(c)},{r.localScale.x.ToString(c)}");
            }
            PlayerPrefs.Save();
            UnityEngine.Object.FindAnyObjectByType<PawJoystick>()?.Rehome();
        }

        public static void ResetAll(Canvas hud)
        {
            foreach (var n in Editable)
            {
                PlayerPrefs.DeleteKey(K + n);
                var r = Find(hud, n);
                if (r && defaults.TryGetValue(n, out var d)) { r.anchoredPosition = d.pos; r.localScale = d.scale; }
            }
            PlayerPrefs.Save();
            UnityEngine.Object.FindAnyObjectByType<PawJoystick>()?.Rehome();
        }
    }

    /// <summary>The on-screen editor: a dim layer over the HUD with a dashed handle on every control. Drag a handle
    /// to move it; the selected one can be resized with - / +. DONE saves, RESET restores the default layout.</summary>
    public class PawHudEditor : MonoBehaviour
    {
        Canvas hud;
        Action onClose;
        RectTransform overlay;
        readonly List<(RectTransform target, RectTransform handle, Text label)> items = new List<(RectTransform, RectTransform, Text)>();
        int selected = -1;
        Font font;

        public static PawHudEditor Open(Canvas hud, Font font, Action onClose)
        {
            var go = new GameObject("HudEditor", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            var ed = go.AddComponent<PawHudEditor>();
            ed.hud = hud; ed.onClose = onClose; ed.font = font;
            ed.Build();
            return ed;
        }

        void Build()
        {
            overlay = (RectTransform)transform;
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.SetAsLastSibling();
            var dim = gameObject.AddComponent<Image>();
            dim.color = new Color(0.1f, 0.07f, 0.05f, 0.45f);   // also blocks taps reaching the game

            for (int i = 0; i < PawHudLayout.Editable.Length; i++)
            {
                var t = PawHudLayout.Find(hud, PawHudLayout.Editable[i]);
                if (!t || !t.gameObject.activeInHierarchy) continue;
                var h = new GameObject("Handle_" + PawHudLayout.Labels[i], typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<RectTransform>();
                h.SetParent(overlay, false);
                var img = h.GetComponent<Image>(); img.color = new Color(1f, 0.85f, 0.4f, 0.18f);
                var ol = h.GetComponent<Outline>(); ol.effectColor = new Color(1f, 0.8f, 0.3f, 0.95f); ol.effectDistance = new Vector2(3, -3);
                var drag = h.gameObject.AddComponent<Drag>(); drag.ed = this; drag.index = items.Count;
                var lab = MakeText(h, PawHudLayout.Labels[i], 26, new Vector2(0, 0));
                items.Add((t, h, lab));
            }

            // toolbar: hint, - / + for the selected control, RESET, DONE
            var bar = new GameObject("Toolbar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bar.SetParent(overlay, false);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0, -20); bar.sizeDelta = new Vector2(1180, 110);
            var bi = bar.GetComponent<Image>(); bi.sprite = Resources.Load<Sprite>("Screens/btn_tan"); bi.type = Image.Type.Sliced; bi.color = new Color(1f, 0.97f, 0.9f);
            MakeText(bar, "Drag a control to move it  ·  - / + to resize", 26, new Vector2(-260, 0)).alignment = TextAnchor.MiddleCenter;
            Button(bar, "-", new Vector2(150, 0), 90, () => Resize(-0.1f));
            Button(bar, "+", new Vector2(250, 0), 90, () => Resize(+0.1f));
            Button(bar, "RESET", new Vector2(380, 0), 150, () => { PawHudLayout.ResetAll(hud); PawAudio.Instance?.Click(); });
            Button(bar, "DONE", new Vector2(510, 0), 130, Close);
        }

        Text MakeText(RectTransform parent, string s, int size, Vector2 pos)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.text = s; t.color = new Color(0.29f, 0.2f, 0.15f);
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = new Vector2(700, 60);
            return t;
        }

        void Button(RectTransform parent, string label, Vector2 pos, float w, Action a)
        {
            var b = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            b.SetParent(parent, false);
            b.anchoredPosition = pos; b.sizeDelta = new Vector2(w, 76);
            var img = b.GetComponent<Image>();
            img.sprite = Resources.Load<Sprite>(label == "DONE" ? "Screens/btn_orange" : "Screens/btn_teal"); img.type = Image.Type.Sliced;
            var t = MakeText(b, label, 30, Vector2.zero); t.color = Color.white; t.rectTransform.sizeDelta = new Vector2(w, 70);
            b.GetComponent<Button>().onClick.AddListener(() => a());
        }

        void Resize(float d)
        {
            PawAudio.Instance?.Click();
            if (selected < 0 || selected >= items.Count) return;
            var t = items[selected].target;
            float s = Mathf.Clamp(t.localScale.x + d, 0.6f, 1.8f);
            t.localScale = Vector3.one * s;
        }

        void Close()
        {
            PawHudLayout.Save(hud);
            PawAudio.Instance?.Success();
            var cb = onClose;
            Destroy(gameObject);
            cb?.Invoke();
        }

        void LateUpdate()
        {
            // keep every handle exactly over its control (in the overlay's space)
            var corners = new Vector3[4];
            for (int i = 0; i < items.Count; i++)
            {
                var (t, h, lab) = items[i];
                if (!t) continue;
                t.GetWorldCorners(corners);
                Vector3 a = overlay.InverseTransformPoint(corners[0]), b = overlay.InverseTransformPoint(corners[2]);
                h.anchorMin = h.anchorMax = new Vector2(0.5f, 0.5f);
                h.anchoredPosition = (a + b) * 0.5f;
                h.sizeDelta = new Vector2(Mathf.Max(80f, Mathf.Abs(b.x - a.x)), Mathf.Max(80f, Mathf.Abs(b.y - a.y)));
                h.GetComponent<Image>().color = i == selected ? new Color(1f, 0.8f, 0.3f, 0.4f) : new Color(1f, 0.85f, 0.4f, 0.18f);
            }
        }

        class Drag : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public PawHudEditor ed; public int index;
            public void OnPointerDown(PointerEventData e) { ed.selected = index; }
            public void OnDrag(PointerEventData e)
            {
                var t = ed.items[index].target;
                var cv = ed.hud;
                float k = cv.scaleFactor > 0f ? cv.scaleFactor : 1f;
                t.anchoredPosition += e.delta / k;
            }
        }
    }
}
