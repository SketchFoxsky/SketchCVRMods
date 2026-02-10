using UnityEngine;
using ABI_RC.Systems.Movement;
using ABI_RC.Core.Player;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Core.Savior;
using ECM2;

namespace Sketch.ClimbingSystem
{
    public class ClimbManager : MonoBehaviour
    {
        #region Properties and Fields

        [Header("Debug")]
        public bool debugLogs = true;
        public bool drawDebugRays = true;

        [Header("Layers & Distances")]
        [Tooltip("Physics layers used for climbable checks and sweeps.")]
        public LayerMask rayMask = Physics.DefaultRaycastLayers;

        [Header("VR Grab Radius (hand-sized, overlap-only)")]
        public float vrHandRadiusBase = 0.06f;
        public float vrHandRadiusMin = 0.035f;
        public float vrHandRadiusMax = 0.12f;
        public float vrHandRadiusMultiplier = 1.0f;
        public float vrOverlapSlack = 0.015f;

        private const float VR_MAX_CARRY_SPEED = 25f;

        private const float VR_PRESS = 0.75f;
        private const float VR_RELEASE = 0.25f;

        private bool vrLatched;
        private bool vrActiveIsLeft;
        private AnchorRef vrAnchor;
        private Vector3 vrCarryVelocity, vrCarryVelocitySmooth;

        [Header("Desktop Climbing")]
        public float desktopReachSlack = 1.05f;
        public float desktopClimbSpeedMult = 1.0f;
        public float desktopLatchLerpTime = 0.12f;
        [Range(0f, 1f)] public float desktopLatchEase = 1.0f;

        [Header("Desktop Launch")]
        public bool desktopAllowLaunch = true;
        public float desktopDefaultLaunch = 8f;

        [Header("Desktop Collision Safety")]
        public float desktopSkin = 0.02f;
        public float desktopCapsuleRadiusFactor = 0.18f;
        public float desktopCapsuleHeightFactor = 0.90f;
        public bool desktopSlideOnHit = true;

        private BetterBetterCharacterController _bbcc;
        private bool _announced;

        private enum DeskHand { None, Q, E }
        private DeskHand deskActiveHand = DeskHand.None;
        private AnchorRef deskAnchor;
        private Climbable deskClimbable;

        // IMPORTANT: local-space offset so rotations of the climbable carry the player correctly
        private Vector3 deskLocalOffset;

        private bool deskLerpActive;
        private float deskLerpT;
        private Vector3 deskLerpStart;

        private bool leftPressedState, rightPressedState;
        private bool leftPressEdge, leftReleaseEdge, rightPressEdge, rightReleaseEdge;
        private bool leftArmedForSwitch, rightArmedForSwitch;

        // Overlap cache
        private readonly Collider[] _overlapCache = new Collider[8];

        #endregion

        #region Structs
        // Anchor Type
        private struct AnchorRef
        {
            public Transform xf;        // follow this (rigidbody transform if present)
            public Vector3 local;       // local-space position of the grab point
            public Rigidbody rb;        // optional rigidbody
            public Vector3 lastWorld;

            public bool valid => xf != null;
            public Vector3 World => xf ? xf.TransformPoint(local) : lastWorld;
        }
        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (gameObject.scene.buildIndex != -1)
                DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (debugLogs)
                MelonLoader.MelonLogger.Msg($"[Climb] Manager Start. Scene={gameObject.scene.name} (idx={gameObject.scene.buildIndex})");
        }

        private void Update()
        {
            if (gameObject.scene.buildIndex != -1)
                DontDestroyOnLoad(gameObject);

            if (_bbcc == null)
            {
                _bbcc = BetterBetterCharacterController.Instance
                      ?? FindObjectOfType<BetterBetterCharacterController>();
                if (_bbcc != null && !_announced)
                {
                    Log("[Climb] BBCC found; climbing active.");
                    _announced = true;
                }
                if (_bbcc == null) return;
            }

            if (PlayerSetup.Instance == null || CVRInputManager.Instance == null) return;

            if (MetaPort.Instance != null && MetaPort.Instance.isUsingVr)
                HandleVr();
            else
                HandleDesktop();

            if (vrLatched && vrAnchor.valid) RefreshAnchor(ref vrAnchor);
            if (deskActiveHand != DeskHand.None && deskAnchor.valid) RefreshAnchor(ref deskAnchor);
        }
#endregion

