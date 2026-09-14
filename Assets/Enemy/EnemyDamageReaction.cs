using UnityEngine;
namespace ParrySystem.Combat
{
    [RequireComponent(typeof(EnemyStateMachine), typeof(Animator))]
    public class EnemyDamageReaction : MonoBehaviour, IEnemyState
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float damagePerHit = 10f;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float CurrentHealth { get; private set; }
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private Animator animator;
        private string reactionState = "Base Layer.Hit";
        private bool aerialReaction;
        [SerializeField, Min(0f)] private float aerialHitBlendTime = 0.1f;
        private void Awake()
        {
            CurrentHealth = MaxHealth;
            machine = GetComponent<EnemyStateMachine>();
            combat = GetComponent<EnemyCombat>();
            animator = GetComponent<Animator>();
        }
        public void ReceiveHit()
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damagePerHit);
            // Attacks and an existing groggy are not interrupted by ordinary damage.
            if (aerialReaction || machine.CurrentState == machine.AttackState ||
                machine.CurrentState == machine.GroggyState) return;
            reactionState = "Base Layer.Hit";
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }
        public void ReceiveSkillHit()
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damagePerHit);
            if (aerialReaction) return;
            aerialReaction = true;
            reactionState = "Base Layer.SkillAKnockDown";
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        public void ReceiveAerialHit(Transform attacker, int strikeIndex = 0)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damagePerHit);
            Vector3 direction = attacker.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
            aerialReaction = true;
            reactionState = strikeIndex == 0 ? "Base Layer.AerialHit" : "Base Layer.AerialHit2";
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        public void Enter()
        {
            combat.SetParryWindowOpen(false);
            animator.CrossFadeInFixedTime(reactionState, aerialReaction ? aerialHitBlendTime : 0.05f, 0, 0f);
        }
        public void Tick(float deltaTime)
        {
            if (animator.IsInTransition(0)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(reactionState) && state.normalizedTime >= 1f)
            {
                if (reactionState == "Base Layer.AerialHit" || reactionState == "Base Layer.AerialHit2" || reactionState == "Base Layer.SkillAKnockDown")
                {
                    reactionState = "Base Layer.AerialGetUp";
                    animator.CrossFadeInFixedTime(reactionState, 0.05f, 0, 0f);
                }
                else machine.ChangeState(machine.ObserveState);
            }
        }
        public void Exit() { aerialReaction = false; }
    }
}