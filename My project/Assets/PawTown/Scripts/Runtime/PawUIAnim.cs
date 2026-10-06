using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>Idle UI motion (unscaled time): gentle bob, breathing pulse, slow spin, ken-burns zoom, confetti flight,
    /// and a squishy press on buttons.</summary>
    public class PawUIAnim : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Mode { None, Bob, Pulse, Spin, KenBurns, Confetti, Wiggle }
        public Mode mode = Mode.None;
        public float amount = 1f, speed = 1f;
        public bool squish;

        // confetti
        public Vector2 velocity;
        public float spin, life = 2.4f;

        RectTransform rt;
        Vector2 basePos;
        Vector3 baseScale;
        float t0, pressK = 1f, pressTarget = 1f, phase;
        Graphic g;

        void Awake()
        {
            rt = (RectTransform)transform;
            g = GetComponent<Graphic>();
            phase = Random.value * 10f;
        }

        void OnEnable()
        {
            if (rt == null) rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
            baseScale = rt.localScale == Vector3.zero ? Vector3.one : rt.localScale;
            t0 = Time.unscaledTime;
        }

        /// <summary>Call after a tween moved/scaled the element so idle motion is relative to the new rest pose.</summary>
        public void Rebase() { basePos = rt.anchoredPosition; baseScale = rt.localScale; }

        void LateUpdate()
        {
            float t = Time.unscaledTime - t0, dt = Time.unscaledDeltaTime;
            pressK = Mathf.MoveTowards(pressK, pressTarget, dt * 6f);
            switch (mode)
            {
                case Mode.Bob:
                    rt.anchoredPosition = basePos + Vector2.up * Mathf.Sin((t + phase) * 1.4f) * 4f * Mathf.Min(amount, 1f);
                    rt.localScale = baseScale * pressK;
                    break;
                case Mode.Pulse:
                    // an occasional soft "breath" (one bump about every 2.6 s, then still), not a non-stop throb
                    rt.localScale = baseScale * (1f + 0.035f * Beat(t + phase * 0.1f, 2.6f)) * pressK;
                    break;
                case Mode.Spin:
                    rt.localRotation = Quaternion.Euler(0, 0, t * 20f * speed);
                    break;
                case Mode.Wiggle:
                    // a single little shake every few seconds instead of rocking back and forth forever
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 22f) * 3f * Beat(t + phase, 4.5f));
                    rt.localScale = baseScale * pressK;
                    break;
                case Mode.KenBurns:
                    rt.localScale = baseScale * (1.02f + 0.04f * amount * (0.5f + 0.5f * Mathf.Sin(t * 0.18f * speed)));
                    break;
                case Mode.Confetti:
                    velocity += Vector2.down * 900f * dt;
                    velocity *= 1f - 0.9f * dt;
                    rt.anchoredPosition += velocity * dt;
                    rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + spin * dt);
                    if (g) { var c = g.color; c.a = Mathf.Clamp01((life - t) / 0.6f); g.color = c; }
                    if (t > life) Destroy(gameObject);
                    break;
                default:
                    if (squish) rt.localScale = baseScale * pressK;
                    break;
            }
        }

        /// <summary>0 most of the time, a smooth 0-1-0 bump lasting 0.6 s once every 'period' seconds.</summary>
        static float Beat(float t, float period)
        {
            float k = Mathf.Repeat(t, period) / 0.6f;
            return k < 1f ? Mathf.Sin(k * Mathf.PI) : 0f;
        }

        public void OnPointerDown(PointerEventData e) { if (squish) pressTarget = 0.9f; }
        public void OnPointerUp(PointerEventData e) { pressTarget = 1f; }
        public void OnPointerExit(PointerEventData e) { pressTarget = 1f; }
    }
}
