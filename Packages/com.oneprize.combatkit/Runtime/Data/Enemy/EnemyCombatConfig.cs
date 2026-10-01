using UnityEngine;
using CombatSystem.Combat;

namespace CombatSystem.Data.Enemy
{
    [CreateAssetMenu(menuName = "CombatKit/Enemy/Enemy Combat Config", fileName = "EnemyCombatConfig")]
    public class EnemyCombatConfig : ScriptableObject
    {
        [Header("Timing (seconds)")]
        public float idleDuration = 2.0f;
        public float staggerDuration = 1.0f;

        [Header("Combo (1~3 약공격, 4 강공격) - Big_Sword 4콤보 클립 길이에 맞춘 타이밍")]
        public ComboStep[] comboSteps = new ComboStep[]
        {
            new ComboStep { animStateName = "Attack.Combo1", windupDuration = 0.5f, parryWindowDuration = 0.25f, recoveryDuration = 0.48f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo2", windupDuration = 0.4f, parryWindowDuration = 0.25f, recoveryDuration = 0.42f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo3", windupDuration = 0.4f, parryWindowDuration = 0.2f, recoveryDuration = 0.4f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo4", windupDuration = 0.6f, parryWindowDuration = 0.25f, recoveryDuration = 0.48f, isStrongAttack = true },
        };

        [Header("Movement (이동↔공격 연동)")]
        public float attackRange = 2.0f;
        public float detectionRange = 6.0f;
        public float moveSpeed = 4.0f;
        public float rotationSpeed = 300f;
        public string moveAnimStateName = "Run";

        [Header("Patrol")]
        public float patrolRadius = 4.0f;
        public float patrolSpeed = 1.5f;
        public string patrolAnimStateName = "Walk";

        [Header("Observe (공격 후 거리를 두고 관찰)")]
        public float combatMinDistance = 2.5f;
        public float combatMaxDistance = 4.0f;
        [Tooltip("플레이어 정면 기준 좌우 목표 선택 각도")]
        [Range(0f, 89f)] public float combatHalfAngle = 60f;
        [Tooltip("작은 방향 변화에는 기존 목표를 유지하는 각도 여유")]
        [Range(0f, 45f)] public float combatAngleTolerance = 15f;
        [Tooltip("자동 목표 재선택의 최소 간격 (초)")]
        [Min(0.1f)] public float repositionMinInterval = 0.75f;
        public float observeSpeed = 1.8f;
        [Tooltip("걷기 모션을 1배속으로 재생할 때의 기준 이동 속도(m/s). 발 미끄러짐을 보며 조절")]
        [Min(0.01f)] public float observeAnimationReferenceSpeed = 1.8f;
        [Tooltip("Observe 발걸음 재생 속도 미세 조절 배율")]
        [Min(0f)] public float observeAnimationSpeedMultiplier = 1f;
        public float observeDuration = 3.0f;
        public float repositionArriveThreshold = 0.4f;
        [Tooltip("목표 위치와 플레이어 사이 거리가 combatMaxDistance보다 이만큼 더 멀어지면 목표 위치를 새로 고른다")]
        public float repositionDriftThreshold = 1.5f;
        public string observeAnimStateName = "ObserveMove";

        [Header("Decision (관찰이 끝난 뒤 다음 행동 결정 - 가중치 랜덤)")]
        public float baseAttackWeight = 50f;
        public float baseContinueObserveWeight = 20f;
        public float baseRepositionWeight = 15f;
        public float baseRetreatWeight = 10f;
        public float baseReapproachWeight = 5f;
        [Tooltip("플레이어가 가드 중일 때 공격 가중치에 곱하는 배율 (패링당할 위험이 크므로 크게 낮춘다)")]
        public float guardingAttackMultiplier = 0.25f;
        [Tooltip("플레이어가 공격 중일 때 공격 가중치에 곱하는 배율 (부딪히지 않게 약간 낮춘다)")]
        public float attackingAttackMultiplier = 0.5f;

        [Header("Retreat (공격 뒤 잠깐 물러나기)")]
        public float retreatDuration = 1.0f;
        public float retreatSpeed = 3.0f;

        [Header("Animation (visual only, no logic here)")]
        public string idleAnimStateName = "Idle";
        public string groggyAnimStateName = "Groggy";
        public float crossFadeTime = 0.1f;

        [Header("Parry Stagger")]
        [Min(1f)] public float maxGroggyGauge = 100f;
        [Min(0f)] public float groggyPerParry = 25f;
    }
}
