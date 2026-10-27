using UnityEngine;
using CombatSystem.Data.Enemy;
namespace CombatSystem.Combat
{
    /// <summary>
    /// 적의 체력과 모든 피격 리액션(일반 경직, 지상 콤보, 관통 돌진, 띄우기, 공중 콤보, 다운·기상)을 담당하는 상태 컴포넌트다.
    /// 자신이 IEnemyState를 구현해 피격 시 상태 머신을 이 상태로 전환하고, 리액션이 끝나면 관찰 상태로 돌려보낸다.
    /// </summary>
    [RequireComponent(typeof(EnemyStateMachine), typeof(Animator))]
    public class EnemyDamageReaction : MonoBehaviour, IEnemyState
    {
        #region 체력과 데미지

        [SerializeField] private EnemyDamageReactionConfig config;
        /// <summary>데미지를 받을 때마다 실제로 깎인 체력량과 함께 발생한다(훈련용 더미 등에서 구독).</summary>
        public event System.Action<float> DamageReceived;
        /// <summary>체력을 최대치로 되돌린다. 훈련 모드 리셋 등에서 사용한다.</summary>
        public void RestoreHealth() { CurrentHealth = MaxHealth; }
        /// <summary>파워 공격처럼 배율이 붙는 피격 동안에만 1이 아닌 값을 갖는 데미지 배율.</summary>
        private float damageScale = 1f;
        /// <summary>기본 피격 데미지에 현재 배율을 곱해 체력을 깎고 DamageReceived를 알린다.</summary>
        private void ApplyDamage()
        {
            float amount = config.damagePerHit * damageScale;
            float damage = Mathf.Min(CurrentHealth, amount);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            DamageReceived?.Invoke(damage);
        }
        /// <summary>
        /// 플레이어 파워 공격 피격. 일반 피격 데미지의 <paramref name="damageMultiplier"/>배를 주며,
        /// 돌진 베기는 지상 콤보 피격처럼, 마지막 내려찍기는 다운으로 반응한다.
        /// </summary>
        public void ReceivePowerHit(Transform attacker, float damageMultiplier, bool slam)
        {
            damageScale = Mathf.Max(0f, damageMultiplier);
            try
            {
                if (slam) ReceiveSkillHit(attacker);
                else ReceiveGroundComboHit(attacker, 1, false);
            }
            finally { damageScale = 1f; }
        }
        public float MaxHealth => Mathf.Max(1f, config.maxHealth);
        public float CurrentHealth { get; private set; }

        #endregion

        #region 참조와 끌어모으기

        private EnemyStateMachine machine;
        private EnemyCombat combat;
        private Animator animator;
        /// <summary>현재 재생 중(또는 재생할) 피격 리액션 애니메이터 상태의 전체 경로.</summary>
        private string reactionState = "Base Layer.Hit";
        /// <summary>공중 리액션 진행 중 여부. 이 동안에는 지상 피격 리액션으로 덮어쓰지 않는다.</summary>
        private bool aerialReaction;
        /// <summary>BeginGather로 지정 지점까지 끌려가는 중인지 여부.</summary>
        public bool IsBeingGathered { get; private set; }
        private Vector3 gatherTarget;
        private float gatherRemaining;
        /// <summary>
        /// 적을 공격자 쪽을 바라보게 한 뒤 지정 시간 동안 목적지로 끌어모은다. 물리 다운 중이면 끌어모을 수 없다.
        /// </summary>
        /// <returns>끌어모으기를 시작했으면 true, 다운 상태라 거부되었으면 false.</returns>
        public bool BeginGather(Vector3 destination, Transform attacker, float duration)
        {
            if (physicalKnockdown) return false;
            if (ReferenceEquals(machine.CurrentState, this) && !physicalKnockdown) ResetReaction();
            gatherTarget = destination; gatherRemaining = Mathf.Max(.05f, duration);
            reactionState = "Base Layer.Hit";
            Vector3 face = attacker.position - transform.position; face.y=0;
            if (face.sqrMagnitude>.001f) transform.rotation=Quaternion.LookRotation(face);
            if (ReferenceEquals(machine.CurrentState, this)) Enter(); else machine.ChangeState(this);
            IsBeingGathered = true;
            return true;
        }

        #endregion

        #region 리액션 상태와 넉백 보조

