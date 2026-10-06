using UnityEngine;

namespace PawTown
{
    /// <summary>Tiny idle motion for cosmetic bits: spin around local Y (propeller) and/or bob up and down (halo).</summary>
    public class PawSpin : MonoBehaviour
    {
        public float speed = 0f;     // degrees per second
        public float bob = 0f;       // metres
        Vector3 basePos;

        void Start() { basePos = transform.localPosition; }

        void Update()
        {
            if (speed != 0f) transform.Rotate(0f, speed * Time.unscaledDeltaTime, 0f, Space.Self);
            if (bob != 0f) transform.localPosition = basePos + Vector3.up * (bob * Mathf.Sin(Time.unscaledTime * 2.5f));
        }
    }
}
