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

        [Serializable]
        public class CombatSettings
        {
            [Header("Attack")]
            public string attackAnimStateName = "Attack";
            [Range(0f, 1f)] public float comboTransitionProgress = 0.5f;
            [Header("Attack Facing")]
            [Min(0f)] public float attackTargetDistance = 10f;
            [Min(0f)] public float attackTargetHeightTolerance = 2f;
            [Header("Parry Input")]
            [Min(0f)] public float parryInputWindow = 0.15f;
            [Header("Air combo")]
            [Range(0, 1)] public float upperAirChainProgress = .22f;
            [Range(0, 1)] public float airChainProgress = .95f;
            [Header("Skill to normal attack")]
            [Min(0f)] public float skillAttackInputBuffer = .4f;
            [Range(0f, 1f)] public float skillGFollowUpProgress = .75f;
            [Range(0f, 1f)] public float skillAFollowUpProgress = .65f;
            [Range(0f, 1f)] public float skillBFollowUpProgress = .65f;
            [Min(0f)] public float skillFollowUpBlendTime = .08f;
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
            public float groundCheckDistance = 0.2f; // 바닥 체크용 거리
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
