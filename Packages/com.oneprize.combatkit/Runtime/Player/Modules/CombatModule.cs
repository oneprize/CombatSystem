using UnityEngine;
using UnityEngine.InputSystem;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Ground combo, skills, upper/air attack chaining, guard and parry input.
    /// </summary>
    public sealed class CombatModule : PlayerModule
    {
        private const int GroundComboCount = 5;
        private const int SkillGIndex = 5, SkillAIndex = 6, UpperIndex = 7, AirStartIndex = 8, SkillBIndex = 14, SkillIIndex = 15, SkillCIndex = 16;

        private PlayerProfile.CombatSettings config;
        private float lastParryInput = float.NegativeInfinity;
        private int attackIndex;
        private bool queuedAttack;
        private int bufferedSkillIndex = -1;
        private float bufferedSkillUntil = float.NegativeInfinity;
        private float skillAttackBufferedUntil = float.NegativeInfinity;

        public bool IsGuarding { get; private set; }
        public bool IsAttacking { get; private set; }
        public int AttackComboIndex => attackIndex;
        public int AttackSequence { get; private set; }

        public bool IsParryInputActive => !Player.IsHitReacting && Time.time - lastParryInput <= config.parryInputWindow;
        public bool IsUsingGroundCombo => IsAttacking && attackIndex >= 0 && attackIndex < GroundComboCount;
        public bool IsUsingAirCombo => IsAttacking && attackIndex >= AirStartIndex && attackIndex < AirStartIndex + 6;
        public int AirComboIndex => IsUsingAirCombo ? attackIndex - AirStartIndex : -1;
        public bool IsUsingSkill => IsAttacking && attackIndex == SkillGIndex;
        public bool IsUsingSkillA => IsAttacking && attackIndex == SkillAIndex;
        public bool IsUsingSkillB => IsAttacking && attackIndex == SkillBIndex;
        public bool IsUsingSkillC => IsAttacking && attackIndex == SkillCIndex;
        public bool IsUsingSkillI => IsAttacking && attackIndex == SkillIIndex;
        public bool IsUsingPiercingSkill => IsUsingSkillA || IsUsingSkillB;
        public bool IsUsingUpperAttack => IsAttacking && attackIndex == UpperIndex;
        public string CurrentAttackStateName => AttackStateName;
        private string AttackStateName => attackIndex == SkillCIndex ? "Skill_C" : attackIndex == SkillIIndex ? "Skill_I" : attackIndex == SkillBIndex ? "Skill_B" : attackIndex >= AirStartIndex ? "AirAttack" + (attackIndex - AirStartIndex + 1) : attackIndex == UpperIndex ? "UpperAttack" : attackIndex == SkillAIndex ? "Skill_A" : attackIndex == SkillGIndex ? "Skill_G" : attackIndex == 0 ? config.attackAnimStateName : config.attackAnimStateName + (attackIndex + 1);

        private AirComboModule airCombo => Player.AirCombo;

        protected override void OnInitialize()
        {
            config = Player.Profile.combat;
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
            UpdateAttackProgress();
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                TrySkill();
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TrySkillA();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                TrySkillB();
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                TrySkillI();
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                TryUpperAttack();

            if (Mouse.current == null)
            {
                return;
            }

            if (Mouse.current.rightButton.isPressed && !IsGuarding && !Player.IsAirComboActive)
                StartGuard();
            else if (!Mouse.current.rightButton.isPressed && IsGuarding)
                StopGuard();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                RegisterParryInput();
                if (Keyboard.current != null && Keyboard.current.wKey.isPressed && !Player.IsAirComboActive && !IsUsingUpperAttack)
                    TrySkillC();
                else
                    TryAttack();
            }
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
                if ((IsUsingGroundCombo && attackIndex < GroundComboCount - 1) || IsUsingUpperAttack || (IsUsingAirCombo && AirComboIndex < 5)) queuedAttack = true;
                return;
            }
            if (Player.IsAirComboActive) return;
            if (airCombo.CanBegin()) { BeginAirAttack(0); return; }
            attackIndex = 0;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            FaceNearbyEnemy();
            AttackSequence++;
            IsAttacking = true;
            PlayAnimState(AttackStateName);
        }

        private void BeginAirAttack(int index)
        {
            if (index < 0 || index > 5 || !animator.HasState(0, Animator.StringToHash("Base Layer.AirAttack" + (index + 1)))) return;
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

        public void EndAirCombo()
        {
            if (!Player.IsAirComboActive) return;
            IsAttacking = false; queuedAttack = false; attackIndex = 0;
            airCombo.Fall();
        }

        private void FaceNearbyEnemy(float targetDistance = -1f)
        {
            float range = targetDistance < 0f ? config.attackTargetDistance : targetDistance;
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
                if (queuedAttack && AirComboIndex < 5 && info.normalizedTime >= config.airChainProgress)
                    BeginAirAttack(AirComboIndex + 1);
                else if (info.normalizedTime >= 1) EndAirCombo();
                return;
            }

            if (queuedAttack && IsUsingGroundCombo && attackIndex < GroundComboCount - 1 &&
                info.normalizedTime >= config.comboTransitionProgress)
            {
                FaceNearbyEnemy();
                attackIndex++;
                AttackSequence++;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, config.crossFadeTime, 0, 0f);
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
