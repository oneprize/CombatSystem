using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// Applies the enemy/boss rig's animator root motion during attacks, knockdowns and hit
    /// reactions. Only ever added to an enemy-shaped rig (Enemy, PracticeDummy); see
    /// CombatSystem.Player.RootMotionModule for the player-side equivalent.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EnemyRootMotion : MonoBehaviour
    {
        private Animator animator;
        private EnemyStateMachine enemy;
        private EnemyDamageReaction enemyReaction;
        private CharacterController character;
        private bool practiceDummy;

        private void Awake()
        {
            practiceDummy = GetComponent<CombatSystem.Training.PracticeDummy>() != null;
            animator = GetComponent<Animator>();
            enemy = GetComponent<EnemyStateMachine>();
            enemyReaction = GetComponent<EnemyDamageReaction>();
            character = GetComponent<CharacterController>();
        }

        private bool HasEnemyReactionMotion(AnimatorStateInfo state)
        {
            return (practiceDummy && (state.IsName("Base Layer.Hit") ||
                    state.IsName("Base Layer.AerialHit") || state.IsName("Base Layer.AerialHit2"))) ||
                   state.IsName("Base Layer.AerialGetUp") ||
                   state.IsName("Base Layer.SkillAKnockDown");
        }

        private void OnAnimatorMove()
        {
            if (enemyReaction != null && enemyReaction.IsBeingGathered) return;
            if (enemyReaction != null && enemyReaction.HasPhysicalKnockdown)
            {
                var motion = GetComponent<EnemyKnockbackMotion>();
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (motion != null && animator.applyRootMotion &&
                    state.IsName("Base Layer.AerialGetUp") && !animator.IsInTransition(0))
                    motion.ApplyRecoveryMotion(animator.deltaPosition);
                return;
            }
            if (enemyReaction != null && enemy != null && enemy.CurrentState == enemyReaction && enemyReaction.IsGroundComboReaction)
            {
                if (animator.applyRootMotion) enemyReaction.ApplyGroundReactionMotion(animator.deltaPosition);
                return;
            }
            bool attack = enemy != null && enemy.CurrentState == enemy.AttackState;
            bool enemyReactionMotion = enemy != null && enemyReaction != null && enemy.CurrentState == enemyReaction &&
                (HasEnemyReactionMotion(animator.GetCurrentAnimatorStateInfo(0)) ||
                 (animator.IsInTransition(0) && HasEnemyReactionMotion(animator.GetNextAnimatorStateInfo(0))));
            if (!animator.applyRootMotion || (!attack && !enemyReactionMotion)) return;
            Vector3 movement = animator.deltaPosition;
            // 중력은 기존 컨트롤러가 담당하고 정면 회전은 전투 코드가 담당한다.
            movement.y = 0f;
            if (attack && enemy.AttackState.IsOpeningAttack) movement *= enemy.AttackState.OpeningMotionScale;
            if (character != null && character.enabled)
                character.Move(movement);
            else
                transform.position += movement;
        }
    }
}