        #region Desktop
        private void HandleDesktop()
        {
            if (!CVRInputManager.Instance.inputEnabled) return;

            bool qDown = Input.GetKeyDown(KeyCode.Q);
            bool eDown = Input.GetKeyDown(KeyCode.E);
            bool jump = Input.GetKeyDown(KeyCode.Space) && desktopAllowLaunch;

            Transform cam = GetCamera();
            if (!cam) return;

            float wingspan = Mathf.Max(0.1f, _bbcc.AvatarHeight);
            float radius = wingspan * 0.5f;
            float speed = _bbcc.BaseMovementSpeed * Mathf.Max(0.05f, desktopClimbSpeedMult);

            if (qDown)
            {
                if (deskActiveHand == DeskHand.Q)
                {
                    deskActiveHand = DeskHand.None;
                    deskClimbable = null;
                    deskLerpActive = false;
                }
                else if (TryGetClimbableDesktopRay(cam, radius, out var c, out var hitPos, out var hitCol))
                {
                    StartDesktopLatch(DeskHand.Q, c, hitCol, hitPos, radius);
                }
            }

            if (eDown)
            {
                if (deskActiveHand == DeskHand.E)
                {
                    deskActiveHand = DeskHand.None;
                    deskClimbable = null;
                    deskLerpActive = false;
                }
                else if (TryGetClimbableDesktopRay(cam, radius, out var c, out var hitPos, out var hitCol))
                {
                    StartDesktopLatch(DeskHand.E, c, hitCol, hitPos, radius);
                }
            }

            if (deskActiveHand == DeskHand.None || !deskAnchor.valid)
                return;

            // Launch (Space): fling + platform/rotation point velocity, then unlatch
            if (jump)
            {
                float baseForce = deskClimbable ? deskClimbable.launchForce : desktopDefaultLaunch;
                float launch = ScaleByAvatar(_bbcc, baseForce);

                Vector3 anchorPoint = deskAnchor.World;
                Vector3 carryVel = GetAnchorPointVelocity(in deskAnchor, anchorPoint, Time.deltaTime);

                Vector3 carry = cam.forward * launch + carryVel;
                _bbcc.LaunchCharacter(carry, true, true);

                deskActiveHand = DeskHand.None;
                deskClimbable = null;
                deskLerpActive = false;
                return;
            }

            // If we're mid-latch tween, glide to new anchor; ignore movement until done
            if (deskLerpActive)
            {
                deskLerpT += Time.deltaTime / Mathf.Max(0.0001f, desktopLatchLerpTime);
                float t = Mathf.Clamp01(deskLerpT);
                float eased = desktopLatchEase <= 0f ? t :
                              desktopLatchEase >= 1f ? SmoothStep2(t) :
                              Mathf.Lerp(t, SmoothStep2(t), desktopLatchEase);

                Vector3 end = deskAnchor.World + ToWorldVec(deskAnchor, deskLocalOffset);
                Vector3 pos = Vector3.Lerp(deskLerpStart, end, eased);

                SafeMoveTo(pos);
                if (t >= 1f) deskLerpActive = false;
                return;
            }

            // Normal latched movement: camera-relative movement modifies the LOCAL offset (so it rotates with the anchor)
            Vector3 inputXZ = CVRInputManager.Instance.movementVector;
            Vector3 moveWorld = inputXZ.relativeTo(cam, cam.up);
            Vector3 moveLocal = ToLocalVec(deskAnchor, moveWorld * (speed * Time.deltaTime));
            deskLocalOffset += moveLocal;

            // Constrain LOCAL offset length to radius
            float len2 = deskLocalOffset.sqrMagnitude;
            float maxR = radius;
            if (len2 > maxR * maxR)
                deskLocalOffset = deskLocalOffset.normalized * maxR;

            Vector3 target = deskAnchor.World + ToWorldVec(deskAnchor, deskLocalOffset);
            SafeMoveTo(target);
        }

        private void StartDesktopLatch(DeskHand newHand, Climbable c, Collider hitCol, Vector3 hitPos, float radius)
        {
            deskAnchor = MakeAnchor(hitCol, hitPos);
            deskClimbable = c;
            deskActiveHand = newHand;

            Vector3 cur = GetPlayerCenter(_bbcc);
            Vector3 anchor = deskAnchor.World;

            // Compute LOCAL offset so rotations carry correctly; preserve player height on latch.
            Vector3 worldOffset = cur - anchor;
            if (worldOffset.sqrMagnitude > radius * radius)
                worldOffset = worldOffset.normalized * radius;

            worldOffset.y = cur.y - anchor.y; // preserve height
            deskLocalOffset = ToLocalVec(deskAnchor, worldOffset);

            // start tween from current pos to the new (anchor + rotated offset)
            deskLerpActive = desktopLatchLerpTime > 0.0001f;
            deskLerpT = 0f;
            deskLerpStart = cur;

            Log($"[Climb][Desk] {(newHand == DeskHand.Q ? "Q" : "E")} latched (lerp start, local offset)");
        }
        #endregion

