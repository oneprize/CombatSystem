using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Ground combo, skills, upper/air attack chaining, guard and parry input.
    /// </summary>
    public sealed class CombatModule : PlayerModule
    {
        private const int GroundComboCount = 4;
        private const int AirComboCount = 4;
        private const int SkillGIndex = 5, SkillAIndex = 6, UpperIndex = 7, AirStartIndex = 8, SkillBIndex = 14, SkillIIndex = 15, SkillCIndex = 16, ChaseIndex = 17, PowerChargeIndex = 18, PowerReleaseIndex = 19;

        private PlayerProfile.CombatSettings config;
        private float lastParryInput = float.NegativeInfinity;
        private int attackIndex;
        private bool queuedAttack;
        private int bufferedSkillIndex = -1;
        private float bufferedSkillUntil = float.NegativeInfinity;
        private float skillAttackBufferedUntil = float.NegativeInfinity;
        private CombatSystem.Combat.EnemyDamageReaction launchedEnemy;
        private PlayerProfile.PowerAttackSettings powerConfig;
        private float attackPressedAt = float.NegativeInfinity;
        private bool holdUsed;

        public bool IsGuarding { get; private set; }
        public bool IsAttacking { get; private set; }
        public int AttackComboIndex => attackIndex;
        public int AttackSequence { get; private set; }
        /// <summary>Enemy the current attack turned to face, or null when none was in range.</summary>
        public CombatSystem.Combat.EnemyDamageReaction AttackTarget { get; private set; }
        public float AirAttackContactDistance => config.airAttackContactDistance;

        public bool IsParryInputActive => !Player.IsHitReacting && Time.time - lastParryInput <= config.parryInputWindow;
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
        /// <summary>Holding the attack button: PowerAttackStart/Loop while the gauge drains.</summary>
        public bool IsChargingPower => IsAttacking && attackIndex == PowerChargeIndex;
        /// <summary>The released power attack (PowerAttackEnd).</summary>
        public bool IsReleasingPower => IsAttacking && attackIndex == PowerReleaseIndex;
        public bool IsUsingPowerAttack => IsChargingPower || IsReleasingPower;
        /// <summary>Stages drained by the current charge.</summary>
        public float PowerDrained { get; private set; }
        /// <summary>Attack level of the current charge or release: stages drained, rounded up (1..stages).</summary>
        public int PowerLevel => Mathf.Clamp(Mathf.CeilToInt(PowerDrained - .0001f), 1, Player.PowerGauge.Stages);
        /// <summary>While charging, hits past this point deal damage but do not interrupt.</summary>
        public bool IsPowerArmored => IsChargingPower && PowerDrained >= powerConfig.armorAfterStages - .0001f;
        public float PowerDamageMultiplier
        {
            get
            {
                var multipliers = powerConfig.damageMultipliers;
                if (multipliers == null || multipliers.Length == 0) return 1f;
                return multipliers[Mathf.Min(PowerLevel, multipliers.Length) - 1];
            }
        }
        public string CurrentAttackStateName => AttackStateName;
        private string AttackStateName => attackIndex == PowerReleaseIndex ? "PowerAttackEnd" : attackIndex == PowerChargeIndex ? "PowerAttackLoop" : attackIndex == ChaseIndex ? "ChaseUpAttack" : attackIndex == SkillCIndex ? "Skill_C" : attackIndex == SkillIIndex ? "Skill_I" : attackIndex == SkillBIndex ? "Skill_B" : attackIndex >= AirStartIndex ? "AirAttack" + (attackIndex - AirStartIndex + 1) : attackIndex == UpperIndex ? "UpperAttack" : attackIndex == SkillAIndex ? "Skill_A" : attackIndex == SkillGIndex ? "Skill_G" : attackIndex == 0 ? config.attackAnimStateName : config.attackAnimStateName + (attackIndex + 1);

        private AirComboModule airCombo => Player.AirCombo;

        protected override void OnInitialize()
        {
            config = Player.Profile.combat;
            powerConfig = Player.Profile.powerAttack;
        }

        public void RegisterParryInput()
        {
            if (!Player.IsHitReacting) lastParryInput = Time.time;
        }

        public bool TryConsumeParry()
        {
            if (!IsParryInputActive) return false;
            lastParryInput = float.NegativeInfinity;
            return true;
        }

        public void CancelForDamage()
        {
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
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
            if (Player.IsHitReacting || Player.IsLinkJumping || Player.IsDodging || airCombo.Recovering) return;
            var controls = Player.Controls;
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

            if (controls.AttackPressed)
            {
                attackPressedAt = Time.time;
                holdUsed = false;
                RegisterParryInput();
                if (controls.SkillCModifierHeld && !Player.IsAirComboActive && !IsUsingUpperAttack)
                    TrySkillC();
                else
                    TryAttack();
            }
            // The press already started a normal attack (and counted as a parry input);
            // keeping it held long enough turns it into the power charge.
            if (!holdUsed && controls.AttackHeld && Time.time - attackPressedAt >= powerConfig.holdThreshold && CanBeginPowerCharge())
                BeginPowerCharge();
        }

        private bool CanBeginPowerCharge()
        {
            return Player.PowerGauge.FilledStages >= 1 && Player.CanStartAction(PlayerAction.Skill) &&
                   Player.Locomotion.IsGrounded && (!IsAttacking || IsUsingGroundCombo);
        }

        private void BeginPowerCharge()
        {
            holdUsed = true;
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            skillAttackBufferedUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
            queuedAttack = false;
            attackIndex = PowerChargeIndex;
            IsAttacking = true;
            AttackSequence++;
            PowerDrained = 0f;
            FaceNearbyEnemy();
            animator.CrossFadeInFixedTime("Base Layer.PowerAttackStart", .1f, 0, 0f);
        }

        private void TickPowerCharge(IPlayerInput controls)
        {
            PowerDrained += Player.PowerGauge.Drain(powerConfig.drainPerSecond * Time.deltaTime);
            if (!controls.AttackHeld || Player.PowerGauge.Value <= 0f) ReleasePower();
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
            launchedEnemy = null;
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            attackIndex = index;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
            FaceNearbyEnemy(config.skillTargetDistance);
            AttackSequence++;
            IsAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, blendTime, 0, 0f);
        }

        public bool TryUpperAttack()
        {
            if (!Player.CanStartAction(PlayerAction.UpperAttack)) return false;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.UpperAttack"))) return false;
            airCombo.BeginUpperMotion();
            attackIndex = UpperIndex;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
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

        /// <summary>Starts ground combo step 1 out of a dodge (called by DodgeModule at its attack cancel point).</summary>
        public void BeginAttackFromDodge(float blendTime)
        {
            if (IsAttacking || !Player.CanStartAction(PlayerAction.Attack)) return;
            BeginGroundCombo(blendTime);
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
            lastParryInput = float.NegativeInfinity;
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
            lastParryInput = float.NegativeInfinity;
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
            IsAttacking = false; queuedAttack = false; attackIndex = 0;
            airCombo.Fall();
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
                bufferedSkillIndex = -1;
                IsAttacking = false;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                attackIndex = 0;
                PlayAnimState(config.idleAnimStateName);
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

        private void PlayAnimState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return;
            animator.CrossFadeInFixedTime(stateName, config.crossFadeTime);
        }
    }
}
