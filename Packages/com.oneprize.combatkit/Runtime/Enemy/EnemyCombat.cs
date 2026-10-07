using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Core;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    [RequireComponent(typeof(EnemyStateMachine))]
    public class EnemyCombat : MonoBehaviour, ICombatant
    {
        [SerializeField] private EnemyCombatConfig config;

        [Header("Target")]
        [SerializeField] private PlayerCharacter player;

        [Header("Animation (visual only, no logic here)")]
        [SerializeField] private Animator animator;

        public Transform Transform => transform;
        public CombatTeam Team => CombatTeam.Enemy;

        public float IdleDuration => config.idleDuration;


        /// <summary>
        /// 플레이어가 우클릭 방어를 유지하고 있는지. 실제 방어는 무기 접촉 시 처리한다.
        /// </summary>
        public bool IsPlayerGuarding => GetPlayer() != null && GetPlayer().IsGuarding;

        public float AttackRange => config.attackRange;
        public float DetectionRange => config.detectionRange;
        public float MoveSpeed => config.moveSpeed;
        public float RotationSpeed => config.rotationSpeed;
        public float PatrolRadius => config.patrolRadius;
        public float PatrolSpeed => config.patrolSpeed;

        public float CombatMinDistance => config.combatMinDistance;
        public float CombatMaxDistance => config.combatMaxDistance;
        public float CombatHalfAngle => Mathf.Clamp(config.combatHalfAngle, 0f, 89f);
        public float CombatAngleTolerance => Mathf.Clamp(config.combatAngleTolerance, 0f, 45f);
        public float RepositionMinInterval => Mathf.Max(0.1f, config.repositionMinInterval);
        public float ObserveSpeed => config.observeSpeed;
        public float ObserveDuration => config.observeDuration;
        public float RepositionArriveThreshold => config.repositionArriveThreshold;
        public float RepositionDriftThreshold => config.repositionDriftThreshold;

        public float BaseAttackWeight => config.baseAttackWeight;
        public float BaseContinueObserveWeight => config.baseContinueObserveWeight;
        public float BaseRepositionWeight => config.baseRepositionWeight;
        public float BaseRetreatWeight => config.baseRetreatWeight;
        public float BaseReapproachWeight => config.baseReapproachWeight;
        public float GuardingAttackMultiplier => config.guardingAttackMultiplier;
        public float AttackingAttackMultiplier => config.attackingAttackMultiplier;

        public float RetreatDuration => config.retreatDuration;
        public float RetreatSpeed => config.retreatSpeed;

        /// <summary>플레이어가 지금 공격 애니메이션을 재생 중인지 (Decision에서 참조).</summary>
        public bool IsPlayerAttacking => GetPlayer() != null && GetPlayer().IsAttacking;

        public Vector3 HomePosition { get; private set; }
        public Transform PlayerTransform => GetPlayer() != null ? GetPlayer().transform : null;

        /// <summary>
        /// 지금 진행 중인 공격이 콤보4(강공격)인지. 체력/Rebound 시스템에서 참조할 값.
        /// </summary>
        public bool IsCurrentAttackStrong =>
            stateMachine.CurrentState == stateMachine.AttackState && stateMachine.AttackState.CurrentComboStepIsStrong;

        private EnemyStateMachine stateMachine;

        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EnemyCombatConfig>();
                Debug.LogWarning("EnemyCombat: no EnemyCombatConfig assigned, using built-in defaults.", this);
            }

            stateMachine = GetComponent<EnemyStateMachine>();
            HomePosition = transform.position;
        }

        private void OnEnable()
        {
            CombatRegistry.Register(this);
        }

        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
        }

        private PlayerCharacter GetPlayer()
        {
            if (player == null)
            {
                var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
                if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
            }
            return player;
        }

        public ComboStep GetComboStep(int index) => config.comboSteps[index];
        public int ComboStepCount => config.comboSteps.Length;

        public void PlayIdleAnim() => PlayAnimState(config.idleAnimStateName);
        public void PlayMoveAnim() => PlayAnimState(config.moveAnimStateName);
        public void PlayPatrolAnim() => PlayAnimState(config.patrolAnimStateName);
        public void PlayObserveAnim()
        {
            UpdateObservePlaybackSpeed(false);
            PlayAnimState(config.observeAnimStateName);
        }

        private void UpdateObservePlaybackSpeed(bool moving)
        {
            if (animator == null) return;
            bool observing = stateMachine != null && stateMachine.CurrentState == stateMachine.ObserveState;
            // Retreat도 ObserveMove를 사용하므로 Observe에서 걸을 때만 속도를 조절한다.
            float playbackSpeed = observing && moving
                ? Mathf.Max(0f, config.observeSpeed) / Mathf.Max(0.01f, config.observeAnimationReferenceSpeed)
                    * Mathf.Max(0f, config.observeAnimationSpeedMultiplier)
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

            animator.CrossFadeInFixedTime(stateName, config.crossFadeTime);
        }
    }
}
