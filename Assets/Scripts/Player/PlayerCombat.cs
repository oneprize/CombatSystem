using UnityEngine;
using UnityEngine.InputSystem;

namespace ParrySystem.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Attack")]
        [SerializeField] private string attackAnimStateName = "Attack";
        [SerializeField, Range(0f, 1f)] private float comboTransitionProgress = 0.5f;
        [Header("Attack Facing")]
        [SerializeField, Min(0f)] private float attackTargetDistance = 3f;
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
        public bool IsUsingAirCombo => isAttacking && attackIndex >= 6 && attackIndex <= 11;
        public int AirComboIndex => IsUsingAirCombo ? attackIndex - 6 : -1;
        public bool IsAirComboActive => airCombo != null && airCombo.Active;
        private int attackIndex;
        private bool queuedAttack;
        public int AttackComboIndex => attackIndex;
        public int AttackSequence { get; private set; }
        public bool IsUsingSkill => isAttacking && attackIndex == 3;
        public bool IsUsingSkillA => isAttacking && attackIndex == 4;
        public bool IsUsingUpperAttack => isAttacking && attackIndex == 5;
        public string CurrentAttackStateName => AttackStateName;
        private string AttackStateName => attackIndex >= 6 ? "AirAttack" + (attackIndex - 5) : attackIndex == 5 ? "UpperAttack" : attackIndex == 4 ? "Skill_A" : attackIndex == 3 ? "Skill_G" : attackIndex == 0 ? attackAnimStateName : attackAnimStateName + (attackIndex + 1);

        [Header("Guard (우클릭을 누르고 있는 동안 유지)")]
        [SerializeField] private string guardAnimStateName = "Guard";


        [Header("Animation (visual only, no logic here)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimStateName = "Idle";
        [SerializeField] private float crossFadeTime = 0.1f;

        public bool IsGuarding { get; private set; }
        public bool isAttacking { get; private set; }

        private PlayerSkillDash skillDash;
        private PlayerDodge dodge;
        public bool IsDodging => dodge != null && dodge.IsDodging;
        private PlayerLinkJump linkJump;
        public bool IsLinkJumping => linkJump != null && linkJump.IsTraversing;
        private PlayerDamageReaction damageReaction;
        public bool IsHitReacting => damageReaction != null && damageReaction.IsReacting;

        private void Awake() { skillDash = GetComponent<PlayerSkillDash>(); if (skillDash == null) skillDash = gameObject.AddComponent<PlayerSkillDash>(); airCombo = GetComponent<PlayerAirCombo>(); if (airCombo == null) airCombo = gameObject.AddComponent<PlayerAirCombo>(); dodge = GetComponent<PlayerDodge>(); damageReaction = GetComponent<PlayerDamageReaction>(); linkJump = GetComponent<PlayerLinkJump>(); }

        public void CancelForDamage()
        {
            if (skillDash != null) skillDash.Cancel();
            lastParryInput = float.NegativeInfinity;
            isAttacking = false;
            IsGuarding = false;
            queuedAttack = false;
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
                TryAttack();
            }
        }

        [SerializeField, Min(0f)] private float skillTargetDistance = 4f;

        private void TrySkill()
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || isAttacking || animator == null) return;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.Skill_G"))) return;
            attackIndex = 3;
            queuedAttack = false;
            lastParryInput = float.NegativeInfinity;
            FaceNearbyEnemy(skillTargetDistance);
            AttackSequence++;
            isAttacking = true;
            animator.CrossFadeInFixedTime("Base Layer.Skill_G", crossFadeTime, 0, 0f);
        }

        private void TrySkillA()
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || isAttacking || animator == null) return;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.Skill_A"))) return;
            attackIndex = 4;
            queuedAttack = false;
            lastParryInput = float.NegativeInfinity;
            FaceNearbyEnemy(skillTargetDistance);
            AttackSequence++;
            isAttacking = true;
            skillDash.Begin();
            animator.CrossFadeInFixedTime("Base Layer.Skill_A", crossFadeTime, 0, 0f);
        }

        public bool TryUpperAttack()
        {
            if (IsDodging || IsLinkJumping || IsHitReacting || IsGuarding || isAttacking || animator == null) return false;
            if (!animator.HasState(0, Animator.StringToHash("Base Layer.UpperAttack"))) return false;
            airCombo.BeginUpperMotion();
            attackIndex = 5;
            queuedAttack = false;
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
                if (attackIndex < 2 || IsUsingUpperAttack || (IsUsingAirCombo && AirComboIndex < 5)) queuedAttack = true;
                return;
            }
            if (IsAirComboActive) return;
            if (airCombo != null && airCombo.CanBegin()) { BeginAirAttack(0); return; }
            attackIndex = 0;
            queuedAttack = false;
            FaceNearbyEnemy();
            AttackSequence++;
            isAttacking = true;
            PlayAnimState(AttackStateName);
        }

        private void BeginAirAttack(int index)
        {
            if (index < 0 || index > 5 || !animator.HasState(0, Animator.StringToHash("Base Layer.AirAttack" + (index + 1)))) return;
            if (!airCombo.Active) airCombo.Begin();
            attackIndex = 6 + index;
            queuedAttack = false;
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

            if (queuedAttack && attackIndex < 2 &&
                info.normalizedTime >= comboTransitionProgress)
            {
                FaceNearbyEnemy();
                attackIndex++;
                queuedAttack = false;
                animator.CrossFadeInFixedTime("Base Layer." + AttackStateName, crossFadeTime, 0, 0f);
            }
            else if (info.normalizedTime >= 1f)
            {
                isAttacking = false;
                queuedAttack = false;
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
