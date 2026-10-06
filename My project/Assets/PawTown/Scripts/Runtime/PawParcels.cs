using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// The parcels you actually deliver (models in Resources/Parcels):
    /// a little stack waits at the pickup door (one box per stop), the cat carries one on her back while delivering,
    /// and each delivery leaves a box on the doorstep for a few seconds.
    /// </summary>
    public class PawParcels : MonoBehaviour
    {
        static readonly string[] Shapes = { "parcel_cube", "parcel_long", "parcel_tall", "parcel_flat", "parcel_heart", "parcel_tube" };
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        PawOrders orders;
        GameObject stack, carried;
        readonly List<(GameObject g, float until)> dropped = new List<(GameObject, float)>();
        PawOrders.Stage lastStage = PawOrders.Stage.Waiting;
        int lastStop = -1;
        PawOrders.Offer lastOffer;
        Material outline;

        void Start()
        {
            orders = GetComponent<PawOrders>();
            if (orders) orders.StageChanged += OnStage;
        }

        void OnDestroy() { if (orders) orders.StageChanged -= OnStage; }

        void OnStage(PawOrders.Stage s)
        {
            var o = orders.Current;
            if (s == PawOrders.Stage.Pickup && o != null) ShowStack(o);
            if (s == PawOrders.Stage.Deliver)
            {
                if (lastStage == PawOrders.Stage.Pickup) { Clear(ref stack); Carry(o); }
                else if (lastStage == PawOrders.Stage.Deliver && lastStop >= 0) DropAtDoor(lastOffer, lastStop);
            }
            if (s == PawOrders.Stage.Done)
            {
                if (lastStage == PawOrders.Stage.Deliver) DropAtDoor(lastOffer, lastStop);
                Clear(ref carried);
            }
            if (s == PawOrders.Stage.Waiting) { Clear(ref stack); Clear(ref carried); }
            lastStage = s;
            lastOffer = o ?? lastOffer;
            lastStop = orders.StopIndex;
        }

        void Update()
        {
            // a cancelled order (free roam) clears everything
            if (orders && !orders.HasTarget && orders.stage == PawOrders.Stage.Waiting && (stack || carried)) { Clear(ref stack); Clear(ref carried); lastStage = PawOrders.Stage.Waiting; }
            // the stack bobs a little so it reads as "pick me up"
            if (stack) stack.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 3f));
            for (int i = dropped.Count - 1; i >= 0; i--)
            {
                var (g, until) = dropped[i];
                if (!g) { dropped.RemoveAt(i); continue; }
                float left = until - Time.time;
                if (left < 0.4f) g.transform.localScale = Vector3.one * Mathf.Max(0f, left / 0.4f);
                if (left <= 0f) { Destroy(g); dropped.RemoveAt(i); }
            }
        }

        // ------------------------------------------------------------------ placing
        static string ShapeFor(string key, int i)
        {
            unchecked { int h = (key ?? "").GetHashCode() * 31 + i * 7919; return Shapes[(h & 0x7fffffff) % Shapes.Length]; }
        }

        void ShowStack(PawOrders.Offer o)
        {
            Clear(ref stack);
            stack = new GameObject("ParcelStack");
            stack.transform.position = orders.TargetPos;
            // face the boxes towards the cat's approach
            if (orders.pet) { var d = orders.pet.position - orders.TargetPos; d.y = 0; if (d.sqrMagnitude > 0.01f) stack.transform.rotation = Quaternion.LookRotation(-d); }
            float y = 0f;
            int n = Mathf.Clamp(o.Stops, 1, 4);
            for (int i = 0; i < n; i++)
            {
                var p = Spawn(ShapeFor(o.dest, i), stack.transform);
                if (!p) break;
                float h = Height(p);
                p.transform.localPosition = new Vector3((i % 2 == 0 ? -0.06f : 0.06f), y, 0);
                p.transform.localRotation = Quaternion.Euler(0, (i * 37) % 30 - 15, 0);
                y += h * 0.98f;
            }
        }

        void Carry(PawOrders.Offer o)
        {
            Clear(ref carried);
            if (o == null || !orders.pet) return;
            var cat = orders.pet.GetComponentInChildren<Animator>();
            Transform body = null;
            if (cat) foreach (var t in cat.GetComponentsInChildren<Transform>()) if (t.name == "Body") { body = t; break; }
            carried = Spawn(ShapeFor(o.dest, 0), cat ? cat.transform : orders.pet);
            if (!carried) return;
            // strapped on her back (cat model space: back top ~0.86 up, a little behind the shoulders)
            carried.transform.localPosition = new Vector3(0f, 0.8f, -0.1f);
            carried.transform.localRotation = Quaternion.Euler(0, 90f, 0);   // long side along her spine
            carried.transform.localScale = Vector3.one * 1.05f;
            if (body) carried.transform.SetParent(body, true);
            foreach (var r in carried.GetComponentsInChildren<Renderer>()) r.gameObject.layer = orders.pet.gameObject.layer;
        }

        void DropAtDoor(PawOrders.Offer o, int stop)
        {
            if (o == null || stop < 0 || stop >= o.Stops) return;
            var g = Spawn(ShapeFor(o.dest, stop), null);
            if (!g) return;
            g.transform.position = o.stopPos[stop];
            if (orders.pet) { var d = orders.pet.position - o.stopPos[stop]; d.y = 0; if (d.sqrMagnitude > 0.01f) g.transform.rotation = Quaternion.LookRotation(-d); }
            dropped.Add((g, Time.time + 6f));
        }

        static void Clear(ref GameObject g) { if (g) Destroy(g); g = null; }

        static float Height(GameObject g)
        {
            var b = new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return Mathf.Max(0.1f, b.size.y);
        }

        // ------------------------------------------------------------------ models + toon materials
        GameObject Spawn(string id, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Parcels/" + id);
            if (!prefab) return null;
            var g = Instantiate(prefab, parent, false);
            g.name = id;
            if (!outline && orders && orders.pet)
                foreach (var r in orders.pet.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name.EndsWith("_Outline")) { outline = r.sharedMaterial; break; }
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    string n = ms[i] ? ms[i].name : "";
                    int k = n.IndexOf("M_C_");
                    if (n.Contains("Outline") && outline) ms[i] = outline;
                    else if (k >= 0 && n.Length >= k + 10)
                    {
                        string hex = n.Substring(k + 4, 6);
                        if (!mats.TryGetValue(hex, out var m) || !m)
                        {
                            ColorUtility.TryParseHtmlString("#" + hex, out var c);
                            var sh = Shader.Find("PawTown/Toon");
                            m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
                            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                            mats[hex] = m;
                        }
                        ms[i] = m;
                    }
                }
                r.sharedMaterials = ms;
            }
            return g;
        }
    }
}
