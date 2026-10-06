using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PawTown
{
    /// <summary>
    /// Cinematic follow camera with two views (CAT VIEW button / V key):
    ///  * CAT VIEW (default): low and behind the cat, aimed ahead of where she walks so you see the street in front.
    ///    It swings in behind her smoothly while she walks (not when you pull the stick back toward the camera).
    ///  * TOP VIEW: the high three-quarter overview along the street grid.
    /// Shared:
    ///  * High three-quarter view along the street grid; the cat sits a little below centre so you see the road ahead.
    ///  * Smooth follow (soft vertical so hops don't bounce the frame) with a small look-ahead in the walking direction,
    ///    and a slightly wider view while sprinting.
    ///  * Never turns while you steer. If you turned it by hand, it gently squares back to the street grid when you rest.
    ///    Drag the right side of the screen (or Q / E) to turn it yourself.
    ///  * Short cinematic beats: a swoop when you pick up an order, a low slow orbit when you deliver, a cosy push-in
    ///    when the cat sits still.
    ///  * Houses / trees between the camera and the cat dither-fade out so the cat is never hidden.
    /// </summary>
    public class PawTownFollowCamera : MonoBehaviour
    {
        public Transform target;

        public enum Mode { CatView, TopView }
        public Mode mode = Mode.CatView;
        public event System.Action<Mode> ModeChanged;

        [Header("Cat view (behind the cat)")]
        public float catDistance = 7.5f;
        public float catPitch = 22f;
        public float catFov = 52f;
        public float catAimAhead = 2.0f;      // aim this far in front of the cat
        [Tooltip("How quickly the camera swings in behind the cat while she walks")] public float followTurn = 2.2f;

        [Header("Top view framing")]
        public float distance = 10f;
        public float pitch = 44f;
        public float fov = 40f;
        public float sprintFov = 46f;
        [Tooltip("Aim this far above the cat: puts the cat a bit below centre")] public float aimHeight = 0.9f;
        public float lookAheadTime = 0.35f, lookAheadMax = 1.8f, lookAheadSmooth = 0.4f;
        public float followSmoothH = 0.25f, followSmoothV = 0.6f;

        [Header("Yaw")]
        public float yaw = 0f;
        public float recenterDelay = 2f;
        public float recenterSpeed = 25f;     // degrees per second (max)
        public float keyOrbitSpeed = 90f;
        public float manualHold = 3f;         // seconds after a manual turn before auto-recentre resumes

        [Header("Cinematic")]
        public bool cinematicBeats = true;
        public float idlePushInAfter = 6f;

        [Header("Occlusion")]
        [Range(0f, 1f)] public float fadeTo = 0.25f;
        public float probeRadius = 0.35f;

        Camera cam;
        PawTownDemoMover mover;
        CharacterController petCC;
        Vector3 focus, focusVelH, lookAhead, lookAheadVel;
        float focusVelY, idle, lastManual = -10f;
        float curDist, curPitch, curYawOff, curFov;
        float beatStart = -10f, beatDur;
        int beatKind;   // 1 = pickup swoop, 2 = delivery orbit

        readonly Dictionary<Renderer, float> fade = new Dictionary<Renderer, float>();
        readonly Dictionary<Collider, Renderer[]> rendCache = new Dictionary<Collider, Renderer[]>();
        readonly HashSet<Renderer> hitNow = new HashSet<Renderer>();
        MaterialPropertyBlock mpb;
        readonly RaycastHit[] hitBuf = new RaycastHit[32];          // no garbage per frame
        readonly List<Renderer> fadeKeys = new List<Renderer>();
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        void Start()
        {
            cam = GetComponent<Camera>();
            mpb = new MaterialPropertyBlock();
            if (target)
            {
                mover = target.GetComponent<PawTownDemoMover>();
                petCC = target.GetComponent<CharacterController>();
                focus = target.position;
            }
            curDist = distance; curPitch = pitch; curFov = fov;
            if (PawOrders.Instance != null) PawOrders.Instance.StageChanged += OnStage;
        }

        void OnDestroy() { if (PawOrders.Instance != null) PawOrders.Instance.StageChanged -= OnStage; }

        void OnStage(PawOrders.Stage s)
        {
            if (!cinematicBeats) return;
            if (Time.time - beatStart < 8f && beatKind != 0) return;   // at most one beat every few seconds
            if (s == PawOrders.Stage.Deliver) Beat(1, 1.0f);
            else if (s == PawOrders.Stage.Done) Beat(2, 1.8f);
        }

        void Beat(int kind, float dur) { beatKind = kind; beatStart = Time.time; beatDur = dur; }

        public void Toggle() => SetMode(mode == Mode.CatView ? Mode.TopView : Mode.CatView);

        public void SetMode(Mode m)
        {
            mode = m;
            if (m == Mode.CatView && target) yaw = target.eulerAngles.y;      // start right behind her
            else yaw = Mathf.Round(yaw / 90f) * 90f;                          // top view sits on the street grid
            ModeChanged?.Invoke(m);
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.deltaTime;

            // ---- manual orbit (drag right side / Q E), recentre when idle
            float orbit = PawCameraDrag.ConsumeYaw();
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.vKey.wasPressedThisFrame) Toggle();
                if (k.qKey.isPressed) orbit -= keyOrbitSpeed * dt;
                if (k.eKey.isPressed) orbit += keyOrbitSpeed * dt;
            }
#endif
            if (Mathf.Abs(orbit) > 0.0001f) { yaw += orbit; lastManual = Time.time; }

            bool moving = mover != null && (mover.InputVector.sqrMagnitude > 0.01f || mover.AutoWalking || mover.CurrentSpeed > 0.15f);
            idle = moving ? 0f : idle + dt;
            if (mode == Mode.CatView)
            {
                // swing in behind the cat while she walks (not while backing toward the camera, not right after a manual turn)
                bool backing = mover != null && mover.InputVector.y < -0.5f && Mathf.Abs(mover.InputVector.x) < 0.5f;
                if (mover != null && mover.CurrentSpeed > 0.3f && !backing && Time.time - lastManual > 1.2f)
                {
                    float sp = Mathf.Clamp01(mover.CurrentSpeed / 2f);
                    yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, 1f - Mathf.Exp(-followTurn * sp * dt));
                }
            }
            else if (idle > recenterDelay && Time.time - lastManual > manualHold)
            {
                float axis = Mathf.Round(yaw / 90f) * 90f;   // square back up to the street grid (never swing behind the cat)
                float ease = Mathf.Clamp01((idle - recenterDelay) / 1.0f);
                yaw = Mathf.MoveTowardsAngle(yaw, axis, recenterSpeed * ease * dt);
            }

            // ---- follow + look-ahead
            Vector3 vel = petCC != null ? petCC.velocity : Vector3.zero;
            vel.y = 0f;
            Vector3 la = Vector3.ClampMagnitude(vel * lookAheadTime, lookAheadMax);
            lookAhead = Vector3.SmoothDamp(lookAhead, la, ref lookAheadVel, lookAheadSmooth);
            Vector3 tp = target.position;
            Vector3 fh = Vector3.SmoothDamp(new Vector3(focus.x, 0, focus.z), new Vector3(tp.x, 0, tp.z), ref focusVelH, followSmoothH);
            float fy = Mathf.SmoothDamp(focus.y, tp.y, ref focusVelY, followSmoothV);
            focus = new Vector3(fh.x, fy, fh.z);

            // ---- framing targets (+ cinematic beats)
            bool catView = mode == Mode.CatView;
            float baseDist = catView ? catDistance : distance, basePitch = catView ? catPitch : pitch, baseFov = catView ? catFov : fov;
            float wantDist = baseDist, wantPitch = basePitch, wantYawOff = 0f;
            float wantFov = mover != null && mover.IsSprinting ? baseFov + (sprintFov - fov) : baseFov;
            if (cinematicBeats && idle > idlePushInAfter)
            {
                float p = Mathf.SmoothStep(0f, 1f, (idle - idlePushInAfter) / 2f);
                wantDist *= Mathf.Lerp(1f, 0.75f, p);
                wantFov = Mathf.Lerp(wantFov, 36f, p);
            }
            float bt = Time.time - beatStart;
            if (beatKind != 0 && bt < beatDur)
            {
                float u = bt / beatDur;
                float w = Mathf.Sin(u * Mathf.PI);   // in, hold, out
                if (beatKind == 1) { wantDist *= Mathf.Lerp(1f, 0.6f, w); wantPitch = Mathf.Lerp(basePitch, Mathf.Min(basePitch, 30f) - 4f, w); wantYawOff = 20f * w; }
                else { wantDist *= Mathf.Lerp(1f, 0.55f, w); wantPitch = Mathf.Lerp(basePitch, 16f, w); wantYawOff = 35f * Mathf.SmoothStep(0f, 1f, u) * w; }
            }
            else if (beatKind != 0 && bt >= beatDur) beatKind = 0;

            float r = 1f - Mathf.Exp(-6f * dt);
            curDist = Mathf.Lerp(curDist, wantDist, r);
            curPitch = Mathf.Lerp(curPitch, wantPitch, r);
            curYawOff = Mathf.Lerp(curYawOff, wantYawOff, r);
            curFov = Mathf.Lerp(curFov, wantFov, 1f - Mathf.Exp(-dt / 0.4f * 3f));

            Vector3 aim = focus + lookAhead + Vector3.up * aimHeight;
            if (catView)
            {
                // look where she is going: aim a few metres in front of the cat (along the camera's facing)
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                aim = focus + fwd * catAimAhead + lookAhead * 0.5f + Vector3.up * 0.6f;
            }
            Quaternion rot = Quaternion.Euler(curPitch, yaw + curYawOff, 0f);
            transform.position = aim - rot * Vector3.forward * curDist;
            transform.rotation = rot;
            if (cam) cam.fieldOfView = curFov;

            Occlusion(dt);
        }

        // ------------------------------------------------------------------ fade whatever hides the cat
        void Occlusion(float dt)
        {
            hitNow.Clear();
            Vector3 from = transform.position;
            Vector3 to = target.position + Vector3.up * 0.3f;
            Vector3 d = to - from;
            float len = d.magnitude - 0.5f;
            if (len > 0.1f)
            {
                // Collide: tree canopies are triggers (walk-through) but must still fade when they hide the cat
                int n = Physics.SphereCastNonAlloc(from, probeRadius, d.normalized, hitBuf, len, ~0, QueryTriggerInteraction.Collide);
                for (int hi = 0; hi < n; hi++)
                {
                    var col = hitBuf[hi].collider;
                    if (col is MeshCollider || col is CharacterController) continue;     // ground tiles / the pet
                    if (col.GetComponentInParent<PawAgent>() != null) continue;          // cars and people stay solid
                    if (!rendCache.TryGetValue(col, out var rs)) rendCache[col] = rs = col.GetComponentsInChildren<Renderer>();
                    foreach (var rr in rs) if (rr) hitNow.Add(rr);
                }
            }
            foreach (var rr in hitNow) if (!fade.ContainsKey(rr)) fade[rr] = 1f;
            fadeKeys.Clear(); fadeKeys.AddRange(fade.Keys);
            foreach (var rr in fadeKeys)
            {
                if (!rr) { fade.Remove(rr); continue; }
                bool hidden = hitNow.Contains(rr);
                float v = Mathf.MoveTowards(fade[rr], hidden ? fadeTo : 1f, dt / (hidden ? 0.15f : 0.3f));
                fade[rr] = v;
                bool outline = rr.sharedMaterial != null && rr.sharedMaterial.name.StartsWith("M_Outline");
                if (outline) rr.enabled = v > 0.95f;
                else
                {
                    if (mpb == null) mpb = new MaterialPropertyBlock();   // survives a script reload during play
                    rr.GetPropertyBlock(mpb);
                    mpb.SetFloat(FadeId, v);
                    rr.SetPropertyBlock(mpb);
                }
                if (!hidden && v >= 1f)
                {
                    if (!outline) rr.SetPropertyBlock(null);
                    fade.Remove(rr);
                }
            }
        }
    }
}
