using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Forward dodge that moves exactly as the dodge clip's root motion does.</summary>
    public sealed class DodgeModule : PlayerModule
    {
        public bool IsDodging { get; private set; }
        public string CurrentStateName { get; private set; }
        private PlayerProfile.DodgeSettings config;
        private Vector3 direction;

        protected override void OnInitialize()
        {
            config = Player.Profile.dodge;
        }

        public bool TryBegin(Vector3 worldInput)
        {
            if (!Player.isActiveAndEnabled || IsDodging || !character.enabled || !Player.CanStartAction(PlayerAction.Dodge)) return false;
            worldInput.y = 0f;
            direction = worldInput.sqrMagnitude > .001f ? worldInput.normalized : transform.forward;
            CurrentStateName = "Base Layer.DodgeFront";
            if (!animator.HasState(0, Animator.StringToHash(CurrentStateName))) return false;
            Player.Combat.CancelForDodge();
            // Face the movement direction so the forward dodge matches its displacement.
            direction.y = 0f;
            direction.Normalize();
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
            IsDodging = true;
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isTurning", false);
            animator.SetBool("isJumping", false);
            animator.CrossFadeInFixedTime(CurrentStateName, config.crossFadeTime, 0, 0f);
            return true;
        }

        public void Tick(float dt)
        {
            if (!IsDodging) return;
            if (Player.IsHitReacting) { IsDodging = false; return; }
            // CombatModule does not tick while dodging, so attack input is read here.
            Player.Combat.CaptureGroundAttackInput(Player.Controls);
            character.Move(Vector3.down * 2f * dt);
            if (animator.IsInTransition(0)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool playing = state.IsName(CurrentStateName) && state.normalizedTime < 1f;
            if (Player.Combat.GroundAttackInputReady && (!playing || state.normalizedTime >= config.attackCancelProgress))
            {
                IsDodging = false;
                Player.Combat.BeginAttackFromDodge(config.attackBlendTime);
                if (Player.IsAttacking) return;
            }
            if (playing && IsDodging) return;
            IsDodging = false;
            animator.CrossFadeInFixedTime("Base Layer.Idle", .1f);
        }

        public void ApplyRootMotion(Vector3 delta)
        {
            if (!IsDodging || !character.enabled) return;
            // The character already faces the dodge direction, so the clip's own horizontal
            // displacement (including any sideways drift) is applied as authored.
            delta.y = 0f;
            character.Move(delta * config.distanceMultiplier);
        }

        public override void OnDisable() { IsDodging = false; }
    }
}
