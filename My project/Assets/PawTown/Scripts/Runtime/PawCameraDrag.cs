using UnityEngine;
using UnityEngine.EventSystems;

namespace PawTown
{
    /// <summary>
    /// Invisible touch area over the right side of the screen.
    ///  * Drag sideways: turn the camera around the cat.
    ///  * Tap the ground: the cat walks there by itself (along sidewalks, crossing at zebras). A little ring marks the spot.
    /// </summary>
    public class PawCameraDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        public float degreesPerScreenWidth = 240f;

        static float pendingYaw;
        public static float LastDragTime { get; private set; } = -100f;

        static readonly RaycastHit[] hits = new RaycastHit[16];
        GameObject marker;
        float markerUntil;

        public static float ConsumeYaw()
        {
            float y = pendingYaw;
            pendingYaw = 0f;
            return y;
        }

        public void OnBeginDrag(PointerEventData e) => LastDragTime = Time.time;

        public void OnDrag(PointerEventData e)
        {
            pendingYaw += e.delta.x / Mathf.Max(1f, Screen.width) * degreesPerScreenWidth;
            LastDragTime = Time.time;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            var cam = Camera.main;
            var mover = FindAnyObjectByType<PawTownDemoMover>();
            if (!cam || !mover) return;
            var ray = cam.ScreenPointToRay(e.position);
            int n = Physics.RaycastNonAlloc(ray, hits, 400f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = Vector3.zero;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (!(hits[i].collider is MeshCollider)) continue;      // ground only (roads, sidewalks, grass, bridges)
                if (hits[i].distance < best) { best = hits[i].distance; point = hits[i].point; found = true; }
            }
            if (!found) return;
            mover.WalkTo(point);
            PawAudio.Instance?.Click();
            ShowMarker(point);
        }

        void ShowMarker(Vector3 p)
        {
            if (marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "TapMarker";
                Destroy(marker.GetComponent<Collider>());
                var sh = Shader.Find("PawTown/Toon");
                if (sh != null)
                {
                    var m = new Material(sh);
                    m.SetColor("_BaseColor", new Color(1f, 0.6f, 0.25f));
                    m.SetColor("_EmissionColor", new Color(0.5f, 0.25f, 0.05f));
                    marker.GetComponent<Renderer>().sharedMaterial = m;
                }
                marker.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            marker.SetActive(true);
            marker.transform.position = p + Vector3.up * 0.03f;
            markerUntil = Time.time + 1.2f;
        }

        void Update()
        {
            if (marker == null || !marker.activeSelf) return;
            float t = markerUntil - Time.time;
            if (t <= 0f) { marker.SetActive(false); return; }
            float s = Mathf.Lerp(0.2f, 0.9f, Mathf.Clamp01(1f - t / 1.2f) * 3f) * (t < 0.3f ? t / 0.3f : 1f);
            marker.transform.localScale = new Vector3(s, 0.015f, s);
        }
    }
}
