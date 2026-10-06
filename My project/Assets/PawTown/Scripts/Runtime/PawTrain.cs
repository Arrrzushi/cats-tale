using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>A train running along a rail row (world X). Rail crossings close while one is near.</summary>
    public class PawTrain : MonoBehaviour
    {
        public static readonly List<PawTrain> Live = new List<PawTrain>();

        public int dir = 1;          // +1 = towards +X
        public float speed = 16f;
        public float length = 37f;   // loco + carriages, metres behind the nose

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        bool honked;

        void Update()
        {
            transform.position += Vector3.right * (dir * speed * Time.deltaTime);
            var l = Camera.main;
            if (!honked && l && PawAudio.Instance != null)
            {
                float ahead = (l.transform.position.x - transform.position.x) * dir;
                if (ahead > 0f && ahead < 110f && Mathf.Abs(l.transform.position.z - transform.position.z) < 80f)
                {
                    honked = true;
                    PawAudio.Instance.Play(PawAudio.Instance.trainHorn, transform.position, 1f, 1f, 160f);
                }
            }
        }

        /// <summary>True when a train is approaching, passing, or just past a crossing at p.</summary>
        public static bool ClosedAt(Vector3 p)
        {
            foreach (var t in Live)
            {
                if (Mathf.Abs(p.z - t.transform.position.z) > 13f) continue;
                float rel = (p.x - t.transform.position.x) * t.dir;   // >0: crossing is ahead of the nose
                if (rel < 75f && rel > -t.length - 8f) return true;
            }
            return false;
        }
    }
}
