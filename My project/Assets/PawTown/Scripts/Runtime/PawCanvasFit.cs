using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// Makes a 1920x1080-designed canvas fit any phone or tablet: on screens wider than 16:9 it scales by height (the
    /// extra width is just more background), on squarer screens (4:3 tablets) it scales by width so nothing runs off
    /// the sides. Also publishes the device safe area (notches, rounded corners) in canvas units.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class PawCanvasFit : MonoBehaviour
    {
        public static Vector4 SafeInsets;   // left, bottom, right, top in canvas units (Menus canvas)
        public bool publishSafeArea;
        CanvasScaler sc;
        Vector2Int lastSize;
        Rect lastSafe;

        void Awake() { sc = GetComponent<CanvasScaler>(); Fit(); }
        void Update() { if (Screen.width != lastSize.x || Screen.height != lastSize.y || Screen.safeArea != lastSafe) Fit(); }

        void Fit()
        {
            lastSize = new Vector2Int(Screen.width, Screen.height);
            lastSafe = Screen.safeArea;
            if (!sc) return;
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            sc.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
            if (!publishSafeArea) return;
            float scale = aspect >= 16f / 9f ? Screen.height / 1080f : Screen.width / 1920f;
            var s = Screen.safeArea;
            SafeInsets = new Vector4(s.xMin, s.yMin, Screen.width - s.xMax, Screen.height - s.yMax) / Mathf.Max(0.001f, scale);
        }
    }
}
