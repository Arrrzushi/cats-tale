using UnityEngine;
using UnityEngine.UI;

namespace PawTown
{
    /// <summary>CAT VIEW / TOP VIEW toggle button.</summary>
    [RequireComponent(typeof(Button))]
    public class PawViewButton : MonoBehaviour
    {
        public PawTownFollowCamera followCamera;
        public Text label;

        void Start()
        {
            if (!followCamera) followCamera = FindAnyObjectByType<PawTownFollowCamera>();
            GetComponent<Button>().onClick.AddListener(() => { if (followCamera) followCamera.Toggle(); PawAudio.Instance?.Click(); });
            if (followCamera) followCamera.ModeChanged += Refresh;
            Refresh(followCamera ? followCamera.mode : PawTownFollowCamera.Mode.CatView);
        }

        void OnDestroy() { if (followCamera) followCamera.ModeChanged -= Refresh; }

        // the label says what tapping it will switch to
        void Refresh(PawTownFollowCamera.Mode m)
        {
            if (label) label.text = m == PawTownFollowCamera.Mode.CatView ? "TOP\nVIEW" : "CAT\nVIEW";
        }
    }
}
