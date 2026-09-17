using UnityEngine;
using ParrySystem.Player;

namespace ParrySystem.Combat
{
    [RequireComponent(typeof(Animator))]
    public class CombatRootMotion : MonoBehaviour
    {
        private PlayerDodge dodge;
        private Animator animator;
        private EnemyStateMachine enemy;
        private PlayerDamageReaction reaction;
        private CharacterController character;
        private PlayerCombat player;
        private EnemyDamageReaction enemyReaction;

        private void Awake()
        {
            dodge = GetComponent<PlayerDodge>();
            animator = GetComponent<Animator>();
            enemy = GetComponent<EnemyStateMachine>();
            enemyReaction = GetComponent<EnemyDamageReaction>();
            reaction = GetComponent<PlayerDamageReaction>();
            character = GetComponent<CharacterController>();
            player = GetComponent<PlayerCombat>();
        }

        private static bool HasEnemyReactionMotion(AnimatorStateInfo state)
        {
            return state.IsName("Base Layer.AerialGetUp") ||
                   state.IsName("Base Layer.SkillAKnockDown");
        }

        private void OnAnimatorMove()
        {
            if (enemyReaction != null && enemyReaction.HasPhysicalKnockdown)
            {
                var motion = GetComponent<EnemyKnockbackMotion>();
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (motion != null && animator.applyRootMotion &&
                    state.IsName("Base Layer.AerialGetUp") && !animator.IsInTransition(0))
                    motion.ApplyRecoveryMotion(animator.deltaPosition);
                return;
            }
            if (dodge != null && dodge.IsDodging)
            {
                if (animator.applyRootMotion) dodge.ApplyRootMotion(animator.deltaPosition);
                return;
            }
            if (player != null && player.IsUsingUpperAttack)
            {
                var air = GetComponent<PlayerAirCombo>();
                if (air != null) air.ApplyUpperMotion(animator.deltaPosition);
                return;
            }
            if (player != null && (player.IsLinkJumping || player.IsAirComboActive)) return;
            bool attack = enemy != null && enemy.CurrentState == enemy.AttackState;
            bool damaged = reaction != null && reaction.IsReacting;
            bool enemyReactionMotion = enemy != null && enemyReaction != null && enemy.CurrentState == enemyReaction &&
                (HasEnemyReactionMotion(animator.GetCurrentAnimatorStateInfo(0)) ||
                 (animator.IsInTransition(0) && HasEnemyReactionMotion(animator.GetNextAnimatorStateInfo(0))));
            if (!animator.applyRootMotion || (!attack && !damaged && !enemyReactionMotion && !(player != null && player.isAttacking))) return;
            Vector3 movement = animator.deltaPosition;
            // 중력은 기존 컨트롤러가 담당하고 정면 회전은 전투 코드가 담당한다.
            if (player == null || !player.IsUsingUpperAttack) movement.y = 0f;
            if (character != null && character.enabled)
                character.Move(movement);
            else
                transform.position += movement;
        }
    }
}
