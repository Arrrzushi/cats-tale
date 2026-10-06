using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Delivery loop: "Pick up the order (head to PAW-LIVERY HQ)" -> "Deliver the order (head to Miss Luna's Bakery)"
    /// -> "Order delivered!" -> next pickup at a shop -> deliver to a house -> ...
    /// Targets are real buildings in the streamed town; the route walks along sidewalks and only crosses at zebras.
    /// </summary>
    public class PawOrders : MonoBehaviour
    {
        public static PawOrders Instance { get; private set; }

        public enum Stage { Waiting, Pickup, Deliver, Done }

        public PawTownGenerator generator;
        public Transform pet;
        public float arriveRadius = 3.5f;
        public Vector2 deliveryDistance = new Vector2(45f, 130f);   // how far away new targets are picked

        public Stage stage { get; private set; } = Stage.Waiting;
        public string Title { get; private set; } = "";
        public string Subtitle { get; private set; } = "";
        public string TargetName { get; private set; } = "";
        public Vector3 TargetPos { get; private set; }
        public bool HasTarget { get; private set; }
        public int Delivered { get; private set; }
        public readonly List<Vector3> route = new List<Vector3>();

        public event Action Changed;
        public event Action<Stage> StageChanged;
        /// <summary>Fired when an order is handed over: (seconds it took, the accepted order).</summary>
        public event Action<float, Offer> Delivered_;

        [Tooltip("Orders start only when the player accepts one on the Order Board")] public bool waitForAccept = true;

        /// <summary>An order on the board.</summary>
        public class Offer
        {
            public string dest;            // display name, e.g. "Miss Luna's Bakery"
            public Vector3 destPos;        // door position
            public float distance;         // HQ -> door, metres (straight line)
            public int reward, stars;
            public bool vip;
            public string[] tags;
            public string customer;        // baker / elder / girl / man
            public string pickup;          // where the parcel is collected (HQ or the nearest shop)
            public Vector3 pickupPos;
            public List<string> stopNames = new List<string>();   // 1 stop early on, up to 4 at high levels
            public List<Vector3> stopPos = new List<Vector3>();
            public int Stops => stopNames.Count;
        }

        public Offer Current { get; private set; }
        public int StopIndex { get; private set; }
        float orderStart;

        float doneUntil, nextRoute;
        Vector2Int lastPetCell = new Vector2Int(int.MinValue, 0);
        System.Random rnd = new System.Random(5);

        public float DistanceToTarget => HasTarget && pet ? Vector3.Distance(Flat(pet.position), Flat(TargetPos)) : 0f;

        void Awake() { Instance = this; if (!GetComponent<PawGuide>()) gameObject.AddComponent<PawGuide>(); if (!GetComponent<PawParcels>()) gameObject.AddComponent<PawParcels>(); }   // arrow + destination beacon
        void OnDestroy() { if (Instance == this) Instance = null; }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void Update()
        {
            if (!pet || !generator) return;
            switch (stage)
            {
                case Stage.Waiting:
                    // pick up at the HQ once the town has streamed in (with the board: only after accepting an order)
                    if (Current != null) { SetTarget(Current.pickup, Current.pickupPos); orderStart = Time.time; SetStage(Stage.Pickup); }
                    else if (!waitForAccept && TryTarget(true, preferHQ: true)) { orderStart = Time.time; SetStage(Stage.Pickup); }
                    break;
                case Stage.Pickup:
                case Stage.Deliver:
                    if (DistanceToTarget < arriveRadius) Arrive();
                    break;
                case Stage.Done:
                    if (waitForAccept) break;   // the board starts the next one
                    if (Time.time > doneUntil && TryTarget(true, preferHQ: false)) SetStage(Stage.Pickup);
                    break;
            }
            var pc = generator.CellOf(pet.position);
            if (HasTarget && (Time.time > nextRoute || pc != lastPetCell))
            {
                nextRoute = Time.time + 1f;
                lastPetCell = pc;
                BuildRoute();
            }
        }

        void Arrive()
        {
            if (stage == Stage.Pickup)
            {
                PawAudio.Instance?.Click();
                if (Current != null) { StopIndex = 0; SetTarget(Current.stopNames[0], Current.stopPos[0]); SetStage(Stage.Deliver); }
                else if (TryTarget(false, false)) SetStage(Stage.Deliver);
            }
            else if (Current != null && StopIndex < Current.Stops - 1)
            {
                // multi-stop order: hand this parcel over and head on to the next door
                PawAudio.Instance?.Success();
                StopIndex++;
                SetTarget(Current.stopNames[StopIndex], Current.stopPos[StopIndex]);
                SetStage(Stage.Deliver);
            }
            else
            {
                Delivered++;
                PawAudio.Instance?.Success();
                HasTarget = false;
                route.Clear();
                doneUntil = Time.time + 3f;
                var done = Current;
                Current = null;
                SetStage(Stage.Done);
                Delivered_?.Invoke(Time.time - orderStart, done);
            }
        }

        void SetStage(Stage s)
        {
            stage = s;
            switch (s)
            {
                case Stage.Pickup:
                    Title = "Pick up the order";
                    Subtitle = $"Head to {TargetName}";
                    break;
                case Stage.Deliver:
                    bool multi = Current != null && Current.Stops > 1;
                    Title = multi ? $"Deliver parcel {StopIndex + 1} of {Current.Stops}" : "Deliver the order";
                    Subtitle = $"Head to {TargetName}";
                    break;
                case Stage.Done:
                    Title = "Order delivered!";
                    Subtitle = Delivered == 1 ? "Purrfect! 1 delivery done" : $"Purrfect! {Delivered} deliveries done";
                    break;
            }
            StageChanged?.Invoke(s);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ targets
        struct Spot { public Transform t; public string kind; }

        List<Spot> Buildings()
        {
            var list = new List<Spot>();
            foreach (Transform tile in generator.transform)
            {
                if (!tile.gameObject.activeInHierarchy) continue;
                foreach (var tr in tile.GetComponentsInChildren<Transform>())
                {
                    string n = tr.name;
                    if (n.StartsWith("HQ_")) list.Add(new Spot { t = tr, kind = "hq" });
                    else if (n.StartsWith("Shop_")) list.Add(new Spot { t = tr, kind = "shop" });
                    else if (n.StartsWith("House_") || n == "Barn") list.Add(new Spot { t = tr, kind = "house" });
                }
            }
            return list;
        }

        bool TryTarget(bool pickup, bool preferHQ)
        {
            var all = Buildings();
            if (all.Count == 0) return false;
            Vector3 p = pet.position;
            Spot? best = null;
            if (preferHQ)
            {
                float bd = float.MaxValue;
                foreach (var s in all) if (s.kind == "hq") { float d = (s.t.position - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } }
            }
            if (best == null)
            {
                var good = new List<Spot>();
                foreach (var s in all)
                {
                    bool kindOk = pickup ? (s.kind == "shop" || s.kind == "hq") : (s.kind == "house" || s.kind == "shop");
                    float d = Vector3.Distance(Flat(s.t.position), Flat(p));
                    if (kindOk && d > deliveryDistance.x && d < deliveryDistance.y) good.Add(s);
                }
                if (good.Count == 0)   // fall back: anything of the right kind, farthest first within reach
                    foreach (var s in all) if (pickup ? s.kind != "house" : s.kind != "hq") good.Add(s);
                if (good.Count == 0) return false;
                best = good[rnd.Next(good.Count)];
            }
            var b = best.Value.t;
            TargetName = DisplayName(b.name);
            TargetPos = DoorOf(b);
            HasTarget = true;
            lastPetCell = new Vector2Int(int.MinValue, 0);
            return true;
        }

        void SetTarget(string name, Vector3 door)
        {
            TargetName = name;
            TargetPos = door;
            HasTarget = true;
            lastPetCell = new Vector2Int(int.MinValue, 0);
        }

        // ------------------------------------------------------------------ endless order board
        static readonly string[] Owners =
        {
            "Mochi", "Biscuit", "Pumpkin", "Noodle", "Clover", "Pepper", "Muffin", "Ziggy", "Hazel", "Tofu", "Maple", "Olive",
            "Poppy", "Waffles", "Bean", "Juniper", "Toffee", "Pico", "Marble", "Sunny", "Cocoa", "Pistachio", "Basil", "Daisy",
            "Fig", "Nori", "Sesame", "Willow", "Pudding", "Kiwi", "Bubbles", "Ginger", "Lulu", "Oscar", "Rosie", "Theo",
            "Miso", "Peaches", "Bramble", "Cosmo", "Dumpling", "Eloise", "Frankie", "Gus", "Honey", "Iggy", "Jellybean", "Koko"
        };

        static int SiteHash(PawTownGenerator.Site s) { unchecked { return (s.cell.x * 73856093) ^ (s.cell.y * 19349663) ^ ((s.index + 1) * 83492791); } }

        /// <summary>Every house has its own owner, so the endless town never runs out of distinct addresses.</summary>
        public static string SiteName(PawTownGenerator.Site s)
        {
            if (s.name.StartsWith("House_") || s.name == "Barn")
            {
                string owner = Owners[(SiteHash(s) & 0x7fffffff) % Owners.Length];
                string poss = owner.EndsWith("s") ? owner + "'" : owner + "'s";   // Waffles' house, Mochi's house
                return s.name == "Barn" ? $"{poss} farm" : $"{poss} house";
            }
            return DisplayName(s.name);
        }

        static string CustomerOf(PawTownGenerator.Site s, string name)
        {
            if (name.Contains("Bakery")) return "baker";
            if (name.Contains("Barkery")) return "elder";
            if (name.Contains("Cafe")) return "girl";
            string[] pool = { "girl", "man", "elder", "girl", "man" };
            return pool[((SiteHash(s) >> 7) & 0x7fffffff) % pool.Length];
        }

        /// <summary>How far orders reach at a level: short hops at first, across town later (never stops growing until the cap).</summary>
        public static Vector2 RangeFor(int level) => new Vector2(Mathf.Min(30f + 4f * (level - 1), 140f), Mathf.Min(110f + 18f * (level - 1), 420f));
        /// <summary>Most parcels one order can carry at a level.</summary>
        public static int MaxStopsFor(int level) => level >= 10 ? 4 : level >= 6 ? 3 : level >= 3 ? 2 : 1;

        List<PawTownGenerator.Site> SitesAround(Vector3 c, float radius)
        {
            var res = new List<PawTownGenerator.Site>();
            var cc = generator.CellOf(c);
            int R = Mathf.CeilToInt(radius / 24f) + 1;
            for (int dx = -R; dx <= R; dx++)
                for (int dz = -R; dz <= R; dz++)
                    foreach (var site in generator.SitesIn(new Vector2Int(cc.x + dx, cc.y + dz)))
                        if (Vector3.Distance(Flat(site.pos), Flat(c)) <= radius) res.Add(site);
            return res;
        }

        /// <summary>Orders for the Order Board, picked fresh around where the cat is: collect at the nearest shop (or
        /// the HQ), deliver to buildings that get farther away, more numerous and better paid as the level rises.</summary>
        public List<Offer> MakeOffers(int count)
        {
            var res = new List<Offer>();
            if (!generator || !pet) return res;
            int level = Mathf.Max(1, PawProgress.Level);
            Vector2 range = RangeFor(level);
            int maxStops = MaxStopsFor(level);

            // pickup: the closest HQ or shop to the cat
            PawTownGenerator.Site? pick = null; float pd = float.MaxValue;
            foreach (var st in SitesAround(pet.position, 110f))
            {
                if (!(st.name.StartsWith("HQ_") || st.name.StartsWith("Shop_"))) continue;
                float d = Vector3.Distance(Flat(st.door), Flat(pet.position));
                if (d < pd) { pd = d; pick = st; }
            }
            if (pick == null) return res;
            var from = pick.Value;

            var all = SitesAround(from.door, range.y + 120f);
            all.RemoveAll(st => st.name.StartsWith("HQ_"));
            for (int i = all.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (all[i], all[j]) = (all[j], all[i]); }

            var used = new HashSet<string> { SiteName(from) };
            string[] tagPool = { "FRAGILE", "HOT", "RUSH" };
            float mult = 1f + 0.06f * (level - 1);
            foreach (var first in all)
            {
                if (res.Count >= count) break;
                string n = SiteName(first);
                float d0 = Vector3.Distance(Flat(from.door), Flat(first.door));
                if (d0 < range.x || d0 > range.y || used.Contains(n)) continue;
                var o = new Offer { dest = n, destPos = first.door, pickup = SiteName(from), pickupPos = from.door, customer = CustomerOf(first, n) };
                o.stopNames.Add(n); o.stopPos.Add(first.door);
                used.Add(n);
                // extra parcels: each next door is a short walk from the previous one
                int want = maxStops > 1 && rnd.NextDouble() < 0.45 ? 2 + rnd.Next(maxStops - 1) : 1;
                float total = d0;
                while (o.Stops < want)
                {
                    Vector3 last = o.stopPos[o.Stops - 1];
                    bool added = false;
                    foreach (var nx in all)
                    {
                        string nn = SiteName(nx);
                        float dd = Vector3.Distance(Flat(last), Flat(nx.door));
                        if (used.Contains(nn) || dd < 30f || dd > 110f) continue;
                        o.stopNames.Add(nn); o.stopPos.Add(nx.door); used.Add(nn); total += dd; added = true;
                        break;
                    }
                    if (!added) break;
                }
                o.distance = total;
                o.stars = total < 80f ? 1 : total < 170f ? 2 : 3;
                o.reward = Mathf.RoundToInt((20f + total * 0.25f + o.stars * 5f + (o.Stops - 1) * 15f) * mult);
                int t1 = rnd.Next(3), t2 = (t1 + 1 + rnd.Next(2)) % 3;
                o.tags = o.Stops > 1 ? new[] { $"{o.Stops} STOPS", tagPool[t1] } : new[] { tagPool[t1], tagPool[t2] };
                res.Add(o);
            }
            if (res.Count > 0) { var v = res[rnd.Next(res.Count)]; v.vip = true; v.reward *= 2; }
            return res;
        }

        /// <summary>Drop the current order (Free Roam): no target, no route, no guide arrow.</summary>
        public void Cancel()
        {
            Current = null;
            HasTarget = false;
            route.Clear();
            stage = Stage.Waiting;
            Title = ""; Subtitle = "";
            Changed?.Invoke();
        }

        /// <summary>Start an order from the board: collect the parcel(s), then deliver to each stop in turn.</summary>
        public void Accept(Offer o)
        {
            Current = o;
            StopIndex = 0;
            HasTarget = false;
            route.Clear();
            stage = Stage.Waiting;
        }

        static Vector3 DoorOf(Transform b)
        {
            var box = b.GetComponent<BoxCollider>();
            float half = box ? box.size.z * 0.5f * b.lossyScale.z : 3f;
            Vector3 fwd = b.forward; fwd.y = 0; fwd.Normalize();
            return b.position + fwd * (half + 1.4f);
        }

        public static string DisplayName(string n)
        {
            if (n.StartsWith("HQ_")) return "PAW-LIVERY HQ";
            switch (n)
            {
                case "Shop_LunaBakery": return "Miss Luna's Bakery";
                case "Shop_Barkery": return "The Barkery";
                case "Shop_CatCafe": return "the Cat Cafe";
                case "Barn": return "the farm barn";
            }
            if (n.StartsWith("House_"))
            {
                string c = n.Substring(6).Split('_')[0].ToLowerInvariant();
                return $"the {c} house";
            }
            return n;
        }

        // ------------------------------------------------------------------ walking route: sidewalks + zebra crossings
        // Every road cell gets 4 sidewalk "corner" nodes. Nodes link along sidewalks (inside a cell and into the next
        // road cell). Crossing a road is only allowed where a zebra is painted: junction / roundabout arms and the
        // mid-block crosswalk tile. Dijkstra over that graph gives a path a cat would actually walk.
        const float SideD = 5.9f;     // sidewalk centre line, from the road centre
        const float ZebraJ = 8.9f;    // zebra on a junction arm, from the junction centre
        const float ZebraR = 10.6f;   // zebra on a roundabout arm
        const float CrossCost = 4f;   // small nudge to stay on one side unless crossing is worth it

        struct Node : IEquatable<Node>   // q: bit0 = +x side, bit1 = +z side
        {
            public Vector2Int cell; public int q;
            public bool Equals(Node o) => o.cell == cell && o.q == q;
            public override bool Equals(object o) => o is Node n && Equals(n);
            public override int GetHashCode() => (cell.x * 73856093) ^ (cell.y * 19349663) ^ q;
        }
        static int Sx(int q) => (q & 1) != 0 ? 1 : -1;
        static int Sz(int q) => (q & 2) != 0 ? 1 : -1;

        float CornerD(string tile) => tile == "Road_Roundabout" ? 8.4f : tile == "River_RoadBridge" ? 5.2f : SideD;

        Vector3 NodePos(Vector2Int c, int q)
        {
            float d = CornerD(generator.TileName(c));
            return generator.CellCenter(c) + new Vector3(Sx(q) * d, 0f, Sz(q) * d);
        }

        bool OnRoad(Vector3 p)
        {
            var c = generator.CellOf(p);
            int m = generator.RoadMask(c);
            if (m == 0) return false;
            Vector3 l = p - generator.CellCenter(c);
            if (generator.TileName(c) == "Road_Roundabout")
            {
                float r = new Vector2(l.x, l.z).magnitude;
                if (r > 3.8f && r < 10.2f) return true;
            }
            const float hw = 5.0f;
            bool alongZ = Mathf.Abs(l.x) < hw && (Mathf.Abs(l.z) < hw || (l.z > 0 && (m & Dir.N) != 0) || (l.z < 0 && (m & Dir.S) != 0));
            bool alongX = Mathf.Abs(l.z) < hw && (Mathf.Abs(l.x) < hw || (l.x > 0 && (m & Dir.E) != 0) || (l.x < 0 && (m & Dir.W) != 0));
            return alongZ || alongX;
        }

        public bool SegmentCrossesRoad(Vector3 a, Vector3 b) => RoadFraction(a, b) > 0f;

        public bool IsOnRoad(Vector3 p) => generator != null && OnRoad(p);

        /// <summary>Is p on a painted zebra crossing (junction / roundabout arms, mid-block crosswalk)?</summary>
        public bool IsOnZebra(Vector3 p)
        {
            if (generator == null || !OnRoad(p)) return false;
            var c = generator.CellOf(p);
            int m = generator.RoadMask(c);
            string tile = generator.TileName(c);
            Vector3 l = p - generator.CellCenter(c);
            const float half = 1.6f, across = 5.2f;
            if (tile == "Road_Cross" || tile == "Road_T" || tile == "Road_Roundabout")
            {
                float z = tile == "Road_Roundabout" ? ZebraR : ZebraJ;
                if ((m & Dir.N) != 0 && Mathf.Abs(l.z - z) < half && Mathf.Abs(l.x) < across) return true;
                if ((m & Dir.S) != 0 && Mathf.Abs(l.z + z) < half && Mathf.Abs(l.x) < across) return true;
                if ((m & Dir.E) != 0 && Mathf.Abs(l.x - z) < half && Mathf.Abs(l.z) < across) return true;
                if ((m & Dir.W) != 0 && Mathf.Abs(l.x + z) < half && Mathf.Abs(l.z) < across) return true;
            }
            if (tile == "Road_Crosswalk")
            {
                bool alongZ = (m & (Dir.N | Dir.S)) != 0;
                return alongZ ? Mathf.Abs(l.z) < half + 0.3f : Mathf.Abs(l.x) < half + 0.3f;
            }
            return false;
        }

        /// <summary>How much of the straight line a-b lies on a road (0..1).</summary>
        float RoadFraction(Vector3 a, Vector3 b)
        {
            int on = 0;
            const int N = 16;
            for (int i = 1; i < N; i++)
                if (OnRoad(Vector3.Lerp(a, b, i / (float)N))) on++;
            return on / (float)(N - 1);
        }

        /// <summary>Edges out of node n: (neighbour, cost, extra waypoints between them).</summary>
        IEnumerable<(Node, float, Vector3[])> Edges(Node n)
        {
            var c = n.cell;
            int m = generator.RoadMask(c);
            string tile = generator.TileName(c);
            Vector3 cc = generator.CellCenter(c);
            Vector3 p = NodePos(c, n.q);
            int sx = Sx(n.q), sz = Sz(n.q);
            float zeb = tile == "Road_Roundabout" ? ZebraR : ZebraJ;
            bool junction = tile == "Road_Cross" || tile == "Road_T" || tile == "Road_Roundabout";

            // inside the cell, moving along Z on side sx (crosses the E/W arm if there is one)
            {
                var o = new Node { cell = c, q = n.q ^ 2 };
                Vector3 op = NodePos(c, o.q);
                int arm = sx > 0 ? Dir.E : Dir.W;
                if ((m & arm) == 0) yield return (o, Vector3.Distance(p, op), null);
                else if (junction)
                {
                    var w = new[] { cc + new Vector3(sx * zeb, 0, sz * SideD), cc + new Vector3(sx * zeb, 0, -sz * SideD) };
                    yield return (o, Vector3.Distance(p, w[0]) + 2f * SideD + Vector3.Distance(w[1], op) + CrossCost, w);
                }
            }
            // inside the cell, moving along X on side sz (crosses the N/S arm if there is one)
            {
                var o = new Node { cell = c, q = n.q ^ 1 };
                Vector3 op = NodePos(c, o.q);
                int arm = sz > 0 ? Dir.N : Dir.S;
                bool roadAlongZ = (m & (Dir.N | Dir.S)) != 0 && (m & (Dir.E | Dir.W)) == 0;
                if ((m & arm) == 0) yield return (o, Vector3.Distance(p, op), null);
                else if (junction)
                {
                    var w = new[] { cc + new Vector3(sx * SideD, 0, sz * zeb), cc + new Vector3(-sx * SideD, 0, sz * zeb) };
                    yield return (o, Vector3.Distance(p, w[0]) + 2f * SideD + Vector3.Distance(w[1], op) + CrossCost, w);
                }
                else if (tile == "Road_Crosswalk" && roadAlongZ)
                {
                    var w = new[] { cc + new Vector3(sx * SideD, 0, 0), cc + new Vector3(-sx * SideD, 0, 0) };
                    yield return (o, Vector3.Distance(p, w[0]) + 2f * SideD + Vector3.Distance(w[1], op) + CrossCost, w);
                }
            }
            // mid-block crosswalk on an east-west road: cross along Z through the zebra at x = 0
            if (tile == "Road_Crosswalk" && (m & (Dir.E | Dir.W)) != 0 && (m & (Dir.N | Dir.S)) == 0)
            {
                var o = new Node { cell = c, q = n.q ^ 2 };
                Vector3 op = NodePos(c, o.q);
                var w = new[] { cc + new Vector3(0, 0, sz * SideD), cc + new Vector3(0, 0, -sz * SideD) };
                yield return (o, Vector3.Distance(p, w[0]) + 2f * SideD + Vector3.Distance(w[1], op) + CrossCost, w);
            }
            // along the sidewalk into the next road cell
            if (sz > 0 && (m & Dir.N) != 0) { var nc = c + new Vector2Int(0, 1); if ((generator.RoadMask(nc) & Dir.S) != 0) { var o = new Node { cell = nc, q = n.q & 1 }; yield return (o, Vector3.Distance(p, NodePos(nc, o.q)), null); } }
            if (sz < 0 && (m & Dir.S) != 0) { var nc = c + new Vector2Int(0, -1); if ((generator.RoadMask(nc) & Dir.N) != 0) { var o = new Node { cell = nc, q = (n.q & 1) | 2 }; yield return (o, Vector3.Distance(p, NodePos(nc, o.q)), null); } }
            if (sx > 0 && (m & Dir.E) != 0) { var nc = c + new Vector2Int(1, 0); if ((generator.RoadMask(nc) & Dir.W) != 0) { var o = new Node { cell = nc, q = n.q & 2 }; yield return (o, Vector3.Distance(p, NodePos(nc, o.q)), null); } }
            if (sx < 0 && (m & Dir.W) != 0) { var nc = c + new Vector2Int(-1, 0); if ((generator.RoadMask(nc) & Dir.E) != 0) { var o = new Node { cell = nc, q = (n.q & 2) | 1 }; yield return (o, Vector3.Distance(p, NodePos(nc, o.q)), null); } }
        }

        /// <summary>Sidewalk nodes near a point, with the cost of walking there (very expensive if it means jaywalking).</summary>
        List<(Node, float)> Entries(Vector3 p)
        {
            var list = new List<(Node, float)>();
            var pc = generator.CellOf(p);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var c = pc + new Vector2Int(dx, dz);
                    if (generator.RoadMask(c) == 0) continue;
                    for (int q = 0; q < 4; q++)
                    {
                        Vector3 np = NodePos(c, q);
                        float d = Vector3.Distance(Flat(p), Flat(np));
                        if (d > generator.library.tileSize * 1.2f) continue;
                        d += 25f * RoadFraction(p, np) * d;   // walking on the road (not at a zebra) is very expensive
                        list.Add((new Node { cell = c, q = q }, d));
                    }
                }
            return list;
        }

        void BuildRoute()
        {
            var r = PlanWalk(pet.position, TargetPos);
            route.Clear();
            route.AddRange(r);
            Changed?.Invoke();
        }

        /// <summary>A walking path from -> to along sidewalks, crossing only at zebras (first point = from, last = to).</summary>
        public List<Vector3> PlanWalk(Vector3 from, Vector3 to)
        {
            var path = new List<Vector3> { from };
            // close and no road in between: just walk straight there (no detour out to a sidewalk corner)
            if (Vector3.Distance(Flat(from), Flat(to)) < 30f && !SegmentCrossesRoad(from, to)) { path.Add(to); return path; }

            var dist = new Dictionary<Node, float>();
            var prev = new Dictionary<Node, (Node, Vector3[])>();
            var open = new NodeHeap();
            // A*: straight-line distance to the door never overestimates the walk, so the first goal found is still the best
            float H(Node n) => Vector3.Distance(Flat(NodePos(n.cell, n.q)), Flat(to));
            foreach (var (n, d) in Entries(from)) { if (!dist.ContainsKey(n) || d < dist[n]) { dist[n] = d; open.Push(n, d + H(n)); } }
            var goals = new Dictionary<Node, float>();
            foreach (var (n, d) in Entries(to)) goals[n] = d;

            Vector2Int a = generator.CellOf(from), b = generator.CellOf(to);
            int minX = Mathf.Min(a.x, b.x) - 4, maxX = Mathf.Max(a.x, b.x) + 4, minZ = Mathf.Min(a.y, b.y) - 4, maxZ = Mathf.Max(a.y, b.y) + 4;
            var done = new HashSet<Node>();
            Node? best = null; float bestCost = float.MaxValue;
            int guard = 0;
            while (open.Count > 0 && guard++ < 60000)
            {
                var (cur, f) = open.Pop();
                if (!done.Add(cur)) continue;
                float cd = dist[cur];
                if (f >= bestCost) break;
                if (goals.TryGetValue(cur, out float gd) && cd + gd < bestCost) { bestCost = cd + gd; best = cur; }
                foreach (var (nb, w, way) in Edges(cur))
                {
                    if (nb.cell.x < minX || nb.cell.x > maxX || nb.cell.y < minZ || nb.cell.y > maxZ || done.Contains(nb)) continue;
                    float nd = cd + w;
                    if (!dist.TryGetValue(nb, out float od) || nd < od)
                    {
                        dist[nb] = nd;
                        prev[nb] = (cur, way);
                        open.Push(nb, nd + H(nb));
                    }
                }
            }
            if (best != null)
            {
                var pts = new List<Vector3>();
                var n = best.Value;
                pts.Add(NodePos(n.cell, n.q));
                while (prev.TryGetValue(n, out var pv))
                {
                    if (pv.Item2 != null) for (int i = pv.Item2.Length - 1; i >= 0; i--) pts.Add(pv.Item2[i]);
                    n = pv.Item1;
                    pts.Add(NodePos(n.cell, n.q));
                }
                pts.Reverse();
                path.AddRange(pts);
            }
            path.Add(to);
            Simplify(path);
            return path;
        }

        /// <summary>Small binary min-heap for the path search (duplicates allowed, stale ones are skipped by 'done').</summary>
        class NodeHeap
        {
            readonly List<(Node n, float k)> h = new List<(Node, float)>();
            public int Count => h.Count;
            public void Push(Node n, float k)
            {
                h.Add((n, k));
                int i = h.Count - 1;
                while (i > 0) { int p = (i - 1) / 2; if (h[p].k <= h[i].k) break; (h[p], h[i]) = (h[i], h[p]); i = p; }
            }
            public (Node, float) Pop()
            {
                var top = h[0];
                h[0] = h[h.Count - 1]; h.RemoveAt(h.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < h.Count && h[l].k < h[m].k) m = l;
                    if (r < h.Count && h[r].k < h[m].k) m = r;
                    if (m == i) break;
                    (h[m], h[i]) = (h[i], h[m]); i = m;
                }
                return top;
            }
        }

        /// <summary>Only drop duplicate points: the zig-zags out to a zebra and back are real and must stay.</summary>
        static void Simplify(List<Vector3> route)
        {
            for (int i = route.Count - 2; i >= 1; i--)
                if (Flat(route[i] - route[i - 1]).sqrMagnitude < 0.09f) route.RemoveAt(i);
        }
    }
}
