using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>
    /// Drives the lamp objects of a TrafficLight or PedLight prefab (child renderers named *_LampRed, *_LampYellow,
    /// *_LampGreen, *_PedRed, *_PedGreen). Lights facing +-Z and lights facing +-X run opposite phases, so a 4-way
    /// crossing is always consistent. Query PedestrianCanWalk from gameplay (the pet waits for green).
    /// </summary>
    public class TrafficLightCycle : MonoBehaviour
    {
        public enum Mode { Car, PedestrianOnly }
        public Mode mode = Mode.Car;
        public float green = 6f, yellow = 2f;
        public float pedGreen = 6f, pedRed = 8f;

        readonly List<Renderer> red = new List<Renderer>(), amber = new List<Renderer>(), grn = new List<Renderer>();
        readonly List<Renderer> pRed = new List<Renderer>(), pGrn = new List<Renderer>();
        int axis;
        float nextBeep;

        public enum Phase { Green, Yellow, Red }
        public Phase CarPhase { get; private set; }
        public bool PedestrianCanWalk { get; private set; }

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.EndsWith("_LampRed")) red.Add(r);
                else if (n.EndsWith("_LampYellow")) amber.Add(r);
                else if (n.EndsWith("_LampGreen")) grn.Add(r);
                else if (n.EndsWith("_PedRed")) pRed.Add(r);
                else if (n.EndsWith("_PedGreen")) pGrn.Add(r);
            }
        }

        void OnEnable() => axis = Mathf.Abs(transform.forward.z) >= Mathf.Abs(transform.forward.x) ? 0 : 1;

        /// <summary>Global, deterministic phase for an axis (0 = traffic along Z, 1 = along X).</summary>
        public static Phase PhaseFor(int axis, float t, float green, float yellow)
        {
            float half = green + yellow, cycle = 2 * half;
            float x = Mathf.Repeat(t + axis * half, cycle);
            if (x < green) return Phase.Green;
            if (x < half) return Phase.Yellow;
            return Phase.Red;
        }

        /// <summary>Mid-block crossing phase for the tile containing p. Shared by PedLights and cars, so they agree.</summary>
        public static bool PedGreenAt(Vector3 p, float t, float tile = 24f)
        {
            int cx = Mathf.RoundToInt(p.x / tile), cz = Mathf.RoundToInt(p.z / tile);
            int off = ((cx * 73856093) ^ (cz * 19349663)) & 15;
            return Mathf.Repeat(t + off, 14f) < 6f;
        }

        void Update()
        {
            float t = Time.time;
            if (mode == Mode.Car)
            {
                CarPhase = PhaseFor(axis, t, green, yellow);
                PedestrianCanWalk = CarPhase == Phase.Red;
            }
            else
            {
                PedestrianCanWalk = PedGreenAt(transform.position, t);
                CarPhase = PedestrianCanWalk ? Phase.Red : Phase.Green;
            }
            if (mode == Mode.PedestrianOnly && PedestrianCanWalk && PawAudio.Instance != null && Time.time >= nextBeep)
            {
                nextBeep = Time.time + 0.5f;
                var l = Camera.main;
                if (l && (l.transform.position - transform.position).sqrMagnitude < 30f * 30f)
                    PawAudio.Instance.Play(PawAudio.Instance.pedBeep, transform.position, 0.45f, 1f, 18f);
            }
            Set(red, CarPhase == Phase.Red);
            Set(amber, CarPhase == Phase.Yellow);
            Set(grn, CarPhase == Phase.Green);
            Set(pRed, !PedestrianCanWalk);
            Set(pGrn, PedestrianCanWalk);
        }

        static void Set(List<Renderer> rs, bool on)
        {
            for (int i = 0; i < rs.Count; i++) if (rs[i].enabled != on) rs[i].enabled = on;
        }
    }
}
