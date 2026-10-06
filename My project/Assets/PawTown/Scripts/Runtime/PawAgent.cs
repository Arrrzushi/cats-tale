using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>One car or pedestrian. Follows the road graph cell by cell (spawned and driven by PawTraffic).</summary>
    public class PawAgent : MonoBehaviour
    {
        // directions: 0 = N (+Z), 1 = E (+X), 2 = S (-Z), 3 = W (-X); bits match Dir.N/E/S/W
        public static int Bit(int d) => 1 << d;
        public static int Opp(int d) => (d + 2) & 3;
        static int Right(int d) => (d + 1) & 3;
        static int Left(int d) => (d + 3) & 3;
        static readonly Vector3[] V = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        internal GameObject source;
        internal bool vehicle, dead;
        internal Vector2Int cell;

        PawTraffic tr;
        internal int heading;
        int nextHeading;
        internal float halfLength = 1f;
        readonly List<Vector3> path = new List<Vector3>();
        int idx;
        float maxSpeed, speed, stuckTime, ghostUntil;
        bool committed, stopForSignal;
        AudioSource engine;
        float basePitch = 1f, petBlockedFor, nextHonk;
        Animator anim;
        System.Random rnd;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public float Speed => speed;
        public bool IsVehicle => vehicle;

        public void Init(PawTraffic t, bool isVehicle, Vector2Int c, int head, float spd, int seed)
        {
            tr = t; vehicle = isVehicle; cell = c; heading = head; maxSpeed = spd;
            rnd = new System.Random(seed);
            dead = false; stuckTime = 0; committed = false;
            anim = GetComponentInChildren<Animator>();
            var box = GetComponent<BoxCollider>();
            halfLength = box ? box.size.z * 0.5f : (vehicle ? 2f : 0.3f);
            BuildPath();
            if (dead) return;
            if (vehicle && PawAudio.Instance != null && engine == null)
            {
                string n = source ? source.name : name;
                basePitch = n.StartsWith("Bus") || n.StartsWith("Truck") ? 0.75f : n.StartsWith("Scooter") ? 1.6f : n.StartsWith("Van") ? 0.9f : 1.05f;
                engine = PawAudio.LoopOn(gameObject, PawAudio.Instance.engineLoop, n.StartsWith("Scooter") ? 0.22f : 0.3f, 26f);
            }
            else if (engine != null && !engine.isPlaying) engine.Play();   // recycled from the pool
            // start part-way along the first cell so nothing appears on the cell edge in a clump
            idx = 1;
            Vector3 p = Vector3.Lerp(path[0], path[1], (float)rnd.NextDouble() * 0.6f);
            transform.position = Ground(p);
            transform.rotation = Quaternion.LookRotation(V[heading]);
            speed = maxSpeed * 0.7f;
        }

        float LaneOffset(string tile) => vehicle ? 2.5f : (tile == "River_RoadBridge" ? 5.15f : 5.85f);

        void BuildPath()
        {
            path.Clear();
            rnd ??= new System.Random(cell.x * 7919 + cell.y);   // lost if scripts reload during play
            var g = tr.generator;
            int mask = g.RoadMask(cell);
            int inSide = Opp(heading);
            if (mask == 0 || (mask & Bit(inSide)) == 0) { dead = true; return; }
            var outs = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                if (d == inSide || (mask & Bit(d)) == 0) continue;
                if (!vehicle && d == Left(heading)) continue;   // people only go straight or turn right (no diagonal road crossing)
                outs.Add(d);
                if (d == heading) outs.Add(d);                   // straight is twice as likely
            }
            int o = outs.Count > 0 ? outs[rnd.Next(outs.Count)] : inSide;
            nextHeading = o;

            string tile = g.TileName(cell);
            float H = tr.Half, off = LaneOffset(tile);
            Vector3 c = g.CellCenter(cell);
            Vector3 E = c - V[heading] * H + V[Right(heading)] * off;
            Vector3 X = c + V[o] * H + V[Right(o)] * off;

            if (tile == "Road_Roundabout" && o != inSide)
            {
                float r = vehicle ? 7f : 11f;   // cars on the ring road, people on the outer sidewalk
                path.Add(E);
                float a0 = Mathf.Atan2(E.z - c.z, E.x - c.x), a1 = Mathf.Atan2(X.z - c.z, X.x - c.x);
                if (vehicle)
                {
                    while (a1 <= a0 + 0.3f) a1 += Mathf.PI * 2f;   // counter-clockwise (right-hand traffic)
                }
                else
                {
                    while (a1 >= a0 - 0.3f) a1 -= Mathf.PI * 2f;   // people hug the corner they are on
                }
                int n = 14;
                for (int i = 1; i < n; i++)
                {
                    float a = Mathf.Lerp(a0 + Mathf.Sign(a1 - a0) * 0.25f, a1 - Mathf.Sign(a1 - a0) * 0.25f, i / (float)n);
                    path.Add(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r);
                }
                path.Add(X);
            }
            else if (o == heading)
            {
                path.Add(E); path.Add(X);
            }
            else if (o == inSide)
            {
                // U-turn (dead end): pull to the middle, swing across, come back
                Vector3 m1 = c + V[Right(heading)] * off;
                Vector3 m2 = c - V[Right(heading)] * off;
                path.Add(E); path.Add(m1 + V[heading] * 2f); path.Add(m2 + V[heading] * 2f); path.Add(X);
            }
            else
            {
                Vector3 P = c + V[Right(heading)] * off + V[Right(o)] * off;   // where the two lane lines meet
                const int n = 10;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n, u = 1f - t;
                    path.Add(u * u * E + 2f * u * t * P + t * t * X);
                }
            }
            committed = false;
        }

        float RemainingPath()
        {
            Vector3 p = transform.position; p.y = 0;
            float d = 0;
            Vector3 prev = p;
            for (int i = idx; i < path.Count; i++)
            {
                Vector3 q = path[i]; q.y = 0;
                d += Vector3.Distance(prev, q);
                prev = q;
            }
            return d;
        }

        bool MustStopBefore(Vector2Int next, int head)
        {
            var g = tr.generator;
            string tile = g.TileName(next);
            if (tile == "Road_Cross" || tile == "Road_T")
            {
                int axis = (head == 0 || head == 2) ? 0 : 1;
                return TrafficLightCycle.PhaseFor(axis, Time.time, 6f, 2f) != TrafficLightCycle.Phase.Green;
            }
            if (tile == "Road_Crosswalk") return TrafficLightCycle.PedGreenAt(g.CellCenter(next), Time.time);
            if (tile == "Rail_RoadCrossing") return PawTrain.ClosedAt(g.CellCenter(next));
            return false;
        }

        void Update()
        {
            if (dead || tr == null) return;
            float dt = Time.deltaTime;
            float target = maxSpeed;
            Vector3 fwd = transform.forward; fwd.y = 0; fwd.Normalize();

            if (vehicle)
            {
                // signals at the next cell: decide while far, commit once close
                float remain = RemainingPath();
                if (remain > 3f)
                {
                    stopForSignal = remain < 14f && MustStopBefore(cell + new Vector2Int((int)V[nextHeading].x, (int)V[nextHeading].z), nextHeading);
                }
                else if (!committed) committed = true;
                if (stopForSignal && !committed) target = Mathf.Min(target, Mathf.Max(0f, remain - 3f) * 1.2f);

                // things in the lane ahead: the pet, people, other cars
                bool ghost = Time.time < ghostUntil;
                float block = float.MaxValue;
                bool petAhead = false;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                Vector3 me = transform.position;
                if (tr.pet)
                {
                    float before = block;
                    Check(tr.pet.position, 1.6f, halfLength + 8f, halfLength + 1.6f);
                    petAhead = block < before;
                }
                foreach (var a in tr.agents)
                {
                    if (a == this || a.dead) continue;
                    if (a.vehicle)
                    {
                        // queues (same direction) are always respected; only crossing traffic can be ghosted to unjam
                        if (ghost && a.heading != heading) continue;
                        float keep = halfLength + a.halfLength + 1.8f;
                        Check(a.transform.position, 1.5f, keep + 6f, keep);
                    }
                    else Check(a.transform.position, 1.4f, halfLength + 7f, halfLength + 1.4f);
                }
                void Check(Vector3 p, float lateral, float look, float keep)
                {
                    Vector3 v = p - me; v.y = 0;
                    float ahead = Vector3.Dot(v, fwd);
                    if (ahead <= 0.5f || ahead > look) return;
                    if (Mathf.Abs(Vector3.Dot(v, right)) > lateral) return;
                    block = Mathf.Min(block, ahead - keep);
                }
                if (block < float.MaxValue) target = Mathf.Min(target, Mathf.Max(0f, block) * 1.5f);

                // a cat in the road: wait, then give a cute double honk (not too often)
                petBlockedFor = petAhead && speed < 1.5f ? petBlockedFor + dt : 0f;
                if (petBlockedFor > 0.7f && Time.time > nextHonk && PawAudio.Instance != null)
                {
                    PawAudio.Instance.Play(PawAudio.Instance.horn, transform.position, 0.8f, basePitch * Random.Range(0.95f, 1.1f), 35f);
                    nextHonk = Time.time + Random.Range(3.5f, 6f);
                }

                // unjam: cars waiting on each other in a junction ghost through after a few seconds
                if (speed < 0.2f && !stopForSignal) stuckTime += dt; else stuckTime = 0f;
                if (stuckTime > 4f) { ghostUntil = Time.time + 2.5f; stuckTime = 0f; }
            }

            float accel = target > speed ? (vehicle ? 5f : 3f) : (vehicle ? 14f : 6f);
            speed = Mathf.MoveTowards(speed, target, accel * dt);

            // advance along the path
            float step = speed * dt;
            Vector3 pos = transform.position;
            Vector3 flatPos = new Vector3(pos.x, 0, pos.z);
            while (step > 0f)
            {
                if (idx >= path.Count)
                {
                    cell += new Vector2Int((int)V[nextHeading].x, (int)V[nextHeading].z);
                    heading = nextHeading;
                    BuildPath();
                    if (dead) return;
                    idx = 1;
                }
                Vector3 tgt = path[idx]; tgt.y = 0;
                float d = Vector3.Distance(flatPos, tgt);
                if (d <= step) { flatPos = tgt; step -= d; idx++; }
                else { flatPos += (tgt - flatPos) / d * step; step = 0f; }
            }

            Vector3 dir = Vector3.zero;
            if (idx < path.Count) { dir = path[idx] - flatPos; dir.y = 0; }
            Vector3 normal;
            Vector3 g = Ground(flatPos, out normal);
            transform.position = g;
            if (dir.sqrMagnitude > 0.0004f)
            {
                Quaternion yaw = Quaternion.LookRotation(dir.normalized);
                Quaternion tilt = vehicle ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;
                transform.rotation = Quaternion.Slerp(transform.rotation, tilt * yaw, 1f - Mathf.Exp(-8f * dt));
            }

            if (engine != null) engine.pitch = basePitch * (0.75f + 0.55f * Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeed)));

            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetFloat("Speed", speed);
                anim.SetFloat("AnimSpeed", Mathf.Clamp(speed / 0.95f, 0.5f, 1.6f));
            }
        }

        Vector3 Ground(Vector3 p) => Ground(p, out _);

        Vector3 Ground(Vector3 p, out Vector3 normal)
        {
            normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, 6f, p.z), Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i].collider;
                if (col is CharacterController) continue;
                if (col.GetComponentInParent<PawAgent>() != null) continue;
                if (col.GetComponentInParent<PawTrain>() != null) continue;
                if (!(col is MeshCollider)) continue;   // only real ground surfaces (roads, sidewalks, grass, bridge decks)
                if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; normal = hits[i].normal; }
            }
            return new Vector3(p.x, y, p.z);
        }
    }
}
