using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// In-world navigation for the current order: a chunky orange arrow on the ground just ahead of the cat that points
    /// along the walking route (sidewalks and zebras, from PawOrders.route), and a soft light pillar + bobbing pin over
    /// the destination door so it can be spotted from far away. Both hide when there is no order.
    /// </summary>
    public class PawGuide : MonoBehaviour
    {
        public Color arrowColor = new Color(1f, 0.6f, 0.2f);
        public Color inkColor = new Color(0.27f, 0.18f, 0.13f);
        public Color beaconColor = new Color(1f, 0.72f, 0.3f);

        Transform arrow, beacon, pin;
        Vector3 dirSmooth = Vector3.forward;

        void Start()
        {
            BuildArrow();
            BuildBeacon();
        }

        void LateUpdate()
        {
            var o = PawOrders.Instance;
            var pet = o != null ? o.pet : null;
            bool has = o != null && pet != null && o.HasTarget;
            float dist = has ? o.DistanceToTarget : 0f;

            // beacon over the destination
            beacon.gameObject.SetActive(has);
            if (has)
            {
                beacon.position = new Vector3(o.TargetPos.x, pet.position.y, o.TargetPos.z);
                pin.localPosition = new Vector3(0f, 2.6f + 0.25f * Mathf.Sin(Time.time * 3f), 0f);
                pin.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }

            // arrow: hidden once the door is close (the beacon takes over) or when the cat is not on screen to follow
            bool showArrow = has && dist > 3f && PawSave.ArrowGuide;   // Settings > Arrow Guide
            arrow.gameObject.SetActive(showArrow);
            if (!showArrow) return;
            Vector3 next = dist < 30f && !o.SegmentCrossesRoad(pet.position, o.TargetPos) ? o.TargetPos : NextPoint(o.route, pet.position, o.TargetPos);
            Vector3 d = next - pet.position; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) dirSmooth = Vector3.Slerp(dirSmooth, d.normalized, 1f - Mathf.Exp(-8f * Time.deltaTime));
            float bob = 0.12f * Mathf.Sin(Time.time * 5f);
            arrow.position = pet.position + dirSmooth * (1.55f + bob) + Vector3.up * 0.05f;
            arrow.rotation = Quaternion.LookRotation(dirSmooth, Vector3.up);
        }

        /// <summary>Pure-pursuit aim point: project the cat onto the route, then walk 4 m further along it. Never aims at
        /// a corner she has already passed, and turns smoothly through corners.</summary>
        static Vector3 NextPoint(List<Vector3> route, Vector3 p, Vector3 target)
        {
            if (route == null || route.Count < 2) return target;
            int seg = 0; float segT = 0f, bestD = float.MaxValue;
            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector3 a = route[i], b = route[i + 1];
                a.y = b.y = p.y;
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                float dd = (a + ab * t - p).sqrMagnitude;
                if (dd < bestD) { bestD = dd; seg = i; segT = t; }
            }
            float ahead = 4f;
            for (int i = seg; i < route.Count - 1; i++)
            {
                Vector3 a = route[i], b = route[i + 1];
                a.y = b.y = p.y;
                Vector3 start = i == seg ? Vector3.Lerp(a, b, segT) : a;
                float len = Vector3.Distance(start, b);
                if (len >= ahead) return start + (b - start).normalized * ahead;
                ahead -= len;
            }
            return target;
        }

        // ------------------------------------------------------------------ meshes
        void BuildArrow()
        {
            arrow = new GameObject("GuideArrow").transform;
            arrow.SetParent(transform, false);
            // shaft + head, each a convex prism; a darker, slightly bigger copy underneath reads as a toon outline
            Vector2[] shaft = { new Vector2(-0.09f, -0.34f), new Vector2(0.09f, -0.34f), new Vector2(0.09f, 0.04f), new Vector2(-0.09f, 0.04f) };
            Vector2[] head = { new Vector2(-0.24f, 0.02f), new Vector2(0.24f, 0.02f), new Vector2(0f, 0.38f) };
            var top = new Mesh { name = "GuideArrow" };
            Combine(top, Prism(shaft, 0.07f), Prism(head, 0.07f));
            Part("Fill", top, arrowColor, Vector3.zero, Vector3.one);
            Part("Ink", top, inkColor, new Vector3(0f, -0.025f, 0f), new Vector3(1.32f, 1f, 1.22f));
            arrow.localScale = Vector3.one * 1.3f;
        }

        void BuildBeacon()
        {
            beacon = new GameObject("GuideBeacon").transform;
            beacon.SetParent(transform, false);
            // soft light pillar fading upwards
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pillar.GetComponent<Collider>());
            pillar.name = "Pillar";
            pillar.transform.SetParent(beacon, false);
            pillar.transform.localPosition = new Vector3(0f, 7f, 0f);
            pillar.transform.localScale = new Vector3(1.1f, 7f, 1.1f);
            var pr = pillar.GetComponent<Renderer>();
            pr.sharedMaterial = Glow(beaconColor);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // bobbing pin (upside-down cone) above the door
            pin = new GameObject("Pin").transform;
            pin.SetParent(beacon, false);
            var cone = new Mesh { name = "GuidePin" };
            Combine(cone, Cone(0.32f, 0.6f));
            var pinFill = new GameObject("Fill");
            pinFill.transform.SetParent(pin, false);
            pinFill.AddComponent<MeshFilter>().sharedMesh = cone;
            pinFill.AddComponent<MeshRenderer>().sharedMaterial = Toon(arrowColor);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(pin, false);
            ball.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            ball.transform.localScale = Vector3.one * 0.5f;
            ball.GetComponent<Renderer>().sharedMaterial = Toon(arrowColor);
        }

        void Part(string n, Mesh m, Color c, Vector3 pos, Vector3 scale)
        {
            var g = new GameObject(n);
            g.transform.SetParent(arrow, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = Toon(c);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material Toon(Color c)
        {
            var sh = Shader.Find("PawTown/Toon");
            var m = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        static Material Glow(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            // vertical fade: solid at the ground, gone at the top (cylinder side UVs run 0..1 bottom to top)
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float a = Mathf.Pow(1f - y / 63f, 1.6f) * 0.55f;
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);   // additive-ish glow
            m.SetInt("_ZWrite", 0);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        // convex polygon in XZ (y up), extruded by h
        static (List<Vector3>, List<int>) Prism(Vector2[] poly, float h)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            int n = poly.Length;
            // top + bottom caps (fan)
            for (int k = 0; k < 2; k++)
            {
                int b0 = v.Count;
                foreach (var p in poly) v.Add(new Vector3(p.x, k == 0 ? h : 0f, p.y));
                for (int i = 1; i < n - 1; i++)
                    if (k == 0) { t.Add(b0); t.Add(b0 + i + 1); t.Add(b0 + i); }
                    else { t.Add(b0); t.Add(b0 + i); t.Add(b0 + i + 1); }
            }
            // sides (own vertices so normals stay flat)
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                int b0 = v.Count;
                v.Add(new Vector3(a.x, 0f, a.y)); v.Add(new Vector3(b.x, 0f, b.y)); v.Add(new Vector3(b.x, h, b.y)); v.Add(new Vector3(a.x, h, a.y));
                t.Add(b0); t.Add(b0 + 2); t.Add(b0 + 1); t.Add(b0); t.Add(b0 + 3); t.Add(b0 + 2);
            }
            return (v, t);
        }

        // cone pointing down (tip at the origin, base ring at height h)
        static (List<Vector3>, List<int>) Cone(float r, float h)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            const int n = 16;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, h, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, h, Mathf.Sin(a1) * r);
                int b = v.Count;
                v.Add(Vector3.zero); v.Add(p0); v.Add(p1);                        // side (clockwise seen from outside)
                v.Add(new Vector3(0f, h, 0f)); v.Add(p1); v.Add(p0);              // top cap (clockwise seen from above)
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b + 3); t.Add(b + 4); t.Add(b + 5);
            }
            return (v, t);
        }

        static void Combine(Mesh m, params (List<Vector3>, List<int>)[] parts)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (var (pv, pt) in parts) { int o = v.Count; v.AddRange(pv); foreach (int i in pt) t.Add(i + o); }
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        }
    }
}
