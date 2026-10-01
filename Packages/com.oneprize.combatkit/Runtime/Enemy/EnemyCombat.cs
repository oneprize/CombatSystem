using UnityEngine;
using ParrySystem.Player;

namespace ParrySystem.Combat
{
    [RequireComponent(typeof(EnemyStateMachine))]
    public class EnemyCombat : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        [SerializeField] private float idleDuration = 2.0f;
        [SerializeField] private float staggerDuration = 1.0f;

        [Header("Combo (1~3 약공격, 4 강공격) - Big_Sword 4콤보 클립 길이에 맞춘 타이밍")]
        [SerializeField]
        private ComboStep[] comboSteps = new ComboStep[]
        {
            new ComboStep { animStateName = "Attack.Combo1", windupDuration = 0.5f, parryWindowDuration = 0.25f, recoveryDuration = 0.48f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo2", windupDuration = 0.4f, parryWindowDuration = 0.25f, recoveryDuration = 0.42f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo3", windupDuration = 0.4f, parryWindowDuration = 0.2f, recoveryDuration = 0.4f, isStrongAttack = false },
            new ComboStep { animStateName = "Attack.Combo4", windupDuration = 0.6f, parryWindowDuration = 0.25f, recoveryDuration = 0.48f, isStrongAttack = true },
        };

        [Header("Movement (이동↔공격 연동)")]
        [SerializeField] private float attackRange = 2.0f;
        [SerializeField] private float detectionRange = 6.0f;
        [SerializeField] private float moveSpeed = 4.0f;
        [SerializeField] private float rotationSpeed = 300f;
        [SerializeField] private string moveAnimStateName = "Run";

        [Header("Patrol")]
        [SerializeField] private float patrolRadius = 4.0f;
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private string patrolAnimStateName = "Walk";

        [Header("Observe (공격 후 거리를 두고 관찰)")]
        [SerializeField] private float combatMinDistance = 2.5f;
        [SerializeField] private float combatMaxDistance = 4.0f;
        [Tooltip("플레이어 정면 기준 좌우 목표 선택 각도")]
        [SerializeField, Range(0f, 89f)] private float combatHalfAngle = 60f;
        [Tooltip("작은 방향 변화에는 기존 목표를 유지하는 각도 여유")]
        [SerializeField, Range(0f, 45f)] private float combatAngleTolerance = 15f;
        [Tooltip("자동 목표 재선택의 최소 간격 (초)")]
        [SerializeField, Min(0.1f)] private float repositionMinInterval = 0.75f;
        [SerializeField] private float observeSpeed = 1.8f;
        [Tooltip("걷기 모션을 1배속으로 재생할 때의 기준 이동 속도(m/s). 발 미끄러짐을 보며 조절")]
        [SerializeField, Min(0.01f)] private float observeAnimationReferenceSpeed = 1.8f;
        [Tooltip("Observe 발걸음 재생 속도 미세 조절 배율")]
        [SerializeField, Min(0f)] private float observeAnimationSpeedMultiplier = 1f;
        [SerializeField] private float observeDuration = 3.0f;
        [SerializeField] private float repositionArriveThreshold = 0.4f;
        [Tooltip("목표 위치와 플레이어 사이 거리가 combatMaxDistance보다 이만큼 더 멀어지면 목표 위치를 새로 고른다")]
        [SerializeField] private float repositionDriftThreshold = 1.5f;
        [SerializeField] private string observeAnimStateName = "ObserveMove";

        [Header("Decision (관찰이 끝난 뒤 다음 행동 결정 - 가중치 랜덤)")]
        [SerializeField] private float baseAttackWeight = 50f;
        [SerializeField] private float baseContinueObserveWeight = 20f;
        [SerializeField] private float baseRepositionWeight = 15f;
        [SerializeField] private float baseRetreatWeight = 10f;
        [SerializeField] private float baseReapproachWeight = 5f;
        [Tooltip("플레이어가 가드 중일 때 공격 가중치에 곱하는 배율 (패링당할 위험이 크므로 크게 낮춘다)")]
        [SerializeField] private float guardingAttackMultiplier = 0.25f;
        [Tooltip("플레이어가 공격 중일 때 공격 가중치에 곱하는 배율 (부딪히지 않게 약간 낮춘다)")]
        [SerializeField] private float attackingAttackMultiplier = 0.5f;

        [Header("Retreat (공격 뒤 잠깐 물러나기)")]
        [SerializeField] private float retreatDuration = 1.0f;
        [SerializeField] private float retreatSpeed = 3.0f;

        [Header("Target")]
        [SerializeField] private PlayerCombat player;