        #region VR
        private void HandleVr()
        {
            var input = CVRInputManager.Instance;
            var leftTf = GetHand(true);
            var rightTf = GetHand(false);

            float lv = input.interactLeftValue;
            float rv = input.interactRightValue;

            leftPressEdge = leftReleaseEdge = false;
            rightPressEdge = rightReleaseEdge = false;

            if (!leftPressedState && lv >= VR_PRESS) { leftPressedState = true; leftPressEdge = true; }
            if (leftPressedState && lv <= VR_RELEASE) { leftPressedState = false; leftReleaseEdge = true; }

            if (!rightPressedState && rv >= VR_PRESS) { rightPressedState = true; rightPressEdge = true; }
            if (rightPressedState && rv <= VR_RELEASE) { rightPressedState = false; rightReleaseEdge = true; }

            if (!vrLatched)
            {
                if (leftPressEdge && leftTf && TryGetClimbableHand(leftTf, out _, out var hitL, out var colL))
                {
                    vrLatched = true;
                    vrActiveIsLeft = true;
                    vrAnchor = MakeAnchor(colL, hitL);
                    _bbcc.SetImmobilized(true);
                    vrCarryVelocity = vrCarryVelocitySmooth = Vector3.zero;

                    rightArmedForSwitch = false;
                    leftArmedForSwitch = false;
                    Log("[Climb][VR] Latched LEFT");
                }
                else if (rightPressEdge && rightTf && TryGetClimbableHand(rightTf, out _, out var hitR, out var colR))
                {
                    vrLatched = true;
                    vrActiveIsLeft = false;
                    vrAnchor = MakeAnchor(colR, hitR);
                    _bbcc.SetImmobilized(true);
                    vrCarryVelocity = vrCarryVelocitySmooth = Vector3.zero;

                    leftArmedForSwitch = false;
                    rightArmedForSwitch = false;
                    Log("[Climb][VR] Latched RIGHT");
                }
                return;
            }
            else
            {
                if (vrActiveIsLeft)
                {
                    if (leftReleaseEdge) { VrReleasePreserveMomentum(); return; }
                    if (rightReleaseEdge) rightArmedForSwitch = true;

                    if (rightPressEdge && rightArmedForSwitch && rightTf &&
                        TryGetClimbableHand(rightTf, out _, out var hitR2, out var colR2))
                    {
                        vrActiveIsLeft = false;
                        vrAnchor = MakeAnchor(colR2, hitR2);
                        vrCarryVelocity = vrCarryVelocitySmooth = Vector3.zero;

                        rightArmedForSwitch = false;
                        leftArmedForSwitch = false;

                        Log("[Climb][VR] Switched to RIGHT");
                    }
                }
                else
                {
                    if (rightReleaseEdge) { VrReleasePreserveMomentum(); return; }
                    if (leftReleaseEdge) leftArmedForSwitch = true;

                    if (leftPressEdge && leftArmedForSwitch && leftTf &&
                        TryGetClimbableHand(leftTf, out _, out var hitL2, out var colL2))
                    {
                        vrActiveIsLeft = true;
                        vrAnchor = MakeAnchor(colL2, hitL2);
                        vrCarryVelocity = vrCarryVelocitySmooth = Vector3.zero;

                        leftArmedForSwitch = false;
                        rightArmedForSwitch = false;

                        Log("[Climb][VR] Switched to LEFT");
                    }
                }
            }

            // Keep the active hand pinned — let BBCC handle collisions
            Transform activeTf = vrActiveIsLeft ? leftTf : rightTf;
            if (!activeTf || !vrAnchor.valid) return;

            Vector3 anchorWorld = vrAnchor.World;
            Vector3 correction = anchorWorld - activeTf.position;

            if (Time.deltaTime > 1e-6f)
                vrCarryVelocity = correction / Time.deltaTime;

            vrCarryVelocitySmooth = Vector3.Lerp(vrCarryVelocitySmooth, vrCarryVelocity, 0.35f);
            if (vrCarryVelocitySmooth.sqrMagnitude > VR_MAX_CARRY_SPEED * VR_MAX_CARRY_SPEED)
                vrCarryVelocitySmooth = vrCarryVelocitySmooth.normalized * VR_MAX_CARRY_SPEED;

            if (correction.sqrMagnitude > 0f)
                _bbcc.TeleportPlayerTo(PlayerSetup.Instance.GetPlayerPosition() + correction, false, true);
        }

