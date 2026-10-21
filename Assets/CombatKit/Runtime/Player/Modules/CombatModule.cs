using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// 플레이어 전투 입력과 공격 진행을 담당한다. 지상 콤보, 스킬(G/A/B/C), 파워 공격(차지),
    /// UpperAttack·공중 콤보·추격 공격 연계, 관통 돌진 후 E 후속 공격, 보스 패리 입력과 가드를 처리한다.
    /// </summary>
    public sealed class CombatModule : PlayerModule
    {
        #region 필드

        private const int GroundComboCount = 4;
        private const int AirComboCount = 4;
        // attackIndex 값별 공격 종류. 0~3은 지상 콤보, AirStartIndex부터 4개는 공중 콤보, 21은 공중 대시 공격이다.
        private const int SkillGIndex = 5, SkillAIndex = 6, UpperIndex = 7, AirStartIndex = 8, SkillBIndex = 14, SkillCIndex = 16, ChaseIndex = 17, PowerChargeIndex = 18, PowerReleaseIndex = 19, EFollowUpIndex = 20;

        private PlayerProfile.CombatSettings config;
        /// <summary>현재 공격 종류/콤보 단계를 나타내는 인덱스. 재생할 애니메이터 상태명도 이 값으로 정해진다.</summary>
        private int attackIndex;
        /// <summary>다음 콤보 단계로 이어갈 공격 입력이 예약되었는지.</summary>
        private bool queuedAttack;
        /// <summary>관통 돌진이 적을 맞힌 뒤 E 후속 공격 입력을 받을 수 있는 마감 시각.</summary>
        private float eFollowUpUntil = float.NegativeInfinity;
        private bool eFollowUpQueued;
        private Transform eFollowUpTarget;
        public bool IsUsingEFollowUp => IsAttacking && attackIndex == EFollowUpIndex;

        #endregion

        #region E 후속 공격

        /// <summary>관통 돌진이 적을 맞혔을 때 호출되어, 그 적을 대상으로 E 후속 공격 입력 창을 연다.</summary>
        public void RegisterPiercingHit(Transform target)
        {
            if (!IsUsingPiercingSkill) return;
            eFollowUpTarget = target;
            eFollowUpUntil = Time.time + config.eFollowUpInputWindow;
        }

        /// <summary>E 후속 공격 예약과 입력 창, 대상을 모두 비운다.</summary>
        private void ClearEFollowUp()
        {
            eFollowUpQueued = false;
            eFollowUpUntil = float.NegativeInfinity;
            eFollowUpTarget = null;
        }

        /// <summary>관통 돌진으로 맞힌 적을 향해 돌아서며 E 후속 공격(EFollowUp)을 시작한다. 다른 예약 입력은 모두 버린다.</summary>
        private void BeginEFollowUp()
        {
            Transform target = eFollowUpTarget;
            ClearEFollowUp();
            ClearGroundAttackInput();
            queuedAttack = false;
            bufferedSkillIndex = -1;
            skillAttackBufferedUntil = float.NegativeInfinity;
            attackIndex = EFollowUpIndex;
            IsAttacking = true;
            AttackSequence++;
            FaceNearbyEnemy();
            if (target != null)
            {
                Vector3 direction = target.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction);
                AttackTarget = target.GetComponent<CombatSystem.Combat.EnemyDamageReaction>();
            }
            animator.CrossFadeInFixedTime("Base Layer.EFollowUp", .08f, 0, 0f);
        }

        #endregion

        #region 입력 버퍼·납도 상태 필드

        /// <summary>지상 콤보 도중 입력된 스킬 인덱스. 전환 시점까지 보관되며 없으면 -1.</summary>
        private int bufferedSkillIndex = -1;
        private float bufferedSkillUntil = float.NegativeInfinity;
        /// <summary>스킬 사용 중 입력된 공격(스킬 후 콤보 연계)을 유효하게 보는 마감 시각.</summary>
        private float skillAttackBufferedUntil = float.NegativeInfinity;
        /// <summary>스킬 C로 띄운 적. 추격 공격의 대상이 된다.</summary>
        private CombatSystem.Combat.EnemyDamageReaction launchedEnemy;
        private PlayerProfile.PowerAttackSettings powerConfig;
        private float attackPressedAt = float.NegativeInfinity;
        /// <summary>
        /// 지상 공격 입력 상태. pending: 눌림이 대기 중, released: 이미 뗐음(짧게 누름 확정),
        /// skillC: 누를 때 스킬 C 보조 키가 함께 눌려 있었음.
        /// </summary>
        private bool groundInputPending, groundInputReleased, groundInputSkillC;
        private float groundInputReleasedAt;
        /// <summary>같은 프레임에 입력을 두 번 수집하지 않도록 마지막 수집 프레임을 기록한다.</summary>
        private int capturedInputFrame = -1;

        // 납도(Sheathed) 공격의 attackIndex: 준비 자세와 2단 공격.
        private const int SheathedReadyIndex = 22, SheathedFirstIndex = 23;
        private int sheathedStep;
        private bool sheathedQueued;

        #endregion

        #region 프로퍼티

        public bool IsSheathedReady => IsAttacking && attackIndex == SheathedReadyIndex;
        public bool IsSheathedCombo => IsAttacking && attackIndex >= SheathedFirstIndex && attackIndex < SheathedFirstIndex + 2;
        public int SheathedComboStep => IsSheathedCombo ? attackIndex - SheathedFirstIndex : -1;
        public bool IsGuarding { get; private set; }
        public bool IsAttacking { get; private set; }
        public int AttackComboIndex => attackIndex;
        /// <summary>새 공격(또는 콤보 단계)이 시작될 때마다 증가하는 번호. 히트박스·돌진 등이 공격 단위로 타격 기록을 초기화하는 데 쓴다.</summary>
        public int AttackSequence { get; private set; }
        /// <summary>현재 공격이 바라보도록 돌아선 적. 범위 안에 적이 없었으면 null.</summary>
        public CombatSystem.Combat.EnemyDamageReaction AttackTarget { get; private set; }
        public float AirAttackContactDistance => config.airAttackContactDistance;

        public bool IsUsingGroundCombo => IsAttacking && attackIndex >= 0 && attackIndex < GroundComboCount;
        public bool IsUsingAirCombo => IsAttacking && attackIndex >= AirStartIndex && attackIndex < AirStartIndex + AirComboCount;
        public int AirComboIndex => IsUsingAirCombo ? attackIndex - AirStartIndex : -1;
        /// <summary>지상 콤보의 마지막 단계(적을 띄운다).</summary>
        public bool IsGroundComboFinisher => IsUsingGroundCombo && attackIndex == GroundComboCount - 1;
        /// <summary>공중 콤보의 마지막 단계(적을 내리찍는다).</summary>
        public bool IsAirComboFinisher => IsUsingAirCombo && AirComboIndex == AirComboCount - 1;
        public bool IsUsingSkill => IsAttacking && attackIndex == SkillGIndex;
        public bool IsUsingSkillA => IsAttacking && attackIndex == SkillAIndex;
        public bool IsUsingSkillB => IsAttacking && attackIndex == SkillBIndex;
        public bool IsUsingSkillC => IsAttacking && attackIndex == SkillCIndex;
        public bool IsUsingPiercingSkill => IsUsingSkillA || IsUsingSkillB;
        public bool IsUsingUpperAttack => IsAttacking && attackIndex == UpperIndex;
        /// <summary>스킬 C로 띄운 적을 향해 뛰어오르는 추격 공격 중인지.</summary>
        public bool IsUsingChaseAttack => IsAttacking && attackIndex == ChaseIndex;
        /// <summary>공격 버튼을 누르고 있는 파워 공격 차지 중(PowerAttackStart → PowerAttackLoop)인지.</summary>
        public bool IsChargingPower => IsAttacking && attackIndex == PowerChargeIndex;
        public bool IsReleasingPower => IsAttacking && attackIndex == PowerReleaseIndex;
        public bool IsUsingPowerAttack => IsChargingPower || IsReleasingPower;
        /// <summary>버튼을 누른 총 시간. 최대 피해에 도달하는 시간에서 상한이 걸린다.</summary>
        public float PowerChargeSeconds { get; private set; }
        /// <summary>차지 진행도(0~1). 홀드 판정 시간부터 최대 차지 시간 사이를 정규화한 값.</summary>
        public float PowerChargeNormalized => Mathf.InverseLerp(powerConfig.holdThreshold,
            Mathf.Max(powerConfig.holdThreshold + .01f, powerConfig.maxChargeDuration), PowerChargeSeconds);
        /// <summary>차지가 일정 시간 이상 진행되어 피격 경직에 저항하는 상태인지.</summary>
        public bool IsPowerArmored => IsChargingPower &&
            PowerChargeSeconds >= powerConfig.holdThreshold + powerConfig.armorAfterSeconds;
        /// <summary>차지 진행도에 따라 최소~최대 배율 사이로 보간한 파워 공격 피해 배율.</summary>
        public float PowerDamageMultiplier => Mathf.Lerp(powerConfig.minDamageMultiplier,
            Mathf.Max(powerConfig.minDamageMultiplier, powerConfig.maxDamageMultiplier), PowerChargeNormalized);
        public string CurrentAttackStateName => AttackStateName;
        /// <summary>attackIndex에 대응하는 애니메이터 상태 이름("Base Layer." 접두사 제외).</summary>
        private string AttackStateName => IsSheathedReady ? "SheathedReady" : IsSheathedCombo ? "SheathedAttack" + (SheathedComboStep + 1) : attackIndex == 21 ? "AirDashAttack" : attackIndex == EFollowUpIndex ? "EFollowUp" : attackIndex == PowerReleaseIndex ? "PowerAttackEnd" : attackIndex == PowerChargeIndex ? "PowerAttackLoop" : attackIndex == ChaseIndex ? "ChaseUpAttack" : attackIndex == SkillCIndex ? "Skill_C" : attackIndex == SkillBIndex ? "Skill_B" : attackIndex >= AirStartIndex ? "AirAttack" + (attackIndex - AirStartIndex + 1) : attackIndex == UpperIndex ? "UpperAttack" : attackIndex == SkillAIndex ? "Skill_A" : attackIndex == SkillGIndex ? "Skill_G" : attackIndex == 0 ? config.attackAnimStateName : config.attackAnimStateName + (attackIndex + 1);

        private AirComboModule airCombo => Player.AirCombo;

        #endregion

        #region 초기화 및 보스 패리

        /// <summary>전투·파워 공격 프로필 설정을 캐싱한다.</summary>
        protected override void OnInitialize()
        {
            config = Player.Profile.combat;
            powerConfig = Player.Profile.powerAttack;
        }

        /// <summary>마지막 공격 입력 시각(보스 패리 입력 창 판정용, unscaled time).</summary>
        private float lastBossParryInput = float.NegativeInfinity;
        /// <summary>보스 패리로 입력을 소모한 프레임. 같은 프레임에 그 입력이 공격으로도 쓰이지 않게 막는다.</summary>
        private int bossParryConsumedFrame = -1;
        /// <summary>
        /// 지금 보스 공격이나 투사체를 패리할 수 있는지. 이번 프레임에 공격을 눌렀거나 패리 입력 창 안에 눌렀고,
        /// 피격·회피·링크 점프 중이 아니며 살아 있어야 한다.
        /// </summary>
        public bool IsBossParryInputActive => !Player.IsHitReacting && !Player.IsDodging &&
            !Player.IsLinkJumping && Player.Health.CurrentHealth > 0 &&
            bossParryConsumedFrame != Time.frameCount &&
            (Player.Controls.AttackPressed || Time.unscaledTime - lastBossParryInput <= config.bossParryInputWindow);

        /// <summary>보스 패리 입력이 유효하면 그 입력을 소모하고, 같은 입력이 공격·E 후속 공격으로 이어지지 않게 비운다.</summary>
        /// <returns>패리가 성립했으면 true.</returns>
        public bool TryConsumeBossParry()
        {
            if (!IsBossParryInputActive) return false;
            lastBossParryInput = float.NegativeInfinity;
            bossParryConsumedFrame = Time.frameCount;
            ClearGroundAttackInput();
            ClearEFollowUp();
            return true;
        }

        #endregion

        #region 프레임 갱신

        /// <summary>피격·사망·공중 대시 시작 등으로 전투 동작을 강제로 끊을 때, 공격·가드·예약 입력을 모두 초기화한다.</summary>
        public void CancelForDamage()
        {
            sheathedQueued = false; sheathedStep = 0;
            airCombo.CancelAirDash();
            lastBossParryInput = float.NegativeInfinity;
            ClearEFollowUp();
            ClearGroundAttackInput();
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            IsAttacking = false;
            IsGuarding = false;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            attackIndex = 0;
            animator.SetBool("isAttacking", false);
            animator.SetBool("isGuarding", false);
            if (airCombo.Active) airCombo.Fall();
        }

        /// <summary>대기 애니메이션으로 전환한다(시작 시 PlayerCharacter가 호출).</summary>
        public void PlayIdle()
        {
            PlayAnimState(config.idleAnimStateName);
        }

        /// <summary>
        /// 매 프레임 전투 입력을 처리한다. 보스 패리 입력 기록, 공중 대시·공중 콤보 회복 중 입력 처리,
        /// 가드 진입/해제, E 후속 공격, 파워 차지, 스킬 입력, 공중/지상 공격 입력과 공격 진행을 순서대로 다룬다.
        /// </summary>
        public void Tick()
        {
            if (bossParryConsumedFrame == Time.frameCount) return;
            if (Player.Controls.AttackPressed && !Player.IsHitReacting && !Player.IsDodging &&
                !Player.IsLinkJumping && bossParryConsumedFrame != Time.frameCount)
                lastBossParryInput = Time.unscaledTime;
            if (Player.IsHitReacting || Player.IsLinkJumping)
            {
                ClearGroundAttackInput();
                return;
            }
            if (airCombo.IsAirDashing)
            {

                return;
            }
            if (Player.Controls.SkillAPressed && airCombo.TryBeginAirDash()) return;
            if (airCombo.Recovering)
            {
                if (airCombo.AllowsLandingFollowUp) CaptureGroundAttackInput(Player.Controls);
                else ClearGroundAttackInput();
                return;
            }
            if (Player.IsDodging) return;
            var controls = Player.Controls;
            bool wantsGuard = controls.GuardHeld && !IsAttacking &&
                Player.Locomotion.IsGrounded && !Player.IsAirComboActive;
            if (wantsGuard)
            {
                ClearGroundAttackInput();
                ClearEFollowUp();
                if (!IsGuarding)
                {
                    IsGuarding = true;
                    animator.SetBool("isGuarding", true);
                    animator.SetBool("isMoving", false);
                    animator.SetBool("isRunning", false);
                    animator.CrossFadeInFixedTime("Base Layer.Guard", .1f, 0, 0f);
                }
                else if (!animator.IsInTransition(0))
                {
                    var guardState = animator.GetCurrentAnimatorStateInfo(0);
                    if (guardState.IsName("GuardAccept") && guardState.normalizedTime >= 1f)
                        animator.CrossFadeInFixedTime("Base Layer.Guard", .08f, 0, 0f);
                }
                return;
            }
            if (IsGuarding)
            {
                IsGuarding = false;
                animator.SetBool("isGuarding", false);
                bool moving = controls.Move.sqrMagnitude > .01f;
                animator.SetBool("isMoving", moving);
                animator.SetBool("isRunning", moving && controls.RunHeld);
                animator.CrossFadeInFixedTime(moving ? (controls.RunHeld ? "Base Layer.Run" : "Base Layer.Walk") : "Base Layer.Idle", .1f, 0, 0f);
            }
            bool followUpPress = controls.AttackPressed && (IsUsingPiercingSkill || !IsAttacking) &&
                eFollowUpTarget != null && Time.time <= eFollowUpUntil && Player.CanStartAction(PlayerAction.Attack);
            if (followUpPress)
            {
                eFollowUpQueued = true;
                ClearGroundAttackInput();
            }
            if (eFollowUpQueued && !IsAttacking) BeginEFollowUp();
            if (IsChargingPower)
            {
                // 차지 중에는 차지가 입력을 독점한다. 버튼을 뗄 때까지 다른 동작은 시작하지 않는다.
                TickPowerCharge(controls);
                return;
            }
            UpdateAttackProgress();
            if (controls.SkillGPressed)
                TrySkill();
            if (controls.SkillAPressed)
                TrySkillA();
            if (controls.SkillBPressed)
                TrySkillB();
            if (controls.UpperAttackPressed)
                TryUpperAttack();



            bool airborneAttack = Player.IsAirComboActive || IsUsingUpperAttack || IsUsingChaseAttack ||
                (!Player.Locomotion.IsGrounded && !IsUsingGroundCombo);
            if (airborneAttack)
            {
                ClearGroundAttackInput();
                if (controls.AttackPressed) TryAttack();
            }
            else
            {
                if (!followUpPress && Player.CanStartAction(PlayerAction.Attack)) CaptureGroundAttackInput(controls);
                ProcessGroundAttackInput();
            }
        }

        #endregion

        #region 지상 공격 입력 버퍼

        /// <summary>대기 중인 지상 공격 입력을 버린다.</summary>
        private void ClearGroundAttackInput()
        {
            groundInputPending = false;
            groundInputReleased = false;
        }

        /// <summary>
        /// 지상 공격 버튼의 누름/뗌 시점을 기록해 짧게 누름(일반 공격·스킬 C)과 길게 누름(파워 공격)을 구분할 수 있게 한다.
        /// 회피 중에는 입력을 DodgeModule이 쥐고 있으므로 DodgeModule도 이 메서드를 호출한다.
        /// </summary>
        public void CaptureGroundAttackInput(IPlayerInput controls)
        {
            if (capturedInputFrame == Time.frameCount || bossParryConsumedFrame == Time.frameCount) return;
            capturedInputFrame = Time.frameCount;
            if (controls.AttackPressed)
            {
                attackPressedAt = Time.time;
                groundInputPending = true;
                groundInputReleased = false;
                groundInputSkillC = controls.SkillCModifierHeld;
            }
            if (groundInputPending && !groundInputReleased && !controls.AttackHeld)
            {
                groundInputReleased = true;
                groundInputReleasedAt = Time.time;
            }
        }

        /// <summary>지상 공격 버튼을 누르고 있던(또는 누르고 있는) 시간.</summary>
        private float GroundHoldSeconds => Mathf.Max(0f,
            (groundInputReleased ? groundInputReleasedAt : Time.time) - attackPressedAt);
        /// <summary>짧게 누름이 확정됐거나 홀드 판정 시간을 넘겨, 버퍼된 지상 공격 입력을 실행할 수 있는지.</summary>
        public bool GroundAttackInputReady => groundInputPending &&
            (groundInputReleased || GroundHoldSeconds >= powerConfig.holdThreshold);

        /// <summary>
        /// 버퍼된 지상 공격 입력을 실행한다. 홀드 시간을 넘기면 파워 차지(이미 뗐으면 즉시 발동)를,
        /// 짧게 눌렀다 떼면 일반 공격 또는 스킬 C를 시작한다. 버퍼 유효 시간이 지난 입력은 버린다.
        /// </summary>
        private void ProcessGroundAttackInput()
        {
            if (!groundInputPending) return;
            if (!Player.CanStartAction(PlayerAction.Attack)) { ClearGroundAttackInput(); return; }
            if (groundInputReleased && Time.time - groundInputReleasedAt > config.skillAttackInputBuffer)
            {
                ClearGroundAttackInput();
                return;
            }
            if (GroundHoldSeconds >= powerConfig.holdThreshold)
            {
                if (CanBeginPowerCharge())
                {
                    float held = GroundHoldSeconds;
                    bool released = groundInputReleased;
                    BeginPowerCharge();
                    PowerChargeSeconds = Mathf.Min(held, powerConfig.maxChargeDuration);
                    if (released) ReleasePower();
                }
                return;
            }
            if (!groundInputReleased) return;
            bool skillC = groundInputSkillC;
            ClearGroundAttackInput();
            if (skillC) TrySkillC(); else TryAttack();
        }

        /// <summary>공중 콤보 피니시 후 착지 순간, 버퍼된 지상 공격 입력으로 착지 연계 공격을 시작한다.</summary>
        /// <returns>연계 공격이 시작되었으면 true.</returns>
        public bool TryLandingFollowUp()
        {
            if (!GroundAttackInputReady) return false;
            if (groundInputReleased && Time.time - groundInputReleasedAt > Player.Profile.airCombo.landingAttackBuffer)
            {
                ClearGroundAttackInput();
                return false;
            }
            ProcessGroundAttackInput();
            return IsAttacking;
        }

        #endregion

        #region 파워 공격

        /// <summary>
        /// 파워 차지를 시작할 수 있는지 판정한다. 지상에서 공격 중이 아니거나, 지상 콤보·스킬이 연계 가능 시점을 지났을 때 허용한다.
        /// </summary>
        private bool CanBeginPowerCharge()
        {
            if (!Player.CanStartAction(PlayerAction.Skill) || !Player.Locomotion.IsGrounded) return false;
            if (!IsAttacking) return true;
            if (IsUsingGroundCombo) return CurrentAttackProgress() >= GroundChainProgress(attackIndex);
            if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillC)
            {
                float followUp = IsUsingSkill ? config.skillGFollowUpProgress :
                    IsUsingSkillA ? config.skillAFollowUpProgress : IsUsingSkillB ? config.skillBFollowUpProgress : .75f;
                return CurrentAttackProgress() >= followUp && (!IsUsingPiercingSkill || !Player.PiercingDash.IsDashing);
            }
            return false;
        }

        /// <summary>다른 예약 입력을 모두 버리고 가까운 적을 향해 파워 공격 차지 애니메이션을 시작한다.</summary>
        private void BeginPowerCharge()
        {
            ClearGroundAttackInput();
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            skillAttackBufferedUntil = float.NegativeInfinity;
            queuedAttack = false;
            attackIndex = PowerChargeIndex;
            IsAttacking = true;
            AttackSequence++;
            PowerChargeSeconds = 0f;
            FaceNearbyEnemy();
            animator.CrossFadeInFixedTime("Base Layer.PowerAttackStart", .1f, 0, 0f);
        }

        /// <summary>차지 시간을 갱신하고 버튼을 떼면 파워 공격을 발동한다.</summary>
        private void TickPowerCharge(IPlayerInput controls)
        {
            PowerChargeSeconds = Mathf.Clamp(Time.time - attackPressedAt, 0f, powerConfig.maxChargeDuration);
            if (!controls.AttackHeld) ReleasePower();
        }

        /// <summary>차지를 끝내고 파워 공격 발동 애니메이션(PowerAttackEnd)으로 전환한다.</summary>
        private void ReleasePower()
        {
            attackIndex = PowerReleaseIndex;
            AttackSequence++;
            FaceNearbyEnemy();
            animator.CrossFadeInFixedTime("Base Layer.PowerAttackEnd", .05f, 0, 0f);
        }

        #endregion

        #region 스킬 및 공중 대시

        /// <summary>스킬 G 사용을 요청한다.</summary>
        public void TrySkill() { RequestSkill(SkillGIndex); }
        /// <summary>스킬 A 입력 처리. 공중이면 공중 대시를 시도하고, 지상이면 관통 돌진 스킬 A를 요청한다.</summary>
        public void TrySkillA()
        {
            if (airCombo.TryBeginAirDash()) return;
            // 공중에서 실패한 요청이 지상용 관통 돌진 스킬로 바뀌어 나가면 안 된다.
            if (!Player.Locomotion.IsGrounded || Player.IsAirComboActive) return;
            RequestSkill(SkillAIndex);
        }

        /// <summary>AirComboModule이 공중 대시를 시작할 때 호출되어, 진행 중인 전투 동작을 끊고 공중 대시 공격 상태로 만든다.</summary>
        public void BeginAirDash()
        {
            CancelForDamage();
            attackIndex = 21;
            IsAttacking = true;
            AttackSequence++;
        }

        /// <summary>후속 공격 없이 공중 대시가 끝났을 때 공격 상태를 해제한다.</summary>
        public void FinishAirDash()
        {
            IsAttacking = false;
            queuedAttack = false;
            attackIndex = 0;
        }
        /// <summary>관통 돌진 스킬 B 사용을 요청한다.</summary>
        public void TrySkillB() { RequestSkill(SkillBIndex); }
        /// <summary>적을 띄우는 스킬 C 사용을 요청한다.</summary>
        public void TrySkillC() { RequestSkill(SkillCIndex); }

        /// <summary>
        /// 스킬 사용 요청을 처리한다. 지상 콤보 중이면 전환 시점까지 버퍼에 담고, 공격 중이 아니면 즉시 시작한다.
        /// </summary>
        private void RequestSkill(int index)
        {
            if (!Player.CanStartAction(PlayerAction.Skill)) return;
            string state = index == SkillCIndex ? "Skill_C" : index == SkillGIndex ? "Skill_G" : index == SkillAIndex ? "Skill_A" : "Skill_B";
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return;
            if (IsUsingGroundCombo)
            {
                bufferedSkillIndex = index;
                bufferedSkillUntil = Time.time + config.normalSkillInputBuffer;
                UpdateAttackProgress();
                return;
            }
            if (IsAttacking) return;
            BeginSkill(index, config.crossFadeTime);
        }

        /// <summary>예약 입력을 모두 비우고 스킬 대상 거리 안의 적을 향해 지정한 스킬 애니메이션을 시작한다.</summary>
        private void BeginSkill(int index, float blendTime)
        {
            ClearEFollowUp();
            ClearGroundAttackInput();
            launchedEnemy = null;
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            attackIndex = index;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            FaceNearbyEnemy(config.skillTargetDistance);
            AttackSequence++;
            IsAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, blendTime, 0, 0f);
        }

        #endregion

        #region 공격 시작

        /// <summary>적을 띄우며 함께 뛰어오르는 UpperAttack을 시작한다. 이후 공중 콤보로 이어질 수 있다.</summary>
        /// <returns>시작할 수 없는 상태이거나 애니메이터 상태가 없으면 false.</returns>
        public bool TryUpperAttack()
        {
            if (!Player.CanStartAction(PlayerAction.UpperAttack)) return false;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.UpperAttack"))) return false;
            ClearGroundAttackInput();
            airCombo.BeginUpperMotion();
            attackIndex = UpperIndex;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            FaceNearbyEnemy(config.attackTargetDistance);
            AttackSequence++;
            IsAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer.UpperAttack", config.crossFadeTime, 0, 0f);
            return true;
        }

        /// <summary>
        /// 일반 공격 입력 처리. 공격 중이면 다음 단계(또는 스킬 후 콤보)로 예약하고, 공중이면 공중 콤보를,
        /// 지상이면 지상 콤보 1단을 시작한다.
        /// </summary>
        public void TryAttack()
        {
            if (!Player.CanStartAction(PlayerAction.Attack)) return;
            if (IsAttacking)
            {
                if (IsUsingEFollowUp) { queuedAttack = true; return; }
                if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillC)
                {
                    skillAttackBufferedUntil = Time.time + config.skillAttackInputBuffer;
                    UpdateAttackProgress();
                    return;
                }
                if (IsUsingGroundCombo || IsUsingUpperAttack || IsUsingChaseAttack || (IsUsingAirCombo && AirComboIndex < AirComboCount - 1)) queuedAttack = true;
                return;
            }
            if (Player.IsAirComboActive) return;
            if (airCombo.CanBegin()) { BeginAirAttack(0); return; }
            BeginGroundCombo(config.crossFadeTime);
        }

        /// <summary>회피의 공격 캔슬 시점에 회피 중 버퍼된 짧게/길게 누른 공격 입력을 실행한다.</summary>
        public void BeginAttackFromDodge(float blendTime)
        {
            if (IsAttacking || !Player.CanStartAction(PlayerAction.Attack)) return;
            ProcessGroundAttackInput();
        }

        /// <summary>가까운 적을 향해 지상 콤보 1단을 시작한다.</summary>
        private void BeginGroundCombo(float blendTime)
        {
            attackIndex = 0;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            FaceNearbyEnemy();
            AttackSequence++;
            IsAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, blendTime, 0, 0f);
        }

        #endregion

        #region 회피 캔슬

        /// <summary>현재 지상 콤보 단계의 휘두르기가 끝나 회피로 캔슬할 수 있는 시점을 지났는지.</summary>
        public bool CanDodgeCancel
        {
            get
            {
                if (!IsUsingGroundCombo) return false;
                var points = config.dodgeCancelProgress;
                float cancelAt = points != null && attackIndex < points.Length ? points[attackIndex] : 1f;
                return CurrentAttackProgress() >= cancelAt;
            }
        }

        /// <summary>예약된 다음 콤보 공격을 취소한다(회피 입력이 더 최근 입력으로 우선할 때).</summary>
        public void ClearQueuedAttack() { queuedAttack = false; }

        /// <summary>회피가 이번 프레임에 바로 이어받을 수 있도록 현재 지상 공격과 예약 입력을 버린다.</summary>
        public void CancelForDodge()
        {
            ClearEFollowUp();
            ClearGroundAttackInput();
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            IsAttacking = false;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            attackIndex = 0;
            animator.SetBool("isAttacking", false);
        }

        /// <summary>현재 공격 상태(전이 중이면 전이 대상 상태)의 정규화 시간. 해당 상태가 재생 중이 아니면 -1.</summary>
        private float CurrentAttackProgress()
        {
            string expected = "Base Layer." + AttackStateName;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(expected)) info = next;
            }
            return info.IsName(expected) ? info.normalizedTime : -1f;
        }

        #endregion

        #region 공중 공격 및 추격

        /// <summary>
        /// 공중 대시 후속 입력으로 공중 콤보 1단을 시작한다. 대시로 맞힌 적이 공중에 떠 있고 가까우면 그 적을 대상으로 돌아선다.
        /// </summary>
        public void BeginAirDashFollowUp(CombatSystem.Combat.EnemyDamageReaction target)
        {
            BeginAirAttack(0);
            if (target == null || !target.isActiveAndEnabled || target.CurrentHealth <= 0 || !target.IsAirborneForCombo) return;
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > config.skillTargetDistance * config.skillTargetDistance) return;
            AttackTarget = target;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>지정한 단계의 공중 콤보 공격을 시작한다. 공중 콤보 체공이 아직 시작되지 않았으면 함께 시작한다.</summary>
        private void BeginAirAttack(int index)
        {
            if (index < 0 || index >= AirComboCount || !animator.HasState(0, Animator.StringToHash("Base Layer.AirAttack" + (index + 1)))) return;
            if (!airCombo.Active) airCombo.Begin();
            attackIndex = AirStartIndex + index;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            IsAttacking = true;
            AttackSequence++;
            FaceNearbyEnemy(config.skillTargetDistance);
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, .08f, 0, 0);
        }

        /// <summary>
        /// 스킬 C가 적을 띄웠을 때 히트박스가 호출한다. 띄운 적 중 락온 대상이 있으면 그 적을,
        /// 없으면 이번 스킬 C로 처음 띄운 적을 추격 대상으로 삼는다.
        /// </summary>
        public void RegisterLaunch(CombatSystem.Combat.EnemyDamageReaction enemy)
        {
            if (!IsUsingSkillC || enemy == null) return;
            var locked = Player.LockOn.Target;
            if (launchedEnemy == null || (locked != null && locked.Transform == enemy.transform)) launchedEnemy = enemy;
        }

        /// <summary>띄운 적을 공중에 고정하고 그 적을 향해 뛰어오르는 추격 공격(ChaseUpAttack)을 시작한다.</summary>
        private void BeginChase()
        {
            var target = launchedEnemy;
            launchedEnemy = null;
            attackIndex = ChaseIndex;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            AttackSequence++;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
            AttackTarget = target;
            // 도약이 적과 만나도록 적을 현재 위치에 띄워 둔다. 이후 추격 공격의 타격이 계속 공중에 붙잡아 둔다.
            target.HoldInAir();
            airCombo.BeginChaseMotion(target.transform, config.airAttackContactDistance);
            animator.CrossFadeInFixedTime("Base Layer.ChaseUpAttack", .08f, 0, 0f);
        }

        /// <summary>추격 도약이 공중 콤보로 이어지지 않고 끝났을 때, 공중 콤보처럼 낙하·착지하게 한다.</summary>
        private void EndChase()
        {
            IsAttacking = false;
            queuedAttack = false;
            attackIndex = 0;
            airCombo.Begin();
            airCombo.Fall();
        }

        /// <summary>공중 콤보 공격을 끝내고 낙하로 전환한다. 피니시였다면 빠른 낙하로 착지 연계를 허용한다.</summary>
        public void EndAirCombo()
        {
            if (!Player.IsAirComboActive) return;
            bool finisher = IsAirComboFinisher;
            IsAttacking = false; queuedAttack = false; attackIndex = 0;
            airCombo.Fall(finisher);
        }

        #endregion

        #region 대상 조준

        /// <summary>
        /// 공격 시작 시 대상 적을 정해 그쪽으로 돌아선다. 락온 대상이 있으면 거리와 관계없이 우선하고,
        /// 없으면 범위·높이 허용치 안에서 가장 가까운 적을 고른다. 음수 거리는 기본 공격 대상 거리를 뜻한다.
        /// </summary>
        private void FaceNearbyEnemy(float targetDistance = -1f)
        {
            float range = targetDistance < 0f ? config.attackTargetDistance : targetDistance;
            AttackTarget = null;
            var locked = Player.LockOn.Target;
            if (locked != null)
            {
                // 락온 대상은 거리와 관계없이 최근접 적 선택보다 우선한다.
                AttackTarget = locked.Transform.GetComponent<CombatSystem.Combat.EnemyDamageReaction>();
                Vector3 toLocked = locked.Transform.position - transform.position;
                toLocked.y = 0f;
                if (toLocked.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(toLocked, Vector3.up);
                return;
            }
            if (range <= 0f) return;
            Transform target = null;
            float nearestSquared = range * range;
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                var enemy = combatant.Transform.GetComponent<CombatSystem.Combat.EnemyDamageReaction>();
                if (enemy == null) continue;
                if (!enemy.isActiveAndEnabled) continue;
                Vector3 direction = enemy.transform.position - transform.position;
                if (Mathf.Abs(direction.y) > config.attackTargetHeightTolerance) continue;
                direction.y = 0f;
                float distanceSquared = direction.sqrMagnitude;
                if (distanceSquared < 0.0001f || distanceSquared > nearestSquared) continue;
                nearestSquared = distanceSquared;
                target = enemy.transform;
                AttackTarget = enemy;
            }
            if (target == null) return;
            Vector3 facing = target.position - transform.position;
            facing.y = 0f;
            transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }

        #endregion

        #region 공격 진행·연계

        /// <summary>
        /// 현재 공격 애니메이션 진행도를 보고 연계를 결정한다. E 후속 공격, 버퍼된 스킬 전환, 스킬 C 추격,
        /// 스킬 후 콤보, UpperAttack/공중 콤보 연계, 지상 콤보 다음 단계 진행, 공격 종료 후 대기 복귀를 처리한다.
        /// </summary>
        private void UpdateAttackProgress()
        {
            if (!IsAttacking) return;
            string expected = "Base Layer." + AttackStateName;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(expected)) info = next;
            }
            if (!info.IsName(expected)) return;
            if (IsUsingPiercingSkill && eFollowUpQueued && !Player.PiercingDash.IsDashing &&
                info.normalizedTime >= (IsUsingSkillB ? config.skillBFollowUpProgress : config.skillAFollowUpProgress))
            {
                BeginEFollowUp();
                return;
            }
            if (IsUsingEFollowUp && queuedAttack && info.normalizedTime >= config.eFollowUpComboProgress)
            {
                BeginGroundCombo(config.skillFollowUpBlendTime);
                return;
            }

            if (bufferedSkillIndex >= 0 && Time.time > bufferedSkillUntil) bufferedSkillIndex = -1;
            bool bufferedPiercingSkill = bufferedSkillIndex == SkillAIndex || bufferedSkillIndex == SkillBIndex;
            float skillTransitionProgress = bufferedPiercingSkill ? config.piercingSkillTransitionProgress : config.normalSkillTransitionProgress;
            if (IsUsingGroundCombo && bufferedSkillIndex >= 0 && info.normalizedTime >= skillTransitionProgress)
            {
                BeginSkill(bufferedSkillIndex, bufferedPiercingSkill ? config.piercingSkillBlendTime : config.normalSkillBlendTime);
                return;
            }

            if (IsUsingSkillC && Time.time <= skillAttackBufferedUntil && info.normalizedTime >= config.skillCChaseProgress &&
                launchedEnemy != null && launchedEnemy.isActiveAndEnabled && launchedEnemy.IsAirborneForCombo)
            {
                BeginChase();
                return;
            }

            if (IsUsingChaseAttack)
            {
                if (queuedAttack && info.normalizedTime >= config.chaseAirChainProgress) BeginAirAttack(0);
                else if (info.normalizedTime >= 1f) EndChase();
                return;
            }

            if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillC)
            {
                float followUp = IsUsingSkillC ? .75f : IsUsingSkill ? config.skillGFollowUpProgress : IsUsingSkillA ? config.skillAFollowUpProgress : config.skillBFollowUpProgress;
                bool dashFinished = !IsUsingPiercingSkill || !Player.PiercingDash.IsDashing;
                if (Time.time <= skillAttackBufferedUntil && info.normalizedTime >= followUp && dashFinished)
                {
                    attackIndex = 0;
                    queuedAttack = false;
                    skillAttackBufferedUntil = float.NegativeInfinity;
                    FaceNearbyEnemy();
                    AttackSequence++;
                    animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, config.skillFollowUpBlendTime, 0, 0f);
                    return;
                }
            }

            if (IsUsingUpperAttack && queuedAttack && info.normalizedTime >= config.upperAirChainProgress &&
                info.normalizedTime < .48f && airCombo.CanBegin())
            {
                BeginAirAttack(0);
                return;
            }
            if (IsUsingAirCombo)
            {
                if (queuedAttack && AirComboIndex < AirComboCount - 1 && info.normalizedTime >= AirChainProgress(AirComboIndex))
                    BeginAirAttack(AirComboIndex + 1);
                else if (info.normalizedTime >= 1) EndAirCombo();
                return;
            }

            // 피니시 도중 입력된 다음 콤보는 보관했다가, 대기 상태를 거치지 않고 1단부터 바로 다시 시작한다.
            if (queuedAttack && IsGroundComboFinisher && info.normalizedTime >= 1f)
            {
                BeginGroundCombo(config.comboCrossFadeTime);
                return;
            }

            if (queuedAttack && IsUsingGroundCombo && attackIndex < GroundComboCount - 1 &&
                info.normalizedTime >= GroundChainProgress(attackIndex))
            {
                FaceNearbyEnemy();
                attackIndex++;
                AttackSequence++;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, config.comboCrossFadeTime, 0, 0f);
            }
            else if (info.normalizedTime >= 1f)
            {
                float idleBlend = IsUsingGroundCombo ? config.groundAttackIdleBlendTime : config.crossFadeTime;
                bufferedSkillIndex = -1;
                IsAttacking = false;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                attackIndex = 0;
                animator.CrossFadeInFixedTime(config.idleAnimStateName, idleBlend, 0);
            }
        }

        /// <summary>지상 콤보 단계별로 다음 단계로 넘어갈 수 있는 진행도. 단계별 값이 없으면 기본값을 쓴다.</summary>
        private float GroundChainProgress(int step)
        {
            var points = config.comboChainProgress;
            return points != null && step < points.Length ? points[step] : config.comboTransitionProgress;
        }

        /// <summary>공중 콤보 단계별로 다음 단계로 넘어갈 수 있는 진행도. 단계별 값이 없으면 기본값을 쓴다.</summary>
        private float AirChainProgress(int step)
        {
            var points = config.airComboChainProgress;
            return points != null && step < points.Length ? points[step] : config.airChainProgress;
        }

        #endregion

        #region 납도 공격

        /// <summary>납도 공격 준비 자세(SheathedStart)로 들어간다. reset이면 공격 단계를 1단부터 다시 센다.</summary>
        private void BeginSheathedReady(bool reset)
        {
            ClearGroundAttackInput(); ClearEFollowUp();
            queuedAttack = false; sheathedQueued = false;
            bufferedSkillIndex = -1; skillAttackBufferedUntil = float.NegativeInfinity;
            IsGuarding = false; IsAttacking = true;
            attackIndex = SheathedReadyIndex;
            if (reset) sheathedStep = 0;
            animator.CrossFadeInFixedTime("Base Layer.SheathedStart", .1f, 0, 0);
        }

        /// <summary>납도 공격 1단/2단을 번갈아 시작한다.</summary>
        private void BeginSheathedAttack()
        {
            sheathedQueued = false;
            attackIndex = SheathedFirstIndex + sheathedStep;
            sheathedStep = (sheathedStep + 1) % 2;
            IsAttacking = true; AttackSequence++;
            FaceNearbyEnemy();
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, config.sheathedBlendTime, 0, 0);
        }

        /// <summary>납도 공격 흐름을 끝내고 대기 상태로 돌아간다.</summary>
        private void EndSheathed()
        {
            sheathedQueued = false; sheathedStep = 0;
            IsAttacking = false; attackIndex = 0;
            ClearGroundAttackInput();
            animator.CrossFadeInFixedTime(config.idleAnimStateName, .15f, 0, 0);
        }

        /// <summary>
        /// 우클릭(가드 키)을 누른 채 공격 입력으로 납도 공격을 이어 가고, 키를 떼면 종료한다.
        /// 현재는 Tick에서 호출되지 않는다.
        /// </summary>
        private void TickSheathed(IPlayerInput controls)
        {
            // 우클릭을 떼면 예약된 다음 공격만 취소하며, PowerAttackEnd를 발동하지 않는다.
            if (!controls.GuardHeld)
            {
                sheathedQueued = false;
                if (IsSheathedReady) { EndSheathed(); return; }
            }
            else if (controls.AttackPressed) sheathedQueued = true;
            if (IsSheathedReady)
            {
                var current = animator.GetCurrentAnimatorStateInfo(0);
                if (sheathedQueued && (current.IsName("SheathedReady") ||
                    (current.IsName("SheathedStart") && current.normalizedTime >= .85f)))
                    BeginSheathedAttack();
                return;
            }
            float progress = CurrentAttackProgress();
            if (progress < 0) return;
            if (sheathedQueued && controls.GuardHeld && progress >= config.sheathedChainProgress)
            { BeginSheathedAttack(); return; }
            if (progress >= 1f)
            {
                if (controls.GuardHeld) BeginSheathedReady(false);
                else EndSheathed();
            }
        }

        #endregion

        #region 가드 및 정리

        /// <summary>가드 중 공격을 막았을 때 가드 피격(GuardAccept) 애니메이션을 재생한다.</summary>
        public void PlayGuardAccept()
        {
            if (!IsGuarding) return;
            animator.CrossFadeInFixedTime("Base Layer.GuardAccept", 0.04f, 0, 0f);
        }

        /// <summary>플레이어 비활성화 시 공격·가드·예약 입력 상태를 모두 초기화한다.</summary>
        public override void OnDisable()
        {
            sheathedQueued = false; sheathedStep = 0;
            lastBossParryInput = float.NegativeInfinity;
            ClearEFollowUp();
            ClearGroundAttackInput();
            IsAttacking = false;
            IsGuarding = false;
            queuedAttack = false;
        }

        /// <summary>상태 이름이 지정되어 있을 때만 기본 크로스페이드 시간으로 해당 애니메이터 상태로 전환한다.</summary>
        private void PlayAnimState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;
            animator.CrossFadeInFixedTime(stateName, config.crossFadeTime);
        }

        #endregion
    }
}