        /// <summary>관통 돌진 피격의 3단(A→B→C) 리액션 진행 여부와 단계·경과 시간.</summary>
        private bool piercingReaction;
        private int piercingStep;
        private float piercingElapsed;
        /// <summary>지상 콤보 피격 리액션 중인지 여부. EnemyRootMotion이 루트 모션을 밀어내기 방향으로만 반영할 때 참조한다.</summary>
        public bool IsGroundComboReaction { get; private set; }
        private Vector3 groundPushDirection;
        /// <summary>지상 콤보 3타 피격 시 목표 간격까지 추가로 밀어낼 거리와 그 진행 시간.</summary>
        private float thirdHitPushDistance, thirdHitPushElapsed;
        /// <summary>
        /// 지상 콤보 3타 리액션 동안 감속 곡선으로 적을 추가로 밀어내, 다음 공격과의 간격을 일정하게 맞춘다.
        /// 리액션이 바뀌면 남은 밀어내기를 취소한다.
        /// </summary>
        private void TickThirdHitPush(float dt)
        {
            if (thirdHitPushDistance <= 0f) return;
            if (!IsGroundComboReaction || reactionState != "Base Layer.GroundHitC")
            { thirdHitPushDistance = 0f; return; }
            float duration = Mathf.Max(.01f, config.thirdHitPushDuration);
            float before = Mathf.Clamp01(thirdHitPushElapsed / duration);
            thirdHitPushElapsed += dt;
            float after = Mathf.Clamp01(thirdHitPushElapsed / duration);
            float movement = thirdHitPushDistance * ((2f * after - after * after) - (2f * before - before * before));
            knockback.Push(groundPushDirection * movement);
            if (after >= 1f) thirdHitPushDistance = 0f;
        }
        /// <summary>
        /// 지상 피격 애니메이션의 루트 모션 중 밀려나는 방향 성분만 골라 배율을 적용해 이동시킨다.
        /// 피격 클립이 적을 공격자 쪽으로 끌어당기지 않게 하기 위함이다.
        /// </summary>
        public void ApplyGroundReactionMotion(Vector3 delta)
        {
            float backward = Mathf.Max(0, Vector3.Dot(delta, groundPushDirection));
            knockback.ApplyGroundMotion(groundPushDirection * backward * config.groundReactionMotionScale);
        }
        private EnemyKnockbackMotion knockback;
        /// <summary>넉백 물리(EnemyKnockbackMotion)로 날아가거나 쓰러져 있는 중인지 여부.</summary>
        private bool physicalKnockdown;
        private float downElapsed;
        /// <summary>공중 피니시 후 착지 시 바운스 예약 여부와, 이번 공중 체류에서 바운스를 이미 썼는지 여부(1회 제한).</summary>
        private bool slamPending, bounceUsed;
        /// <summary>바운스 후 추가타를 이어 받을 수 있는 남은 시간.</summary>
        private float bounceWindowRemaining;
        public bool CanFollowUpBounce => bounceWindowRemaining > 0f;
        public bool BounceUsed => bounceUsed;

        /// <summary>바운스 추가타 구간에 새 피격이 들어오면 그 구간을 소모하고 바운스 관련 예약을 정리한다.</summary>
        private void ConsumeBounceFollowUp()
        {
            if (!CanFollowUpBounce) return;
            bounceWindowRemaining = 0f;
            slamPending = false;
            aerialReaction = false;
            // 낮은 바운스가 실제로 바닥에 닿을 때까지 중력은 계속 적용한다.
        }
        public bool HasPhysicalKnockdown => physicalKnockdown;
        public bool IsKnockdownGrounded => knockback != null && knockback.Grounded;

        #endregion

        #region Unity 생명주기

        /// <summary>config가 비어 있으면 기본값으로 대체하고 체력과 컴포넌트 참조를 초기화한다. 넉백 컴포넌트가 없으면 추가한다.</summary>
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

        #endregion

        #region 피격 수신 API

