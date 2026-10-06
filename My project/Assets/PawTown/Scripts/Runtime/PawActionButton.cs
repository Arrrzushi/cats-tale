using UnityEngine;
using UnityEngine.EventSystems;

namespace PawTown
{
    /// <summary>On-screen JUMP / RUN / MEOW / SOUND buttons. Jump and Meow are taps, Run is held, Sound toggles mute.</summary>
    public class PawActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum Action { Jump, Run, Meow, Sound }
        public UnityEngine.UI.Text label;
        [Tooltip("Sound button art (on / off)")] public Sprite soundOn, soundOff;
        public Action action;
        public float pressedScale = 0.9f;

        static int jumpFrame = -10, meowFrame = -10;
        static int runHolders;

        /// <summary>True on the frame (or the frame after) the JUMP button was pressed.</summary>
        public static bool JumpPressed => Time.frameCount - jumpFrame <= 1;
        public static bool RunHeld => runHolders > 0;

        /// <summary>Returns true once per tap.</summary>
        public static bool ConsumeJump()
        {
            if (!JumpPressed) return false;
            jumpFrame = -10;
            return true;
        }

        public static bool ConsumeMeow()
        {
            if (Time.frameCount - meowFrame > 1) return false;
            meowFrame = -10;
            return true;
        }

        void Start() => RefreshLabel();

        void RefreshLabel()
        {
            if (action != Action.Sound) return;
            bool muted = PawAudio.Instance != null && PawAudio.Instance.Muted;
            if (label) label.text = muted ? "SOUND\nOFF" : "SOUND\nON";
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img && soundOn && soundOff) img.sprite = muted ? soundOff : soundOn;
        }

        bool down;

        public void OnPointerDown(PointerEventData e)
        {
            down = true;
            transform.localScale = Vector3.one * pressedScale;
            switch (action)
            {
                case Action.Jump: jumpFrame = Time.frameCount; break;
                case Action.Run: runHolders++; break;
                case Action.Meow: meowFrame = Time.frameCount; break;
                case Action.Sound:
                    if (PawAudio.Instance != null) { PawAudio.Instance.ToggleMute(); PawAudio.Instance.Click(); }
                    RefreshLabel();
                    break;
            }
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (action == Action.Run) Release(); }
        void OnDisable() => Release();

        void Release()
        {
            if (!down) return;
            down = false;
            transform.localScale = Vector3.one;
            if (action == Action.Run) runHolders = Mathf.Max(0, runHolders - 1);
        }
    }
}
