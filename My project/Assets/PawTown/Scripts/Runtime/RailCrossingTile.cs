using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>On the Rail_RoadCrossing tile. Call Close()/Open() from your train logic, or leave autoCycle on.</summary>
    public class RailCrossingTile : MonoBehaviour
    {
        public bool autoCycle = true;
        public float period = 24f, closedFor = 7f;
        RailCrossingPart[] parts;

        public bool IsClosed { get; private set; }

        void OnEnable() => parts = GetComponentsInChildren<RailCrossingPart>(true);

        public void Close() { IsClosed = true; Apply(); }
        public void Open() { IsClosed = false; Apply(); }

        AudioSource bell;

        void Apply()
        {
            foreach (var p in parts) p.closed = IsClosed;
            if (IsClosed && PawAudio.Instance != null)
            {
                if (bell == null) bell = PawAudio.LoopOn(gameObject, PawAudio.Instance.crossingBell, 0.6f, 40f);
                else if (!bell.isPlaying) bell.Play();
            }
            else if (bell != null) bell.Stop();
        }

        void OnDisable() { if (bell != null) bell.Stop(); IsClosed = false; }

        void Update()
        {
            if (PawTraffic.Instance != null && PawTraffic.Instance.trainLoco != null)
            {
                bool t = PawTrain.ClosedAt(transform.position);   // real trains drive the barriers
                if (t != IsClosed) { IsClosed = t; Apply(); }
                return;
            }
            if (!autoCycle) return;
            bool c = Mathf.Repeat(Time.time + transform.position.x * 0.05f, period) < closedFor;
            if (c != IsClosed) { IsClosed = c; Apply(); }
        }
    }
}
