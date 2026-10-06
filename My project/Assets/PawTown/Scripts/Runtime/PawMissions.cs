using UnityEngine;

namespace PawTown
{
    /// <summary>Daily mission tracking that needs the world: safe zebra crossings (sidewalk -> zebra only -> sidewalk).
    /// Deliveries and treats are counted where they happen (PawMenus / PawGame).</summary>
    public class PawMissions : MonoBehaviour
    {
        public Transform pet;
        bool onRoad, clean;
        Vector3 enter;

        void Update()
        {
            var o = PawOrders.Instance;
            if (o == null || pet == null) return;
            Vector3 p = pet.position;
            bool road = o.IsOnRoad(p);
            if (road && !onRoad) { onRoad = true; clean = true; enter = p; }
            if (road && !o.IsOnZebra(p)) clean = false;      // stepped off the zebra: jaywalking, does not count
            if (!road && onRoad)
            {
                onRoad = false;
                Vector3 d = p - enter; d.y = 0f;
                if (clean && d.magnitude > 7f) PawSave.MissionAdd("zebra");
            }
        }
    }
}
