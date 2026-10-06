using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Keeps a 9-sliced pill button's round end caps round: scales the slice so each cap is half the button tall.</summary>
    [RequireComponent(typeof(Image))]
    public class PawPill : MonoBehaviour
    {
        Image img;

        void OnEnable() { Fit(); }
        void OnRectTransformDimensionsChange() { Fit(); }

        bool fitting;

        void Fit()
        {
            if (fitting) return;   // the multiplier change can re-trigger a layout pass; never recurse
            if (!img) img = GetComponent<Image>();
            if (!img || !img.sprite) return;
            float h = ((RectTransform)transform).rect.height;
            if (h < 1f) return;
            float m = Mathf.Max(0.01f, img.sprite.rect.height / h);
            if (Mathf.Abs(img.pixelsPerUnitMultiplier - m) < 0.001f) return;
            fitting = true;
            img.pixelsPerUnitMultiplier = m;
            fitting = false;
        }
    }
}