        /// <summary>일반 피격. 데미지를 주고 기본 경직 리액션을 재생하되, 공격 중이거나 공중 리액션 중이면 데미지만 받는다.</summary>
        public void ReceiveHit()
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ApplyDamage();
            // 일반 피격으로는 공격 동작이 끊기지 않는다.
            if (aerialReaction || machine.CurrentState == machine.AttackState) return;
            reactionState = "Base Layer.Hit";
            if (ReferenceEquals(machine.CurrentState, this)) Enter();
            else machine.ChangeState(this);
        }
        /// <summary>관통 돌진 피격. 돌진 방향을 마주 보게 돌린 뒤 3단 관통 리액션(PiercingHitA→B→C)을 시작한다.</summary>
        public void ReceivePiercingHit(Transform attacker, Vector3 dashDirection)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ConsumeBounceFollowUp();
            ApplyDamage();
            if (aerialReaction) return;
            if (ReferenceEquals(machine.CurrentState, this) && !physicalKnockdown) ResetReaction();
            IsGroundComboReaction = false;
            piercingReaction = true; piercingStep = 0; piercingElapsed = 0;
            dashDirection.y = 0;
            if (dashDirection.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-dashDirection);
            reactionState = "Base Layer.PiercingHitA";
            if (ReferenceEquals(machine.CurrentState, this)) Enter(); else machine.ChangeState(this);
        }

        /// <summary>
        /// 플레이어 지상 콤보 피격. 공격자 반대 방향으로 밀려나는 콤보 단계별 리액션(GroundHitA/B/C)을 재생하고,
        /// 3타에서는 목표 간격까지 추가로 밀어낸다. 공격 중이거나 공중 리액션 중이면 데미지만 받는다.
        /// </summary>
        public void ReceiveGroundComboHit(Transform attacker, int comboIndex, bool finisher)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ConsumeBounceFollowUp();
            // 공격 중 아머와 이미 진행 중인 공중/다운 리액션은 그대로 유지한다.
            if (aerialReaction || machine.CurrentState == machine.AttackState)
            { ApplyDamage(); return; }
            // 지상 콤보 피니시도 지상 밀어내기 리액션을 쓰며, 스킬의 띄우기 리액션과는 분리되어 있다.
            ApplyDamage();
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            groundPushDirection = away.normalized;
            thirdHitPushElapsed = 0f;
            thirdHitPushDistance = comboIndex == 2 && attacker != null
                ? Mathf.Clamp(config.thirdHitTargetDistance - away.magnitude, 0f, config.thirdHitMaxExtraPush) : 0f;
            transform.rotation = Quaternion.LookRotation(-groundPushDirection);
            IsGroundComboReaction = true;
            reactionState = comboIndex == 0 ? "Base Layer.GroundHitA" : comboIndex == 2 ? "Base Layer.GroundHitC" : "Base Layer.GroundHitB";
            if (ReferenceEquals(machine.CurrentState, this)) Enter();
            else machine.ChangeState(this);
        }

        /// <summary>스킬 피격. 공격자 반대 방향으로 날려 보내 넉백 물리로 다운시킨다.</summary>
        public void ReceiveSkillHit(Transform attacker = null)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ConsumeBounceFollowUp();
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
            if (ReferenceEquals(machine.CurrentState, this)) Enter();
            else machine.ChangeState(this);
        }

        /// <summary>올려베기 피격. 적을 위로 띄워 공중 콤보를 이어 갈 수 있는 상태로 만든다.</summary>
        public void ReceiveUpperHit(Transform attacker)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ConsumeBounceFollowUp();
            ApplyDamage();
            if (aerialReaction) return;
            Vector3 away = attacker != null ? transform.position - attacker.position : -transform.forward;
            away.y = 0;
            if (away.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(-away);
            aerialReaction = physicalKnockdown = true;
            reactionState = "Base Layer.UpperHit";
            downElapsed = 0;
            knockback.LaunchUpper(away, config.upperLiftSpeed, config.upperPushSpeed);
            if (ReferenceEquals(machine.CurrentState, this)) Enter();
            else machine.ChangeState(this);
        }

        /// <summary>띄워진 채 아직 착지하지 않아 공중 콤보 대상이 될 수 있는 상태인지 여부.</summary>
        public bool IsAirborneForCombo => physicalKnockdown && knockback != null && knockback.Active && !knockback.Grounded;
        /// <summary>띄워진 적을 현재 높이에 붙잡아(공중 콤보 피격과 같은 체공) 플레이어가 따라붙을 수 있게 한다.</summary>
        public void HoldInAir() { if (IsAirborneForCombo) knockback.AirComboImpact(false); }
        /// <summary>공중 콤보로 붙잡힌 적을 공격자와 함께 밀어 옮긴다. 실제로 적용된 이동량을 돌려준다.</summary>
        public Vector3 PushAirborne(Vector3 offset) => IsAirborneForCombo ? knockback.Push(offset) : Vector3.zero;
        /// <summary>
        /// 공중 콤보 피격. 공중에 떠 있는 적에게만 적용되며 체공을 갱신하고, 피니시면 착지 시 바운스를 예약한다(1회 한정).
        /// </summary>
        public void ReceiveAirComboHit(Transform attacker, int comboIndex, bool finisher)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            // 공중 공격은 지상의 적을 띄우거나 기존 다운 상태를 무시하지 않는다.
            if (!IsAirborneForCombo) return;
            ApplyDamage();
            var toward = attacker.position - transform.position;
            toward.y = 0;
            if (toward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(toward);
            reactionState = "Base Layer.AirComboHitB";
            if (finisher) slamPending = !bounceUsed;
            knockback.AirComboImpact(finisher);
            animator.CrossFadeInFixedTime(reactionState, .05f, 0, 0);
        }

        /// <summary>
        /// 공중 공격(Q) 피격. 타격 순서에 따라 AerialHit/AerialHit2 리액션을 재생하며,
        /// 두 번째 타격은 바운스를 아직 쓰지 않았으면 다운 후 착지 바운스로 이어진다.
        /// </summary>
        public void ReceiveAerialHit(Transform attacker, int strikeIndex = 0)
        {
            if (combat.IsGroggy) { ApplyDamage(); return; }
            ApplyDamage();
            // Q의 두 번째 타격은 공중 피니시와 1회 바운스 허용치를 공유한다.
            bool bounceOnLanding = strikeIndex == 1 && !bounceUsed;
            if (physicalKnockdown && !bounceOnLanding) return;
            Vector3 direction = attacker.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
            aerialReaction = true;
            IsGroundComboReaction = false;
            piercingReaction = false;
            IsBeingGathered = false;
            reactionState = strikeIndex == 0 ? "Base Layer.AerialHit" : "Base Layer.AerialHit2";
            if (bounceOnLanding)
            {
                physicalKnockdown = true;
                slamPending = true;
                downElapsed = 0f;
                knockback.LaunchUpper(Vector3.zero, 0f, 0f);
                knockback.AirComboImpact(true);
            }
            if (ReferenceEquals(machine.CurrentState, this)) Enter();
            else machine.ChangeState(this);
        }

        #endregion

        #region 상태 머신 (IEnemyState)

        /// <summary>현재 지정된 피격 리액션 애니메이션을 처음부터 크로스페이드로 재생한다.</summary>
        public void Enter()
        {
            animator.CrossFadeInFixedTime(reactionState, aerialReaction ? config.aerialHitBlendTime : 0.05f, 0, 0f);
        }
        /// <summary>
        /// 리액션 종류별 진행을 처리한다. 끌어모으기 이동, 관통 3단 리액션 전환, 넉백 물리와 착지 바운스,
        /// 다운 유지 후 기상을 차례로 다루고, 일반 리액션 애니메이션이 끝나면 관찰 상태로 복귀시킨다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            TickThirdHitPush(deltaTime);
            // 추가타 허용 시간은 시각적 리액션을 공중 자세로 붙잡아 두지 않도록 독립적으로 감소시킨다.
            bounceWindowRemaining = Mathf.Max(0f, bounceWindowRemaining - deltaTime);
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
                if (slamPending)
                {
                    slamPending = false;
                    bounceUsed = true;
                    bounceWindowRemaining = config.slamFollowUpWindow;
                    knockback.Bounce(config.slamBounceHeight);
                    reactionState = "Base Layer.AirComboHitB";
                    animator.CrossFadeInFixedTime(reactionState, .05f, 0, 0f);
                    return;
                }
                if (!aerialReaction)
                {
                    physicalKnockdown = false;
                    knockback.Stop();
                }
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
        /// <summary>리액션 상태를 벗어날 때 바운스 기회와 모든 리액션 플래그·넉백을 초기화한다.</summary>
        public void Exit() { bounceUsed = slamPending = false; bounceWindowRemaining = 0f; ResetReaction(); }
        /// <summary>진행 중인 리액션 플래그와 밀어내기·넉백 이동을 모두 정리한다. 새 리액션으로 덮어쓸 때도 사용한다.</summary>
        private void ResetReaction() { thirdHitPushDistance = thirdHitPushElapsed = 0f; IsBeingGathered = false; piercingReaction = false; IsGroundComboReaction = false; aerialReaction = false; physicalKnockdown = false; if (knockback != null) knockback.Stop(); }

        #endregion
    }
}