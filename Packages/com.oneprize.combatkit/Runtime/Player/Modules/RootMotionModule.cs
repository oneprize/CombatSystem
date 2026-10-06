using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// Routes the animator's root motion to whichever action currently owns movement
    /// (dodge, piercing dash, upper attack, attacks and hit reactions).
    /// </summary>
    public sealed class RootMotionModule : PlayerModule
    {
        public void Apply(Vector3 delta)
        {
            var combat = Player.Combat;
            if (combat.IsUsingSkillI) return;
            if (combat.IsUsingPiercingSkill)
            {
                Player.PiercingDash.TickAnimation();
                return;
            }
            if (Player.IsDodging)
            {
                if (animator.applyRootMotion) Player.Dodge.ApplyRootMotion(delta);
                return;
            }
            if (combat.IsUsingUpperAttack)
            {
                Player.AirCombo.ApplyUpperMotion(delta);
                return;
            }
            if (combat.IsUsingChaseAttack)
            {
                Player.AirCombo.ApplyChaseMotion();
                return;
            }
            if (combat.IsReleasingPower)
            {
                // The power attack's dashes and leap (including height) come from its clip's motion curves.
                if (animator.applyRootMotion && character != null && character.enabled) character.Move(delta);
                return;
            }
            if (combat.IsUsingAirCombo)
            {
                // Air attacks travel with their clip; AirComboModule keeps owning the height.
                if (!animator.applyRootMotion || character == null || !character.enabled) return;
                delta.y = 0f;
                character.Move(PushTarget(delta, combat.AttackTarget, combat.AirAttackContactDistance));
                return;
            }
            if (Player.IsLinkJumping || Player.IsAirComboActive) return;
            if (!animator.applyRootMotion || (!Player.IsHitReacting && !Player.IsAttacking)) return;
            // 중력은 이동 모듈이 담당하고 정면 회전은 전투 코드가 담당한다.
            delta.y = 0f;
            if (character != null && character.enabled)
                character.Move(delta);
            else
                transform.position += delta;
        }

        /// <summary>
        /// The player follows the clip's root motion in full; once that would carry it closer than
        /// <paramref name="contactDistance"/> to an airborne target, the target is pushed by the excess
        /// in the same frame. Whatever the target cannot move (wall) is taken back off the player's move.
        /// </summary>
        private Vector3 PushTarget(Vector3 delta, CombatSystem.Combat.EnemyDamageReaction target, float contactDistance)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsAirborneForCombo) return delta;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < .0001f) return delta;
            Vector3 direction = toTarget / distance;
            float excess = Vector3.Dot(delta, direction) - Mathf.Max(0f, distance - contactDistance);
            if (excess <= 0f) return delta;
            float pushed = Vector3.Dot(target.PushAirborne(direction * excess), direction);
            delta -= direction * Mathf.Max(0f, excess - pushed);
            return delta;
        }
    }
}
