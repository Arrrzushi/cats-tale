using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Pet energy, the treat bag, and treat spawning.
    /// Energy drains while walking (more when running / jumping) and slowly refills when resting. Low energy = slower
    /// cat, and below runMinEnergy it cannot run. Treats lie around the town (sidewalks, gardens, parks); walking into
    /// one puts it in the bag (or eats it straight away when the bag is full). Tap a bag slot to eat it.
    /// </summary>
    public class PawGame : MonoBehaviour
    {
        public static PawGame Instance { get; private set; }

        public PawTownGenerator generator;
        public Transform pet;

        [Header("Energy")]
        public float maxEnergy = 100f;
        public float energy = 100f;
        public float walkDrain = 0.6f;     // per second while walking (full bar = about 5.5 minutes of walking)
        public float runDrain = 1.8f;      // per second while running
        public float jumpCost = 1.5f;
        public float restRegen = 1.2f;
        public float swimDrain = 2.4f;     // per second while paddling (about 4x walking); treading water costs half of it     // per second while standing still
        public float runMinEnergy = 12f;
        [Tooltip("Speed multiplier at 0 energy")] public float minSpeedFactor = 0.55f;
        [Tooltip("Speed multiplier at full energy (1 = the old top speed, reached at half energy)")] public float maxSpeedFactor = 1.5f;

        [Header("Treats")]
        public GameObject fishPrefab, canPrefab, yarnPrefab;
        public Sprite fishIcon, canIcon, yarnIcon;
        public int treatCount = 14;
        public float pickupRadius = 0.9f;
        public int bagSize = 5;

        public readonly List<TreatKind> bag = new List<TreatKind>();
        public readonly List<PawTreat> treats = new List<PawTreat>();

        bool freeRoam;
        /// <summary>Free Roam mode: no treats lying around (they come back with the next delivery).</summary>
        public bool FreeRoam
        {
            get => freeRoam;
            set
            {
                freeRoam = value;
                if (value) for (int i = treats.Count - 1; i >= 0; i--) Recycle(treats[i]);
                Changed?.Invoke();
            }
        }

        /// <summary>Raised when energy or the bag changes (HUD refresh).</summary>
        public event Action Changed;
        /// <summary>Raised when a treat is picked up (bool = went into the bag, false = eaten straight away).</summary>
        public event Action<TreatKind, bool> Collected;
        public event Action<TreatKind> Eaten;
        /// <summary>Energy just ran out (fires once, re-arms when energy is back above 20).</summary>
        public event Action Exhausted;
        bool exhaustedArmed = true;

        PawTownDemoMover mover;
        readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        System.Random rnd = new System.Random(11);
        float nextSpawn;
        static readonly Collider[] overlap = new Collider[16];
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public float Energy01 => Mathf.Clamp01(energy / maxEnergy);
        /// <summary>Half energy = 1 (the old top speed); full energy = maxSpeedFactor; empty = minSpeedFactor.</summary>
        public float SpeedMultiplier => Energy01 >= 0.5f
            ? Mathf.Lerp(1f, maxSpeedFactor, (Energy01 - 0.5f) / 0.5f)
            : Mathf.Lerp(minSpeedFactor, 1f, Mathf.SmoothStep(0f, 1f, Energy01 / 0.5f));
        public bool CanRun => energy > runMinEnergy;

        /// <summary>Per-order stats for the results screen (reset when an order is accepted).</summary>
        public int Bumps { get; private set; }
        public int TreatsCollected { get; private set; }
        float bumpCooldown;
        public void ResetOrderStats() { Bumps = 0; TreatsCollected = 0; }

        // ---- penalties (traffic rules)
        float roadTime, offRoadTime; bool roadArmed = true;
        /// <summary>Raised with the reason and what was taken ("-1 Golden Fish").</summary>
        public event Action<string, string> Penalized;
        public int Penalties { get; private set; }

        void Penalty(string reason)
        {
            string lost;
            if (PawProgress.Fish > 0) { PawProgress.AddFish(-1); lost = "-1 Golden Fish"; }
            else { PawProgress.AddCoins(-10); lost = "-10 Paw Coins"; }
            Penalties++;
            PawAudio.Instance?.Oops();
            Penalized?.Invoke(reason, lost);
        }

        public static float EnergyOf(TreatKind k) => k == TreatKind.Fish ? 25f : k == TreatKind.Can ? 40f : 15f;
        public Sprite IconOf(TreatKind k) => k == TreatKind.Fish ? fishIcon : k == TreatKind.Can ? canIcon : yarnIcon;
        public static string NameOf(TreatKind k) => k == TreatKind.Fish ? "Fish cookie" : k == TreatKind.Can ? "Tuna can" : "Yarn ball";

        /// <summary>Fresh start after leaving a run: full energy, no treats lying around (the bag is kept, it is inventory).</summary>
        public void ResetRun()
        {
            for (int i = treats.Count - 1; i >= 0; i--) Recycle(treats[i]);
            energy = maxEnergy;
            exhaustedArmed = true;
            freeRoam = false;
            ResetOrderStats();
            Changed?.Invoke();
        }

        void Awake()
        {
            Instance = this;
            bagSize = PawSave.BagSize;
            bag.AddRange(PawSave.LoadBag());
            if (bag.Count > bagSize) bag.RemoveRange(bagSize, bag.Count - bagSize);
            Changed += () => PawSave.SaveBag(bag);
        }

        /// <summary>Put a treat in the bag (shop, rewards). False when the bag is full.</summary>
        public bool AddToBag(TreatKind k)
        {
            if (bag.Count >= bagSize) return false;
            bag.Add(k);
            Changed?.Invoke();
            return true;
        }

        public void DropItem(int i)
        {
            if (i < 0 || i >= bag.Count) return;
            bag.RemoveAt(i);
            Changed?.Invoke();
        }

        public void UpgradeBag(int slots)
        {
            PawSave.BagSize = PawSave.BagSize + slots;
            bagSize = PawSave.BagSize;
            Changed?.Invoke();
        }

        public void Refill() => AddEnergy(maxEnergy);
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SpendJump() => AddEnergy(-jumpCost);

        public void AddEnergy(float v)
        {
            float before = energy;
            energy = Mathf.Clamp(energy + v, 0f, maxEnergy);
            if (Mathf.Abs(before - energy) > 0.001f) Changed?.Invoke();
        }

        /// <summary>Eat the treat in bag slot i.</summary>
        public bool UseItem(int i)
        {
            if (i < 0 || i >= bag.Count) return false;
            var k = bag[i];
            bag.RemoveAt(i);
            AddEnergy(EnergyOf(k));
            PawAudio.Instance?.Meow();             // a happy meow every time she eats
            PawAudio.Instance?.Success();
            Eaten?.Invoke(k);
            Changed?.Invoke();
            return true;
        }

        void Update()
        {
            if (!pet || !generator || generator.library == null) return;
            if (!mover) mover = pet.GetComponent<PawTownDemoMover>();

            // energy drain / regen (Changed fires a few times a second, not every frame)
            float s = mover ? mover.CurrentSpeed : 0f;
            bool sprinting = mover != null && mover.IsSprinting && s > 0.5f;
            float d = sprinting ? -runDrain : s > 0.1f ? -walkDrain : restRegen;
            if (mover != null && mover.Swimming) d = s > 0.1f ? -swimDrain : -swimDrain * 0.5f;
            float before = energy;
            // ran dry (walking, or an instant cost): ask for help once, before the slow rest-regen ticks it back up
            if (energy <= 0.5f && exhaustedArmed && mover != null && mover.enabled) { exhaustedArmed = false; Exhausted?.Invoke(); }
            energy = Mathf.Clamp(energy + d * Time.deltaTime, 0f, maxEnergy);
            if (Mathf.FloorToInt(before * 4f) != Mathf.FloorToInt(energy * 4f)) Changed?.Invoke();
            if (energy > 20f) exhaustedArmed = true;

            // bumps: a car got really close to the cat
            Vector3 pp = pet.position;
            bumpCooldown -= Time.deltaTime;
            var tr = PawTraffic.Instance;
            if (tr != null && bumpCooldown <= 0f)
                foreach (var a in tr.Agents)
                    if (a != null && a.IsVehicle && (a.transform.position - pp).sqrMagnitude < 1.6f * 1.6f) { Bumps++; bumpCooldown = 2f; Penalty("Watch out for cars!"); break; }

            // traffic rules: walking on the road away from a zebra crossing costs a fish (once per time on the road)
            var ord = PawOrders.Instance;
            if (ord != null && mover != null && mover.enabled && !mover.Swimming)
            {
                bool jaywalk = ord.IsOnRoad(pp) && !ord.IsOnZebra(pp);
                if (jaywalk) { roadTime += Time.deltaTime; offRoadTime = 0f; }
                else { offRoadTime += Time.deltaTime; if (offRoadTime > 1f) { roadTime = 0f; roadArmed = true; } }
                if (jaywalk && roadArmed && roadTime > 0.35f) { roadArmed = false; Penalty("Use the zebra crossing!"); }
            }

            // pick-ups
            for (int i = treats.Count - 1; i >= 0; i--)
            {
                var t = treats[i];
                Vector3 v = t.basePos - pp;
                if (Mathf.Abs(v.y) < 1.4f && new Vector2(v.x, v.z).sqrMagnitude < pickupRadius * pickupRadius)
                {
                    Collect(t);
                    continue;
                }
                var c = generator.CellOf(t.basePos) - generator.CellOf(pp);
                if (Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) > generator.radius) Recycle(t);   // walked away from it
            }

            if (!freeRoam && Time.time > nextSpawn && treats.Count < treatCount)
            {
                nextSpawn = Time.time + 0.15f;
                TrySpawn();
            }
        }

        void Collect(PawTreat t)
        {
            var k = t.kind;
            Recycle(t);
            TreatsCollected++;
            PawSave.MissionAdd("treats");
            bool stored = bag.Count < bagSize;
            if (stored) bag.Add(k);
            else { AddEnergy(EnergyOf(k)); PawAudio.Instance?.Meow(); }   // bag full: eaten on the spot
            PawAudio.Instance?.Success();
            Collected?.Invoke(k, stored);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ spawning
        void TrySpawn()
        {
            var pc = generator.CellOf(pet.position);
            int R = Mathf.Max(2, generator.radius - 1);
            float H = generator.library.tileSize * 0.5f;
            for (int tries = 0; tries < 6; tries++)
            {
                var cell = pc + new Vector2Int(rnd.Next(-R, R + 1), rnd.Next(-R, R + 1));
                Vector3 c = generator.CellCenter(cell);
                int mask = generator.RoadMask(cell);
                Vector3 p;
                if (mask != 0)
                {
                    // on a sidewalk, never on the road itself
                    bool alongZ = (mask & (Dir.N | Dir.S)) != 0;
                    float side = rnd.Next(2) == 0 ? -5.8f : 5.8f, along = (float)(rnd.NextDouble() * 2 - 1) * (H - 2.5f);
                    p = c + (alongZ ? new Vector3(side, 0, along) : new Vector3(along, 0, side));
                }
                else
                {
                    p = c + new Vector3((float)(rnd.NextDouble() * 2 - 1) * (H - 2f), 0, (float)(rnd.NextDouble() * 2 - 1) * (H - 2f));
                }
                if ((p - pet.position).sqrMagnitude < 36f) continue;
                if (!GroundAt(p, out Vector3 g)) continue;               // tile not spawned yet, or water
                if (Blocked(g)) continue;                                  // inside a house, tree, bench...
                bool near = false;
                foreach (var o in treats) if ((o.basePos - g).sqrMagnitude < 25f) { near = true; break; }
                if (near) continue;
                Spawn(PickKind(), g);
                return;
            }
        }

        TreatKind PickKind()
        {
            double r = rnd.NextDouble();
            return r < 0.5 ? TreatKind.Fish : r < 0.78 ? TreatKind.Yarn : TreatKind.Can;   // cans are the rare big boost
        }

        static bool GroundAt(Vector3 p, out Vector3 g)
        {
            g = p;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, 25f, p.z), Vector3.down, hits, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (!(hits[i].collider is MeshCollider)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; g = hits[i].point; found = true; }
            }
            return found && g.y > -0.2f;
        }

        static bool Blocked(Vector3 g)
        {
            int n = Physics.OverlapSphereNonAlloc(g + Vector3.up * 0.7f, 0.55f, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (overlap[i] is MeshCollider || overlap[i] is CharacterController) continue;
                return true;
            }
            return false;
        }

        void Spawn(TreatKind k, Vector3 ground)
        {
            var prefab = k == TreatKind.Fish ? fishPrefab : k == TreatKind.Can ? canPrefab : yarnPrefab;
            if (!prefab) return;
            GameObject go;
            if (pool.TryGetValue(prefab, out var st) && st.Count > 0) { go = st.Pop(); go.SetActive(true); }
            else
            {
                go = Instantiate(prefab, transform);
                go.transform.localScale = Vector3.one * 1.3f;   // easy to spot
                foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            }
            var t = go.GetComponent<PawTreat>();
            if (t == null) t = go.AddComponent<PawTreat>();
            t.kind = k;
            t.source = prefab;
            t.Place(ground);
            treats.Add(t);
        }

        void Recycle(PawTreat t)
        {
            treats.Remove(t);
            t.gameObject.SetActive(false);
            if (!pool.TryGetValue(t.source, out var st)) pool[t.source] = st = new Stack<GameObject>();
            st.Push(t.gameObject);
        }
    }
}
