using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// Floating thumb stick. Touch anywhere in the left part of the screen: the stick appears under the thumb, follows
    /// the thumb if it slides past the edge, and fades back to a faint hint when released. Push to the rim and hold
    /// for a moment to sprint (no run button needed). Value is -1..1 with a dead zone and a smooth response curve.
    /// </summary>
    public class PawJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public static PawJoystick Active { get; private set; }

        public RectTransform pad;      // the ring
        public RectTransform knob;     // the paw knob
        [Range(0f, 0.5f)] public float deadZone = 0.12f;
        [Tooltip("Knob travel as a fraction of the ring radius")] public float travel = 0.72f;
        public float sprintThreshold = 0.92f;
        public float sprintHold = 0.25f;
        [Range(0f, 1f)] public float idleAlpha = 0.35f;

        public Vector2 Value { get; private set; }
        public bool Held { get; private set; }
        /// <summary>Pushed to the rim for a moment: run.</summary>
        public bool Sprinting { get; private set; }

        Vector2 padHome;
        Canvas canvas;
        CanvasGroup group;
        Image knobImg;
        float rimTime, alpha;
        int pointerId = int.MinValue;

        void OnEnable() { Active = this; }
        void OnDisable() { if (Active == this) Active = null; Release(); }
        void OnApplicationPause(bool paused) { if (paused) Release(); }

        /// <summary>The idle spot moved (Settings > Edit Controls): rest there from now on.</summary>
        public void Rehome() { if (pad) padHome = pad.anchoredPosition; }

        void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            if (pad)
            {
                padHome = pad.anchoredPosition;
                group = pad.GetComponent<CanvasGroup>();
                if (group == null) group = pad.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.alpha = alpha = idleAlpha;
            }
            if (knob) knobImg = knob.GetComponent<Image>();
        }

        float Radius => pad ? pad.rect.width * 0.5f * travel : 100f;
        Camera Cam(PointerEventData e) => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? e.pressEventCamera : null;

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;                       // one thumb only; extra fingers are ignored
            pointerId = e.pointerId;
            Held = true;
            if (pad && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)pad.parent, e.position, Cam(e), out var lp))
                pad.anchoredPosition = lp;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!Held || e.pointerId != pointerId || !pad || !knob) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(pad, e.position, Cam(e), out var lp)) return;
            float r = Radius;
            // thumb ran past the rim: drag the whole stick along behind it
            if (lp.magnitude > r)
            {
                Vector2 excess = lp - lp.normalized * r;
                pad.anchoredPosition += excess;
                lp -= excess;
            }
            Vector2 v = lp / r;
            knob.anchoredPosition = v * r;
            float m = v.magnitude;
            float curved = m <= deadZone ? 0f : Mathf.SmoothStep(0f, 1f, (m - deadZone) / (1f - deadZone)) * 0.4f + (m - deadZone) / (1f - deadZone) * 0.6f;
            Value = m > 0.0001f ? v / m * Mathf.Clamp01(curved) : Vector2.zero;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            Release();
        }

        void Release()
        {
            Held = false;
            pointerId = int.MinValue;
            Value = Vector2.zero;
            Sprinting = false;
            rimTime = 0f;
            if (knob) knob.anchoredPosition = Vector2.zero;
            if (pad) pad.anchoredPosition = padHome;
        }

        void Update()
        {
            float m = Value.magnitude;
            rimTime = Held && m >= sprintThreshold ? rimTime + Time.unscaledDeltaTime : 0f;
            Sprinting = rimTime >= sprintHold;
            // visible while held, a faint hint when idle
            alpha = Mathf.MoveTowards(alpha, Held ? 1f : idleAlpha, Time.unscaledDeltaTime / (Held ? 0.08f : 0.3f));
            if (group) group.alpha = alpha;
            if (knobImg) knobImg.color = Color.Lerp(knobImg.color, Sprinting ? new Color(1f, 0.82f, 0.6f) : Color.white, Time.unscaledDeltaTime * 10f);
        }
    }
}
