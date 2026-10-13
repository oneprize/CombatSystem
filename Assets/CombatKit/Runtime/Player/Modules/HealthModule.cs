using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    public enum PlayerHitResult { None, Blocked, Damaged, Evaded, Parried }

    /// <summary>
    /// Incoming hits: dodge/guard resolution, HP, hit-reaction animations and the
    /// boss knockdown with directional stand-up.
    /// </summary>
    public sealed class HealthModule : PlayerModule
    {
        /// <summary>Raised for every hit that actually lands. Blocked hits never raise it.</summary>
        public event System.Action<int> DamageReceived;

        private PlayerProfile.HealthSettings config;
        private string stateName;
        private bool entered;
        private bool bossDown, awaitingStandUp;

        public string PlayerName => config.playerName;
        public float MaxHealth => Mathf.Max(1f, config.maxHealth);
        public float CurrentHealth { get; private set; }
        public PlayerHitResult LastResult { get; private set; }
        public bool IsReacting { get; private set; }
        public bool IsDead { get; private set; }
        public int LastComboIndex { get; private set; } = -1;
        public bool IsKnockedDown => bossDown;
        public bool IsAwaitingStandUp => awaitingStandUp;

        protected override void OnInitialize()
        {
            config = Player.Profile.health;
            CurrentHealth = MaxHealth;
        }

        public void PlayProjectileParry()
        {
            if (IsDead || bossDown) return;
            Player.Combat.CancelForDamage();
            LastResult = PlayerHitResult.Parried;
            stateName = "Base Layer.ProjectileParry";
            IsReacting = true; entered = false;
            animator.CrossFadeInFixedTime(stateName, .05f, 0, 0);
        }

        private void ReadStandUpInput()
        {
            Vector2 move = Player.Controls.Move;
            Vector3 input = new Vector3(move.x, 0, move.y);
            if (input.sqrMagnitude < .01f) return;
            Transform basis = Player.CameraTransform;
            Vector3 forward = basis.forward; forward.y = 0; forward.Normalize();
            Vector3 right = basis.right; right.y = 0; right.Normalize();
            TryStandUp(forward * input.z + right * input.x);
        }

        public bool TryStandUp(Vector3 worldDirection)
        {
            if (!awaitingStandUp || worldDirection.sqrMagnitude < .01f) return false;
            worldDirection.y = 0; worldDirection.Normalize();
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            bool backward = local.z < 0 && Mathf.Abs(local.z) >= Mathf.Abs(local.x);
            transform.rotation = Quaternion.LookRotation(backward ? -worldDirection : worldDirection);
            stateName = backward ? "Base Layer.BossStandUpBack" : "Base Layer.BossStandUpFront";
            awaitingStandUp = false; entered = false;
            animator.CrossFadeInFixedTime(stateName, .1f, 0, 0);
            return true;
        }

        public bool ReceiveHit(int comboIndex, Transform attacker, bool knockDown = false, bool allowBossParry = true)
        {
            if (IsDead) return true;
            if (comboIndex < 0 || comboIndex > 3)
                return false;
            var combat = Player.Combat;
            // A dodged contact is consumed, so the same swing cannot hit again after recovery.
            if (Player.IsDodging)
            {
                LastResult = PlayerHitResult.Evaded;
                return true;
            }
            LastComboIndex = comboIndex;
            var boss = attacker != null ? attacker.GetComponentInParent<CombatSystem.Boss.BossCombat>() : null;
            if (allowBossParry && boss != null && combat.TryConsumeBossParry())
            {
                LastResult = PlayerHitResult.Parried;
                boss.RegisterParry();
                Player.PlayParryFeedback();
                Debug.Log("[Parry Success] Boss attack parried", Player);
                return true;
            }
            if (combat.IsGuarding)
            {
                LastResult = PlayerHitResult.Blocked;
                combat.PlayGuardAccept();
                return true;
            }
            LastResult = PlayerHitResult.Damaged;
            ApplyDamage(comboIndex);
            DamageReceived?.Invoke(comboIndex);
            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                IsReacting = false;
                bossDown = awaitingStandUp = false;
                Player.EnterDeath();
                animator.CrossFadeInFixedTime("Base Layer.Death", .08f, 0, 0f);
                return true;
            }
            if (bossDown) return true;
            // Attacks keep animation ownership through incoming hits, including strong hits.
            // Damage is already applied above; this is interruption resistance, not invulnerability.
            if (combat.IsAttacking) return true;
            bossDown = knockDown; awaitingStandUp = false;
            stateName = knockDown ? "Base Layer.BossKnockDown" : "Base Layer.Damage" + (comboIndex + 1);
            if (!animator.HasState(0, Animator.StringToHash(stateName))) return false;
            combat.CancelForDamage();
            Vector3 towardAttacker = attacker.position - transform.position;
            towardAttacker.y = 0f;
            if (towardAttacker.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(towardAttacker);
            LastComboIndex = comboIndex;
            IsReacting = true;
            entered = false;
            animator.CrossFadeInFixedTime(stateName, 0.05f, 0, 0f);
            return true;
        }

        private void ApplyDamage(int comboIndex)
        {
            float damage = comboIndex == 3 ? config.strongAttackDamage : config.weakAttackDamage;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, MaxHealth);
        }

        public void Tick()
        {
            if (IsDead || !IsReacting) return;
            if (awaitingStandUp) { ReadStandUpInput(); return; }
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
            bool transitioningToHit = next.IsName(stateName);
            if (transitioningToHit) entered = true;
            if (current.IsName(stateName) && !transitioningToHit)
            {
                entered = true;
                if (current.normalizedTime >= 1f)
                {
                    if (bossDown && stateName == "Base Layer.BossKnockDown") { awaitingStandUp = true; return; }
                    bossDown = false;
                    IsReacting = false;
                    animator.CrossFadeInFixedTime("Base Layer.Idle", 0.1f);
                }
            }
            else if (entered && !transitioningToHit)
            {
                IsReacting = false;
            }
        }

        public override void OnDisable() { IsReacting = false; bossDown = false; awaitingStandUp = false; }
    }
}
