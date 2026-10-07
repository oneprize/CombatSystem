using System;
using UnityEngine;

namespace CombatSystem.Data.Player
{
    /// <summary>
    /// Every tuning value for one player character, grouped by module. One asset per
    /// character; assign it to PlayerCharacter.
    /// </summary>
    [CreateAssetMenu(menuName = "CombatKit/Player/Player Profile", fileName = "PlayerProfile")]
    public class PlayerProfile : ScriptableObject
    {
        public CombatSettings combat = new CombatSettings();
        public LocomotionSettings locomotion = new LocomotionSettings();
        public HealthSettings health = new HealthSettings();
        public DodgeSettings dodge = new DodgeSettings();
        public LinkJumpSettings linkJump = new LinkJumpSettings();
        public AirComboSettings airCombo = new AirComboSettings();
        public PiercingDashSettings piercingDash = new PiercingDashSettings();
        public GatherSkillSettings gatherSkill = new GatherSkillSettings();
        public AttackHeightOffsetSettings attackHeightOffset = new AttackHeightOffsetSettings();
        public LockOnSettings lockOn = new LockOnSettings();
        public PowerAttackSettings powerAttack = new PowerAttackSettings();

        [Serializable]
        public class PowerAttackSettings
        {
            [Header("Charge")]
            [Min(.01f)] public float holdThreshold = .3f;
            [Tooltip("Total button hold time at which damage stops increasing. Holding longer keeps charging.")]
            [Min(.31f)] public float maxChargeDuration = 5f;
            [Tooltip("Seconds after charge entry before incoming hits stop interrupting.")]
            [Min(0f)] public float armorAfterSeconds = .7f;
            [Header("Release")]
            [Min(1f)] public float minDamageMultiplier = 2f;
            [Min(1f)] public float maxDamageMultiplier = 5f;
            [Tooltip("Normalized time of the dash slashes in PowerAttackEnd.")]
            public Vector2 dashHitWindow = new Vector2(.03f, .36f);
            [Min(.1f)] public float dashHitRadius = 1.2f;
            [Tooltip("Normalized time of the closing slam in PowerAttackEnd.")]
            public Vector2 slamHitWindow = new Vector2(.42f, .50f);
            [Min(.1f)] public float slamHitRadius = 3f;
        }

        [Serializable]
        public class LockOnSettings
        {
            [Tooltip("Enemies within this distance can be locked on to.")]
            [Min(0f)] public float acquireRange = 20f;
            [Tooltip("The lock breaks (or moves to the nearest other enemy) beyond this distance.")]
            [Min(0f)] public float keepRange = 25f;
        }

        [Serializable]
        public class CombatSettings
        {
            [Header("Attack")]
            public string attackAnimStateName = "Attack";
            [Tooltip("Fallback chain point for steps without an entry in comboChainProgress.")]
            [Range(0f, 1f)] public float comboTransitionProgress = 0.5f;
            [Tooltip("Normalized time per ground combo step at which a queued attack chains into the next step. " +
                     "Set to where the clip's pose matches the next clip's first frame (before the recovery).")]
            public float[] comboChainProgress = { .25f, .30f, .30f };
            [Tooltip("Blend time (seconds) from one ground combo step into the next.")]
            [Min(0f)] public float comboCrossFadeTime = .1f;
            [Tooltip("Blend duration from a finished ground attack back to Idle.")]
            [Min(0f)] public float groundAttackIdleBlendTime = .35f;
            [Header("Attack to dodge")]
            [Tooltip("Normalized time per ground combo step after which a dodge may cancel the attack (end of the swing).")]
            public float[] dodgeCancelProgress = { .25f, .30f, .30f, .40f };
            [Header("Attack Facing")]
            [Min(0f)] public float attackTargetDistance = 10f;
            [Min(0f)] public float attackTargetHeightTolerance = 2f;
            [Header("Air combo")]
            [Range(0, 1)] public float upperAirChainProgress = .22f;
            [Tooltip("Fallback chain point for air steps without an entry in airComboChainProgress.")]
            [Range(0, 1)] public float airChainProgress = .95f;
            [Tooltip("Normalized time per air combo step at which a queued attack chains into the next step. " +
                     "Set to where the clip's pose matches the next clip's first frame (before the recovery).")]
            public float[] airComboChainProgress = { .40f, .33f, .40f };
            [Tooltip("Horizontal gap kept to an airborne attack target; root motion closer than this pushes the target along.")]
            [Min(0f)] public float airAttackContactDistance = 1.1f;
            [Header("Skill C chase (launch -> leap -> air combo)")]
            [Tooltip("Skill C progress from which a buffered attack leaps after the launched enemy (just after the launch hit).")]
            [Range(0, 1)] public float skillCChaseProgress = .45f;
            [Tooltip("Chase leap progress from which a queued attack chains into air combo step 1.")]
            [Range(0, 1)] public float chaseAirChainProgress = .75f;
            [Header("Skill to normal attack")]
            [Min(0f)] public float skillAttackInputBuffer = .4f;
            [Range(0f, 1f)] public float skillGFollowUpProgress = .75f;
            [Range(0f, 1f)] public float skillAFollowUpProgress = .65f;
            [Range(0f, 1f)] public float skillBFollowUpProgress = .65f;
            [Min(0f)] public float skillFollowUpBlendTime = .08f;
            [Header("Boss parry")]
            [Min(0f)] public float bossParryInputWindow = .15f;
            [Header("E hit follow-up")]
            [Min(.05f)] public float eFollowUpInputWindow = .8f;
            [Range(0f, 1f)] public float eFollowUpComboProgress = .6f;
            public Vector2 eFollowUpHitWindow = new Vector2(.18f, .42f);
            [Header("Normal attack to skill")]
            [Min(0f)] public float normalSkillInputBuffer = .4f;
            [Range(0f, 1f)] public float normalSkillTransitionProgress = .6f;
            [Min(0f)] public float normalSkillBlendTime = .08f;
            [Header("Guard (우클릭을 누르고 있는 동안 유지)")]
            public string guardAnimStateName = "Guard";
            [Header("Animation")]
            public string idleAnimStateName = "Idle";
            public float crossFadeTime = 0.1f;
            [Header("Skill targeting")]
            [Min(0f)] public float skillTargetDistance = 10f;
        }