        [Header("Animation (visual only, no logic here)")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimStateName = "Idle";
        [SerializeField] private string groggyAnimStateName = "Groggy";
        [SerializeField] private float crossFadeTime = 0.1f;

        public float IdleDuration => idleDuration;
        public float StaggerDuration => staggerDuration;

        public bool IsParryWindowOpen { get; private set; }

        /// <summary>
        /// 플레이어가 우클릭 방어를 유지하고 있는지. 실제 방어는 무기 접촉 시 처리한다.
        /// </summary>
        public bool IsPlayerGuarding => player != null && player.IsGuarding;

        public float AttackRange => attackRange;
        public float DetectionRange => detectionRange;
        public float MoveSpeed => moveSpeed;
        public float RotationSpeed => rotationSpeed;
        public float PatrolRadius => patrolRadius;
        public float PatrolSpeed => patrolSpeed;

        public float CombatMinDistance => combatMinDistance;
        public float CombatMaxDistance => combatMaxDistance;
        public float CombatHalfAngle => Mathf.Clamp(combatHalfAngle, 0f, 89f);
        public float CombatAngleTolerance => Mathf.Clamp(combatAngleTolerance, 0f, 45f);
        public float RepositionMinInterval => Mathf.Max(0.1f, repositionMinInterval);
        public float ObserveSpeed => observeSpeed;
        public float ObserveDuration => observeDuration;
        public float RepositionArriveThreshold => repositionArriveThreshold;
        public float RepositionDriftThreshold => repositionDriftThreshold;

        public float BaseAttackWeight => baseAttackWeight;
        public float BaseContinueObserveWeight => baseContinueObserveWeight;
        public float BaseRepositionWeight => baseRepositionWeight;
        public float BaseRetreatWeight => baseRetreatWeight;
        public float BaseReapproachWeight => baseReapproachWeight;
        public float GuardingAttackMultiplier => guardingAttackMultiplier;
        public float AttackingAttackMultiplier => attackingAttackMultiplier;

        public float RetreatDuration => retreatDuration;
        public float RetreatSpeed => retreatSpeed;

        /// <summary>플레이어가 지금 공격 애니메이션을 재생 중인지 (Decision에서 참조).</summary>
        public bool IsPlayerAttacking => player != null && player.isAttacking;

        public Vector3 HomePosition { get; private set; }
        public Transform PlayerTransform => player != null ? player.transform : null;

        /// <summary>
        /// 지금 진행 중인 공격이 콤보4(강공격)인지. 체력/Rebound 시스템에서 참조할 값.
        /// </summary>
        public bool IsCurrentAttackStrong =>
            stateMachine.CurrentState == stateMachine.AttackState && stateMachine.AttackState.CurrentComboStepIsStrong;

        private EnemyStateMachine stateMachine;

        private void Awake()
        {
            stateMachine = GetComponent<EnemyStateMachine>();
            HomePosition = transform.position;
            if (player == null) player = FindFirstObjectByType<PlayerCombat>();
        }

        public ComboStep GetComboStep(int index) => comboSteps[index];
        public int ComboStepCount => comboSteps.Length;

        public void SetParryWindowOpen(bool open)
        {
            IsParryWindowOpen = open;
        }

        [Header("Parry Stagger")]
        [SerializeField, Min(1f)] private float maxGroggyGauge = 100f;
        [SerializeField, Min(0f)] private float groggyPerParry = 25f;
        public float GroggyGauge { get; private set; }
        public float MaxGroggyGauge => Mathf.Max(1f, maxGroggyGauge);

        public void RegisterParry()
        {
            if (stateMachine.CurrentState != stateMachine.AttackState) return;
            GroggyGauge = Mathf.Min(MaxGroggyGauge, GroggyGauge + groggyPerParry);
            if (GroggyGauge >= MaxGroggyGauge)
            {
                SetParryWindowOpen(false);
                stateMachine.ChangeState(stateMachine.GroggyState);
            }
        }

        public void ResetGroggyGauge() { GroggyGauge = 0f; }

        public void PlayIdleAnim() => PlayAnimState(idleAnimStateName);
        public void PlayGroggyAnim() => PlayAnimState(groggyAnimStateName);
        public void PlayMoveAnim() => PlayAnimState(moveAnimStateName);
        public void PlayPatrolAnim() => PlayAnimState(patrolAnimStateName);
        public void PlayObserveAnim()
        {
            UpdateObservePlaybackSpeed(false);
            PlayAnimState(observeAnimStateName);
        }

        private void UpdateObservePlaybackSpeed(bool moving)
        {
            if (animator == null) return;
            bool observing = stateMachine != null && stateMachine.CurrentState == stateMachine.ObserveState;
            // Retreat도 ObserveMove를 사용하므로 Observe에서 걸을 때만 속도를 조절한다.
            float playbackSpeed = observing && moving
                ? Mathf.Max(0f, observeSpeed) / Mathf.Max(0.01f, observeAnimationReferenceSpeed)
                    * Mathf.Max(0f, observeAnimationSpeedMultiplier)
                : 1f;
            animator.SetFloat("observePlaybackSpeed", playbackSpeed);
        }

        /// <summary>
        /// Observe/Retreat 중 실제 이동 방향(월드 스페이스)을 적 기준 로컬 좌표로 바꿔
        /// ObserveMove 블렌드 트리(moveX/moveZ)에 반영한다. 정면 기준이 아니라 항상
        /// 플레이어를 바라보는 상태에서의 로컬 좌표라, 옆/뒤로 움직이면 그에 맞는
        /// 방향별 클립으로 자연스럽게 블렌드된다.
        /// </summary>
        public void SetObserveMoveDirection(float localX, float localZ)
        {
            if (animator == null)
            {
                return;
            }

            UpdateObservePlaybackSpeed(localX * localX + localZ * localZ > 0.0001f);
            animator.SetFloat("moveX", localX);
            animator.SetFloat("moveZ", localZ);
        }

        public void PlayAnimState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            animator.CrossFadeInFixedTime(stateName, crossFadeTime);
        }
    }
}
