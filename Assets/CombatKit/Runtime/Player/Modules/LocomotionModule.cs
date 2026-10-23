using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 카메라 기준 걷기/달리기, 2단 점프와 중력을 처리한다. 회피와 링크 점프의 시작 입력도 받으며,
    /// 이동을 소유한 동작(공중 콤보, 회피, 링크 점프)이 있으면 그 프레임을 해당 모듈에 넘긴다.
    /// </summary>
    public sealed class LocomotionModule : PlayerModule
    {
        #region 필드

        private const int MaxJumps = 2; // 최대 연속 점프 횟수
        private const string RunTurnState = "Base Layer.RunTurn";
        /// <summary>Player_RunTurn180 클립이 루트 모션으로 도는 각도(좌회전). 실제 목표 각도에 맞춰 회전량을 비례 조정하는 기준이다.</summary>
        private const float RunTurnClipYaw = 181f;

        private PlayerProfile.LocomotionSettings config;
        /// <summary>급격한 방향 반전 후 방향 전환 모션(isTurning)을 유지할 남은 시간.</summary>
        private float turningTimer;
        private int jumpCount; // 현재 공중에서 사용한 점프 횟수
        private Vector3 velocity;
        private Vector3 wallJumpHorizontalVelocity;
        private bool wallJumpAirControl;
        private int wallJumpFrame = -1;
        private Vector3 lastMoveDirection; // 방향 반전 감지용 직전 이동 방향
        /// <summary>지상 콤보 도중 누른 회피 입력을 회피 캔슬 시점까지 보관해 두는 플래그.</summary>
        private bool dodgeBuffered;
        private float airborneTime;
        /// <summary>재생 중인 착지 애니메이터 상태. 착지 모션이 끝나거나 이동을 시작하면 null로 비운다.</summary>
        private string landingState;
        // Wall exits must show the landing pose before held movement resumes.
        private float minimumLandingProgress;
        /// <summary>공중 회피가 끝나 낙하 중임을 표시. 착지 시 이동 입력과 관계없이 JumpEnd 착지 모션을 재생하게 한다.</summary>
        private bool airDodgeLanding;
        /// <summary>달리기 턴 애니메이션에 실제로 진입했는지. 진입 전에는 다른 동작이 애니메이터를 가져갔다고 판정하지 않는다.</summary>
        private bool enteredRunTurn;
        /// <summary>달리기 턴을 요청한 뒤 아직 진입하지 못한 프레임 수. 진입이 끝내 일어나지 않으면 턴을 포기한다.</summary>
        private int runTurnWaitFrames;
        /// <summary>클립의 회전량(181°)을 턴 시작 시점의 목표 각도에 맞추는 배율.</summary>
        private float runTurnYawScale = 1f;
        /// <summary>Run 애니메이션으로 마지막으로 달린 시각(Time.time). 달리기 턴 입력 유예 판정의 기준이다.</summary>
        private float lastRunTime = float.NegativeInfinity;

        #endregion

        #region 프로퍼티

        /// <summary>Normal movement momentum handed to an airborne traversal grab.</summary>
        public Vector3 TraversalVelocity => Vector3.up * velocity.y + (wallJumpAirControl ? wallJumpHorizontalVelocity : IsMoving ? lastMoveDirection *
            (IsRunning ? config.runSpeed : config.walkSpeed) * (IsTurning ? .3f : 1f) : Vector3.zero);
        public bool IsMoving { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsJumping { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsTurning { get; private set; }
        /// <summary>달리던 중 반대 방향 입력으로 180° 달리기 턴을 재생 중인지. 이 동안 이동과 회전은 클립의 루트 모션이 담당한다.</summary>
        public bool IsRunTurning { get; private set; }

        #endregion

        #region 프레임 갱신 및 동작 위임

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.locomotion;
        }

        /// <summary>
        /// 매 프레임 이동을 갱신한다. 공중 콤보·회피·링크 점프가 진행 중이면 해당 모듈에 위임하고,
        /// 아니면 회피/링크 점프 시작 입력을 확인한 뒤 일반 이동과 점프·중력을 처리한다.
        /// </summary>
        public void Tick(float dt)
        {
            var air = Player.AirCombo;
            // 공중 콤보·회피·링크 점프가 이동을 가져가면 진행 중이던 달리기 턴은 끝난 것으로 본다.
            if (air.Active || Player.Dodge.IsDodging || Player.LinkJump.IsTraversing) IsRunTurning = false;
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
                // 공격 모션 도중 눌렀다면 현재 콤보 단계가 회피 캔슬 시점에 도달할 때까지 입력을 보관한다.
                // 가장 최근 입력이 우선이므로, 앞서 예약된 콤보 후속 공격은 버린다.
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
            if (JumpInputAvailable && !wallJumpAirControl && linkJump.TryBegin())
            {
                velocity = Vector3.zero;
                ResetJumpState();
                return;
            }
            HandleMovement(dt);
            if (Player.Combat.IsUsingUpperAttack || Player.Combat.IsUsingChaseAttack || Player.Combat.IsReleasingPower || Player.Combat.IsGroundComboFinisher)
            {
                // UpperAttack(및 추격 공격·파워 공격·지상 콤보 피니시)는 충돌을 고려한 루트 모션으로 수직 이동을 직접 제어한다.
                // 이 애니메이션 동안에는 일반 점프 중력을 더하거나 Space 입력을 받지 않는다.
                velocity = Vector3.zero;
                ResetJumpState();
                animator.SetBool("isGrounded", character.isGrounded);
                return;
            }
            HandleJump(dt);
        }

        /// <summary>공중 콤보 낙하가 착지했을 때 호출되어 접지 상태와 점프 상태를 초기화한다.</summary>
        public void OnCombatLanded()
        {
            IsGrounded = true;
            velocity = Vector3.zero;
            ResetJumpState();
            animator.SetBool("isGrounded", true);
        }

        /// <summary>새 벽에 실제로 붙으면 이전 공중 이동을 끝내고 추가 점프 한 번을 다시 준비한다.</summary>
        public void OnWallRunAttached()
        {
            ResumeAirMovement(0f, true, .6f, false);
        }

        /// <summary>벽에서 뛰어오르는 속도를 유지하면서 공중 조작과 추가 점프 한 번을 활성화한다.</summary>
        public void BeginWallJump(Vector3 launchVelocity)
        {
            ResumeAirMovement(launchVelocity.y, true, .6f);
            wallJumpAirControl = true;
            wallJumpHorizontalVelocity = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
            wallJumpFrame = Time.frameCount;
        }

        /// <summary>벽 이탈 속도를 이어받고 JumpLoop에서 JumpEnd까지 재생한 뒤 이동으로 복귀한다.</summary>
        public void ResumeWallExit(float verticalSpeed)
        {
            ResumeAirMovement(verticalSpeed, false, .6f);
        }

        public void ResumeAirDodge()
        {
            ResumeAirMovement(-2f, false, 0f);
        }

        private void ResumeAirMovement(float verticalSpeed, bool forceAirborne, float landingProgress, bool playJumpAnimation = true)
        {
            IsRunTurning = false;
            IsTurning = false;
            turningTimer = 0f;
            landingState = null;
            minimumLandingProgress = landingProgress;
            wallJumpAirControl = false;
            wallJumpHorizontalVelocity = Vector3.zero;
            airDodgeLanding = true;
            airborneTime = 0f;
            IsGrounded = !forceAirborne && character.isGrounded;
            IsJumping = !IsGrounded;
            jumpCount = IsGrounded ? 0 : 1;
            velocity = Vector3.up * (IsGrounded ? -2f : verticalSpeed);
            animator.SetBool("isGrounded", IsGrounded);
            animator.SetBool("isJumping", IsJumping);
            animator.SetBool("isTurning", false);
            animator.SetInteger("jumpCount", jumpCount);
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            if (!playJumpAnimation) return;
            if (IsGrounded) Land();
            else animator.CrossFadeInFixedTime(JumpLoopState, .08f, 0, 0f);
        }

        /// <summary>지상 회피가 끝난 프레임에 곧바로 이동을 이어 받아 멈칫거림 없이 걷기/달리기로 복귀시킨다.</summary>
        public void ResumeAfterDodge(float dt)
        {
            HandleMovement(dt);
        }

        #endregion

        #region 이동 처리

        /// <summary>
        /// 카메라 기준 입력으로 캐릭터를 회전·이동시키고 이동 관련 애니메이터 파라미터를 갱신한다.
        /// 급격히 반대 방향으로 꺾으면 잠시 방향 전환 모션과 감속을 적용한다.
        /// </summary>
        private void HandleMovement(float dt)
        {
            // 공격, 가드, 피격 리액션 중에는 이동을 막는다.
            bool isCombatLocked = Player.IsAttacking || Player.IsGuarding || Player.IsHitReacting;
            if (isCombatLocked)
            {
                wallJumpAirControl = false;
                wallJumpHorizontalVelocity = Vector3.zero;
                IsRunTurning = false;
                animator.SetBool("isMoving", false);
                animator.SetBool("isRunning", false);
                animator.SetBool("isTurning", false);
                animator.SetFloat("moveX", 0f);
                animator.SetFloat("moveY", 0f);
                IsMoving = false;
                IsRunning = false;
                return;
            }

            if (IsRunTurning)
            {
                TickRunTurn();
                return;
            }

            Transform cam = Player.CameraTransform;
            Vector3 inputDir = GetInputDirection();
            bool isMoving = inputDir.magnitude > 0.1f;
            bool isRunning = isMoving && Player.Controls.RunHeld;

            Vector2 move = Player.Controls.Move;
            Vector3 currentMoveDirection = cam.forward * move.y + cam.right * move.x;
            currentMoveDirection.y = 0f;
            currentMoveDirection.Normalize();

            if (isRunning && TryBeginRunTurn(currentMoveDirection)) return;

            if (!wallJumpAirControl && isMoving && lastMoveDirection.magnitude > 0.1f)
            {
                float dot = Vector3.Dot(lastMoveDirection.normalized, currentMoveDirection.normalized);
                if (dot < -0.8f)
                    turningTimer = config.turningHoldTime; // 거의 정반대로 꺾으면 방향 전환 모션 시작
            }

            // 방향 전환 유지 시간 동안은 isTurning을 켜 두어 전환 모션과 감속을 보여 준다.
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
                if (!wallJumpAirControl) character.Move(currentMoveDirection * speed * dt);

                lastMoveDirection = currentMoveDirection;
            }

            if (wallJumpAirControl)
            {
                // Steer the wall impulse toward camera-relative input instead of locking its direction.
                Vector3 targetVelocity = isMoving ? currentMoveDirection * (isRunning ? config.runSpeed : config.walkSpeed) : Vector3.zero;
                float acceleration = isMoving ? 24f : 8f;
                wallJumpHorizontalVelocity = Vector3.MoveTowards(wallJumpHorizontalVelocity, targetVelocity, acceleration * dt);
                character.Move(wallJumpHorizontalVelocity * dt);
            }

            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isRunning", isRunning);
            animator.SetBool("isTurning", IsTurning);

            Vector2 animInput = TransformToLocalInput(inputDir, cam);
            animator.SetFloat("moveX", animInput.x);
            animator.SetFloat("moveY", animInput.y);

            IsMoving = isMoving;
            IsRunning = isRunning;
            if (isRunning && IsRunAnimationActive()) lastRunTime = Time.time;
        }

        #endregion

        #region 달리기 턴

        /// <summary>
        /// 현재 상태 또는 전이 대상이 Run인지. 입력이 잠깐 끊겨 Run에서 빠져나가는 전이 중이거나
        /// Walk/Idle에서 Run으로 들어오는 중이어도 달리기 턴을 시작할 수 있게 한다.
        /// </summary>
        private bool IsRunAnimationActive()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Run")) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Run");
        }

        /// <summary>
        /// 접지 상태로 Run 애니메이션을 재생하는 중에 입력이 바라보는 방향과 runTurnMinAngle 이상 반대이면 180° 달리기 턴을 시작한다.
        /// 클립은 항상 왼쪽으로 돌므로, 목표 방향까지의 왼쪽 회전량에 맞춰 루트 모션 회전을 비례 조정한다.
        /// </summary>
        /// <returns>턴을 시작했으면 true.</returns>
        private bool TryBeginRunTurn(Vector3 desiredDirection)
        {
            if (!IsGrounded || landingState != null || desiredDirection.sqrMagnitude < .01f) return false;
            // 키를 바꾸는 사이 이동 입력이 몇 프레임 0이 되어도(공백, 또는 정반대 키 동시 입력) 턴이 되도록
            // 직전 프레임이 아니라 유예 시간 안에 달리고 있었는지로 판정한다.
            if (Time.time - lastRunTime > config.runTurnInputGrace || !IsRunAnimationActive()) return false;
            if (!animator.HasState(0, Animator.StringToHash(RunTurnState))) return false;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            float signedAngle = Vector3.SignedAngle(forward, desiredDirection, Vector3.up);
            if (Mathf.Abs(signedAngle) < config.runTurnMinAngle) return false;
            // 왼쪽 회전량으로 환산: 왼쪽 목표(-160°)는 160°, 오른쪽 목표(+160°)는 200°를 돈다.
            float leftTurn = signedAngle <= 0f ? -signedAngle : 360f - signedAngle;
            runTurnYawScale = leftTurn / RunTurnClipYaw;
            IsRunTurning = true;
            enteredRunTurn = false;
            runTurnWaitFrames = 0;
            turningTimer = 0f;
            IsTurning = false;
            animator.SetBool("isTurning", false);
            animator.CrossFadeInFixedTime(RunTurnState, config.runTurnBlendIn, 0, 0f);
            return true;
        }

        /// <summary>
        /// 달리기 턴 진행을 감시한다. 다른 동작(회피, 점프, 낙하 등)이 애니메이터를 가져가면 턴을 끝내고,
        /// runTurnExitProgress에 도달하면 그 시점의 입력에 맞춰 Run/Walk/Idle로 복귀한다.
        /// </summary>
        private void TickRunTurn()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool inTurn = state.IsName(RunTurnState);
            bool inTransition = animator.IsInTransition(0);
            bool turnIsNext = inTransition && animator.GetNextAnimatorStateInfo(0).IsName(RunTurnState);
            bool leaving = inTurn && inTransition && !turnIsNext;
            if (inTurn && !leaving) enteredRunTurn = true;
            if (!enteredRunTurn)
            {
                // CrossFade 요청은 다음 애니메이터 갱신부터 반영되므로 몇 프레임은 진입을 기다린다.
                if (!turnIsNext && ++runTurnWaitFrames > 3) IsRunTurning = false;
                return;
            }
            if (!inTurn || leaving)
            {
                IsRunTurning = false;
                return;
            }
            if (state.normalizedTime < config.runTurnExitProgress) return;

            IsRunTurning = false;
            bool moving = Player.Controls.Move.sqrMagnitude > .01f;
            bool running = moving && Player.Controls.RunHeld;
            animator.SetBool("isMoving", moving);
            animator.SetBool("isRunning", running);
            animator.CrossFadeInFixedTime(running ? "Base Layer.Run" : moving ? "Base Layer.Walk" : "Base Layer.Idle",
                config.runTurnBlendOut, 0, 0f);
            // 턴 직후 같은 입력이 다시 '반대 방향'으로 판정되지 않도록 기준 방향을 새 정면으로 맞춘다.
            lastMoveDirection = transform.forward;
            IsMoving = moving;
            IsRunning = running;
        }

        /// <summary>
        /// 달리기 턴 동안 클립의 루트 모션을 적용한다. 회전은 목표 각도에 맞게 비례 조정하고,
        /// 수직 이동은 버려 중력과 접지는 점프·중력 처리에 맡긴다.
        /// </summary>
        public void ApplyRunTurnRootMotion(Vector3 delta, Quaternion deltaRotation)
        {
            if (!IsRunTurning || !character.enabled) return;
            float yaw = Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y) * runTurnYawScale;
            transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;
            delta.y = 0f;
            character.Move(delta);
        }

        #endregion

        #region 점프·착지

        /// <summary>
        /// 접지 판정, 2단 점프 입력, 낭떠러지 낙하 전환, 착지 처리와 중력 적용을 담당한다.
        /// </summary>
        private void HandleJump(float dt)
        {
            // 아래의 Land()보다 먼저 호출해야 한다. CrossFade는 다음 업데이트부터 애니메이터 상태에 반영되므로,
            // 요청한 같은 프레임에 착지 상태를 검사하면 착지 처리가 누락된다.
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

            if (JumpInputAvailable && jumpCount < MaxJumps && CanPlayJumpAnimation)
            {
                velocity.y = config.jumpForce;
                IsJumping = true;
                jumpCount++;
                landingState = null;
                if (!wallJumpAirControl) minimumLandingProgress = 0f;
                animator.CrossFadeInFixedTime(jumpCount == 1 ? JumpStartState : DoubleJumpStartState, .08f, 0, 0f);
            }

            animator.SetBool("isGrounded", IsGrounded);
            animator.SetBool("isJumping", IsJumping);
            animator.SetInteger("jumpCount", jumpCount);

            velocity.y += config.gravity * dt;
            character.Move(velocity * dt);
        }

        // The Space press that leaves the wall must not also consume the extra jump in the same frame.
        private bool JumpInputAvailable => Player.Controls.JumpPressed && Time.frameCount != wallJumpFrame;

        private const string JumpStartState = "Base Layer.JumpStart";
        private const string JumpLoopState = "Base Layer.JumpLoop";
        private const string JumpEndState = "Base Layer.JumpEnd";
        private const string DoubleJumpStartState = "Base Layer.DoubleJumpStart";
        private const string DoubleJumpEndState = "Base Layer.DoubleJumpEnd";

        /// <summary>점프/낙하/착지 애니메이션은 다른 동작(피격, 공격, 가드)이 애니메이터를 쓰지 않을 때만 재생한다.</summary>
        private bool CanPlayJumpAnimation => !Player.IsHitReacting && !Player.IsAttacking && !Player.IsGuarding;

        /// <summary>
        /// 캡슐 하단에서 짧게 레이캐스트해 접지 여부를 판정한다. Transform이 캡슐 중심에 있으므로
        /// Transform에서 쏘면 바닥에 닿지 않아 착지하지 못하기 때문이다.
        /// </summary>
        private bool CheckGrounded()
        {
            if (character.isGrounded) return true;
            var bounds = character.bounds;
            Vector3 bottom = bounds.center - Vector3.up * bounds.extents.y;
            return Physics.Raycast(bottom + Vector3.up * .05f, Vector3.down, .05f + config.groundCheckDistance,
                ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 점프 상태를 끝내고 상황에 맞는 착지 모션을 재생한다. 이동 중이면 착지 모션 없이 바로 걷기/달리기로,
        /// 2단 점프 후면 2단 점프 착지 모션으로 전환한다.
        /// </summary>
        private void Land()
        {
            wallJumpAirControl = false;
            wallJumpHorizontalVelocity = Vector3.zero;
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

        /// <summary>착지 애니메이션이 끝나면 대기 상태로, 그 전에 이동을 시작하면 즉시 걷기/달리기로 빠져나간다.</summary>
        private void UpdateLanding()
        {
            if (landingState == null) return;
            if (!CanPlayJumpAnimation || IsJumping) { landingState = null; minimumLandingProgress = 0f; return; }
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                if (animator.GetNextAnimatorStateInfo(0).IsName(landingState)) return;
                state = animator.GetNextAnimatorStateInfo(0);
            }
            if (!state.IsName(landingState)) { landingState = null; minimumLandingProgress = 0f; return; }
            if (IsMoving && state.normalizedTime >= minimumLandingProgress)
                animator.CrossFadeInFixedTime(IsRunning ? "Base Layer.Run" : "Base Layer.Walk", .15f, 0, 0f);
            else if (state.normalizedTime >= 1f)
                animator.CrossFadeInFixedTime("Base Layer.Idle", .15f, 0, 0f);
            else return;
            landingState = null;
            minimumLandingProgress = 0f;
        }

        /// <summary>다른 동작(공중 콤보, 회피, 링크 점프, UpperAttack 등)이 이동을 가져갔을 때 점프·착지 상태를 모두 초기화한다.</summary>
        private void ResetJumpState()
        {
            wallJumpAirControl = false;
            wallJumpHorizontalVelocity = Vector3.zero;
            minimumLandingProgress = 0f;
            airDodgeLanding = false;
            jumpCount = 0;
            IsJumping = false;
            airborneTime = 0f;
            landingState = null;
            animator.SetBool("isJumping", false);
            animator.SetInteger("jumpCount", 0);
        }

        #endregion

        #region 입력 변환

        /// <summary>이동 입력(2D)을 XZ 평면의 정규화된 방향으로 바꾼다.</summary>
        private Vector3 GetInputDirection()
        {
            Vector2 move = Player.Controls.Move;
            return new Vector3(move.x, 0, move.y).normalized;
        }

        /// <summary>입력 방향을 카메라의 수평 오른쪽/정면 축에 투영해 블렌드 트리용 moveX/moveY 값으로 만든다.</summary>
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

        #endregion
    }
}
