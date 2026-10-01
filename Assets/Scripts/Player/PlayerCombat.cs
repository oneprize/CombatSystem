using UnityEngine;
using UnityEngine.InputSystem;

namespace ParrySystem.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private string attackAnimStateName = "Attack";
        [SerializeField, Range(0f, 1f)] private float comboTransitionProgress = 0.85f;
        [Header("Attack Facing")]
        [SerializeField, Min(0f)] private float attackTargetDistance = 10f;
        [SerializeField, Min(0f)] private float attackTargetHeightTolerance = 2f;
        [Header("Parry Input")]
        [SerializeField, Min(0f)] private float parryInputWindow = 0.15f;
        private float lastParryInput = float.NegativeInfinity;
        public bool IsParryInputActive => !IsHitReacting && Time.time - lastParryInput <= parryInputWindow;
        public void RegisterParryInput()
        {
            if (!IsHitReacting) lastParryInput = Time.time;
        }
        public bool TryConsumeParry()
        {
            if (!IsParryInputActive) return false;
            lastParryInput = float.NegativeInfinity;
            return true;
        }
        [Header("Air combo")]
        [SerializeField, Range(0, 1)] private float upperAirChainProgress = .22f;
        [SerializeField, Range(0, 1)] private float airChainProgress = .95f;
        private PlayerAirCombo airCombo;
        public bool IsUsingAirCombo => isAttacking && attackIndex >= AirStartIndex && attackIndex < AirStartIndex + 6;
        public int AirComboIndex => IsUsingAirCombo ? attackIndex - AirStartIndex : -1;
        public bool IsAirComboActive => airCombo != null && airCombo.Active;
        private const int GroundComboCount = 5;
        private const int SkillGIndex = 5, SkillAIndex = 6, UpperIndex = 7, AirStartIndex = 8, SkillBIndex = 14, SkillIIndex = 15, SkillCIndex = 16;
        public bool IsUsingGroundCombo => isAttacking && attackIndex >= 0 && attackIndex < GroundComboCount;
        private int attackIndex;
        private bool queuedAttack;
        [Header("Skill to normal attack")]
        [SerializeField, Min(0f)] private float skillAttackInputBuffer = .4f;
        [SerializeField, Range(0f, 1f)] private float skillGFollowUpProgress = .75f;
        [SerializeField, Range(0f, 1f)] private float skillAFollowUpProgress = .65f;
        [SerializeField, Range(0f, 1f)] private float skillBFollowUpProgress = .65f;
        [SerializeField, Min(0f)] private float skillFollowUpBlendTime = .08f;
        [Header("Normal attack to skill")]
        [SerializeField, Min(0f)] private float normalSkillInputBuffer = .4f;
        [SerializeField, Range(0f, 1f)] private float normalSkillTransitionProgress = .6f;
        [SerializeField, Min(0f)] private float normalSkillBlendTime = .08f;
        private int bufferedSkillIndex = -1;
        private float bufferedSkillUntil = float.NegativeInfinity;
        private float skillAttackBufferedUntil = float.NegativeInfinity;
        public int AttackComboIndex => attackIndex;
        public int AttackSequence { get; private set; }
        public bool IsUsingSkill => isAttacking && attackIndex == SkillGIndex;
        public bool IsUsingSkillA => isAttacking && attackIndex == SkillAIndex;
        public bool IsUsingSkillC => isAttacking && attackIndex == SkillCIndex;
        public bool IsUsingSkillI => isAttacking && attackIndex == SkillIIndex;
        public bool IsUsingSkillB => isAttacking && attackIndex == SkillBIndex;
        public bool IsUsingPiercingSkill => IsUsingSkillA || IsUsingSkillB;
        public bool IsUsingUpperAttack => isAttacking && attackIndex == UpperIndex;
        public string CurrentAttackStateName => AttackStateName;
        private string AttackStateName => attackIndex == SkillCIndex ? "Skill_C" : attackIndex == SkillIIndex ? "Skill_I" : attackIndex == SkillBIndex ? "Skill_B" : attackIndex >= AirStartIndex ? "AirAttack" + (attackIndex - AirStartIndex + 1) : attackIndex == UpperIndex ? "UpperAttack" : attackIndex == SkillAIndex ? "Skill_A" : attackIndex == SkillGIndex ? "Skill_G" : attackIndex == 0 ? attackAnimStateName : attackAnimStateName + (attackIndex + 1);

        [Header("Guard (우클릭을 누르고 있는 동안 유지)")]
        [SerializeField] private string guardAnimStateName = "Guard";


        [Header("Animation (visual only, no logic here)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimStateName = "Idle";
        [SerializeField] private float crossFadeTime = 0.1f;

        public bool IsGuarding { get; private set; }
        public bool isAttacking { get; private set; }

        private PlayerDodge dodge;
        public bool IsDodging => dodge != null && dodge.IsDodging;
        private PlayerLinkJump linkJump;
        public bool IsLinkJumping => linkJump != null && linkJump.IsTraversing;
        private PlayerDamageReaction damageReaction;
        public bool IsHitReacting => damageReaction != null && damageReaction.IsReacting;

        private void Awake() { if (GetComponent<PlayerGatherSkill>() == null) gameObject.AddComponent<PlayerGatherSkill>(); if (GetComponent<PlayerPiercingDash>() == null) gameObject.AddComponent<PlayerPiercingDash>(); airCombo = GetComponent<PlayerAirCombo>(); if (airCombo == null) airCombo = gameObject.AddComponent<PlayerAirCombo>(); dodge = GetComponent<PlayerDodge>(); damageReaction = GetComponent<PlayerDamageReaction>(); linkJump = GetComponent<PlayerLinkJump>(); }

        public void CancelForDamage()
        {
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
            isAttacking = false;
            IsGuarding = false;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            attackIndex = 0;
            animator.SetBool("isAttacking", false);
            animator.SetBool("isGuarding", false);
            if (airCombo != null && airCombo.Active) airCombo.Fall();
        }



        private void Start()
        {
            PlayAnimState(idleAnimStateName);
        }

        private void Update()
        {
            if (IsHitReacting || IsLinkJumping || IsDodging || (airCombo != null && airCombo.Recovering)) return;
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

            if (Mouse.current.rightButton.isPressed && !IsGuarding && !IsAirComboActive)
                StartGuard();
            else if (!Mouse.current.rightButton.isPressed && IsGuarding)
                StopGuard();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                RegisterParryInput();
                if (Keyboard.current != null && Keyboard.current.wKey.isPressed && !IsAirComboActive && !IsUsingUpperAttack)
                    TrySkillC();
                else
                    TryAttack();
            }
        }

        [SerializeField, Min(0f)] private float skillTargetDistance = 10f;

        private void TrySkill() { RequestSkill(SkillGIndex); }
        private void TrySkillA() { RequestSkill(SkillAIndex); }
        public void TrySkillB() { RequestSkill(SkillBIndex); }
        public void TrySkillI() { RequestSkill(SkillIIndex); }
        public void TrySkillC() { RequestSkill(SkillCIndex); }

        private void RequestSkill(int index)
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || animator == null ||
                IsAirComboActive || (airCombo != null && airCombo.Recovering)) return;
            string state = index == SkillCIndex ? "Skill_C" : index == SkillIIndex ? "Skill_I" : index == SkillGIndex ? "Skill_G" : index == SkillAIndex ? "Skill_A" : "Skill_B";
            if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return;
            if (IsUsingGroundCombo)
            {
                bufferedSkillIndex = index;
                bufferedSkillUntil = Time.time + normalSkillInputBuffer;
                UpdateAttackProgress();
                return;
            }
            if (isAttacking) return;
            BeginSkill(index, crossFadeTime);
        }

        private void BeginSkill(int index, float blendTime)
        {
            bufferedSkillIndex = -1;
            bufferedSkillUntil = float.NegativeInfinity;
            attackIndex = index;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
            FaceNearbyEnemy(skillTargetDistance);
            AttackSequence++;
            isAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, blendTime, 0, 0f);
        }

        public bool TryUpperAttack()
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || isAttacking || animator == null) return false;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.UpperAttack"))) return false;
            airCombo.BeginUpperMotion();
            attackIndex = UpperIndex;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            lastParryInput = float.NegativeInfinity;
            FaceNearbyEnemy(attackTargetDistance);
            AttackSequence++;
            isAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer.UpperAttack", crossFadeTime, 0, 0f);
            return true;
        }

        public void TryAttack()
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || animator == null) return;
            if (isAttacking)
            {
                if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillI || IsUsingSkillC)
                {
                    skillAttackBufferedUntil = Time.time + skillAttackInputBuffer;
                    UpdateAttackProgress();
                    return;
                }
                if ((IsUsingGroundCombo && attackIndex < GroundComboCount - 1) || IsUsingUpperAttack || (IsUsingAirCombo && AirComboIndex < 5)) queuedAttack = true;
                return;
            }
            if (IsAirComboActive) return;
            if (airCombo != null && airCombo.CanBegin()) { BeginAirAttack(0); return; }
            attackIndex = 0;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            FaceNearbyEnemy();
            AttackSequence++;
            isAttacking = true;
            PlayAnimState(AttackStateName);
        }

        private void BeginAirAttack(int index)
        {
            if (index < 0 || index > 5 || !animator.HasState(0, Animator.StringToHash("Base Layer.AirAttack" + (index + 1)))) return;
            if (!airCombo.Active) airCombo.Begin();
            attackIndex = AirStartIndex + index;
            queuedAttack = false;
            skillAttackBufferedUntil = float.NegativeInfinity;
            isAttacking = true;
            lastParryInput = float.NegativeInfinity;
            AttackSequence++;
            FaceNearbyEnemy(skillTargetDistance);
            animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, .08f, 0, 0);
        }

        public void EndAirCombo()
        {
            if (!IsAirComboActive) return;
            isAttacking = false; queuedAttack = false; attackIndex = 0;
            airCombo.Fall();
        }

        private void FaceNearbyEnemy(float targetDistance = -1f)
        {
            float range = targetDistance < 0f ? attackTargetDistance : targetDistance;
            if (range <= 0f) return;
            Transform target = null;
            float nearestSquared = range * range;
            foreach (var enemy in FindObjectsByType<ParrySystem.Combat.EnemyDamageReaction>(FindObjectsSortMode.None))
            {
                if (!enemy.isActiveAndEnabled) continue;
                Vector3 direction = enemy.transform.position - transform.position;
                if (Mathf.Abs(direction.y) > attackTargetHeightTolerance) continue;
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
            if (!isAttacking || animator == null) return;
            string expected = "Base Layer." + AttackStateName;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(expected)) info = next;
            }
            if (!info.IsName(expected)) return;

            if (bufferedSkillIndex >= 0 && Time.time > bufferedSkillUntil) bufferedSkillIndex = -1;
            if (IsUsingGroundCombo && bufferedSkillIndex >= 0 && info.normalizedTime >= normalSkillTransitionProgress)
            {
                BeginSkill(bufferedSkillIndex, normalSkillBlendTime);
                return;
            }

            if (IsUsingSkill || IsUsingPiercingSkill || IsUsingSkillI || IsUsingSkillC)
            {
                float followUp = IsUsingSkillC ? .75f : IsUsingSkillI ? .75f : IsUsingSkill ? skillGFollowUpProgress : IsUsingSkillA ? skillAFollowUpProgress : skillBFollowUpProgress;
                var dash = GetComponent<PlayerPiercingDash>();
                bool dashFinished = !IsUsingPiercingSkill || dash == null || !dash.IsDashing;
                if (Time.time <= skillAttackBufferedUntil && info.normalizedTime >= followUp && dashFinished)
                {
                    attackIndex = 0;
                    queuedAttack = false;
                    skillAttackBufferedUntil = float.NegativeInfinity;
                    FaceNearbyEnemy();
                    AttackSequence++;
                    animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, skillFollowUpBlendTime, 0, 0f);
                    return;
                }
            }

            if (IsUsingUpperAttack && queuedAttack && info.normalizedTime >= upperAirChainProgress &&
                info.normalizedTime < .48f && airCombo.CanBegin())
            {
                BeginAirAttack(0);
                return;
            }
            if (IsUsingAirCombo)
            {
                if (queuedAttack && AirComboIndex < 5 && info.normalizedTime >= airChainProgress)
                    BeginAirAttack(AirComboIndex + 1);
                else if (info.normalizedTime >= 1) EndAirCombo();
                return;
            }

            if (queuedAttack && IsUsingGroundCombo && attackIndex < GroundComboCount - 1 &&
                info.normalizedTime >= comboTransitionProgress)
            {
                FaceNearbyEnemy();
                attackIndex++;
                AttackSequence++;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, crossFadeTime, 0, 0f);
            }
            else if (info.normalizedTime >= 1f)
            {
                bufferedSkillIndex = -1;
                isAttacking = false;
                queuedAttack = false;
                skillAttackBufferedUntil = float.NegativeInfinity;
                attackIndex = 0;
                PlayAnimState(idleAnimStateName);
            }
        }

        private void StartGuard()
        {
            CancelForDamage();
            IsGuarding = true;
            PlayAnimState(guardAnimStateName);
            Debug.Log("[PlayerCombat] Guard ON");
        }

        private void StopGuard()
        {
            IsGuarding = false;
            PlayAnimState(idleAnimStateName);
            Debug.Log("[PlayerCombat] Guard OFF");
        }

        public void PlayGuardAccept()
        {
            if (!IsGuarding || animator == null) return;
            animator.CrossFadeInFixedTime("Base Layer.GuardAccept", 0.04f, 0, 0f);
        }

        private void PlayAnimState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            animator.CrossFadeInFixedTime(stateName, crossFadeTime);
        }
    }
}
