using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 공격, 다운, 피격 리액션 동안 적(보스 포함) 리그의 애니메이터 루트 모션을 실제 이동으로 반영한다.
    /// 적 형태의 리그(Enemy, PracticeDummy)에만 붙이며, 플레이어 쪽 대응 기능은 CombatSystem.Player.RootMotionModule이다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EnemyRootMotion : MonoBehaviour
    {
        #region 필드

        private Animator animator;
        private EnemyStateMachine enemy;
        private EnemyDamageReaction enemyReaction;
        private CharacterController character;
        /// <summary>훈련용 더미 여부. 더미는 일반·공중 피격 리액션에도 루트 모션을 적용한다.</summary>
        private bool practiceDummy;

        #endregion

        #region Unity 생명주기

        /// <summary>같은 오브젝트의 애니메이터·상태 머신·피격 리액션·캐릭터 컨트롤러 참조를 캐시한다.</summary>
        private void Awake()
        {
            practiceDummy = GetComponent<CombatSystem.Training.PracticeDummy>() != null;
            animator = GetComponent<Animator>();
            enemy = GetComponent<EnemyStateMachine>();
            enemyReaction = GetComponent<EnemyDamageReaction>();
            character = GetComponent<CharacterController>();
        }

        #endregion

        #region 루트 모션 적용

        /// <summary>루트 모션을 그대로 반영해야 하는 피격 리액션 애니메이터 상태인지 판정한다.</summary>
        private bool HasEnemyReactionMotion(AnimatorStateInfo state)
        {
            return (practiceDummy && (state.IsName("Base Layer.Hit") ||
                    state.IsName("Base Layer.AerialHit") || state.IsName("Base Layer.AerialHit2"))) ||
                   state.IsName("Base Layer.AerialGetUp") ||
                   state.IsName("Base Layer.SkillAKnockDown");
        }

        /// <summary>
        /// 현재 상황에 맞게 루트 모션의 적용 주체를 고른다. 끌어모으기 중에는 무시하고, 물리 다운 중에는 기상 동작만
        /// 넉백 물리에 넘기며, 지상 콤보 피격은 밀려나는 성분만 반영한다. 그 외 공격·특정 리액션에서는 수평 이동만 적용한다.
        /// </summary>
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
            if (enemyReaction != null && enemy != null && ReferenceEquals(enemy.CurrentState, enemyReaction) && enemyReaction.IsGroundComboReaction)
            {
                if (animator.applyRootMotion) enemyReaction.ApplyGroundReactionMotion(animator.deltaPosition);
                return;
            }
            bool attack = enemy != null && enemy.CurrentState == enemy.AttackState;
            bool enemyReactionMotion = enemy != null && enemyReaction != null && ReferenceEquals(enemy.CurrentState, enemyReaction) &&
                (HasEnemyReactionMotion(animator.GetCurrentAnimatorStateInfo(0)) ||
                 (animator.IsInTransition(0) && HasEnemyReactionMotion(animator.GetNextAnimatorStateInfo(0))));
            if (!animator.applyRootMotion || (!attack && !enemyReactionMotion)) return;
            Vector3 movement = animator.deltaPosition;
            // 수직 이동(중력)은 기존 컨트롤러가, 정면 회전은 전투 코드가 담당하므로 수평 이동만 반영한다.
            movement.y = 0f;
            if (attack && enemy.AttackState.IsOpeningAttack) movement *= enemy.AttackState.OpeningMotionScale;
            if (character != null && character.enabled)
                character.Move(movement);
            else
                transform.position += movement;
        }

        #endregion
    }
}
