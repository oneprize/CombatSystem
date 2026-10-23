using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>플레이어가 받은 공격의 마지막 판정 결과(가드, 피해, 회피, 패리).</summary>
    public enum PlayerHitResult { None, Blocked, Damaged, Evaded, Parried }

    /// <summary>
    /// 플레이어 피격 처리. 회피/패리/가드 판정, 체력, 피격 리액션 애니메이션, 사망,
    /// 보스 공격에 의한 다운과 입력 방향에 따른 기상을 담당한다.
    /// </summary>
    public sealed class HealthModule : PlayerModule
    {
        #region 필드

        /// <summary>실제로 피해가 들어간 공격마다 발생한다(인자는 콤보 인덱스). 가드·회피·패리된 공격에서는 발생하지 않는다.</summary>
        public event System.Action<int> DamageReceived;

        private PlayerProfile.HealthSettings config;
        /// <summary>현재 재생 중인 리액션 애니메이터 상태 경로.</summary>
        private string stateName;
        /// <summary>리액션 상태에 실제로 진입했는지. 진입 전 종료 판정을 막는다.</summary>
        private bool entered;
        /// <summary>보스 공격으로 다운된 상태인지, 다운 후 기상 방향 입력을 기다리는 중인지.</summary>
        private bool bossDown, awaitingStandUp;

        #endregion

        #region 프로퍼티

        public string PlayerName => config.playerName;
        public float MaxHealth => Mathf.Max(1f, config.maxHealth);
        public float CurrentHealth { get; private set; }
        public PlayerHitResult LastResult { get; private set; }
        public bool IsReacting { get; private set; }
        public bool IsDead { get; private set; }
        public int LastComboIndex { get; private set; } = -1;
        public bool IsKnockedDown => bossDown;
        public bool IsAwaitingStandUp => awaitingStandUp;

        #endregion

        #region 피격 처리

        /// <summary>프로필 설정을 캐싱하고 체력을 최대치로 채운다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.health;
            CurrentHealth = MaxHealth;
        }

        /// <summary>투사체를 패리했을 때 진행 중인 공격을 끊고 투사체 패리 리액션을 재생한다.</summary>
        public void PlayProjectileParry()
        {
            if (IsDead || bossDown) return;
            Player.Combat.CancelForDamage();
            LastResult = PlayerHitResult.Parried;
            stateName = "Base Layer.ProjectileParry";
            IsReacting = true; entered = false;
            animator.CrossFadeInFixedTime(stateName, .05f, 0, 0);
        }

        /// <summary>다운 후 대기 중 이동 입력을 카메라 기준 월드 방향으로 바꿔 기상을 시도한다.</summary>
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

        /// <summary>
        /// 다운 상태에서 입력 방향으로 기상한다. 방향이 캐릭터 뒤쪽이면 뒤로, 아니면 앞으로 일어나는 애니메이션을 고른다.
        /// </summary>
        /// <returns>기상 대기 중이 아니거나 방향 입력이 너무 작으면 false.</returns>
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

        /// <summary>
        /// 적 공격 한 번을 판정한다. 회피 → 보스 패리 → 가드 순으로 막아 내고, 통과하면 피해를 적용한 뒤
        /// 사망 또는 피격 리액션/다운을 재생한다. 공격 중에는 피해만 받고 동작은 끊기지 않는다.
        /// </summary>
        /// <returns>공격이 처리(소모)되었으면 true. 콤보 인덱스가 범위를 벗어나거나 리액션 상태가 없으면 false.</returns>
        public bool ReceiveHit(int comboIndex, Transform attacker, bool knockDown = false, bool allowBossParry = true)
        {
            if (IsDead) return true;
            if (comboIndex < 0 || comboIndex > 3)
                return false;
            var combat = Player.Combat;
            // 회피로 흘린 접촉도 소모 처리해, 회피가 끝난 뒤 같은 휘두르기에 다시 맞지 않게 한다.
            if (Player.IsDodging)
            {
                LastResult = PlayerHitResult.Evaded;
                // 회피 직후 짧은 구간 안에 흘린 공격이면 저스트 회피 연출을 재생한다.
                if (Player.Dodge.TryConsumePerfectDodge()) Player.PlayPerfectDodgeFeedback();
                return true;
            }
            LastComboIndex = comboIndex;
            var boss = attacker != null ? attacker.GetComponentInParent<CombatSystem.Boss.BossCombat>() : null;
            var enemy = attacker != null ? attacker.GetComponentInParent<CombatSystem.Combat.EnemyCombat>() : null;
            if ((enemy != null || (allowBossParry && boss != null)) && combat.TryParryIncoming(attacker))
            {
                LastResult = PlayerHitResult.Parried;
                if (enemy != null) enemy.RegisterParry(Player.transform);
                else boss.RegisterParry();
                return true;
            }
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
            // 공격 중에는 강공격에 맞더라도 애니메이션 주도권을 공격이 유지한다.
            // 피해는 위에서 이미 적용했으므로, 무적이 아니라 경직 저항(슈퍼아머)이다.
            if (combat.IsAttacking && !combat.IsParryAttempt) return true;
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

        /// <summary>콤보 인덱스 3(강공격)이면 강공격 피해, 그 외에는 약공격 피해를 체력에서 뺀다.</summary>
        private void ApplyDamage(int comboIndex)
        {
            float damage = comboIndex == 3 ? config.strongAttackDamage : config.weakAttackDamage;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, MaxHealth);
        }

        #endregion

        #region 리액션 진행

        /// <summary>
        /// 피격 리액션 애니메이션 진행을 추적해 끝나면 대기 상태로 복귀시킨다. 다운 애니메이션이 끝나면 기상 입력 대기로 넘어간다.
        /// </summary>
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

        /// <summary>플레이어 비활성화 시 리액션·다운 상태를 해제한다(체력과 사망 상태는 유지).</summary>
        public override void OnDisable() { IsReacting = false; bossDown = false; awaitingStandUp = false; }

        #endregion
    }
}
