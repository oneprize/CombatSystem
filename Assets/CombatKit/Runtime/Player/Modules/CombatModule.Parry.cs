using UnityEngine;

namespace CombatSystem.Player
{
    public sealed partial class CombatModule
    {
        private const int ParryAttemptIndex = 25, ParryCounterIndex = 26;
        private float parryOpenedAt;
        private bool parryConsumed, counterFollowUpQueued;
        private int parrySuccessFrame;
        private Transform counterTarget;
        private CombatSystem.Data.Player.PlayerProfile.ParrySettings ParryConfig => Player.Profile.parry;

        public bool IsParryAttempt => IsAttacking && attackIndex == ParryAttemptIndex;
        public bool IsParryCounter => IsAttacking && attackIndex == ParryCounterIndex;
        public bool IsParryAction => IsParryAttempt || IsParryCounter;
        public bool IsCounterAttacking => IsParryCounter;
        public bool IsParryWindowOpen => IsParryAttempt && !parryConsumed &&
            Time.time - parryOpenedAt <= ParryConfig.inputWindow;

        public void TryParry()
        {
            if (!Player.CanStartAction(PlayerAction.Skill) || !Player.Locomotion.IsGrounded ||
                (IsAttacking && !IsUsingGroundCombo) ||
                (Player.WeaponStance != null && !Player.WeaponStance.IsDrawn)) return;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.Skill_F"))) return;
            CancelForDamage();
            attackIndex = ParryAttemptIndex;
            IsAttacking = true;
            AttackSequence++;
            parryOpenedAt = Time.time;
            FaceNearbyEnemy(config.skillTargetDistance);
            animator.CrossFadeInFixedTime("Base Layer.Skill_F", .05f, 0, 0f);
        }

        // Called only when an enemy's active weapon hit actually reaches the player.
        public bool TryParryIncoming(Transform attacker)
        {
            if (attacker == null || Player.IsDead || Player.IsHitReacting || Player.IsDodging) return false;
            if (!IsParryAttempt && Player.Controls.ParryPressed) TryParry();
            if (!IsParryWindowOpen) return false;
            Vector3 toward = attacker.position - transform.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .001f && Vector3.Angle(transform.forward, toward) > ParryConfig.facingHalfAngle) return false;
            parryConsumed = true;
            ClearGroundAttackInput(); ClearEFollowUp();
            attackIndex = ParryCounterIndex; AttackSequence++;
            counterFollowUpQueued = false;
            counterTarget = attacker; parrySuccessFrame = Time.frameCount;
            if (toward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toward);
            AttackTarget = attacker.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>();
            animator.SetFloat("CounterPlaybackSpeed", 1f);
            animator.CrossFadeInFixedTime("Base Layer.ParryCounter", .04f, 0, 0f);
            Player.PlayParryFeedback();
            Debug.Log("[Parry Success] E parry: " + attacker.name, Player);
            return true;
        }

        private void ResetParry()
        {
            parryOpenedAt = float.NegativeInfinity;
            parryConsumed = false;
            counterFollowUpQueued = false;
            counterTarget = null;
            animator.SetFloat("CounterPlaybackSpeed", 1f);
        }

        private void TickParry()
        {
            string expected = IsParryAttempt ? "Base Layer.Skill_F" : "Base Layer.ParryCounter";
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(expected))
                state = animator.GetNextAnimatorStateInfo(0);
            if (!state.IsName(expected)) return;
            if (IsParryAttempt)
            {
                if (state.normalizedTime >= 1f) EndParry();
                return;
            }
            // A successful parry plays the complete counter automatically. A new click queues its follow-up.
            if (Player.Controls.AttackPressed && Time.frameCount > parrySuccessFrame)
                counterFollowUpQueued = true;
            if (counterFollowUpQueued && state.normalizedTime >= ParryConfig.followUpProgress)
            {
                Transform target = counterTarget;
                ResetParry();
                eFollowUpTarget = target;
                BeginEFollowUp();
                return;
            }
            if (state.normalizedTime >= 1f) EndParry();
        }

        private void EndParry()
        {
            ResetParry();
            IsAttacking = false; attackIndex = 0;
            ClearGroundAttackInput();
            animator.CrossFadeInFixedTime(config.idleAnimStateName, .15f, 0, 0f);
        }
    }
}