        private void VrReleasePreserveMomentum()
        {
            Vector3 carry = vrCarryVelocitySmooth;

            // Use point velocity (includes angular) when we have a rigidbody
            carry += GetAnchorPointVelocity(in vrAnchor, vrAnchor.World, Time.deltaTime);

            _bbcc.SetImmobilized(false);
            if (carry.sqrMagnitude > 1e-6f)
                _bbcc.LaunchCharacter(carry, true, true);

            vrLatched = false;
            Log("[Climb][VR] Released (momentum+platform preserved)");
        }

        #endregion

        #region Collision Safety
        private void SafeMoveTo(Vector3 target)
        {
            Vector3 cur = GetPlayerCenter(_bbcc);
            Vector3 delta = target - cur;
            float dist = delta.magnitude;
            if (dist <= 1e-5f) return;

            GetCapsuleAt(cur, out Vector3 p1, out Vector3 p2, out float rad);

            Vector3 dir = delta / dist;
            float castDist = dist + Mathf.Max(0f, desktopSkin);

            if (Physics.CapsuleCast(p1, p2, rad, dir, out var hit, castDist, rayMask, QueryTriggerInteraction.Collide))
            {
                float move = Mathf.Max(0f, hit.distance - desktopSkin);
                Vector3 pos = cur + dir * move;

                if (desktopSlideOnHit && move < dist * 0.999f)
                {
                    Vector3 remain = delta - dir * move;
                    Vector3 slideDir = Vector3.ProjectOnPlane(remain, hit.normal);
                    float slideLen = slideDir.magnitude;
                    if (slideLen > 1e-5f)
                    {
                        slideDir /= slideLen;
                        GetCapsuleAt(pos, out Vector3 sp1, out Vector3 sp2, out float srad);
                        if (!Physics.CapsuleCast(sp1, sp2, srad, slideDir, out var hit2, slideLen, rayMask, QueryTriggerInteraction.Collide))
                            pos += slideDir * slideLen;
                        else
                            pos += slideDir * Mathf.Max(0f, hit2.distance - desktopSkin);
                    }
                }

                GetCapsuleAt(pos, out Vector3 fp1, out Vector3 fp2, out float fr);
                if (Physics.CheckCapsule(fp1, fp2, fr, rayMask, QueryTriggerInteraction.Collide))
                    pos -= dir * desktopSkin;

                _bbcc.TeleportPlayerTo(pos, false, true);
            }
            else
            {
                _bbcc.TeleportPlayerTo(target, false, true);
            }
        }

        private void GetCapsuleAt(Vector3 center, out Vector3 p1, out Vector3 p2, out float radius)
        {
            float h = Mathf.Max(1.0f, _bbcc.AvatarHeight) * Mathf.Clamp01(desktopCapsuleHeightFactor);
            radius = Mathf.Clamp(_bbcc.AvatarHeight * Mathf.Clamp(desktopCapsuleRadiusFactor, 0.12f, 0.28f), 0.14f, 0.35f);

            Vector3 up = _bbcc ? _bbcc.transform.up : Vector3.up;
            float half = Mathf.Max(0.01f, (h * 0.5f - radius));
            p1 = center + up * half;
            p2 = center - up * half;
        }
        #endregion

        #region HELPERS
        private Transform GetCamera()
        {
            if (PlayerSetup.Instance.activeCam)
                return PlayerSetup.Instance.activeCam.transform;
            var go = PlayerSetup.Instance.GetActiveCamera();
            return go ? go.transform : null;
        }

        private Transform GetHand(bool left)
        {
            var input = CVRInputManager.Instance;
            Transform t = left ? input.leftHandTransform : input.rightHandTransform;
            return t;
        }

        // Desktop: camera ray (reach = radius): thin ray first, then hand-sized spherecast
        private bool TryGetClimbableDesktopRay(Transform cam, float reach, out Climbable c, out Vector3 hitPoint, out Collider hitCol)
        {
            c = null; hitPoint = default; hitCol = null;
            if (!cam) return false;

            float handR = ComputeVrHandRadiusMeters();
            Ray ray = new Ray(cam.position, cam.forward);

            if (drawDebugRays) Debug.DrawRay(ray.origin, ray.direction * reach, Color.cyan, 0.05f);

            if (Physics.Raycast(ray, out var hit, reach, rayMask, QueryTriggerInteraction.Collide))
            {
                c = hit.collider.GetComponentInParent<Climbable>();
                if (c) { hitPoint = hit.point; hitCol = hit.collider; return true; }
            }

            if (Physics.SphereCast(ray, handR, out hit, reach, rayMask, QueryTriggerInteraction.Collide))
            {
                c = hit.collider.GetComponentInParent<Climbable>();
                if (c) { hitPoint = hit.point; hitCol = hit.collider; return true; }
            }

            return false;
        }

