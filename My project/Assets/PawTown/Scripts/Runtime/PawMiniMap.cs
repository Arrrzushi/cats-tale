using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>
    /// Cat-head minimap: a top-down camera renders into the frame, turned with the game camera so "up" on the map is
    /// always "ahead" on screen (heading-up, like a car GPS); a live compass needle shows where north is and an arrow
    /// in front of the cat marker shows which way she faces. The order target, treats, shops and the road route to the
    /// target are drawn on top. Tap to maximise / tap again (or the dim backdrop) to shrink.
    /// </summary>
    public class PawMiniMap : MonoBehaviour, IPointerClickHandler
    {
        public Transform pet;
        public RawImage mapImage;          // sits under the oval mask
        public RectTransform overlay;      // markers + route live here (same rect as mapImage, also masked)
        public RectTransform widget;       // the whole map (frame + mask) - this is what grows when maximised
        public GameObject backdrop;        // dim panel behind the big map

        [Header("Icons")]
        public Sprite petIcon, targetIcon, fishIcon, canIcon, yarnIcon;

        [Header("Look")]
        public Color routeColor = new Color(1f, 0.55f, 0.18f);
        public Color routeInk = new Color(0.25f, 0.16f, 0.12f);
        public float routeWidth = 7f;
        public float miniOrtho = 34f, bigOrtho = 60f;
        public Vector2 bigSize = new Vector2(1260f, 990f);

        Camera cam;
        PawTownGenerator gen;
        RenderTexture rt;
        bool big;
        Vector2 miniPos, miniSize, miniMin, miniMax, miniPivot;
        float anim = 0f;
        RectTransform petMark, targetMark, inkLayer, lineLayer, needle, heading;
        float mapYaw, nextDraw;
        Vector2 area;
        readonly List<RectTransform> treatMarks = new List<RectTransform>();
        readonly List<Image> segs = new List<Image>(), segInk = new List<Image>();

        void Start()
        {
            var go = new GameObject("MapCamera");
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = miniOrtho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.7f, 0.3f);
            cam.nearClipPlane = 70f;     // camera sits 100 m up: only draw the town, not the clouds above it
            cam.farClipPlane = 140f;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // looking down; turned to the game camera's yaw every frame
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            var size = mapImage.rectTransform.rect.size;
            rt = new RenderTexture(512, Mathf.Max(64, Mathf.RoundToInt(512 * size.y / Mathf.Max(1f, size.x))), 16);
            rt.name = "MiniMapRT";
            cam.targetTexture = rt;
            cam.enabled = false;            // rendered on demand: 15x a second while small, every frame when the map is open
            mapImage.texture = rt;

            miniPos = widget.anchoredPosition; miniSize = widget.sizeDelta;
            miniMin = widget.anchorMin; miniMax = widget.anchorMax; miniPivot = widget.pivot;
            if (backdrop)
            {
                backdrop.SetActive(false);
                var b = backdrop.GetComponent<Button>();
                if (b == null) b = backdrop.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (PawMenus.Instance != null) PawMenus.Instance.CloseMap(); else Toggle(); });   // tap outside to close
            }

            inkLayer = Layer("RouteInk");
            lineLayer = Layer("Route");
            targetMark = Marker("Target", targetIcon, 46f);
            petMark = Marker("Pet", petIcon, 44f);

            // heading arrow just in front of the cat marker (a pivot at the marker centre that we spin)
            heading = new GameObject("Heading", typeof(RectTransform)).GetComponent<RectTransform>();
            heading.SetParent(petMark, false);
            heading.sizeDelta = Vector2.zero;
            var ar = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ar.transform.SetParent(heading, false);
            ar.sprite = Resources.Load<Sprite>("Screens/map_heading");
            ar.raycastTarget = false; ar.preserveAspect = true;
            ar.rectTransform.sizeDelta = new Vector2(26f, 26f);
            ar.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            heading.SetAsFirstSibling();

            // live compass on the frame (where the old painted one sat), scales with the widget when maximised
            FrameImage("Compass", "Screens/map_compass");
            needle = FrameImage("Needle", "Screens/map_needle");
        }

        RectTransform FrameImage(string n, string sprite)
        {
            var img = new GameObject(n, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(widget, false);
            img.sprite = Resources.Load<Sprite>(sprite);
            img.raycastTarget = false; img.preserveAspect = true;
            var r = img.rectTransform;
            r.anchorMin = new Vector2(0.791f - 0.073f, 0.630f - 0.093f);
            r.anchorMax = new Vector2(0.791f + 0.073f, 0.630f + 0.093f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            r.SetAsLastSibling();
            return r;
        }

        void OnDestroy() { if (rt) rt.Release(); if (cam) Destroy(cam.gameObject); }

        RectTransform Layer(string n)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(overlay, false);
            var r = (RectTransform)g.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.zero;
            return r;
        }

        RectTransform Marker(string n, Sprite s, float size)
        {
            var g = new GameObject(n, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(overlay, false);
            var img = g.GetComponent<Image>();
            img.sprite = s; img.preserveAspect = true; img.raycastTarget = false;
            var r = (RectTransform)g.transform;
            r.sizeDelta = new Vector2(size, size);
            return r;
        }

        public void OnPointerClick(PointerEventData e)
        {
            var menus = PawMenus.Instance;
            if (menus == null) { Toggle(); return; }
            if (menus.MapOpen) menus.CloseMap(); else { PawAudio.Instance?.Click(); menus.ShowMap(null); }
        }

        public void Toggle() { SetBig(!big); PawAudio.Instance?.Click(); }

        public void SetBig(bool on)
        {
            big = on;
            if (backdrop) backdrop.SetActive(big);
        }

        // legend layers: 0 order (route + target), 1 treats, 2 shops
        readonly bool[] layers = { true, true, true };
        public bool LayerOn(int i) => layers[i];
        public void SetLayer(int i, bool on) => layers[i] = on;
        public Sprite shopIcon;
        readonly List<RectTransform> shopMarks = new List<RectTransform>();
        float nextShopScan;
        readonly List<Transform> shops = new List<Transform>();
        readonly Dictionary<Transform, List<Transform>> shopCache = new Dictionary<Transform, List<Transform>>();

        void LateUpdate()
        {
            if (!pet || !cam) return;
            // grow / shrink (smooth)
            anim = Mathf.MoveTowards(anim, big ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            float k = Mathf.SmoothStep(0f, 1f, anim);
            widget.anchorMin = Vector2.Lerp(miniMin, new Vector2(0.5f, 0.5f), k);
            widget.anchorMax = Vector2.Lerp(miniMax, new Vector2(0.5f, 0.5f), k);
            widget.pivot = Vector2.Lerp(miniPivot, new Vector2(0.5f, 0.5f), k);
            widget.anchoredPosition = Vector2.Lerp(miniPos, Vector2.zero, k);
            widget.sizeDelta = Vector2.Lerp(miniSize, bigSize, k);
            cam.orthographicSize = Mathf.Lerp(miniOrtho, bigOrtho, k);
            float iconScale = Mathf.Lerp(1f, 1.4f, k);

            // the camera (and so the markers) only move on frames the map is redrawn, so they always line up
            bool redraw = big || anim > 0f || Time.unscaledTime >= nextDraw;
            if (redraw)
            {
            nextDraw = Time.unscaledTime + 1f / 15f;
            cam.transform.position = new Vector3(pet.position.x, pet.position.y + 100f, pet.position.z);
            // heading-up: the map turns with the game camera, so straight ahead on screen is straight up on the map
            var view = Camera.main;
            float want = view ? view.transform.eulerAngles.y : 0f;
            mapYaw = Mathf.LerpAngle(mapYaw, want, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            cam.transform.rotation = Quaternion.Euler(90f, mapYaw, 0f);
            cam.Render();
            }
            if (needle) needle.localRotation = Quaternion.Euler(0f, 0f, mapYaw);                                   // keeps pointing at world north
            if (heading) heading.localRotation = Quaternion.Euler(0f, 0f, -Mathf.DeltaAngle(mapYaw, pet.eulerAngles.y));   // the way the cat faces

            // keep the town streamed out to the edge of whatever the map shows, so it never runs off the end
            if (gen == null) gen = FindAnyObjectByType<PawTownGenerator>();
            if (gen != null && gen.library != null)
            {
                float halfW = cam.orthographicSize * Mathf.Max(1f, cam.aspect);
                int need = Mathf.CeilToInt((halfW + gen.library.tileSize * 0.5f) / gen.library.tileSize);
                gen.EnsureReach(need);
            }

            area = overlay.rect.size;

            petMark.anchoredPosition = ToMap(pet.position);
            petMark.localScale = Vector3.one * iconScale;
            petMark.SetAsLastSibling();

            var orders = PawOrders.Instance;
            bool hasT = orders != null && orders.HasTarget && layers[0];
            targetMark.gameObject.SetActive(hasT);
            if (hasT)
            {
                Vector2 m = ToMap(orders.TargetPos);
                // off the edge of the small map: pin it to the rim so you still know which way to go
                Vector2 half = area * 0.5f - new Vector2(26f, 26f);
                float s = Mathf.Max(Mathf.Abs(m.x) / half.x, Mathf.Abs(m.y) / half.y);
                if (s > 1f) m /= s;
                targetMark.anchoredPosition = m;
                targetMark.localScale = Vector3.one * iconScale * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
            }

            // route
            int used = 0;
            if (hasT && orders.route.Count > 1)
            {
                var pts = orders.route;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = i == 0 ? ToMap(pet.position) : ToMap(pts[i]);
                    Vector2 b = ToMap(pts[i + 1]);
                    if (!Inside(a) && !Inside(b) && Vector2.Distance(a, b) < area.magnitude) continue;
                    Segment(used++, a, b);
                }
            }
            for (int i = used; i < segs.Count; i++) { segs[i].enabled = false; segInk[i].enabled = false; }

            // treats
            var game = PawGame.Instance;
            int tm = 0;
            if (game != null && layers[1])
            {
                foreach (var t in game.treats)
                {
                    Vector2 m = ToMap(t.basePos);
                    if (!Inside(m)) continue;
                    if (tm >= treatMarks.Count) treatMarks.Add(Marker("Treat", fishIcon, 28f));
                    var r = treatMarks[tm++];
                    r.gameObject.SetActive(true);
                    r.GetComponent<Image>().sprite = t.kind == TreatKind.Fish ? fishIcon : t.kind == TreatKind.Can ? canIcon : yarnIcon;
                    r.anchoredPosition = m;
                    r.localScale = Vector3.one * iconScale;
                }
            }
            for (int i = tm; i < treatMarks.Count; i++) treatMarks[i].gameObject.SetActive(false);

            // shops (named buildings), rescanned twice a second as tiles stream in and out
            if (Time.unscaledTime > nextShopScan)
            {
                nextShopScan = Time.unscaledTime + 0.5f;
                shops.Clear();
                if (gen)
                    foreach (Transform tile in gen.transform)
                    {
                        if (!tile.gameObject.activeInHierarchy) continue;
                        if (!shopCache.TryGetValue(tile, out var found))
                        {
                            shopCache[tile] = found = new List<Transform>();
                            foreach (var tr in tile.GetComponentsInChildren<Transform>())
                                if (tr.name.StartsWith("Shop_") || tr.name.StartsWith("HQ_")) found.Add(tr);
                        }
                        shops.AddRange(found);
                    }
            }
            int sm = 0;
            if (layers[2] && shopIcon)
                foreach (var tr in shops)
                {
                    if (!tr) continue;
                    Vector2 m = ToMap(tr.position);
                    if (!Inside(m)) continue;
                    if (sm >= shopMarks.Count) shopMarks.Add(Marker("Shop", shopIcon, 40f));
                    var r = shopMarks[sm++];
                    r.gameObject.SetActive(true);
                    r.anchoredPosition = m;
                    r.localScale = Vector3.one * iconScale;
                }
            for (int i = sm; i < shopMarks.Count; i++) shopMarks[i].gameObject.SetActive(false);
            targetMark.SetAsLastSibling();
            petMark.SetAsLastSibling();
        }

        Vector2 ToMap(Vector3 w)
        {
            Vector3 vp = cam.WorldToViewportPoint(w);
            return new Vector2((vp.x - 0.5f) * area.x, (vp.y - 0.5f) * area.y);
        }

        bool Inside(Vector2 m) => Mathf.Abs(m.x) < area.x * 0.5f + 20f && Mathf.Abs(m.y) < area.y * 0.5f + 20f;

        void Segment(int i, Vector2 a, Vector2 b)
        {
            while (segs.Count <= i)
            {
                segInk.Add(Line(inkLayer, routeInk));
                segs.Add(Line(lineLayer, routeColor));
            }
            Place(segInk[i], a, b, routeWidth + 5f);
            Place(segs[i], a, b, routeWidth);
        }

        Image Line(RectTransform layer, Color c)
        {
            var g = new GameObject("Seg", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(layer, false);
            var img = g.GetComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static void Place(Image img, Vector2 a, Vector2 b, float w)
        {
            img.enabled = true;
            var r = img.rectTransform;
            Vector2 d = b - a;
            r.anchoredPosition = (a + b) * 0.5f;
            r.sizeDelta = new Vector2(d.magnitude + w, w);   // + w: rounded-looking overlap at the corners
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }
}
