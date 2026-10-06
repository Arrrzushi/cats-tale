using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PawTown
{
    /// <summary>
    /// The cat controller (simple and forgiving, after mobile control research):
    ///  * Floating stick (PawJoystick) or WASD. Push to the rim (or hold Shift) to sprint.
    ///  * The stick direction is taken from the camera when you touch it and then only eases toward the camera
    ///    slowly while held, so a camera swinging behind the cat never makes her spin in tight circles.
    ///  * The cat moves mostly where you push (not just where it faces), turns fast, pivots on the spot for big
    ///    turns, and starts / stops crisply. No gliding, no orbiting.
    ///  * Auto-hop: pushing into a low ledge or bench hops onto it. Curbs are simply stepped over.
    ///  * Tap-to-walk (WalkTo) and auto-walk along the order route (FollowOrderRoute). Any stick input takes over.
    ///  * Energy (PawGame): half energy = normal top speed, full energy = faster, low energy = slower, no sprint.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PawTownDemoMover : MonoBehaviour
    {
        [Header("Speeds (m/s, at half energy)")]
        public float walkSpeed = 1.6f;
        public float runSpeed = 3.0f;
        public float acceleration = 12f;
        public float deceleration = 18f;
        public float turnSpeed = 600f;          // degrees per second
        [Range(0f, 1f)] public float inputWeight = 0.7f;   // move along 70% input direction, 30% facing

        [Header("Animation match")]
        public float walkClipSpeed = 0.85f;
        public float runClipSpeed = 1.95f;

        [Header("Jump")]
        public float jumpHeight = 0.85f;
        public float coyoteTime = 0.12f;
        public float gravity = -25f;
        [Tooltip("Hop automatically onto low obstacles you push into")] public bool autoHop = true;

        [Tooltip("Degrees per second the stick's direction frame follows the camera while held")] public float frameFollow = 22f;

        [Header("Assist")]
        public float arriveRadius = 0.7f;
        public bool autoWalk;   // legacy test switch

        CharacterController cc;
        Animator anim;
        float speed, vy, airTime, stepDist, idleFor, nextIdleMeow = 9f, stuckFor;
        bool wasAirborne, frameLocked;
        float frameYaw;
        Vector3 moveDir = Vector3.forward;

        // autopilot (tap-to-walk / follow the order route)
        readonly List<Vector3> autoPath = new List<Vector3>();
        bool followOrder;

        public float CurrentSpeed => speed;
        public Vector2 InputVector { get; private set; }
        public bool Grounded => airTime < 0.1f || Swimming;

        // ---- swimming: the river is open water (no bank walls); she floats half-submerged and paddles slowly
        public bool Swimming { get; private set; }
        [Header("Swimming")] public float swimSpeedFactor = 0.5f;
        public float floatDepth = 0.34f;          // feet this far below the water surface
        PawTownGenerator gen;
        float swimSoundT, swimStuck;

        /// <summary>Water surface height under her, if she is in a river channel (not on a bridge deck).</summary>
        bool WaterAt(Vector3 p, out float surface)
        {
            surface = 0f;
            if (!gen) gen = FindAnyObjectByType<PawTownGenerator>();
            if (!gen || gen.library == null) return false;
            var c = gen.CellOf(p);
            if (!gen.TileName(c).StartsWith("River_")) return false;
            Vector3 centre = gen.CellCenter(c);
            if (Mathf.Abs(p.z - centre.z) > 3.9f) return false;          // on the bank
            surface = centre.y - 0.32f;
            return p.y < surface + 0.25f;                                 // bridge decks are higher up
        }
        public bool IsSprinting { get; private set; }
        public bool AutoWalking => followOrder || autoPath.Count > 0;
        public event System.Action<bool> AutoWalkChanged;

        void Awake() { cc = GetComponent<CharacterController>(); frameYaw = 0f; }

        // ------------------------------------------------------------------ assist API
        public void WalkTo(Vector3 point)
        {
            var orders = PawOrders.Instance;
            autoPath.Clear();
            if (orders != null) autoPath.AddRange(orders.PlanWalk(transform.position, point));
            else { autoPath.Add(transform.position); autoPath.Add(point); }
            followOrder = false;
            AutoWalkChanged?.Invoke(true);
        }

        public void FollowOrderRoute(bool on)
        {
            followOrder = on;
            autoPath.Clear();
            AutoWalkChanged?.Invoke(on);
        }

        public void StopAuto()
        {
            if (!AutoWalking) return;
            followOrder = false;
            autoPath.Clear();
            AutoWalkChanged?.Invoke(false);
        }

        /// <summary>
        /// Pure pursuit: find where the cat is along the path line and head for a point a little further along it.
        /// Never turns back to a point it already passed (routes are re-planned from the cat's position every second).
        /// </summary>
        static bool Pursue(List<Vector3> path, Vector3 p, float lookahead, float arrive, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (path.Count < 2) return false;
            Vector3 last = path[path.Count - 1];
            Vector3 toEnd = last - p; toEnd.y = 0f;
            if (toEnd.magnitude < arrive) return false;
            int bestSeg = 0; float bestT = 0f, bestD = float.MaxValue;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 ab = b - a; ab.y = 0f;
                Vector3 ap = p - a; ap.y = 0f;
                float L2 = ab.sqrMagnitude;
                float t = L2 < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(ap, ab) / L2);
                Vector3 q = a + ab * t; q.y = p.y;
                float d = (q - p).sqrMagnitude;
                if (d < bestD - 0.01f || (Mathf.Abs(d - bestD) <= 0.01f && i > bestSeg)) { bestD = d; bestSeg = i; bestT = t; }
            }
            // walk 'lookahead' metres forward along the path from the closest point
            float left = lookahead;
            Vector3 cur = Vector3.Lerp(path[bestSeg], path[bestSeg + 1], bestT);
            Vector3 aim = last;
            for (int i = bestSeg; i < path.Count - 1; i++)
            {
                Vector3 nxt = path[i + 1];
                Vector3 seg = nxt - cur; seg.y = 0f;
                if (seg.magnitude >= left) { aim = cur + seg.normalized * left; break; }
                left -= seg.magnitude;
                cur = nxt;
            }
            dir = aim - p; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();
            return true;
        }

        bool AutoDirection(out Vector3 dir)
        {
            dir = Vector3.zero;
            if (followOrder)
            {
                var o = PawOrders.Instance;
                if (o == null || !o.HasTarget) { StopAuto(); return false; }
                return Pursue(o.route, transform.position, 1.6f, arriveRadius, out dir);
            }
            if (autoPath.Count > 0)
            {
                if (Pursue(autoPath, transform.position, 1.6f, arriveRadius, out dir)) return true;
                StopAuto();   // arrived
            }
            return false;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 m = Vector2.zero;
            bool runKey = false, jumpKey = false;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) m.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) m.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) m.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) m.x -= 1;
                runKey = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                jumpKey = k.spaceKey.wasPressedThisFrame;
            }
