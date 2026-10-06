using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// Progress bars without stretched art: the code drives an invisible Filled image's fillAmount as before, and this
    /// sizes a 9-sliced pill child to match, so the rounded ends keep their shape at any value and any screen size.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PawBarFill : MonoBehaviour
    {
        Image src;
        RectTransform pill;

        public static Image Make(Transform track, Sprite sprite, Color color, float inset)
        {
            var holder = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            holder.transform.SetParent(track, false);
            var hr = holder.rectTransform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(inset, inset); hr.offsetMax = new Vector2(-inset, -inset);
            holder.type = Image.Type.Filled;
            holder.fillMethod = Image.FillMethod.Horizontal;
            holder.color = new Color(1, 1, 1, 0);
            holder.raycastTarget = false;
            var p = new GameObject("Pill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            p.transform.SetParent(holder.transform, false);
            p.sprite = sprite; p.color = color; p.raycastTarget = false;
            if (sprite && sprite.border.x > 0) { p.type = Image.Type.Sliced; p.gameObject.AddComponent<PawPill>(); }
            var f = holder.gameObject.AddComponent<PawBarFill>();
            f.src = holder; f.pill = p.rectTransform;
            f.LateUpdate();
            return holder;
        }

        void LateUpdate()
        {
            if (!src) src = GetComponent<Image>();
            if (!pill) return;
            var r = ((RectTransform)transform).rect;
            float v = Mathf.Clamp01(src.fillAmount);
            pill.gameObject.SetActive(v > 0.001f);
            // never thinner than the bar is tall, so the round ends always fit
            float w = Mathf.Max(r.height, r.width * v);
            pill.anchorMin = new Vector2(0, 0); pill.anchorMax = new Vector2(0, 1);
            pill.pivot = new Vector2(0, 0.5f);
            pill.anchoredPosition = Vector2.zero;
            pill.sizeDelta = new Vector2(Mathf.Min(w, r.width), 0);
        }
    }
}
