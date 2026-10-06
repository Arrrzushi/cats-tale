using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Endless deterministic town. Streams 24 m tiles around a target (the pet).
    /// Layout: N-S roads every 4 columns (with random gaps -> T junctions), E-W roads every 3 rows,
    /// a river and a railway band repeating every 15 rows. Tiles are picked by matching their CONN_ sockets.
    /// </summary>
    public class PawTownGenerator : MonoBehaviour
    {
        public PawTownLibrary library;
        public Transform target;
        public int seed = 1234;
        [Range(2, 8)] public int radius = 4;
        [Range(0f, 0.5f)] public float roadGapChance = 0.2f;
        [Range(0f, 1f)] public float crosswalkChance = 0.25f;
        [Range(0f, 1f)] public float roundaboutChance = 0.2f;
        public Vector2Int hqCell = new Vector2Int(2, 1);
        public float backdropDistance = 260f;
        public int backdropCount = 14;
        public float groundY = -0.85f;   // below every tile's base, so it never covers roads

        [Header("Clouds")]
        public int cloudCount = 16;
        public Vector2 cloudHeight = new Vector2(30f, 60f);
        public float cloudRange = 260f;      // clouds live within this distance of the pet and wrap around
        public Vector3 wind = new Vector3(1.6f, 0f, 0.5f);

        readonly Dictionary<Vector2Int, GameObject> live = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> sourceOf = new Dictionary<GameObject, GameObject>();
        Vector2Int lastCenter = new Vector2Int(int.MinValue, 0);
        Transform backdropRoot;

        float S => library.tileSize;

        void Start()
        {
            if (library != null && (library.backdrop.Count > 0 || library.ground != null))
            {
                backdropRoot = new GameObject("Backdrop").transform;
                backdropRoot.SetParent(transform, false);
            }
            if (library != null && library.ground != null)
            {
                var g = new GameObject("FarGround", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(backdropRoot, false);
                g.transform.localPosition = new Vector3(0, groundY, 0);
                float e = 2000f;
                var m = new Mesh { name = "FarGround" };
                m.vertices = new[] { new Vector3(-e, 0, -e), new Vector3(-e, 0, e), new Vector3(e, 0, e), new Vector3(e, 0, -e) };
                m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                m.bounds = new Bounds(Vector3.zero, new Vector3(2 * e, 1, 2 * e));
                g.GetComponent<MeshFilter>().sharedMesh = m;
                var r = g.GetComponent<MeshRenderer>();
                r.sharedMaterial = library.ground;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            SpawnClouds();
            if (library != null && library.backdrop.Count > 0)
            {
                var rnd = new System.Random(seed);
                for (int i = 0; i < backdropCount; i++)
                {
                    var p = library.backdrop[i % library.backdrop.Count];
                    float a = i * Mathf.PI * 2f / backdropCount;
                    var go = Instantiate(p, backdropRoot);
                    go.transform.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * backdropDistance * (0.9f + 0.2f * (float)rnd.NextDouble());
                    go.transform.localRotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                    go.transform.localScale = Vector3.one * (1.4f + 0.6f * (float)rnd.NextDouble());
                }
            }
            Refresh(true);
        }

        readonly List<Transform> clouds = new List<Transform>();

        void SpawnClouds()
        {
            if (library == null || library.clouds == null || library.clouds.Count == 0) return;
            var root = new GameObject("Clouds").transform;
            root.SetParent(transform, false);
            var rnd = new System.Random(seed + 99);
            Vector3 c = target ? target.position : transform.position;
            for (int i = 0; i < cloudCount; i++)
            {
                var go = Instantiate(library.clouds[i % library.clouds.Count], root);
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = Mathf.Lerp(40f, cloudRange, (float)rnd.NextDouble());
                go.transform.position = new Vector3(c.x + Mathf.Cos(a) * d, Mathf.Lerp(cloudHeight.x, cloudHeight.y, (float)rnd.NextDouble()), c.z + Mathf.Sin(a) * d);
                go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                go.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 2.6f, (float)rnd.NextDouble());
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                clouds.Add(go.transform);
            }
        }

        void DriftClouds()
        {
            if (clouds.Count == 0 || target == null) return;
            Vector3 c = target.position;
            Vector3 step = wind * Time.deltaTime;
            foreach (var t in clouds)
            {
                Vector3 p = t.position + step;
                // wrap: a cloud that drifts too far from the pet reappears on the opposite side
                float dx = p.x - c.x, dz = p.z - c.z;
                if (dx > cloudRange) p.x -= 2f * cloudRange; else if (dx < -cloudRange) p.x += 2f * cloudRange;
                if (dz > cloudRange) p.z -= 2f * cloudRange; else if (dz < -cloudRange) p.z += 2f * cloudRange;
                t.position = p;
            }
        }

        void LateUpdate()
        {
            if (target == null || library == null) return;
            DriftClouds();
            if (backdropRoot) backdropRoot.position = new Vector3(target.position.x, 0, target.position.z);
            Refresh(false);
        }

        int extraRadius;
        int Reach => radius + extraRadius;

        /// <summary>Ask for tiles out to at least this many cells (e.g. the big map). 0 = back to normal.</summary>
        public void EnsureReach(int cells)
        {
            int extra = Mathf.Max(0, cells - radius);
            if (extra == extraRadius) return;
            extraRadius = extra;
            Refresh(true);
        }

        public Vector2Int CellOf(Vector3 world)
        {
            Vector3 p = world - transform.position;
            return new Vector2Int(Mathf.RoundToInt(p.x / S), Mathf.RoundToInt(p.z / S));
        }

        public Vector3 CellCenter(Vector2Int c) => transform.position + new Vector3(c.x * S, 0, c.y * S);

        // ------------------------------------------------------------------ building sites (no spawning needed)
        /// <summary>A deliverable building: its prefab child name, world position and the spot in front of its door.</summary>
        public struct Site { public string name; public Vector3 pos, door; public Vector2Int cell; public int index; }

        struct LocalSite { public string name; public Vector3 pos, fwd; public float half; }
        readonly Dictionary<GameObject, List<LocalSite>> siteCache = new Dictionary<GameObject, List<LocalSite>>();

        /// <summary>Buildings in a cell, worked out from the layout rules and the tile prefab, so it also works for
        /// tiles that are not streamed in yet (orders can point anywhere in the endless town).</summary>
        public List<Site> SitesIn(Vector2Int cell)
        {
            var res = new List<Site>();
            var (e, k) = Choose(cell.x, cell.y);
            if (e == null || e.prefab == null) return res;
            if (!siteCache.TryGetValue(e.prefab, out var locals))
            {
                locals = new List<LocalSite>();
                var root = e.prefab.transform;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = tr.name;
                    if (!(n.StartsWith("HQ_") || n.StartsWith("Shop_") || n.StartsWith("House_") || n == "Barn")) continue;
                    var box = tr.GetComponent<BoxCollider>();
                    Vector3 f = root.InverseTransformDirection(tr.forward); f.y = 0f;
                    locals.Add(new LocalSite { name = n, pos = root.InverseTransformPoint(tr.position), fwd = f.normalized,
                        half = box ? box.size.z * 0.5f * tr.lossyScale.z : 3f });
                }
                siteCache[e.prefab] = locals;
            }
            Quaternion rot = transform.rotation * Quaternion.Euler(0, 90f * k, 0);
            Vector3 scale = e.prefab.transform.localScale, c = CellCenter(cell);
            for (int i = 0; i < locals.Count; i++)
            {
                var l = locals[i];
                Vector3 p = c + rot * Vector3.Scale(scale, l.pos);
                res.Add(new Site { name = l.name, pos = p, door = p + rot * l.fwd * (l.half + 1.4f), cell = cell, index = i });
            }
            return res;
        }

        void Refresh(bool force)
        {
            Vector2Int c = target ? CellOf(target.position) : Vector2Int.zero;
            if (!force && c == lastCenter) return;
            lastCenter = c;
            var keep = new HashSet<Vector2Int>();
            int R = Reach;
            for (int dx = -R; dx <= R; dx++)
            for (int dz = -R; dz <= R; dz++)
            {
                var cell = new Vector2Int(c.x + dx, c.y + dz);
                keep.Add(cell);
                if (!live.ContainsKey(cell)) live[cell] = Spawn(cell);
            }
            var drop = new List<Vector2Int>();
            foreach (var kv in live) if (!keep.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (var k in drop) { Despawn(live[k]); live.Remove(k); }
        }

        // ------------------------------------------------------------------ layout rules
        static int Mod(int a, int m) => ((a % m) + m) % m;
        static int FloorDiv(int a, int m) => (int)Mathf.Floor(a / (float)m);

        float Hash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663) ^ (uint)(salt * 83492791) ^ (uint)(seed * 2654435761u);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public bool RoadCol(int x) => Mod(x, 4) == 1;
        public bool RoadRow(int z) => Mod(z, 3) == 0;
        public bool RiverRow(int z) => Mod(z, 15) == 7;
        public bool RailRow(int z) => Mod(z, 15) == 11;

        public bool ColActive(int x, int z)
        {
            if (!RoadCol(x)) return false;
            if (Mathf.Abs(x) < 6 && Mathf.Abs(z) < 6) return true;
            return Hash(x, FloorDiv(z, 3), 77) >= roadGapChance;
        }

        /// <summary>World-space road sides (Dir bits) of a cell, 0 if no road. Used by traffic.</summary>
        public int RoadMask(Vector2Int c) => Describe(c.x, c.y).road;

        readonly Dictionary<Vector2Int, string> nameCache = new Dictionary<Vector2Int, string>();

        /// <summary>Which tile sits in a cell (e.g. "Road_Cross"), cached.</summary>
        public string TileName(Vector2Int c)
        {
            if (nameCache.TryGetValue(c, out var n)) return n;
            var e = Choose(c.x, c.y).entry;
            n = e != null ? e.name : "";
            if (nameCache.Count > 20000) nameCache.Clear();
            nameCache[c] = n;
            return n;
        }

        struct Need { public int road, rail, river; public bool lot; public int face; }

        Need Describe(int x, int z)
        {
            var n = new Need();
            bool col = ColActive(x, z);
            if (RiverRow(z)) { n.river = Dir.E | Dir.W; if (col) n.road = Dir.N | Dir.S; return n; }
            if (RailRow(z)) { n.rail = Dir.E | Dir.W; if (col) n.road = Dir.N | Dir.S; return n; }
            if (RoadRow(z))
            {
                n.road = Dir.E | Dir.W;
                if (RoadCol(x))
                {
                    if (ColActive(x, z + 1)) n.road |= Dir.N;
                    if (ColActive(x, z - 1)) n.road |= Dir.S;
                }
                return n;
            }
            if (col) { n.road = Dir.N | Dir.S; return n; }
            n.lot = true;
            if (RoadRow(z - 1)) n.face = Dir.S;
            else if (RoadRow(z + 1)) n.face = Dir.N;
            else if (ColActive(x - 1, z)) n.face = Dir.W;
            else if (ColActive(x + 1, z)) n.face = Dir.E;
            return n;
        }

        float Weight(string name)
        {
            switch (name)
            {
                case "Road_Straight": return 1f - crosswalkChance;
                case "Road_Crosswalk": return crosswalkChance;
                case "Road_Cross": return 1f - roundaboutChance;
                case "Road_Roundabout": return roundaboutChance;
                case "Lot_Houses_A": case "Lot_Houses_B": case "Lot_Houses_C": return 3f;
                case "Lot_Shops": case "Lot_Park": case "Lot_Forest": return 1.5f;
                case "Lot_Farm": return 1f;
                case "Lot_LunaBakery": return 0.4f;
                case "Lot_HQ": case "Grass": return 0f;
                default: return 1f;
            }
        }

        public (TileEntry entry, int rot) Choose(int x, int z)
        {
            var need = Describe(x, z);
            float r = Hash(x, z, 11);
            if (need.lot)
            {
                if (x == hqCell.x && z == hqCell.y)
                {
                    var hq = library.Get("Lot_HQ");
                    if (hq != null) return (hq, RotFor(hq.front, need.face == 0 ? Dir.S : need.face));
                }
                var opts = new List<TileEntry>();
                foreach (var t in library.tiles)
                    if (t.isLot && Weight(t.name) > 0 && (need.face != 0 || t.name == "Lot_Forest" || t.name == "Lot_Farm" || t.name == "Lot_Park"))
                        opts.Add(t);
                var pick = Pick(opts, r);
                int face = need.face != 0 ? need.face : (1 << (int)(Hash(x, z, 5) * 4f));
                return (pick, RotFor(pick.front, face));
            }
            var cands = new List<(TileEntry, int)>();
            float total = 0;
            foreach (var t in library.tiles)
            {
                if (t.isLot) continue;
                for (int k = 0; k < 4; k++)
                {
                    if (Dir.Rotate(t.road, k) == need.road && Dir.Rotate(t.rail, k) == need.rail && Dir.Rotate(t.river, k) == need.river)
                    {
                        cands.Add((t, k));
                        total += Weight(t.name);
                        if (t.road == 0 || t.road == 15) break;   // symmetric: one rotation is enough
                    }
                }
            }
            if (cands.Count == 0) return (library.Get("Grass"), 0);
            float acc = 0, want = r * total;
            foreach (var c in cands) { acc += Weight(c.Item1.name); if (want <= acc) return c; }
            return cands[cands.Count - 1];
        }

        TileEntry Pick(List<TileEntry> opts, float r)
        {
            float total = 0;
            foreach (var t in opts) total += Weight(t.name);
            float acc = 0, want = r * total;
            foreach (var t in opts) { acc += Weight(t.name); if (want <= acc) return t; }
            return opts[opts.Count - 1];
        }

        static int RotFor(int front, int face)
        {
            if (front == 0) return 0;
            for (int k = 0; k < 4; k++) if (Dir.Rotate(front, k) == face) return k;
            return 0;
        }

        // ------------------------------------------------------------------ spawning / pooling
        GameObject Spawn(Vector2Int cell)
        {
            var (e, k) = Choose(cell.x, cell.y);
            if (e == null || e.prefab == null) return null;
            GameObject go;
            if (pool.TryGetValue(e.prefab, out var st) && st.Count > 0) { go = st.Pop(); go.SetActive(true); }
            else { go = Instantiate(e.prefab, transform); sourceOf[go] = e.prefab; }
            go.name = $"{e.name} [{cell.x},{cell.y}]";
            go.transform.SetPositionAndRotation(CellCenter(cell), transform.rotation * Quaternion.Euler(0, 90f * k, 0));
            // the river is swimmable: switch off the old invisible bank walls (tall thin box colliders on river tiles)
            if (e.name.StartsWith("River_"))
                foreach (var bc in go.GetComponents<BoxCollider>())
                    if (bc.size.y > 2f && bc.size.z < 0.5f) bc.enabled = false;
            return go;
        }

        void Despawn(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            var src = sourceOf[go];
            if (!pool.TryGetValue(src, out var st)) pool[src] = st = new Stack<GameObject>();
            st.Push(go);
        }
    }
}