#else
            m = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            runKey = Input.GetKey(KeyCode.LeftShift);
            jumpKey = Input.GetKeyDown(KeyCode.Space);
#endif
            float push = Mathf.Clamp01(m.magnitude);
            if (push > 0) push = runKey ? 1f : 0.6f;
            var stick = PawJoystick.Active;
            bool sprint = runKey;
            if (stick != null && stick.Value.sqrMagnitude > 0.0001f) { m = stick.Value; push = Mathf.Clamp01(m.magnitude); sprint = stick.Sprinting; }
            jumpKey |= PawActionButton.ConsumeJump();
            // RUN button: hold it to run (with the stick: that way; without: straight ahead), let go to walk again
            if (PawActionButton.RunHeld)
            {
                if (push < 0.01f) m = new Vector2(0f, 1f);
                push = 1f;
                sprint = true;
            }
            if (autoWalk) { m = new Vector2(0f, 1f); push = 0.6f; }

            var game = PawGame.Instance;
            if (game != null && !game.CanRun) sprint = false;
            bool wasSwimming = Swimming;
            Swimming = WaterAt(transform.position, out float waterY);
            if (Swimming) sprint = false;
            if (Swimming && !wasSwimming) PawAudio.Instance?.Splash();
            IsSprinting = sprint && push > 0.5f;

            // input frame: lock the camera yaw while the player is steering
            bool steering = push > 0.01f;
            var cam = Camera.main;
            if (steering && !frameLocked) { frameYaw = cam ? cam.transform.eulerAngles.y : 0f; frameLocked = true; }
            if (!steering) frameLocked = false;
            // while held, the direction frame eases toward the camera (frameFollow deg/s): a sideways push makes a smooth wide
            // curve while the camera swings in behind, and "up" always ends up meaning "where she's looking"
            if (steering && cam) frameYaw = Mathf.MoveTowardsAngle(frameYaw, cam.transform.eulerAngles.y, frameFollow * dt);

            Vector3 want = Vector3.zero;
            float target = 0f;
            if (steering)
            {
                if (AutoWalking) StopAuto();                       // the player takes over
                want = (Quaternion.Euler(0f, frameYaw, 0f) * new Vector3(m.x, 0f, m.y)).normalized;
                target = IsSprinting ? runSpeed
                       : walkSpeed * Mathf.Clamp01(push / 0.6f) + (push > 0.6f ? (runSpeed - walkSpeed) * 0.35f * (push - 0.6f) / 0.4f : 0f);
            }
            else if (AutoDirection(out var ad))
            {
                want = ad;
                target = walkSpeed * 1.3f;   // brisk walk: costs only walking energy
            }
            InputVector = steering ? m : Vector2.zero;
            if (game != null) target *= game.SpeedMultiplier;
            if (Swimming) target *= swimSpeedFactor;

            // turning: fast, with a quick on-the-spot pivot for big direction changes
            if (want.sqrMagnitude > 0.0001f)
            {
                float ang = Vector3.Angle(transform.forward, want);
                float turnRate = ang > 120f ? turnSpeed * 2f : turnSpeed;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(want), turnRate * dt);
                if (ang > 120f) target *= 0.25f;                   // pivot first, then go
                moveDir = Vector3.Slerp(transform.forward, want, inputWeight).normalized;
            }
            speed = Mathf.MoveTowards(speed, target, (target > speed ? acceleration : deceleration) * dt);

            // vertical: gravity, jump, auto-hop
            if (cc.isGrounded) airTime = 0f; else airTime += dt;
            if (cc.isGrounded && vy < 0f) vy = -2f;
            bool hop = autoHop && speed > 0.3f && Grounded && LowObstacleAhead();
            if ((jumpKey || hop) && airTime <= coyoteTime && vy <= 0f)
            {
                vy = Mathf.Sqrt(2f * -gravity * (hop ? 0.75f : jumpHeight));
                airTime = coyoteTime + 0.01f;
                PawAudio.Instance?.Jump();
                if (!hop) game?.SpendJump();
            }
            if (Swimming)
            {
                // buoyancy: spring to the floating height with a little bob; a jump (or bumping into the bank) climbs out
                float floatY = waterY - floatDepth + 0.04f * Mathf.Sin(Time.time * 2.6f);
                if (vy <= 0f || transform.position.y < floatY) vy = Mathf.Lerp(vy, (floatY - transform.position.y) * 8f, 10f * dt);
                else vy += gravity * dt;
                if (target > 0.3f && cc.velocity.sqrMagnitude < 0.05f) swimStuck += dt; else swimStuck = 0f;
                if (swimStuck > 0.25f) { vy = Mathf.Sqrt(2f * -gravity * 1.15f); swimStuck = 0f; PawAudio.Instance?.Splash(); }
            }
            else vy += gravity * dt;
            cc.Move((moveDir * speed + Vector3.up * vy) * dt);

            Vector3 hv = cc.velocity; hv.y = 0f;
            // pushing into something: let the speed fall so the legs don't run in place
            if (hv.magnitude < speed * 0.5f) speed = Mathf.Lerp(speed, hv.magnitude, 10f * dt);
            stuckFor = (target > 0.5f && hv.magnitude < 0.1f) ? stuckFor + dt : 0f;
            if (stuckFor > 1.5f && AutoWalking) { StopAuto(); stuckFor = 0f; }   // autopilot gave up on an obstacle

            Sounds(hv, steering);
            Animate();
        }

        /// <summary>Something 0.33 - 0.7 m tall right in front (curbs are lower and just stepped over), with room on top: hop onto it.</summary>
        bool LowObstacleAhead()
        {
            Vector3 f = moveDir; f.y = 0f;
            if (f.sqrMagnitude < 0.01f) return false;
            f.Normalize();
            Vector3 b = transform.position;
            float reach = cc.radius + 0.35f;
            if (!Physics.Raycast(b + Vector3.up * 0.33f, f, out var low, reach, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (low.collider is CharacterController || low.collider.GetComponentInParent<PawAgent>() != null) return false;   // never hop onto cars / people
            if (Physics.Raycast(b + Vector3.up * 0.7f, f, reach + 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;   // too tall
            return true;
        }

        void Sounds(Vector3 hv, bool steering)
        {
            var au = PawAudio.Instance;
            if (au == null) return;
            if (Swimming)
            {
                swimSoundT -= Time.deltaTime;
                if (hv.magnitude > 0.15f && swimSoundT <= 0f) { au.Swim(); swimSoundT = 0.55f; }
                return;
            }
            if (Grounded && hv.magnitude > 0.1f)
            {
                stepDist += hv.magnitude * Time.deltaTime;
                float stride = speed > 2.25f ? 0.55f : 0.3f;
                if (stepDist >= stride) { stepDist = 0f; au.Step(speed > 2.25f ? 0.75f : 0.5f); }
            }
            if (!Grounded && airTime > 0.25f) wasAirborne = true;
            if (Grounded && wasAirborne) { wasAirborne = false; au.Land(); }
            idleFor = steering || AutoWalking || !Grounded ? 0f : idleFor + Time.deltaTime;
            if (idleFor > nextIdleMeow) { au.Meow(); idleFor = 0f; nextIdleMeow = Random.Range(9f, 18f); }
        }

        void Animate()
        {
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null) return;
            anim.SetFloat("Speed", speed);
            anim.SetBool("Grounded", Grounded);
            float clip = speed > 2.25f ? runClipSpeed : walkClipSpeed;
            anim.SetFloat("AnimSpeed", speed < 0.06f ? 1f : Mathf.Clamp(speed / clip, 0.5f, 2.4f));
        }
    }
}
