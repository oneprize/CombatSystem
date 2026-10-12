using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Camera-relative walk/run, double jump and gravity. Also starts dodges and link jumps,
    /// and hands the frame to whichever movement action (air combo, dodge, link jump) is active.
    /// </summary>
    public sealed class LocomotionModule : PlayerModule
    {
        private const int MaxJumps = 2; // 최대 연속 점프 횟수

        private PlayerProfile.LocomotionSettings config;
        private float turningTimer;
        private int jumpCount; // 플레이어가 점프한 횟수
        private Vector3 velocity;
        private Vector3 lastMoveDirection; // 이전 이동 방향을 저장할 변수
        private bool dodgeBuffered;
        private float airborneTime;
        private string landingState;
        private bool airDodgeLanding;

        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsTurning { get; private set; }

        protected override void OnInitialize()
        {
            config = Player.Profile.locomotion;
        }

        public void Tick(float dt)
        {
            var air = Player.AirCombo;
            if (air.Active)
            {
                air.Tick(dt);
                velocity = Vector3.zero;
                ResetJumpState();
                return;
            }
            var dodge = Player.Dodge;
            if (dodge.IsDodging)
            {
                velocity = Vector3.zero;
                dodge.Tick(dt);
                return;
            }
            bool dodgePressed = Player.Controls.DodgePressed;
            if (dodgeBuffered && !Player.Combat.IsUsingGroundCombo) dodgeBuffered = false;
            if (dodgePressed || dodgeBuffered)
            {
                Transform basis = Player.CameraTransform;
                Vector3 forward = basis.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = basis.right; right.y = 0f; right.Normalize();
                Vector2 move = Player.Controls.Move;
                Vector3 input = forward * move.y + right * move.x;
                if (dodge.TryBegin(input)) { dodgeBuffered = false; velocity = Vector3.zero; ResetJumpState(); return; }
                // Pressed mid-swing: hold it until this step reaches its dodge cancel point.
                // The latest input wins, so a combo follow-up queued earlier is dropped.
                if (dodgePressed && Player.Combat.IsUsingGroundCombo)
                {
                    dodgeBuffered = true;
                    Player.Combat.ClearQueuedAttack();
                }
            }
            var linkJump = Player.LinkJump;
            if (linkJump.IsTraversing)
            {
                linkJump.Tick(dt);
                velocity = Vector3.zero;
                ResetJumpState();
                return;
            }
            if (Player.Controls.JumpPressed && linkJump.TryBegin())
            {
                velocity = Vector3.zero;
                ResetJumpState();
                return;
            }
            HandleMovement(dt);
            if (Player.Combat.IsUsingUpperAttack || Player.Combat.IsUsingChaseAttack || Player.Combat.IsReleasingPower || Player.Combat.IsGroundComboFinisher)
            {
                // UpperAttack owns vertical movement through collision-aware root motion.
                // Do not add normal jump gravity or accept Space during this animation.
                velocity = Vector3.zero;
                ResetJumpState();
                animator.SetBool("isGrounded", character.isGrounded);
                return;
            }
            HandleJump(dt);
        }

        public void OnCombatLanded()
        {
            IsGrounded = true;
            velocity = Vector3.zero;
            ResetJumpState();
            animator.SetBool("isGrounded", true);
        }

        public void ResumeAirDodge()
        {
            airDodgeLanding = true;
            velocity = Vector3.down * 2f;
            IsGrounded = character.isGrounded;
            IsJumping = !IsGrounded;
            jumpCount = IsGrounded ? 0 : 1;
            animator.SetBool("isGrounded", IsGrounded);
            animator.SetBool("isJumping", IsJumping);
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            if (IsGrounded) Land();
            else animator.CrossFadeInFixedTime(JumpLoopState, .08f, 0, 0f);
        }

        public void ResumeAfterDodge(float dt)
        {
            HandleMovement(dt);
        }

        private void HandleMovement(float dt)
        {
            // 공격 또는 방어 중일 때는 이동을 막는다
            bool isCombatLocked = Player.IsAttacking || Player.IsGuarding || Player.IsHitReacting;
            if (isCombatLocked)
            {
                animator.SetBool("isMoving", false);
                animator.SetBool("isRunning", false);
                animator.SetBool("isTurning", false);
                animator.SetFloat("moveX", 0f);
                animator.SetFloat("moveY", 0f);
                IsMoving = false;
                IsRunning = false;
                return;
            }

            Transform cam = Player.CameraTransform;
            Vector3 inputDir = GetInputDirection();
            bool isMoving = inputDir.magnitude > 0.1f;
            bool isRunning = isMoving && Player.Controls.RunHeld;

            // 현재 이동 방향을 계산합니다.
            Vector2 move = Player.Controls.Move;
            Vector3 currentMoveDirection = cam.forward * move.y + cam.right * move.x;
            currentMoveDirection.y = 0f;
            currentMoveDirection.Normalize();

            if (isMoving && lastMoveDirection.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(lastMoveDirection.normalized, currentMoveDirection.normalized);
                if (dot < -0.8f)
                    turningTimer = config.turningHoldTime; // 반전 시 타이머 시작
            }

            // 타이머가 남아있으면 isTurning 유지
            if (turningTimer > 0f)
            {
                turningTimer -= dt;
                IsTurning = true;
            }
            else
            {
                IsTurning = false;
            }

            if (isMoving)
            {
                Quaternion targetRot = Quaternion.LookRotation(currentMoveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, config.rotationSpeed * dt);

                float speed = isRunning ? config.runSpeed : config.walkSpeed;
                if (IsTurning) speed *= 0.3f;
                character.Move(currentMoveDirection * speed * dt);

                lastMoveDirection = currentMoveDirection;
            }

            // 애니메이터 파라미터 업데이트
            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isRunning", isRunning);
            animator.SetBool("isTurning", IsTurning);

            Vector2 animInput = TransformToLocalInput(inputDir, cam);
            animator.SetFloat("moveX", animInput.x);
            animator.SetFloat("moveY", animInput.y);

            IsMoving = isMoving;
            IsRunning = isRunning;
        }

        private void HandleJump(float dt)
        {
            // Before Land() below: a CrossFade only shows up in the animator state from the next update,
            // so checking the landing state in the same frame it was requested would drop it.
            UpdateLanding();
            IsGrounded = CheckGrounded();

            if (IsGrounded && velocity.y <= 0f)
            {
                // 하강 중에 바닥에 닿았을 때만 착지로 본다 (점프 직후 프레임은 velocity.y > 0).
                if (IsJumping) Land();
                jumpCount = 0;
                airborneTime = 0f;
                // CharacterController가 바닥에 잘 붙도록 y축 속도 보정
                if (velocity.y < 0) velocity.y = -2f;
            }
            else if (!IsGrounded)
            {
                airborneTime += dt;
                // 점프 없이 낭떠러지에서 떨어지면 낙하 모션으로 바꾸고, 첫 점프를 쓴 것으로 친다.
                if (!IsJumping && velocity.y < 0f && airborneTime >= config.fallStartDelay && CanPlayJumpAnimation)
                {
                    IsJumping = true;
                    jumpCount = 1;
                    animator.CrossFadeInFixedTime(JumpLoopState, .15f, 0, 0f);
                }
            }

            if (Player.Controls.JumpPressed && jumpCount < MaxJumps && CanPlayJumpAnimation)
            {
                velocity.y = config.jumpForce;
                IsJumping = true;
                jumpCount++;
                landingState = null;
                animator.CrossFadeInFixedTime(jumpCount == 1 ? JumpStartState : DoubleJumpStartState, .08f, 0, 0f);
            }

            animator.SetBool("isGrounded", IsGrounded);
            animator.SetBool("isJumping", IsJumping);
            animator.SetInteger("jumpCount", jumpCount);

            // 중력 적용
            velocity.y += config.gravity * dt;
            character.Move(velocity * dt);
        }

        private const string JumpStartState = "Base Layer.JumpStart";
        private const string JumpLoopState = "Base Layer.JumpLoop";
        private const string JumpEndState = "Base Layer.JumpEnd";
        private const string DoubleJumpStartState = "Base Layer.DoubleJumpStart";
        private const string DoubleJumpEndState = "Base Layer.DoubleJumpEnd";

        /// <summary>Jump/fall/landing animations only play when no action owns the animator.</summary>
        private bool CanPlayJumpAnimation => !Player.IsHitReacting && !Player.IsAttacking && !Player.IsGuarding;

        /// <summary>
        /// The transform sits at the capsule's centre, so the probe starts at the capsule bottom
        /// (a probe from the transform never reaches the floor and the player would never land).
        /// </summary>
        private bool CheckGrounded()
        {
            if (character.isGrounded) return true;
            var bounds = character.bounds;
            Vector3 bottom = bounds.center - Vector3.up * bounds.extents.y;
            return Physics.Raycast(bottom + Vector3.up * .05f, Vector3.down, .05f + config.groundCheckDistance,
                ~0, QueryTriggerInteraction.Ignore);
        }

        private void Land()
        {
            bool fromDoubleJump = jumpCount >= MaxJumps;
            IsJumping = false;
            if (!CanPlayJumpAnimation) return;
            if (airDodgeLanding)
            {
                airDodgeLanding = false;
                landingState = JumpEndState;
                animator.CrossFadeInFixedTime(landingState, .08f, 0, 0f);
                return;
            }
            if (IsMoving)
            {
                // 이동 입력이 있으면 착지 모션 없이 바로 이동 모션으로 (미끄러지는 착지 방지).
                animator.CrossFadeInFixedTime(IsRunning ? "Base Layer.Run" : "Base Layer.Walk", .15f, 0, 0f);
                return;
            }
            landingState = fromDoubleJump ? DoubleJumpEndState : JumpEndState;
            animator.CrossFadeInFixedTime(landingState, .08f, 0, 0f);
        }

        /// <summary>Leaves the landing animation for Idle when it ends, or for Walk/Run as soon as the player moves.</summary>
        private void UpdateLanding()
        {
            if (landingState == null) return;
            if (!CanPlayJumpAnimation || IsJumping) { landingState = null; return; }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                if (animator.GetNextAnimatorStateInfo(0).IsName(landingState)) return;
                state = animator.GetNextAnimatorStateInfo(0);
            }
            if (!state.IsName(landingState)) { landingState = null; return; }
            if (IsMoving)
                animator.CrossFadeInFixedTime(IsRunning ? "Base Layer.Run" : "Base Layer.Walk", .15f, 0, 0f);
            else if (state.normalizedTime >= 1f)
                animator.CrossFadeInFixedTime("Base Layer.Idle", .15f, 0, 0f);
            else return;
            landingState = null;
        }

        /// <summary>Another action took over movement (air combo, dodge, link jump, upper attack).</summary>
        private void ResetJumpState()
        {
            airDodgeLanding = false;
            jumpCount = 0;
            IsJumping = false;
            airborneTime = 0f;
            landingState = null;
            animator.SetBool("isJumping", false);
            animator.SetInteger("jumpCount", 0);
        }

        private Vector3 GetInputDirection()
        {
            Vector2 move = Player.Controls.Move;
            return new Vector3(move.x, 0, move.y).normalized;
        }

        private static Vector2 TransformToLocalInput(Vector3 input, Transform cam)
        {
            Vector3 camForward = cam.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = cam.right;
            camRight.y = 0;
            camRight.Normalize();

            float localX = Vector3.Dot(input, camRight);
            float localY = Vector3.Dot(input, camForward);

            return new Vector2(localX, localY);
        }
    }
}
