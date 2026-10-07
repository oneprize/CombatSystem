using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Ground combo, skills, upper/air attack chaining, guard input.
    /// </summary>
    public sealed class CombatModule : PlayerModule
    {
        private const int GroundComboCount = 4;
        private const int AirComboCount = 4;
        private const int SkillGIndex = 5, SkillAIndex = 6, UpperIndex = 7, AirStartIndex = 8, SkillBIndex = 14, SkillIIndex = 15, SkillCIndex = 16, ChaseIndex = 17, PowerChargeIndex = 18, PowerReleaseIndex = 19, EFollowUpIndex = 20;

        private PlayerProfile.CombatSettings config;
        private int attackIndex;
        private bool queuedAttack;
        private float eFollowUpUntil = float.NegativeInfinity;
        private bool eFollowUpQueued;
        private Transform eFollowUpTarget;
        public bool IsUsingEFollowUp => IsAttacking && attackIndex == EFollowUpIndex;

        public void RegisterPiercingHit(Transform target)
        {
            if (!IsUsingSkillA) return;
            eFollowUpTarget = target;
            eFollowUpUntil = Time.time + config.eFollowUpInputWindow;
        }

        private void ClearEFollowUp()
        {
            eFollowUpQueued = false;
            eFollowUpUntil = float.NegativeInfinity;
            eFollowUpTarget = null;
        }

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
        private int bufferedSkillIndex = -1;
        private float bufferedSkillUntil = float.NegativeInfinity;
        private float skillAttackBufferedUntil = float.NegativeInfinity;
        private CombatSystem.Combat.EnemyDamageReaction launchedEnemy;
        private PlayerProfile.PowerAttackSettings powerConfig;
        private float attackPressedAt = float.NegativeInfinity;
        private bool groundInputPending, groundInputReleased, groundInputSkillC;
        private float groundInputReleasedAt;
        private int capturedInputFrame = -1;

        public bool IsGuarding { get; private set; }
        public bool IsAttacking { get; private set; }
        public int AttackComboIndex => attackIndex;
        public int AttackSequence { get; private set; }
        /// <summary>Enemy the current attack turned to face, or null when none was in range.</summary>
        public CombatSystem.Combat.EnemyDamageReaction AttackTarget { get; private set; }
        public float AirAttackContactDistance => config.airAttackContactDistance;

        public bool IsUsingGroundCombo => IsAttacking && attackIndex >= 0 && attackIndex < GroundComboCount;
        public bool IsUsingAirCombo => IsAttacking && attackIndex >= AirStartIndex && attackIndex < AirStartIndex + AirComboCount;
        public int AirComboIndex => IsUsingAirCombo ? attackIndex - AirStartIndex : -1;
        /// <summary>Last step of the ground combo (launches the enemy).</summary>
        public bool IsGroundComboFinisher => IsUsingGroundCombo && attackIndex == GroundComboCount - 1;
        /// <summary>Last step of the air combo (slams the enemy down).</summary>
        public bool IsAirComboFinisher => IsUsingAirCombo && AirComboIndex == AirComboCount - 1;
        public bool IsUsingSkill => IsAttacking && attackIndex == SkillGIndex;
        public bool IsUsingSkillA => IsAttacking && attackIndex == SkillAIndex;
        public bool IsUsingSkillB => IsAttacking && attackIndex == SkillBIndex;
        public bool IsUsingSkillC => IsAttacking && attackIndex == SkillCIndex;
        public bool IsUsingSkillI => IsAttacking && attackIndex == SkillIIndex;
        public bool IsUsingPiercingSkill => IsUsingSkillA || IsUsingSkillB;
        public bool IsUsingUpperAttack => IsAttacking && attackIndex == UpperIndex;
        /// <summary>The leap after an enemy launched by Skill C.</summary>
        public bool IsUsingChaseAttack => IsAttacking && attackIndex == ChaseIndex;
        /// <summary>Holding the attack button: PowerAttackStart followed by PowerAttackLoop.</summary>
        public bool IsChargingPower => IsAttacking && attackIndex == PowerChargeIndex;
        public bool IsReleasingPower => IsAttacking && attackIndex == PowerReleaseIndex;
        public bool IsUsingPowerAttack => IsChargingPower || IsReleasingPower;
        /// <summary>Total button hold duration, capped at the maximum damage time.</summary>
        public float PowerChargeSeconds { get; private set; }
        public float PowerChargeNormalized => Mathf.InverseLerp(powerConfig.holdThreshold,
            Mathf.Max(powerConfig.holdThreshold + .01f, powerConfig.maxChargeDuration), PowerChargeSeconds);
        public bool IsPowerArmored => IsChargingPower &&
            PowerChargeSeconds >= powerConfig.holdThreshold + powerConfig.armorAfterSeconds;
        public float PowerDamageMultiplier => Mathf.Lerp(powerConfig.minDamageMultiplier,
            Mathf.Max(powerConfig.minDamageMultiplier, powerConfig.maxDamageMultiplier), PowerChargeNormalized);
        public string CurrentAttackStateName => AttackStateName;
        private string AttackStateName => attackIndex == EFollowUpIndex ? "EFollowUp" : attackIndex == PowerReleaseIndex ? "PowerAttackEnd" : attackIndex == PowerChargeIndex ? "PowerAttackLoop" : attackIndex == ChaseIndex ? "ChaseUpAttack" : attackIndex == SkillCIndex ? "Skill_C" : attackIndex == SkillIIndex ? "Skill_I" : attackIndex == SkillBIndex ? "Skill_B" : attackIndex >= AirStartIndex ? "AirAttack" + (attackIndex - AirStartIndex + 1) : attackIndex == UpperIndex ? "UpperAttack" : attackIndex == SkillAIndex ? "Skill_A" : attackIndex == SkillGIndex ? "Skill_G" : attackIndex == 0 ? config.attackAnimStateName : config.attackAnimStateName + (attackIndex + 1);

        private AirComboModule airCombo => Player.AirCombo;

        protected override void OnInitialize()
        {
            config = Player.Profile.combat;
            powerConfig = Player.Profile.powerAttack;
        }

        private float lastBossParryInput = float.NegativeInfinity;
        private int bossParryConsumedFrame = -1;
        public bool IsBossParryInputActive => !Player.IsHitReacting && !Player.IsDodging &&
            !Player.IsLinkJumping && Player.Health.CurrentHealth > 0 &&
            bossParryConsumedFrame != Time.frameCount &&
            (Player.Controls.AttackPressed || Time.unscaledTime - lastBossParryInput <= config.bossParryInputWindow);

        public bool TryConsumeBossParry()
        {
            if (!IsBossParryInputActive) return false;
            lastBossParryInput = float.NegativeInfinity;
            bossParryConsumedFrame = Time.frameCount;
            ClearGroundAttackInput();
            ClearEFollowUp();
            return true;
        }

        public void CancelForDamage()
        {
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

        public void PlayIdle()
        {
            PlayAnimState(config.idleAnimStateName);
        }

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
            if (airCombo.Recovering)
            {
                if (airCombo.AllowsLandingFollowUp) CaptureGroundAttackInput(Player.Controls);
                else ClearGroundAttackInput();
                return;
            }
            if (Player.IsDodging) return;
            var controls = Player.Controls;
            bool followUpPress = controls.AttackPressed && (IsUsingSkillA || !IsAttacking) &&
                eFollowUpTarget != null && Time.time <= eFollowUpUntil && Player.CanStartAction(PlayerAction.Attack);
            if (followUpPress)
            {
                eFollowUpQueued = true;
                ClearGroundAttackInput();
            }
            if (eFollowUpQueued && !IsAttacking) BeginEFollowUp();
            if (IsChargingPower)
            {
                // Charging owns the input: nothing else starts until the button is released.
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
            if (controls.SkillIPressed)
                TrySkillI();
            if (controls.UpperAttackPressed)
                TryUpperAttack();

            if (controls.GuardHeld && !IsGuarding && !Player.IsAirComboActive)
                StartGuard();
            else if (!controls.GuardHeld && IsGuarding)
                StopGuard();

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

        private void ClearGroundAttackInput()
        {
            groundInputPending = false;
            groundInputReleased = false;
        }

        // Also called by DodgeModule, which owns input during a dodge.
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

        private float GroundHoldSeconds => Mathf.Max(0f,
            (groundInputReleased ? groundInputReleasedAt : Time.time) - attackPressedAt);
        public bool GroundAttackInputReady => groundInputPending &&
            (groundInputReleased || GroundHoldSeconds >= powerConfig.holdThreshold);

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

        private bool CanBeginPowerCharge()
        {
            if (!Player.CanStartAction(PlayerAction.Skill) || !Player.Locomotion.IsGrounded) return false;
            if (!IsAttacking) return true;
            if (IsUsingGroundCombo) return CurrentAttackProgress() >= GroundChainProgress(attackIndex);
            if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillI || IsUsingSkillC)
            {
                float followUp = IsUsingSkill ? config.skillGFollowUpProgress :
                    IsUsingSkillA ? config.skillAFollowUpProgress : IsUsingSkillB ? config.skillBFollowUpProgress : .75f;
                return CurrentAttackProgress() >= followUp && (!IsUsingPiercingSkill || !Player.PiercingDash.IsDashing);
            }
            return false;
        }

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

        private void TickPowerCharge(IPlayerInput controls)
        {
            PowerChargeSeconds = Mathf.Clamp(Time.time - attackPressedAt, 0f, powerConfig.maxChargeDuration);
            if (!controls.AttackHeld) ReleasePower();
        }

        private void ReleasePower()
        {
            attackIndex = PowerReleaseIndex;
            AttackSequence++;
            FaceNearbyEnemy();
            animator.CrossFadeInFixedTime("Base Layer.PowerAttackEnd", .05f, 0, 0f);
        }

        public void TrySkill() { RequestSkill(SkillGIndex); }
        public void TrySkillA() { RequestSkill(SkillAIndex); }
        public void TrySkillB() { RequestSkill(SkillBIndex); }
        public void TrySkillI() { RequestSkill(SkillIIndex); }
        public void TrySkillC() { RequestSkill(SkillCIndex); }

        private void RequestSkill(int index)
        {
            if (!Player.CanStartAction(PlayerAction.Skill)) return;
            string state = index == SkillCIndex ? "Skill_C" : index == SkillIIndex ? "Skill_I" : index == SkillGIndex ? "Skill_G" : index == SkillAIndex ? "Skill_A" : "Skill_B";
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

        public void TryAttack()
        {
            if (!Player.CanStartAction(PlayerAction.Attack)) return;
            if (IsAttacking)
            {
                if (IsUsingEFollowUp) { queuedAttack = true; return; }
                if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillI || IsUsingSkillC)
                {
                    skillAttackBufferedUntil = Time.time + config.skillAttackInputBuffer;
                    UpdateAttackProgress();
                    return;
                }
                if ((IsUsingGroundCombo && attackIndex < GroundComboCount - 1) || IsUsingUpperAttack || IsUsingChaseAttack || (IsUsingAirCombo && AirComboIndex < AirComboCount - 1)) queuedAttack = true;
                return;
            }
            if (Player.IsAirComboActive) return;
            if (airCombo.CanBegin()) { BeginAirAttack(0); return; }
            BeginGroundCombo(config.crossFadeTime);
        }

        /// <summary>Resolves the buffered short/long attack out of a dodge at its attack cancel point.</summary>
        public void BeginAttackFromDodge(float blendTime)
        {
            if (IsAttacking || !Player.CanStartAction(PlayerAction.Attack)) return;
            ProcessGroundAttackInput();
        }

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

        /// <summary>True once the current ground combo step has finished its swing and may be cancelled by a dodge.</summary>
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

        public void ClearQueuedAttack() { queuedAttack = false; }

        /// <summary>Drops the current ground attack so a dodge can take over this frame.</summary>
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

        /// <summary>Normalized time of the current attack state (or the one being blended into), -1 if not playing.</summary>
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
        /// Skill C hit an enemy into the air. The lock-on target wins if it was launched,
        /// otherwise the first enemy launched by this Skill C is chased.
        /// </summary>
        public void RegisterLaunch(CombatSystem.Combat.EnemyDamageReaction enemy)
        {
            if (!IsUsingSkillC || enemy == null) return;
            var locked = Player.LockOn.Target;
            if (launchedEnemy == null || (locked != null && locked.Transform == enemy.transform)) launchedEnemy = enemy;
        }

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
            // Hang the enemy where it is so the leap meets it; the chase hit then keeps it held.
            target.HoldInAir();
            airCombo.BeginChaseMotion(target.transform, config.airAttackContactDistance);
            animator.CrossFadeInFixedTime("Base Layer.ChaseUpAttack", .08f, 0, 0f);
        }

        /// <summary>The leap ended without chaining into the air combo: fall and land like an air combo.</summary>
        private void EndChase()
        {
            IsAttacking = false;
            queuedAttack = false;
            attackIndex = 0;
            airCombo.Begin();
            airCombo.Fall();
        }

        public void EndAirCombo()
        {
            if (!Player.IsAirComboActive) return;
            bool finisher = IsAirComboFinisher;
            IsAttacking = false; queuedAttack = false; attackIndex = 0;
            airCombo.Fall(finisher);
        }

        private void FaceNearbyEnemy(float targetDistance = -1f)
        {
            float range = targetDistance < 0f ? config.attackTargetDistance : targetDistance;
            AttackTarget = null;
            var locked = Player.LockOn.Target;
            if (locked != null)
            {
                // Lock-on overrides the nearest-enemy pick, whatever the distance.
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
            if (IsUsingSkillA && eFollowUpQueued && !Player.PiercingDash.IsDashing &&
                info.normalizedTime >= config.skillAFollowUpProgress)
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
            if (IsUsingGroundCombo && bufferedSkillIndex >= 0 && info.normalizedTime >= config.normalSkillTransitionProgress)
            {
                BeginSkill(bufferedSkillIndex, config.normalSkillBlendTime);
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

            if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillI || IsUsingSkillC)
            {
                float followUp = IsUsingSkillC ? .75f : IsUsingSkillI ? .75f : IsUsingSkill ? config.skillGFollowUpProgress : IsUsingSkillA ? config.skillAFollowUpProgress : config.skillBFollowUpProgress;
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

        private float GroundChainProgress(int step)
        {
            var points = config.comboChainProgress;
            return points != null && step < points.Length ? points[step] : config.comboTransitionProgress;
        }

        private float AirChainProgress(int step)
        {
            var points = config.airComboChainProgress;
            return points != null && step < points.Length ? points[step] : config.airChainProgress;
        }

        private void StartGuard()
        {
            CancelForDamage();
            IsGuarding = true;
            PlayAnimState(config.guardAnimStateName);
            Debug.Log("[PlayerCombat] Guard ON");
        }

        private void StopGuard()
        {
            IsGuarding = false;
            PlayAnimState(config.idleAnimStateName);
            Debug.Log("[PlayerCombat] Guard OFF");
        }

        public void PlayGuardAccept()
        {
            if (!IsGuarding) return;
            animator.CrossFadeInFixedTime("Base Layer.GuardAccept", 0.04f, 0, 0f);
        }

        public override void OnDisable()
        {
            lastBossParryInput = float.NegativeInfinity;
            ClearEFollowUp();
            ClearGroundAttackInput();
            IsAttacking = false;
            IsGuarding = false;
            queuedAttack = false;
        }

        private void PlayAnimState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;
            animator.CrossFadeInFixedTime(stateName, config.crossFadeTime);
        }
    }
}
