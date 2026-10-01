using UnityEngine;
using CombatSystem.Data.Enemy;
namespace CombatSystem.Combat
{
    [RequireComponent(typeof(EnemyStateMachine), typeof(Animator))]
    public class EnemyDamageReaction : MonoBehaviour, IEnemyState
    {
        [SerializeField] private EnemyDamageReactionConfig config;
        public event System.Action<float> DamageReceived;
        public void RestoreHealth() { CurrentHealth = MaxHealth; }
        private void ApplyDamage()
        {
            float damage = Mathf.Min(CurrentHealth, config.damagePerHit);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - config.damagePerHit);
            DamageReceived?.Invoke(damage);
        }
        public float MaxHealth => Mathf.Max(1f, config.maxHealth);
        public float CurrentHealth { get; private set; }
        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private Animator animator;
        private string reactionState = "Base Layer.Hit";
        private bool aerialReaction;
        public bool IsBeingGathered { get; private set; }
        private Vector3 gatherTarget;
        private float gatherRemaining;
        public bool BeginGather(Vector3 destination, Transform attacker, float duration)
        {
            if (physicalKnockdown) return false;
            if (machine.CurrentState == this) Exit();
            gatherTarget = destination; gatherRemaining = Mathf.Max(.05f, duration);
            reactionState = "Base Layer.Hit";
            Vector3 face = attacker.position - transform.position; face.y=0;
            if (face.sqrMagnitude>.001f) transform.rotation=Quaternion.LookRotation(face);
            if (machine.CurrentState == this) Enter(); else machine.ChangeState(this);
            IsBeingGathered = true;
            return true;
        }
        private bool piercingReaction;
        private int piercingStep;
        private float piercingElapsed;
        public bool IsGroundComboReaction { get; private set; }
        private Vector3 groundPushDirection;
        public void ApplyGroundReactionMotion(Vector3 delta)
        {
            float backward = Mathf.Max(0, Vector3.Dot(delta, groundPushDirection));
            knockback.ApplyGroundMotion(groundPushDirection * backward * config.groundReactionMotionScale);
        }
        private EnemyKnockbackMotion knockback;
        private bool physicalKnockdown;
        private float downElapsed;
        public bool HasPhysicalKnockdown => physicalKnockdown;
        public bool IsKnockdownGrounded => knockback != null && knockback.Grounded;
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnemyDamageReactionConfig>();
                Debug.LogWarning("EnemyDamageReaction: no EnemyDamageReactionConfig assigned, using built-in defaults.", this);
            }

            CurrentHealth = MaxHealth;
            machine = GetComponent<EnemyStateMachine>();
            combat = GetComponent<EnemyCombat>();
            animator = GetComponent<Animator>();
            knockback = GetComponent<EnemyKnockbackMotion>();
            if (knockback == null) knockback = gameObject.AddComponent<EnemyKnockbackMotion>();
        }
        public void ReceiveHit()
        {
            ApplyDamage();
            // Attacks and an existing groggy are not interrupted by ordinary damage.
            if (aerialReaction || machine.CurrentState == machine.AttackState ||
                machine.CurrentState == machine.GroggyState) return;
            reactionState = "Base Layer.Hit";
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }
        public void ReceivePiercingHit(Transform attacker, Vector3 dashDirection)
        {
            ApplyDamage();
            if (aerialReaction) return;
            if (machine.CurrentState == this) Exit();
            IsGroundComboReaction = false;
            piercingReaction = true; piercingStep = 0; piercingElapsed = 0;
            dashDirection.y = 0;
            if (dashDirection.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-dashDirection);
            reactionState = "Base Layer.PiercingHitA";
            if (machine.CurrentState == this) Enter(); else machine.ChangeState(this);
        }

        public void ReceiveGroundComboHit(Transform attacker, int comboIndex)
        {
            // Keep attack armour and existing airborne/down reactions.
            if (aerialReaction || machine.CurrentState == machine.AttackState || machine.CurrentState == machine.GroggyState)
            { ApplyDamage(); return; }
            if (comboIndex >= 4) { ReceiveSkillHit(attacker); return; }
            ApplyDamage();
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            groundPushDirection = away.normalized;
            transform.rotation = Quaternion.LookRotation(-groundPushDirection);
            IsGroundComboReaction = true;
            reactionState = comboIndex == 0 ? "Base Layer.GroundHitA" : comboIndex == 2 ? "Base Layer.GroundHitC" : "Base Layer.GroundHitB";
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        public void ReceiveSkillHit(Transform attacker = null)
        {
            ApplyDamage();
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

        public void ReceiveUpperHit(Transform attacker)
        {
            ApplyDamage();
            if (aerialReaction) return;
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-away);
            aerialReaction = physicalKnockdown = true;
            reactionState = "Base Layer.UpperHit";
            downElapsed = 0;
            knockback.LaunchUpper(away, config.upperLiftSpeed, config.upperPushSpeed);
            if (machine.CurrentState == this) Enter();
            else machine.ChangeState(this);
        }

        public bool IsAirborneForCombo => physicalKnockdown && knockback != null && knockback.Active && !knockback.Grounded;
        public void ReceiveAirComboHit(Transform attacker, int comboIndex)
        {
            // Air attacks do not launch a grounded enemy or bypass the existing down state.
            if (!IsAirborneForCombo) return;
            ApplyDamage();
            var toward = attacker.position - transform.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toward);
            reactionState = "Base Layer.AirComboHitB";
            knockback.AirComboImpact(comboIndex == 5);
            animator.CrossFadeInFixedTime(reactionState, .05f, 0, 0);
        }

        public void ReceiveAerialHit(Transform attacker, int strikeIndex = 0)
        {
            if (physicalKnockdown) { ApplyDamage(); return; }
            ApplyDamage();
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
            animator.CrossFadeInFixedTime(reactionState, aerialReaction ? config.aerialHitBlendTime : 0.05f, 0, 0f);
        }
        public void Tick(float deltaTime)
        {
            if (IsBeingGathered)
            {
                Vector3 move=(gatherTarget-transform.position)*Mathf.Clamp01(deltaTime/Mathf.Max(.001f,gatherRemaining));
                knockback.ApplyGroundMotion(move);
                gatherRemaining-=deltaTime;
                if(gatherRemaining<=0) IsBeingGathered=false;
                return;
            }
            if (piercingReaction && piercingStep < 2)
            {
                piercingElapsed += deltaTime;
                if (piercingElapsed >= config.piercingHitInterval)
                {
                    piercingElapsed -= config.piercingHitInterval; piercingStep++;
                    reactionState = piercingStep == 1 ? "Base Layer.PiercingHitB" : "Base Layer.PiercingHitC";
                    animator.CrossFadeInFixedTime(reactionState, .035f, 0, 0);
                }
                return;
            }
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
                    if (downElapsed >= config.downDuration)
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
        public void Exit() { IsBeingGathered = false; piercingReaction = false; IsGroundComboReaction = false; aerialReaction = false; physicalKnockdown = false; if (knockback != null) knockback.Stop(); }
    }
}