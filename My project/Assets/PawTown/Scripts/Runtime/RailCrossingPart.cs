using System.Collections.Generic;
using UnityEngine;

namespace PawTown
{
    /// <summary>Barrier boom or flashing crossbuck. Controlled by the RailCrossingTile on the tile root.</summary>
    public class RailCrossingPart : MonoBehaviour
    {
        public bool closed;
        public float openAngle = 80f, speed = 120f, blinkRate = 1.6f;

        Transform boom;
        float sign = 1f, angle;
        readonly List<Renderer> lamps = new List<Renderer>();

        void Awake()
        {
            boom = FindDeep(transform, "BoomPivot");
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name.EndsWith("_CrossRed")) lamps.Add(r);
            if (boom != null)
            {
                // pick the rotation direction that lifts the arm tip upward
                Vector3 tip = Vector3.zero; int n = 0;
                foreach (var r in boom.GetComponentsInChildren<Renderer>()) { tip += boom.InverseTransformPoint(r.bounds.center); n++; }
                tip /= Mathf.Max(1, n);
                Vector3 up = boom.InverseTransformDirection(Vector3.up);
                Vector3 axis = Vector3.Cross(tip, up).normalized;
                sign = Vector3.Dot(Quaternion.AngleAxis(30f, axis) * tip, up) > Vector3.Dot(tip, up) ? 1f : -1f;
                liftAxis = axis;
                baseRot = boom.localRotation;
            }
            angle = closed ? 0 : openAngle;
        }

        Vector3 liftAxis = Vector3.forward;
        Quaternion baseRot;

        void Update()
        {
            if (boom != null)
            {
                angle = Mathf.MoveTowards(angle, closed ? 0f : openAngle, speed * Time.deltaTime);
                boom.localRotation = baseRot * Quaternion.AngleAxis(sign * angle, liftAxis);
            }
            for (int i = 0; i < lamps.Count; i++)
            {
                bool on = closed && Mathf.Repeat(Time.time * blinkRate + i * 0.5f, 1f) < 0.5f;
                if (lamps[i].enabled != on) lamps[i].enabled = on;
            }
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name.StartsWith(n)) return t;
            foreach (Transform c in t) { var f = FindDeep(c, n); if (f) return f; }
            return null;
        }
    }
}
