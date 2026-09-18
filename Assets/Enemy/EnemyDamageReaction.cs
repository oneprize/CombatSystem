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
        private EnemyKnockbackMotion knockback;
        private bool physicalKnockdown;
        private float downElapsed;
        [SerializeField, Min(0)] private float downDuration = 1f;
        public bool HasPhysicalKnockdown => physicalKnockdown;
        public bool IsKnockdownGrounded => knockback != null && knockback.Grounded;
        [SerializeField, Min(0f)] private float aerialHitBlendTime = 0.1f;
        private void Awake()
        {
            CurrentHealth = MaxHealth;
            machine = GetComponent<EnemyStateMachine>();
            combat = GetComponent<EnemyCombat>();
            animator = GetComponent<Animator>();
            knockback = GetComponent<EnemyKnockbackMotion>();
            if (knockback == null) knockback = gameObject.AddComponent<EnemyKnockbackMotion>();
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
        public void ReceiveSkillHit(Transform attacker = null)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damagePerHit);
            if (aerialReaction) return;
            aerialReaction = true;
            reactionState = "Base Layer.SkillAKnockDown";
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-away);
            physicalKnockdown = true;
            downElapsed = 0;
            knockback.Launch(away);
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        [Header("Upper attack reaction")]
        [SerializeField, Min(0)] private float upperLiftSpeed = 8f;
        [SerializeField, Min(0)] private float upperPushSpeed = 1.5f;
        public void ReceiveUpperHit(Transform attacker)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - damagePerHit);
            if (aerialReaction) return;
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-away);
            aerialReaction = physicalKnockdown = true;
            reactionState = "Base Layer.UpperHit";
            downElapsed = 0;
            knockback.LaunchUpper(away, upperLiftSpeed, upperPushSpeed);
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        public bool IsAirborneForCombo => physicalKnockdown && knockback != null && knockback.Active && !knockback.Grounded;
        public void ReceiveAirComboHit(Transform attacker, int comboIndex)
        {
            // Air attacks do not launch a grounded enemy or bypass the existing down state.
            if (!IsAirborneForCombo) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damagePerHit);
            var toward = attacker.position - transform.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toward);
            reactionState = "Base Layer.AirComboHitB";
            knockback.AirComboImpact(comboIndex == 5);
            animator.CrossFadeInFixedTime(reactionState, .05f, 0, 0);
        }

        public void ReceiveAerialHit(Transform attacker, int strikeIndex = 0)
        {
            if (physicalKnockdown) { CurrentHealth = Mathf.Max(0f, CurrentHealth - damagePerHit); return; }
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
            if (physicalKnockdown)
            {
                knockback.Tick(deltaTime);
                if (!knockback.Grounded) return;
                if (reactionState == "Base Layer.SkillAKnockDown" || reactionState == "Base Layer.UpperHit" ||
                    reactionState == "Base Layer.AirComboHitA" || reactionState == "Base Layer.AirComboHitB")
                {
                    reactionState = "Base Layer.KnockDownGrounded";
                    downElapsed = 0;
                    animator.CrossFadeInFixedTime(reactionState, .12f, 0, 0f);
                    return;
                }
                if (reactionState == "Base Layer.KnockDownGrounded")
                {
                    downElapsed += deltaTime;
                    if (downElapsed >= downDuration)
                    {
                        reactionState = "Base Layer.AerialGetUp";
                        animator.CrossFadeInFixedTime(reactionState, .12f, 0, 0f);
                    }
                    return;
                }
            }
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
        public void Exit() { aerialReaction = false; physicalKnockdown = false; if (knockback != null) knockback.Stop(); }
    }
}