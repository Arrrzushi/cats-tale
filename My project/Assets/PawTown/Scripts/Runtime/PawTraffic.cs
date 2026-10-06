using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Living town: keeps cars and people moving on the streamed roads around the pet, and runs trains on the railway.
    /// Cars drive in the right-hand lane, turn at junctions, go round roundabouts, stop at red lights, at zebra
    /// crossings when the walk light is green, at closed rail crossings, behind other cars, and for the pet or people.
    /// </summary>
    public class PawTraffic : MonoBehaviour
    {
        public static PawTraffic Instance { get; private set; }

        public PawTownGenerator generator;
        public Transform pet;

        [Header("Vehicles")]
        public List<GameObject> vehiclePrefabs = new List<GameObject>();
        public int maxVehicles = 18;
        public float vehicleSpeed = 10f;

        [Header("People")]
        public List<GameObject> peoplePrefabs = new List<GameObject>();
        public int maxPeople = 16;
        public float peopleSpeed = 1.1f;

        [Header("Trains")]
        public GameObject trainLoco;
        public GameObject trainCarriage;
        public int trainCarriages = 3;
        public float trainSpeed = 16f;
        public Vector2 trainGapSeconds = new Vector2(10f, 22f);

        internal readonly List<PawAgent> agents = new List<PawAgent>();
        /// <summary>All live cars and people (read-only), e.g. to detect the pet being bumped.</summary>
        public IReadOnlyList<PawAgent> Agents => agents;
        readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        float nextSpawn, nextTrain = 3f;
        PawTrain train;
        System.Random rnd = new System.Random(7);

        public float Half => generator.library.tileSize * 0.5f;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (!generator || !pet || generator.library == null) return;
            var pc = generator.CellOf(pet.position);
            int R = generator.radius;
            for (int i = agents.Count - 1; i >= 0; i--)
            {
                var a = agents[i];
                if (a == null) { agents.RemoveAt(i); continue; }
                var d = a.cell - pc;
                if (a.dead || Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) > R) Recycle(a);
            }
            if (Time.time >= nextSpawn)
            {
                nextSpawn = Time.time + 0.2f;
                int cars = 0, ppl = 0;
                foreach (var a in agents) if (a.vehicle) cars++; else ppl++;
                if (cars < maxVehicles && vehiclePrefabs.Count > 0) TrySpawn(true, pc, R);
                if (ppl < maxPeople && peoplePrefabs.Count > 0) TrySpawn(false, pc, R);
            }
            UpdateTrain(pc, R);
        }

        // ------------------------------------------------------------------ cars + people
        void TrySpawn(bool vehicle, Vector2Int pc, int R)
        {
            int reach = Mathf.Max(2, R - 1);
            for (int tries = 0; tries < 12; tries++)
            {
                var off = new Vector2Int(rnd.Next(-reach, reach + 1), rnd.Next(-reach, reach + 1));
                if (Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.y)) < 2) continue;   // never pop in right next to the pet
                var cell = pc + off;
                int mask = generator.RoadMask(cell);
                if (mask == 0) continue;
                var heads = new List<int>();
                for (int d = 0; d < 4; d++) if ((mask & PawAgent.Bit(PawAgent.Opp(d))) != 0) heads.Add(d);
                if (heads.Count == 0) continue;
                var list = vehicle ? vehiclePrefabs : peoplePrefabs;
                var prefab = list[rnd.Next(list.Count)];
                var go = Take(prefab);
                var a = go.GetComponent<PawAgent>();
                if (a == null) a = go.AddComponent<PawAgent>();
                a.source = prefab;
                float spd = vehicle ? vehicleSpeed * (prefab.name.StartsWith("Bus") ? 0.75f : 1f) * (0.85f + 0.3f * (float)rnd.NextDouble())
                                    : peopleSpeed * (0.85f + 0.3f * (float)rnd.NextDouble());
                a.Init(this, vehicle, cell, heads[rnd.Next(heads.Count)], spd, rnd.Next());
                agents.Add(a);
                return;
            }
        }

        GameObject Take(GameObject prefab)
        {
            if (pool.TryGetValue(prefab, out var st) && st.Count > 0)
            {
                var g = st.Pop();
                g.SetActive(true);
                return g;
            }
            return Instantiate(prefab, transform);
        }

        internal void Recycle(PawAgent a)
        {
            agents.Remove(a);
            a.gameObject.SetActive(false);
            if (!pool.TryGetValue(a.source, out var st)) pool[a.source] = st = new Stack<GameObject>();
            st.Push(a.gameObject);
        }

        // ------------------------------------------------------------------ trains
        void UpdateTrain(Vector2Int pc, int R)
        {
            if (!trainLoco) return;
            if (train != null)
            {
                float ahead = (train.transform.position.x - pet.position.x) * train.dir;
                if (ahead > 220f || Mathf.Abs(train.transform.position.z - pet.position.z) > (R + 2) * generator.library.tileSize)
                {
                    Destroy(train.gameObject);
                    train = null;
                    nextTrain = Time.time + Mathf.Lerp(trainGapSeconds.x, trainGapSeconds.y, (float)rnd.NextDouble());
                }
                return;
            }
            if (Time.time < nextTrain) return;
            int row = int.MinValue;
            for (int dz = 0; dz <= R && row == int.MinValue; dz++)
            {
                if (generator.RailRow(pc.y + dz)) row = pc.y + dz;
                else if (generator.RailRow(pc.y - dz)) row = pc.y - dz;
            }
            if (row == int.MinValue) { nextTrain = Time.time + 2f; return; }
            int dir = rnd.Next(2) == 0 ? 1 : -1;
            float z = generator.CellCenter(new Vector2Int(0, row)).z;
            var root = new GameObject("Train");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(pet.position.x - dir * 200f, generator.transform.position.y, z);
            root.transform.rotation = Quaternion.Euler(0, dir > 0 ? 90f : -90f, 0);
            Instantiate(trainLoco, root.transform).transform.localPosition = Vector3.zero;
            float back = -9.0f;
            for (int i = 0; i < trainCarriages && trainCarriage; i++, back -= 9.6f)
                Instantiate(trainCarriage, root.transform).transform.localPosition = new Vector3(0, 0, back);
            if (PawAudio.Instance != null) PawAudio.LoopOn(root, PawAudio.Instance.trainLoop, 0.9f, 110f, 8f);
            train = root.AddComponent<PawTrain>();
            train.dir = dir;
            train.speed = trainSpeed;
            train.length = -back;
        }
    }
}
