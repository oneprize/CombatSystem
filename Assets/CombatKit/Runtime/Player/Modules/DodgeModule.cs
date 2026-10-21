using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 회피 기능. 이동 입력이 있으면 그 방향으로 전방 회피(달리는 중이면 달리기 회피),
    /// 입력이 없으면 정면을 유지한 채 뒤로 회피한다.
    /// </summary>
    public sealed class DodgeModule : PlayerModule
    {
        #region 필드

        public bool IsDodging { get; private set; }
        /// <summary>이번 회피에 재생 중인 애니메이터 상태의 전체 경로(DodgeFront/DodgeBack/DodgeRun).</summary>
        public string CurrentStateName { get; private set; }
        private PlayerProfile.DodgeSettings config;
        private Vector3 direction;
        /// <summary>크로스페이드가 끝나 회피 상태에 실제로 들어갔는지. 진입 전에 종료 판정을 하지 않기 위해 쓴다.</summary>
        private bool enteredDodgeAnimation;
        /// <summary>회피 도중 한 번이라도 공중에 있었는지. 끝날 때 공중 상태로 복귀할지 결정한다.</summary>
        private bool airborneDodge;

        #endregion

        #region 회피 처리

        /// <summary>프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.dodge;
        }

        /// <summary>
        /// 입력 방향에 맞는 회피 애니메이션을 고르고 진행 중인 공격을 취소한 뒤 회피를 시작한다.
        /// </summary>
        /// <returns>다른 동작 중이거나 해당 애니메이터 상태가 없어 시작하지 못하면 false.</returns>
        public bool TryBegin(Vector3 worldInput)
        {
            if (!Player.isActiveAndEnabled || IsDodging || !character.enabled || !Player.CanStartAction(PlayerAction.Dodge)) return false;
            worldInput.y = 0f;
            bool hasDirectionInput = worldInput.sqrMagnitude > .001f;
            direction = hasDirectionInput ? worldInput.normalized : transform.forward;
            bool runningDodge = hasDirectionInput && Player.Locomotion.IsRunning && Player.Controls.RunHeld;
            CurrentStateName = runningDodge ? "Base Layer.DodgeRun" : hasDirectionInput ? "Base Layer.DodgeFront" : "Base Layer.DodgeBack";
            if (!animator.HasState(0, Animator.StringToHash(CurrentStateName))) return false;
            Player.Combat.CancelForDodge();
            // 뒤 회피는 정면을 유지하고, 뒤로 가는 이동은 클립의 루트 모션이 담당한다.
            direction.y = 0f;
            direction.Normalize();
            if (hasDirectionInput && direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
            airborneDodge = !character.isGrounded;
            enteredDodgeAnimation = false;
            IsDodging = true;
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isTurning", false);
            animator.SetBool("isJumping", false);
            animator.CrossFadeInFixedTime(CurrentStateName, config.crossFadeTime, 0, 0f);
            return true;
        }

        /// <summary>
        /// 회피 애니메이션 진행을 추적하다가 공격 캔슬 입력이 들어오면 공격으로, 끝나면 공중/이동/대기 상태로 복귀시킨다.
        /// </summary>
        public void Tick(float dt)
        {
            if (!IsDodging) return;
            if (Player.IsHitReacting) { IsDodging = false; return; }
            // 회피 중에는 CombatModule이 Tick되지 않으므로 공격 입력을 여기서 받아 둔다.
            Player.Combat.CaptureGroundAttackInput(Player.Controls);
            character.Move(Vector3.down * 2f * dt);
            airborneDodge |= !character.isGrounded;
            if (animator.IsInTransition(0)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(CurrentStateName)) enteredDodgeAnimation = true;
            if (!enteredDodgeAnimation) return;
            bool continuingRun = CurrentStateName == "Base Layer.DodgeRun" && Player.Controls.Move.sqrMagnitude > .01f && Player.Controls.RunHeld;
            bool playing = state.IsName(CurrentStateName) && state.normalizedTime < (continuingRun ? .9f : 1f);
            if (Player.Combat.GroundAttackInputReady && (!playing || state.normalizedTime >= config.attackCancelProgress))
            {
                IsDodging = false;
                Player.Combat.BeginAttackFromDodge(config.attackBlendTime);
                if (Player.IsAttacking) return;
            }
            if (playing && IsDodging) return;
            IsDodging = false;
            if (airborneDodge)
            {
                Player.Locomotion.ResumeAirDodge();
                return;
            }
            bool moving = Player.Controls.Move.sqrMagnitude > .01f;
            bool running = moving && Player.Controls.RunHeld;
            animator.SetBool("isMoving", moving);
            animator.SetBool("isRunning", running);
            animator.CrossFadeInFixedTime(running ? "Base Layer.Run" : moving ? "Base Layer.Walk" : "Base Layer.Idle", .15f, 0, 0f);
            Player.Locomotion.ResumeAfterDodge(dt);
        }

        /// <summary>회피 클립의 수평 루트 모션을 거리 배율을 적용해 캐릭터 이동으로 반영한다.</summary>
        public void ApplyRootMotion(Vector3 delta)
        {
            if (!IsDodging || !character.enabled) return;
            // 캐릭터가 이미 회피 방향을 보고 있으므로, 클립의 수평 이동(옆으로 흐르는 성분 포함)을 제작된 그대로 적용한다.
            delta.y = 0f;
            character.Move(delta * config.distanceMultiplier);
        }

        /// <summary>플레이어 비활성화 시 회피 상태를 해제한다.</summary>
        public override void OnDisable() { IsDodging = false; }

        #endregion
    }
}
