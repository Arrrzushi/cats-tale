using UnityEngine;
using UnityEngine.EventSystems;

namespace PawTown
{
    /// <summary>Drag sideways on a 3D preview to spin the model; reports degrees and when the last drag happened.</summary>
    public class PawDragSpin : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public float yaw;                 // accumulated turn, degrees
        public float lastTouch = -99f;    // unscaled time of the last drag
        public bool dragging;
        public float degreesPerPixel = 0.45f;

        public void OnBeginDrag(PointerEventData e) { dragging = true; lastTouch = Time.unscaledTime; }
        public void OnDrag(PointerEventData e) { yaw -= e.delta.x * degreesPerPixel; lastTouch = Time.unscaledTime; }
        public void OnEndDrag(PointerEventData e) { dragging = false; lastTouch = Time.unscaledTime; }
    }
}
