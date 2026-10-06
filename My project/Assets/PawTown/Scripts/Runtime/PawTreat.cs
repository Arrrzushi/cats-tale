using UnityEngine;

namespace PawTown
{
    public enum TreatKind { Fish, Can, Yarn }

    /// <summary>A collectible treat lying in the town. Bobs and spins; PawGame picks it up when the pet touches it.</summary>
    public class PawTreat : MonoBehaviour
    {
        public TreatKind kind;
        internal GameObject source;
        internal Vector3 basePos;
        /// <summary>Resting spot on the ground (+0.25 m), without the bob.</summary>
        public Vector3 BasePos => basePos;
        float phase;

        public void Place(Vector3 groundPoint)
        {
            basePos = groundPoint + Vector3.up * 0.25f;
            transform.position = basePos;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time + phase;
            transform.position = basePos + Vector3.up * (0.12f * Mathf.Sin(t * 2.4f));
            transform.rotation = Quaternion.Euler(0f, t * 70f, 0f);
        }
    }
}
