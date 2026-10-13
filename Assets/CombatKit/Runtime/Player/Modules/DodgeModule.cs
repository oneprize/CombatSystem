using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Directional forward dodge, or a backward dodge without movement input.</summary>
    public sealed class DodgeModule : PlayerModule
    {
        public bool IsDodging { get; private set; }
        public string CurrentStateName { get; private set; }
        private PlayerProfile.DodgeSettings config;
        private Vector3 direction;
        private bool enteredDodgeAnimation;
        private bool airborneDodge;

        protected override void OnInitialize()
        {
            config = Player.Profile.dodge;
        }

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
            // Back dodge keeps facing forward; its clip supplies backward root motion.
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

        public void Tick(float dt)
        {
            if (!IsDodging) return;
            if (Player.IsHitReacting) { IsDodging = false; return; }
            // CombatModule does not tick while dodging, so attack input is read here.
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