        [Serializable]
        public class LocomotionSettings
        {
            public float walkSpeed = 3f;
            public float runSpeed = 6f;
            public float rotationSpeed = 720f;
            public float jumpForce = 5f;
            public float gravity = -9.81f;
            public float groundCheckDistance = 0.2f; // 바닥 체크용 거리 (캡슐 바닥 기준)
            [Tooltip("Seconds airborne without jumping (walking off a ledge) before the fall animation plays and the first jump counts as used.")]
            [Min(0f)] public float fallStartDelay = 0.15f;
            public float turningHoldTime = 0.2f;
        }

        [Serializable]
        public class HealthSettings
        {
            public string playerName = "Player";
            [Min(1)] public float maxHealth = 100f;
            [Min(0)] public float weakAttackDamage = 10f;
            [Min(0)] public float strongAttackDamage = 20f;
        }

        [Serializable]
        public class DodgeSettings
        {
            [Min(0f)] public float crossFadeTime = 0.06f;
            [Min(0f)] public float distanceMultiplier = 1f;
            [Header("Dodge to attack")]
            [Tooltip("Normalized dodge time at which an attack pressed during the dodge starts the ground combo.")]
            [Range(0f, 1f)] public float attackCancelProgress = .7f;
            [Min(0f)] public float attackBlendTime = .15f;
        }

        [Serializable]
        public class LinkJumpSettings
        {
            [Min(0.1f)] public float activationDistance = 1.2f;
            [Min(0.1f)] public float endpointHeightTolerance = 1f;
            [Min(0.1f)] public float travelSpeed = 5f;
            [Min(0f)] public float arcHeight = 1.6f;
            [Min(0f)] public float takeoffTime = 0.18f;
        }

        [Serializable]
        public class AirComboSettings
        {
            [Min(.1f)] public float fallGravity = 18f;
            [Min(.1f)] public float maximumHoldTime = 10f;
            [Min(1f)] public float finisherFallSpeed = 12f;
            [Min(1f)] public float finisherFallGravity = 40f;
            [Min(.01f)] public float landingAttackBuffer = .3f;
        }

        [Serializable]
        public class PiercingDashSettings
        {
            [Min(0)] public float distance = 5f;
            [Min(.01f)] public float duration = .22f;
            [Range(0, 1)] public float startProgress = 15f / 45f;
            [Min(.05f)] public float hitRadius = .7f;
            [Header("Skill B (R)")]
            [Min(0)] public float skillBDistance = 5f;
            [Min(.01f)] public float skillBDuration = .28f;
            [Range(0, 1)] public float skillBStartProgress = .32f;
        }

        [Serializable]
        public class GatherSkillSettings
        {
            [Range(0, 1)] public float recallProgress = .36f;
            [Min(0)] public float range = 5f;
            [Range(0, 90)] public float halfAngle = 30f;
            [Min(.05f)] public float pullDuration = .35f;
            [Min(.5f)] public float gatherDistance = 1.5f;
            [Min(.1f)] public float spacing = .9f;
            [Min(0)] public float heightTolerance = 2f;
        }

        [Serializable]
        public class AttackHeightOffsetSettings
        {
            [Tooltip("Combo1 원본의 발 기준과 플레이어 몸 중심 기준 사이 높이 차이(m)")]
            public float heightOffset = 0.95f;
            public float combo2HeightOffset = 0.95f;
            public float combo3HeightOffset = 0.19f;
            [Tooltip("플레이어 외형 전체를 아래로 내리는 높이(m)")]
            [Min(0f)] public float visualLowering = 0.12f;
            [Min(0f)] public float guardHeightOffset = 0.23f;
            [Min(0f)] public float guardAcceptHeightOffset = 0.25f;
        }
    }
}