        // VR: Overlap-only grab so the anchor is exactly where the hand touches
        private bool TryGetClimbableHand(Transform hand, out Climbable c, out Vector3 hitPoint, out Collider hitCol)
        {
            c = null; hitPoint = default; hitCol = null;
            if (!hand) return false;

            float r = ComputeVrHandRadiusMeters();
            float grabR = r + vrOverlapSlack;

            if (drawDebugRays) Debug.DrawRay(hand.position, Vector3.up * 0.04f, Color.yellow, 0.05f);

            int count = Physics.OverlapSphereNonAlloc(hand.position, grabR, _overlapCache, rayMask, QueryTriggerInteraction.Collide);

            float bestSqr = float.PositiveInfinity;
            Collider best = null;
            for (int i = 0; i < count; i++)
            {
                var col = _overlapCache[i];
                if (!col) continue;

                var cc = col.GetComponentInParent<Climbable>();
                if (!cc) continue;

                Vector3 cp = col.bounds.ClosestPoint(hand.position);
                float d2 = (cp - hand.position).sqrMagnitude;
                if (d2 < bestSqr) { bestSqr = d2; best = col; c = cc; }
            }

            if (best != null && c != null)
            {
                hitCol = best;
                Vector3 cp = best.bounds.ClosestPoint(hand.position);
                hitPoint = (cp - hand.position).sqrMagnitude > 1e-10f ? cp : hand.position;
                return true;
            }
            return false;
        }

        private static AnchorRef MakeAnchor(Collider col, Vector3 hitPoint)
        {
            Transform xf = col && col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            Rigidbody rb = col ? col.attachedRigidbody : null;

            return new AnchorRef
            {
                xf = xf,
                local = xf ? xf.InverseTransformPoint(hitPoint) : hitPoint,
                rb = rb,
                lastWorld = hitPoint
            };
        }

        private static void RefreshAnchor(ref AnchorRef a)
        {
            if (a.valid) a.lastWorld = a.World;
        }

        // Point velocity (includes angular) when RB exists; else finite-difference of anchor transform
        private static Vector3 GetAnchorPointVelocity(in AnchorRef a, Vector3 worldPoint, float dt)
        {
            if (a.rb)
            {
                // Rigidbody.GetPointVelocity returns v + ω × r at that point
                return a.rb.GetPointVelocity(worldPoint);
            }
            // Fallback: includes rotation via transform change since last frame
            if (dt > 1e-6f) return (a.World - a.lastWorld) / dt;
            return Vector3.zero;
        }

        // Convert vectors between world and anchor local (handles rotation properly)
        private static Vector3 ToLocalVec(in AnchorRef a, Vector3 worldVec)
        {
            if (a.xf) return a.xf.InverseTransformVector(worldVec);
            return worldVec; // no transform: treat as world
        }
        private static Vector3 ToWorldVec(in AnchorRef a, Vector3 localVec)
        {
            if (a.xf) return a.xf.TransformVector(localVec);
            return localVec;
        }

        private float ComputeVrHandRadiusMeters()
        {
            float h = (_bbcc && _bbcc.AvatarHeight > 0f) ? _bbcc.AvatarHeight : 1.8f;
            float scaled = vrHandRadiusBase * (h / 1.8f) * Mathf.Max(0.001f, vrHandRadiusMultiplier);
            return Mathf.Clamp(scaled, vrHandRadiusMin, vrHandRadiusMax);
        }

        private static Vector3 GetPlayerCenter(BetterBetterCharacterController bbcc)
        {
            return bbcc ? bbcc.ColliderCenterWorld : PlayerSetup.Instance.GetPlayerPosition();
        }

        private static float ScaleByAvatar(BetterBetterCharacterController bbcc, float baseForce)
        {
            float h = (bbcc && bbcc.AvatarHeight > 0f) ? bbcc.AvatarHeight : 1.8f;
            return baseForce * (h / 1.8f);
        }

        private static float SmoothStep2(float t)
        {
            t = Mathf.Clamp01(t);
            t = t * t * (3f - 2f * t);
            return t * t * (3f - 2f * t);
        }

        private void Log(string msg) { if (debugLogs) MelonLoader.MelonLogger.Msg(msg); }
    }
    #endregion
}
