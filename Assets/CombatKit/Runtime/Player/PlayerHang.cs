using UnityEngine;

namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHang : MonoBehaviour
    {
        public float grabRange = 1.8f, maxGrabHeight = 3.6f;
        // IsHanging also owns the approach/exit, so combat and normal locomotion cannot interrupt it.
        public bool IsHanging { get; private set; }
        public bool IsApproaching => IsHanging && !hasGrip && exitPhase == 0;
        public bool HasGrip => IsHanging && hasGrip && exitPhase < 2;

        [Header("Jump and catch")]
        [Min(.2f)] public float approachFlightTime = .58f;
        [Min(1f)] public float maximumLaunchSpeed = 8.5f;
        [Min(1f)] public float maximumApproachSpeed = 4.5f;
        [Range(.05f, .3f)] public float takeoffNormalizedTime = .1f;
        [Range(.4f, .9f)] public float catchNormalizedTime = .75f;
        [Range(.05f, .3f)] public float catchDistance = .22f;
        [Range(0f, .1f)] public float maximumCatchCorrection = .08f;
        [Min(.05f)] public float handLockBlendTime = .12f;
        [Min(.1f)] public float settleDuration = .32f;
        // Root-relative hand positions in the assigned JumpToHang and HangingIdle poses.
        public Vector3 catchHandLocalOffset = new Vector3(.02f, .58f, .66f);
        public Vector3 holdHandLocalOffset = new Vector3(.01f, 1.189f, 0f);

        [Header("Grip and swing")]
        [Range(.15f, .7f)] public float gripSpacing = .42f;
        public float gripHeightOffset = .035f, gripBackOffset = .035f;
        [Range(0, 90)] public float maximumSwingArc = 90f;
        public float swingBuildRate = 16f, swingDecayRate = 12f;
        [Min(.1f)] public float swingCycleDuration = 1.8f;
        public float SwingAngle => swingAngle;
        public float releaseJumpUpSpeed = 4f, releaseJumpForwardSpeed = 3f;

        PlayerCharacter player;
        Animator animator;
        CharacterController controller;
        Transform left, right;
        HangRail rail;
        Collider railCollider;
        bool ignored, idle, launched, hasGrip;
        Vector3 grip, leftGrip, rightGrip, approachVelocity;
        Vector3 settleStart, settleTarget;
        Quaternion approachRotation;
        float elapsed, cooldown, flightTime, plannedFlightTime, gripTime;
        float entryClipLength = .5f, entrySpeed = 1f;
        float swingPhase, swingAmplitude, swingAngle;
        int exitPhase; // 0 approach/attached, 1 jump preparation, 2 airborne, 3 landing
        float exitTime, verticalSpeed;
        Vector3 exitVelocity;
        const string EntrySpeedParameter = "HangEntrySpeed";
        bool GrabPressed => UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame;
        float Gravity => Mathf.Min(-.1f, player.Profile.locomotion.gravity);
        Vector3 GripOffset => Vector3.up * gripHeightOffset - approachRotation * Vector3.forward * gripBackOffset;

        void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
            controller = GetComponent<CharacterController>();
            if (animator.isHuman)
            {
                left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                right = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (animator.runtimeAnimatorController != null)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name == "Player_TraversalJumpToHang") { entryClipLength = clip.length; break; }
        }

        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled || !controller.enabled) return false;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (IsHanging)
            {
                if (player.IsDead || player.IsHitReacting) { Release(false); return false; }
                if (exitPhase != 0) return TickExit(dt);
                if (rail == null) { MissGrab(); return true; }
                elapsed += dt;
                if (!hasGrip) return TickApproach(dt);
                gripTime += dt;
                // The capsule settles under the fixed grip; it is never chased toward animated hands.
                float settle = Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / settleDuration));
                Vector3 target = Vector3.Lerp(settleStart, settleTarget, settle);
                controller.Move(Vector3.ClampMagnitude(target - transform.position, 4f * dt));
                if (idle && player.Controls.JumpPressed) { BeginExit(swingAmplitude > 2f); return true; }
                var state = EntryState();
                if (!idle && ((state.IsName("JumpToHang") && state.normalizedTime >= .95f) || gripTime > .5f))
                {
                    idle = true;
                    animator.CrossFadeInFixedTime("Base Layer.HangingIdle", .2f, 0, 0);
                }
                if (idle) TickSwing(dt);
                return true;
            }
            if (cooldown > 0 || !GrabPressed || player.IsDead || player.IsAttacking || player.IsDodging ||
                player.IsHitReacting || player.IsLinkJumping || player.IsAirComboActive ||
                (player.WallRun != null && player.WallRun.IsWallRunning) ||
                (player.WeaponStance != null && (player.WeaponStance.IsDrawn || player.WeaponStance.IsTransitioning))) return false;
            if (left == null || right == null || !animator.HasState(0, Animator.StringToHash("Base Layer.JumpToHang"))) return false;

            HangRail best = null;
            float distance = grabRange;
            Vector3 bestVelocity = Vector3.zero, bestGrip = Vector3.zero;
            Quaternion bestRotation = transform.rotation;
            float bestFlight = approachFlightTime;
            bool grounded = controller.isGrounded;
            foreach (var candidate in FindObjectsByType<HangRail>(FindObjectsSortMode.None))
            {
                Vector3 point = candidate.ClosestPoint(transform.position);
                float height = point.y - controller.bounds.min.y;
                Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
                if (height < 1.4f || height > maxGrabHeight || flat.magnitude > distance) continue;
                Vector3 facing = Vector3.Cross(candidate.Axis, Vector3.up).normalized;
                if (Vector3.Dot(facing, flat) < 0) facing = -facing;
                if (facing.sqrMagnitude < .1f) continue;
                Quaternion rotation = Quaternion.LookRotation(facing);
                Vector3 velocity;
                float duration;
                if (!PlanApproach(point, rotation, grounded, out velocity, out duration)) continue;
                if (PathBlocked((left.position + right.position) * .5f, point, candidate.GetComponent<Collider>())) continue;
                best = candidate; distance = flat.magnitude;
                bestVelocity = velocity; bestGrip = point; bestRotation = rotation; bestFlight = duration;
            }
            if (best == null) return false;
            rail = best; grip = bestGrip; approachRotation = bestRotation;
            railCollider = rail.GetComponent<Collider>();
            Vector3 axis = rail.Axis;
            if (Vector3.Dot(axis, approachRotation * Vector3.right) < 0) axis = -axis;
            leftGrip = rail.ClosestPoint(grip - axis * gripSpacing * .5f);
            rightGrip = rail.ClosestPoint(grip + axis * gripSpacing * .5f);
            approachVelocity = bestVelocity; plannedFlightTime = bestFlight;
            float startNormalized = grounded ? 0f : .2f;
            entrySpeed = (catchNormalizedTime - (grounded ? takeoffNormalizedTime : startNormalized)) * entryClipLength / plannedFlightTime;
            animator.SetFloat(EntrySpeedParameter, entrySpeed);
            player.Combat.CancelForDamage(); player.AirCombo.OnDisable();
            IsHanging = true; hasGrip = false; idle = false; launched = !grounded;
            elapsed = flightTime = gripTime = 0; exitPhase = 0;
            swingPhase = swingAmplitude = swingAngle = 0;
            if (launched) IgnoreRailCollision();
            animator.CrossFadeInFixedTime("Base Layer.JumpToHang", .08f, 0, startNormalized * entryClipLength);
            return true;
        }

        bool PlanApproach(Vector3 point, Quaternion rotation, bool grounded, out Vector3 velocity, out float duration)
        {
            Vector3 target = point + Vector3.up * gripHeightOffset - rotation * Vector3.forward * gripBackOffset -
                rotation * Vector3.Scale(catchHandLocalOffset, transform.lossyScale);
            duration = Mathf.Max(.2f, approachFlightTime);
            if (grounded)
            {
                velocity = (target - transform.position) / duration - Vector3.up * (.5f * Gravity * duration);
                return velocity.y > 0 && velocity.y <= maximumLaunchSpeed &&
                    Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude <= maximumApproachSpeed;
            }
            // An airborne grab preserves momentum: no second jump or homing toward the bar.
            velocity = player.Locomotion.TraversalVelocity;
            float closest = float.PositiveInfinity;
            for (float t = .08f; t <= .8f; t += .02f)
            {
                Vector3 predicted = transform.position + velocity * t + Vector3.up * (.5f * Gravity * t * t);
                float error = Vector3.Distance(predicted, target);
                if (error < closest) { closest = error; duration = t; }
            }
            return closest <= catchDistance;
        }

        AnimatorStateInfo EntryState()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("JumpToHang"))
                state = animator.GetNextAnimatorStateInfo(0);
            return state;
        }

        bool TickApproach(float dt)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, approachRotation, 540f * dt);
            var state = EntryState();
            if (!launched)
            {
                if (!state.IsName("JumpToHang") || state.normalizedTime < takeoffNormalizedTime)
                {
                    controller.Move(Vector3.down * 2f * dt);
                    if (elapsed > 1f) MissGrab();
                    return true;
                }
                launched = true;
                IgnoreRailCollision();
            }
            flightTime += dt;
            Vector3 movement = approachVelocity * dt + Vector3.up * (.5f * Gravity * dt * dt);
            approachVelocity.y += Gravity * dt;
            var flags = controller.Move(movement);
            if ((flags & CollisionFlags.Above) != 0) approachVelocity.y = Mathf.Min(0, approachVelocity.y);
            if ((flags & CollisionFlags.Sides) != 0 || (flightTime > .08f && controller.isGrounded && approachVelocity.y <= 0) ||
                flightTime > plannedFlightTime + .25f || (state.IsName("JumpToHang") && state.normalizedTime > 1.05f)) MissGrab();
            return true;
        }

        bool PathBlocked(Vector3 from, Vector3 to, Collider allowed)
        {
            foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.collider != allowed) return true;
            return false;
        }

        void CatchGrip(Vector3 handMidpoint)
        {
            // Only the final few centimetres may move the capsule at contact.
            Vector3 correction = Vector3.ClampMagnitude(grip + GripOffset - handMidpoint, maximumCatchCorrection);
            Vector3 before = transform.position;
            controller.Move(correction);
            animator.bodyPosition += transform.position - before;
            hasGrip = true; gripTime = 0;
            settleStart = transform.position;
            settleTarget = grip + GripOffset - approachRotation * Vector3.Scale(holdHandLocalOffset, transform.lossyScale);
            float incoming = Vector3.Dot(approachVelocity, approachRotation * Vector3.forward);
            swingAmplitude = Mathf.Min(Mathf.Abs(incoming) * 3f, 8f, Mathf.Clamp(maximumSwingArc, 0, 90) * .5f);
            swingPhase = incoming >= 0 ? 0f : .5f;
            approachVelocity = Vector3.zero;
        }

        void MissGrab()
        {
            hasGrip = false; exitPhase = 2; exitTime = 0;
            verticalSpeed = launched ? approachVelocity.y : 0f;
            exitVelocity = Vector3.ProjectOnPlane(approachVelocity, Vector3.up);
            animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
        }

        void TickSwing(float dt)
        {
            float input = player.Controls.Move.y;
            float limit = Mathf.Clamp(maximumSwingArc, 0, 90) * .5f;
            swingAmplitude = Mathf.MoveTowards(swingAmplitude, input > .1f ? limit : 0,
                (input > .1f ? swingBuildRate : input < -.1f ? swingDecayRate * 2 : swingDecayRate) * dt);
            // Preserve the established W pump / S brake cadence.
            swingPhase = Mathf.Repeat(swingPhase + dt / 1.8f, 1f);
            swingAngle = Mathf.Clamp(Mathf.Sin(swingPhase * Mathf.PI * 2) * swingAmplitude, -limit, limit);
        }

        public void ApplyHandIK(int layerIndex)
        {
            if (layerIndex != 0 || !animator.isHuman) return;
            if (IsApproaching && launched && rail != null && controller.enabled)
            {
                var state = EntryState();
                Vector3 midpoint = (left.position + right.position) * .5f;
                if (state.IsName("JumpToHang") && state.normalizedTime >= catchNormalizedTime - .18f && state.normalizedTime <= 1f &&
                    Quaternion.Angle(transform.rotation, approachRotation) < 15f &&
                    Vector3.Distance(midpoint, grip + GripOffset) <= catchDistance &&
                    !PathBlocked(midpoint, grip, railCollider)) CatchGrip(midpoint);
            }
            bool attached = HasGrip && rail != null && left != null && right != null && controller.enabled;
            float weight = attached ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(gripTime / handLockBlendTime)) : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
            if (!attached) return;
            Vector3 l = leftGrip + GripOffset, r = rightGrip + GripOffset;
            // After contact, pin the pose around the fixed hands while the capsule settles underneath.
            // This is visual pose alignment only; approaching characters are never pulled by IK.
            Vector3 poseCorrection = ((l + r) - (left.position + right.position)) * .5f;
            animator.bodyPosition += Vector3.ClampMagnitude(poseCorrection, 1f) * weight;
            if (idle && exitPhase == 0)
            {
                Vector3 pivot = (l + r) * .5f;
                Quaternion rotation = Quaternion.AngleAxis(swingAngle, transform.right);
                animator.bodyPosition = pivot + rotation * (animator.bodyPosition - pivot);
                animator.bodyRotation = rotation * animator.bodyRotation;
            }
            Vector3 leftGoalOffset = animator.GetIKPosition(AvatarIKGoal.LeftHand) - left.position;
            Vector3 rightGoalOffset = animator.GetIKPosition(AvatarIKGoal.RightHand) - right.position;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, l + leftGoalOffset);
            animator.SetIKPosition(AvatarIKGoal.RightHand, r + rightGoalOffset);
        }

        void IgnoreRailCollision()
        {
            if (railCollider == null) return;
            ignored = !Physics.GetIgnoreCollision(controller, railCollider);
            if (ignored) Physics.IgnoreCollision(controller, railCollider, true);
        }

        void BeginExit(bool jump)
        {
            exitTime = 0;
            if (jump) { exitPhase = 1; animator.CrossFadeInFixedTime("Base Layer.HangJumpStart", .12f, 0, 0); }
            else Detach(false);
        }

        void Detach(bool jump)
        {
            exitPhase = 2; exitTime = 0; verticalSpeed = jump ? releaseJumpUpSpeed : 0;
            exitVelocity = jump ? transform.forward * releaseJumpForwardSpeed : Vector3.zero;
            if (!jump) animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
        }

        bool TickExit(float dt)
        {
            exitTime += dt;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (exitPhase == 1)
            {
                if ((state.IsName("HangJumpStart") && state.normalizedTime >= .45f) || exitTime > .8f) Detach(true);
                return true;
            }
            if (exitPhase == 2)
            {
                Vector3 movement = (exitVelocity + Vector3.up * verticalSpeed) * dt + Vector3.up * (.5f * Gravity * dt * dt);
                verticalSpeed += Gravity * dt;
                var flags = controller.Move(movement);
                if ((flags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(0, verticalSpeed);
                if ((flags & CollisionFlags.Sides) != 0) exitVelocity = Vector3.zero;
                if (verticalSpeed <= 0 && controller.isGrounded)
                {
                    exitPhase = 3; exitTime = 0;
                    animator.CrossFadeInFixedTime("Base Layer.HangLand", .08f, 0, 0);
                }
                else if (!animator.IsInTransition(0) &&
                    (state.IsName("HangJumpStart") || state.IsName("HangJumpAir") || state.IsName("JumpToHang")) && state.normalizedTime >= .9f)
                    animator.CrossFadeInFixedTime("Base Layer.HangFall", .1f, 0, 0);
                return true;
            }
            controller.Move(Vector3.down * 2f * dt);
            if ((state.IsName("HangLand") && state.normalizedTime >= .9f) || exitTime > 1.8f)
            {
                Release(false);
                animator.CrossFadeInFixedTime("Base Layer.Idle", .15f, 0, 0);
            }
            return true;
        }

        public void Release(bool animate)
        {
            if (!IsHanging) return;
            IsHanging = false; hasGrip = false; cooldown = .6f; exitPhase = 0;
            if (ignored && railCollider != null) Physics.IgnoreCollision(controller, railCollider, false);
            ignored = false; rail = null;
            if (player != null && player.Locomotion != null) player.Locomotion.ResumeAirDodge();
            if (animate) animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop", .1f, 0, 0);
        }

        void OnDisable() { Release(false); }
    }
}
