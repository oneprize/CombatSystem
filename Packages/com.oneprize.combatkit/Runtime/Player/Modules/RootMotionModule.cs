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
            if (Player.IsLinkJumping || Player.IsAirComboActive) return;
            if (!animator.applyRootMotion || (!Player.IsHitReacting && !Player.IsAttacking)) return;
            // 중력은 이동 모듈이 담당하고 정면 회전은 전투 코드가 담당한다.
            delta.y = 0f;
            if (character != null && character.enabled)
                character.Move(delta);
            else
                transform.position += delta;
        }
    }
}
