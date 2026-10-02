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
                jumpCount = 0;
                return;
            }
            var dodge = Player.Dodge;
            if (dodge.IsDodging)
            {
                dodge.Tick(dt);
                velocity = Vector3.zero;
                return;
            }
            if (Player.Controls.DodgePressed)
            {
                Transform basis = Player.CameraTransform;
                Vector3 forward = basis.forward; forward.y = 0f; forward.Normalize();
                Vector3 right = basis.right; right.y = 0f; right.Normalize();
                Vector2 move = Player.Controls.Move;
                Vector3 input = forward * move.y + right * move.x;
                if (dodge.TryBegin(input)) { velocity = Vector3.zero; return; }
            }
            var linkJump = Player.LinkJump;
            if (linkJump.IsTraversing)
            {
                linkJump.Tick(dt);
                velocity = Vector3.zero;
                jumpCount = 0;
                return;
            }
            if (Player.Controls.JumpPressed && linkJump.TryBegin())
            {
                velocity = Vector3.zero;
                jumpCount = 0;
                return;
            }
            HandleMovement(dt);
            if (Player.Combat.IsUsingUpperAttack)
            {
                // UpperAttack owns vertical movement through collision-aware root motion.
                // Do not add normal jump gravity or accept Space during this animation.
                velocity = Vector3.zero;
                jumpCount = 0;
                IsJumping = false;
                animator.SetBool("isJumping", false);
                animator.SetInteger("jumpCount", 0);
                animator.SetBool("isGrounded", character.isGrounded);
                return;
            }
            HandleJump(dt);
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
            // Raycast를 사용하여 바닥에 닿았는지 확인
            IsGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, config.groundCheckDistance + 0.1f);

            if (IsGrounded)
            {
                // 착지 시점에 점프 횟수를 초기화
                jumpCount = 0;
                IsJumping = false;
                animator.SetBool("isJumping", false);

                animator.SetInteger("jumpCount", jumpCount);
                // CharacterController가 바닥에 잘 붙도록 y축 속도 보정
                if (velocity.y < 0)
                {
                    velocity.y = -2f;
                }
            }

            animator.SetBool("isGrounded", IsGrounded);

            if (!Player.IsHitReacting && Player.Controls.JumpPressed && jumpCount < MaxJumps)
            {
                velocity.y = config.jumpForce;
                IsJumping = true;
                animator.SetBool("isJumping", true);
                jumpCount++; // 점프 횟수 1 증가
                animator.SetInteger("jumpCount", jumpCount);
            }

            // 중력 적용
            velocity.y += config.gravity * dt;
            character.Move(velocity * dt);
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
