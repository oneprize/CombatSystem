using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Player
{
    /// <summary>G로 벽의 윗모서리를 잡고 A/D로 이동한다. Space로 놓는다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLedgeHang : MonoBehaviour
    {
        public float reach = 1f, maxHeight = 3.4f, shimmySpeed = 1.2f;
        [Min(0f)] public float grabBuffer = .4f;
        [Range(0f, 70f)] public float facingTolerance = 50f;
        [Min(0f)] public float handSurfaceOffset = .035f;
        [Min(0f)] public float handBelowEdge = .07f;
        [Min(.1f)] public float holdHandHeight = .7f;
        [Range(0f, 80f)] public float handGripTilt = 45f;
        [Min(.6f)] public float climbForwardDistance = .75f;

        public bool IsHanging { get; private set; }
        public bool IsClimbing { get; private set; }
        public string LastDecision { get; private set; } = "G로 잡기";
        public Vector3 GripPoint => edge;

        PlayerCharacter player;
        Animator animator;
        CharacterController cc;
        Transform leftHand, rightHand;
        Collider wallCollider;
        Vector3 edge, normal, entryPosition, lastPoseCorrection, releaseCorrection;
        float timer, cooldown, bufferedUntil = -1f, grabLength = 1.3333334f;
        float entryStart, releaseRemaining;
        bool entering;
        string motion;
        readonly RaycastHit[] hits = new RaycastHit[24];
        readonly Collider[] overlaps = new Collider[24];
        static readonly string[] States = { "LedgeGrab", "LedgeIdle", "LedgeLeft", "LedgeRight" };
        Vector3 climbStartRoot, climbLanding, climbAnchorStart, climbAnchorEnd, climbStartCorrection;
        Vector3 climbExitCorrection;
        Collider climbIgnoredCollider;
        bool climbFinishing, climbCollisionChanged, climbBlocked;
        float climbTimer, climbFinishTimer;
        const float ClimbExitBlend = .18f;
        // Player_LedgeClimbUp (Traversal_Climb_End_toCliffTop)의 현재 Humanoid에서 측정한
        // 시작 손 중심 / 끝 몸 중심 / 발 높이. 원본에 Bake된 이동은 월드 기준점에 재배치한다.
        static readonly Vector3 ClimbFirstHands = new Vector3(.019f, -.1825f, .2475f);
        static readonly Vector3 ClimbLastBody = new Vector3(.004f, .997f, .597f);
        const float ClimbLastFootHeight = .103f;
        // 같은 클립의 몸 중심 이동을 10구간으로 샘플링한 정규화 곡선.
        static readonly float[] ClimbRise = { 0f, .078f, .250f, .458f, .585f, .668f, .758f, .845f, .910f, .971f, 1f };
        static readonly float[] ClimbAdvance = { 0f, .060f, .188f, .364f, .483f, .563f, .630f, .777f, .861f, .963f, 1f };

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            cc = GetComponent<CharacterController>();
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Player_WallClimbGrab") grabLength = clip.length;
            }
        }

        public void CaptureInput()
        {
            // 납도/점프 전환 프레임에 PlayerCharacter.Tick이 생략되어도 짧은 G 입력은 보존한다.
            if (Time.timeScale <= 0f) { bufferedUntil = -1f; return; }
            if (!IsHanging && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                bufferedUntil = Time.time + grabBuffer;
        }

        bool Valid(Collider c)
        {
            return c != null && !c.isTrigger && !c.transform.IsChildOf(transform) &&
                c.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>() == null &&
                c.GetComponentInParent<CombatSystem.Boss.BossCombat>() == null &&
                c.GetComponentInParent<HangRail>() == null;
        }

        bool Cast(Vector3 origin, Vector3 direction, float length, out RaycastHit hit)
        {
            hit = default;
            float nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Valid(hits[i].collider) && hits[i].distance < nearest)
                { hit = hits[i]; nearest = hit.distance; }
            return nearest < float.PositiveInfinity;
        }

        bool Supported(Vector3 point, Vector3 outward, float halfWidth)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -outward);
            for (int side = -1; side <= 1; side++)
            {
                RaycastHit top, face;
                Vector3 grip = point + right * halfWidth * side;
                if (!Cast(grip - outward * .12f + Vector3.up * .12f, Vector3.down, .24f, out top) ||
                    top.normal.y < .8f || Mathf.Abs(top.point.y - point.y) > .06f) return false;
                if (!Cast(grip + outward * .2f - Vector3.up * .15f, -outward, .4f, out face) ||
                    Vector3.Dot(face.normal, outward) < .95f) return false;
            }
            return true;
        }

        bool FindEdge(Vector3 origin, Vector3 forward, float topY, float bottomY,
            float halfWidth, out Vector3 point, out Vector3 outward, out Collider surface)
        {
            point = outward = Vector3.zero;
            surface = null;
            RaycastHit wall, top;
            if (!Cast(origin, forward, reach, out wall) || Mathf.Abs(wall.normal.y) > .2f) return false;
            Vector3 ray = wall.point - wall.normal * .12f;
            ray.y = topY;
            if (!Cast(ray, Vector3.down, topY - bottomY, out top) || top.normal.y < .8f) return false;
            point = new Vector3(wall.point.x, top.point.y, wall.point.z);
            outward = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
            if (!Supported(point, outward, halfWidth)) return false;
            surface = wall.collider;
            return true;
        }

        bool FindReachableEdge(out Vector3 point, out Vector3 outward, out Collider surface)
        {
            point = outward = Vector3.zero;
            surface = null;
            float footY = cc.bounds.min.y;
            float minHeight = cc.isGrounded ? 1.3f : .35f;
            float best = float.PositiveInfinity;
            // 발/가슴 높이 모두 검사해 점프 도중 가슴이 모서리를 지나도 잡을 수 있다.
            for (int angleIndex = 0; angleIndex < 5; angleIndex++)
            {
                float angle = angleIndex == 0 ? 0f : (angleIndex % 2 == 1 ? -1f : 1f) *
                    (angleIndex < 3 ? facingTolerance * .5f : facingTolerance);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                for (int heightIndex = 0; heightIndex < 3; heightIndex++)
                {
                    Vector3 origin = cc.bounds.center;
                    origin.y = heightIndex == 0 ? cc.bounds.center.y :
                        heightIndex == 1 ? footY + .15f : cc.bounds.center.y + .35f;
                    Vector3 candidate, n;
                    Collider c;
                    if (!FindEdge(origin, direction, footY + maxHeight + .1f, footY + minHeight,
                        .27f, out candidate, out n, out c)) continue;
                    if (Vector3.Angle(transform.forward, -n) > facingTolerance + 1f) continue;
                    float height = candidate.y - footY;
                    if (height < minHeight || height > maxHeight) continue;
                    float score = Vector3.ProjectOnPlane(candidate - transform.position, Vector3.up).sqrMagnitude + Mathf.Abs(angle) * .002f;
                    if (score >= best) continue;
                    best = score; point = candidate; outward = n; surface = c;
                }
            }
            return surface != null;
        }

        AnimatorStateInfo GrabState()
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            return animator.IsInTransition(0) && next.IsName("LedgeGrab") ? next : animator.GetCurrentAnimatorStateInfo(0);
        }

        void Play(string state, float normalizedStart = 0f)
        {
            if (motion == state) return;
            motion = state;
            animator.CrossFadeInFixedTime("Base Layer." + state, .15f, 0,
                state == "LedgeGrab" ? normalizedStart * grabLength : 0f);
        }

        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled || cc == null || !cc.enabled || dt <= 0f) return false;
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (IsHanging)
            {
                if (player.IsDead || player.IsHitReacting || wallCollider == null || !wallCollider.enabled)
                { Release(false); return false; }
                if (IsClimbing) { TickClimb(dt); return true; }
                timer += dt;
                if (timer > .25f && player.Controls.JumpPressed) { Release(true); return true; }
                var state = GrabState();
                if (entering && ((state.IsName("LedgeGrab") && state.normalizedTime >= .88f) || timer > grabLength + .4f))
                { entering = false; Play("LedgeIdle"); }
                if (!entering)
                {
                    climbBlocked = false;
                    if (player.Controls.Move.y > .5f && TryStartClimb()) return true;
                    float x = player.Controls.Move.x;
                    Vector3 right = Vector3.Cross(Vector3.up, -normal), next, n;
                    Collider surface;
                    if (Mathf.Abs(x) > .1f && FindEdge(edge + normal * .75f - Vector3.up * .3f + right * x * shimmySpeed * dt,
                        -normal, edge.y + .15f, edge.y - .15f, .72f, out next, out n, out surface) &&
                        Vector3.Dot(n, normal) > .95f && Mathf.Abs(next.y - edge.y) < .06f)
                    { edge = next; normal = n; wallCollider = surface; Play(x < 0f ? "LedgeLeft" : "LedgeRight"); }
                    else Play("LedgeIdle");
                }
                float clearance = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) + cc.skinWidth + .015f;
                Vector3 target = edge + normal * clearance - Vector3.up * (holdHandHeight * transform.lossyScale.y + handBelowEdge);
                if (entering)
                {
                    float n = state.IsName("LedgeGrab") ? state.normalizedTime : entryStart;
                    float approach = entryStart > 0f ? Mathf.Clamp01(timer / .22f) : Mathf.InverseLerp(.27f, .52f, n);
                    target = Vector3.Lerp(entryPosition, target, Mathf.SmoothStep(0f, 1f, approach));
                }
                cc.Move(target - transform.position);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-normal), 540f * dt);
                return true;
            }
            if (cooldown > 0f || Time.time > bufferedUntil) return false;
            if (player.WeaponStance != null && (player.WeaponStance.IsDrawn || player.WeaponStance.IsTransitioning))
            { LastDecision = "납도 후 G로 잡기"; return false; }
            if (player.IsDead || player.IsAttacking || player.IsDodging || player.IsHitReacting || player.IsLinkJumping || player.IsAirComboActive ||
                (player.Hang != null && player.Hang.IsHanging) || (player.WallRun != null && player.WallRun.IsWallRunning)) return false;
            if (leftHand == null || rightHand == null) return false;
            foreach (var state in States)
                if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state)))
                { LastDecision = "매달리기 Animator 상태 누락: " + state; return false; }
            Vector3 candidate, norm;
            Collider collider;
            if (!FindReachableEdge(out candidate, out norm, out collider))
            { LastDecision = "손이 닿는 연속된 윗모서리가 없음"; return false; }
            edge = candidate; normal = norm; wallCollider = collider;
            entryPosition = transform.position;
            entryStart = cc.isGrounded ? 0f : .42f;
            IsHanging = entering = true;
            timer = 0f; bufferedUntil = -1f; releaseRemaining = 0f; motion = null;
            player.Combat.CancelForDamage();
            player.AirCombo.OnDisable();
            Play("LedgeGrab", entryStart);
            LastDecision = "모서리 잡기";
            return true;
        }

        public void ApplyIK(int layer)
        {
            if (layer != 0 || animator == null || !animator.isHuman) return;
            if (IsClimbing) { ApplyClimbPose(); return; }
            if (!IsHanging)
            {
                if (releaseRemaining > 0f && (player.Hang == null || !player.Hang.IsHanging))
                {
                    animator.bodyPosition += releaseCorrection * (releaseRemaining / .15f);
                    releaseRemaining = Mathf.Max(0f, releaseRemaining - Time.deltaTime);
                }
                return;
            }
            var state = GrabState();
            float weight = !entering ? 1f : entryStart > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / .22f)) :
                state.IsName("LedgeGrab") ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, .5f, state.normalizedTime)) : 0f;
            Vector3 left = leftHand.position, right = rightHand.position;
            // 손목과 Animator IK 목표의 차이는 몸을 보정하기 전에 저장한다.
            Vector3 leftGoalOffset = animator.GetIKPosition(AvatarIKGoal.LeftHand) - left;
            Vector3 rightGoalOffset = animator.GetIKPosition(AvatarIKGoal.RightHand) - right;
            Quaternion leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftHand);
            Quaternion rightRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
            Vector3 middle = (left + right) * .5f;
            Vector3 gripCenter = edge + normal * handSurfaceOffset - Vector3.up * handBelowEdge;
            Vector3 correction = normal * Vector3.Dot(gripCenter - middle, normal) + Vector3.up * (gripCenter.y - middle.y);
            // Start에는 상승이 Bake되어 있고 Idle에는 없다. 손을 기준으로 몸까지 옮겨
            // 그 높이 차이를 흡수하고, 캡슐 반지름 때문에 팔만 무리하게 뻗는 것을 막는다.
            lastPoseCorrection = correction * weight;
            animator.bodyPosition += lastPoseCorrection;
            Vector3 tangent = Vector3.Cross(Vector3.up, -normal);
            Quaternion tilt = Quaternion.AngleAxis(handGripTilt, tangent);
            FitHand(AvatarIKGoal.LeftHand, gripCenter + tangent * Vector3.Dot(left - edge, tangent), leftGoalOffset, tilt * leftRotation, weight);
            FitHand(AvatarIKGoal.RightHand, gripCenter + tangent * Vector3.Dot(right - edge, tangent), rightGoalOffset, tilt * rightRotation, weight);
            if (!climbBlocked) LastDecision = entering ? "모서리 잡기" : "매달림: W 올라가기, A/D 이동, Space 놓기";
        }

        float Curve(float[] values, float time)
        {
            float sample = Mathf.Clamp01(time) * (values.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(sample), values.Length - 2);
            return Mathf.Lerp(values[index], values[index + 1], sample - index);
        }

        Vector3 ClimbRootAt(float progress)
        {
            Vector3 result = Vector3.Lerp(climbStartRoot, climbLanding, Curve(ClimbAdvance, progress));
            result.y = Mathf.Lerp(climbStartRoot.y, climbLanding.y, Curve(ClimbRise, progress));
            return result;
        }

        bool ClimbSpaceClear(Vector3 root)
        {
            Vector3 scale = transform.lossyScale;
            float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - .015f;
            float halfSegment = Mathf.Max(0f, cc.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = root + transform.TransformVector(cc.center);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * halfSegment,
                center - Vector3.up * halfSegment, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (c != null && c != wallCollider && !c.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        bool TryStartClimb()
        {
            climbBlocked = true;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.LedgeClimbUp")))
            { LastDecision = "올라가기 Animator 상태 누락"; return false; }
            float radius = cc.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float depth = Mathf.Max(climbForwardDistance, radius + cc.skinWidth + .1f);
            Vector3 topPoint = edge - normal * depth;
            Vector3 tangent = Vector3.Cross(Vector3.up, -normal);
            // 캡슐 발판 전체가 같은 높이의 바닥에 있어야 한다. 얇은 담장이나 낭떠러지로 올라서지 않는다.
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero : i < 3 ? tangent * radius * (i == 1 ? -.9f : .9f) :
                    normal * radius * (i == 3 ? -.9f : .9f);
                RaycastHit support;
                if (!Cast(topPoint + offset + Vector3.up * .25f, Vector3.down, .5f, out support) ||
                    support.normal.y < .8f || Mathf.Abs(support.point.y - edge.y) > .08f)
                { LastDecision = "벽 위에 설 공간이 부족함"; return false; }
            }
            float rootHeight = cc.height * transform.lossyScale.y * .5f + cc.skinWidth - transform.TransformVector(cc.center).y;
            climbStartRoot = transform.position;
            climbLanding = topPoint + Vector3.up * rootHeight;
            // 현재 벽만 캡슐 충돌에서 잠깐 제외할 것이므로, 다른 벽/천장/캐릭터는 경로 전체에서 검사한다.
            for (int i = 0; i <= 16; i++)
                if (!ClimbSpaceClear(ClimbRootAt(i / 16f)))
                { LastDecision = "벽 위 또는 올라가는 경로가 막혀 있음"; return false; }
            climbAnchorStart = edge + normal * handSurfaceOffset - Vector3.up * handBelowEdge - transform.TransformVector(ClimbFirstHands);
            climbAnchorEnd = topPoint - transform.TransformVector(new Vector3(ClimbLastBody.x, ClimbLastFootHeight, ClimbLastBody.z)) + Vector3.up * .04f;
            climbStartCorrection = lastPoseCorrection;
            climbTimer = climbFinishTimer = 0f;
            climbFinishing = climbBlocked = false;
            climbIgnoredCollider = wallCollider;
            climbCollisionChanged = !Physics.GetIgnoreCollision(cc, climbIgnoredCollider);
            if (climbCollisionChanged) Physics.IgnoreCollision(cc, climbIgnoredCollider, true);
            IsClimbing = true;
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isJumping", false);
            animator.SetBool("isGrounded", false);
            Play("LedgeClimbUp");
            LastDecision = "벽 위로 올라가는 중";
            return true;
        }

        AnimatorStateInfo ClimbState()
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            return animator.IsInTransition(0) && next.IsName("LedgeClimbUp") ? next : animator.GetCurrentAnimatorStateInfo(0);
        }

        void TickClimb(float dt)
        {
            climbTimer += dt;
            if (climbFinishing)
            {
                climbFinishTimer += dt;
                if ((animator.IsInTransition(0) || !animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) && climbFinishTimer < .6f) return;
                RestoreClimbCollision();
                IsClimbing = IsHanging = entering = false;
                cooldown = .7f; bufferedUntil = -1f; motion = null; wallCollider = null;
                lastPoseCorrection = Vector3.zero;
                player.Locomotion.OnCombatLanded();
                LastDecision = "벽 위 도착";
                return;
            }
            var state = ClimbState();
            if (!state.IsName("LedgeClimbUp"))
            {
                if (climbTimer > .5f) Release(true);
                return;
            }
            float progress = Mathf.Clamp01(state.normalizedTime);
            Vector3 target = ClimbRootAt(progress);
            if (!ClimbSpaceClear(target)) { Release(true); return; }
            cc.Move(target - transform.position);
            if (Vector3.Distance(transform.position, target) > .2f) { Release(true); return; }
            if (state.normalizedTime < 1f) return;
            climbFinishing = true;
            climbExitCorrection = lastPoseCorrection;
            RestoreClimbCollision();
            cc.Move(Vector3.down * .01f);
            player.Locomotion.OnCombatLanded();
            motion = "Idle";
            // Climb의 Bake된 이동 보정을 Idle 블렌드와 함께 풀어 시작점으로 돌아가지 않는다.
            animator.CrossFadeInFixedTime("Base Layer.Idle", ClimbExitBlend, 0, 0f);
        }

        void ApplyClimbPose()
        {
            if (climbFinishing)
            {
                float blend = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Idle") ?
                    Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) :
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") ? 1f : 0f;
                animator.bodyPosition += Vector3.Lerp(climbExitCorrection,
                    -transform.up * player.Profile.attackHeightOffset.visualLowering * transform.lossyScale.y, blend);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
                return;
            }
            var state = ClimbState();
            float progress = state.IsName("LedgeClimbUp") ? Mathf.Clamp01(state.normalizedTime) : 0f;
            float warp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, .95f, progress));
            Vector3 anchor = Vector3.Lerp(climbAnchorStart, climbAnchorEnd, warp);
            float entryBlend = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("LedgeClimbUp") ?
                Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 1f;
            Vector3 left = leftHand.position, right = rightHand.position;
            Vector3 leftOffset = animator.GetIKPosition(AvatarIKGoal.LeftHand) - left;
            Vector3 rightOffset = animator.GetIKPosition(AvatarIKGoal.RightHand) - right;
            Quaternion leftRotation = animator.GetIKRotation(AvatarIKGoal.LeftHand);
            Quaternion rightRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
            lastPoseCorrection = Vector3.Lerp(climbStartCorrection + climbStartRoot - transform.position,
                anchor - transform.position, entryBlend);
            animator.bodyPosition += lastPoseCorrection;
            float handWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, .38f, progress));
            Vector3 tangent = Vector3.Cross(Vector3.up, -normal);
            Vector3 grip = edge + normal * handSurfaceOffset - Vector3.up * handBelowEdge;
            Quaternion tilt = Quaternion.AngleAxis(handGripTilt * handWeight, tangent);
            FitHand(AvatarIKGoal.LeftHand, grip + tangent * Vector3.Dot(left - edge, tangent), leftOffset, tilt * leftRotation, handWeight);
            FitHand(AvatarIKGoal.RightHand, grip + tangent * Vector3.Dot(right - edge, tangent), rightOffset, tilt * rightRotation, handWeight);
        }

        void RestoreClimbCollision()
        {
            if (climbCollisionChanged && climbIgnoredCollider != null && cc != null)
                Physics.IgnoreCollision(cc, climbIgnoredCollider, false);
            climbCollisionChanged = false;
            climbIgnoredCollider = null;
        }

        void FitHand(AvatarIKGoal goal, Vector3 point, Vector3 goalOffset, Quaternion rotation, float weight)
        {
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, point + goalOffset);
            animator.SetIKRotationWeight(goal, weight);
            animator.SetIKRotation(goal, rotation);
        }

        public void Release(bool animate)
        {
            if (!IsHanging) return;
            RestoreClimbCollision();
            IsClimbing = climbFinishing = false;
            IsHanging = entering = false;
            cooldown = .7f; bufferedUntil = -1f; motion = null; wallCollider = null;
            // 클립 전환이 끝나기 전까지 몸의 깊이 보정도 함께 풀어 이탈 순간의 뒤쪽 스냅을 줄인다.
            releaseCorrection = lastPoseCorrection;
            releaseRemaining = animate ? .15f : 0f;
            if (player.Locomotion != null) player.Locomotion.ResumeAirDodge();
            if (animate) animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .15f, 0, 0f);
            LastDecision = "모서리 놓음";
        }

        void OnDisable() { if (player != null) Release(false); }

        void OnDrawGizmosSelected()
        {
            if (!IsHanging) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(edge, .04f);
            Gizmos.DrawLine(edge, edge + normal * .5f);
        }
    }
}
