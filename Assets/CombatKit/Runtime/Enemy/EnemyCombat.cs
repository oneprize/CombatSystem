using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Core;
using CombatSystem.Data.Enemy;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 적 AI의 설정값·대상(플레이어) 조회·애니메이션 재생을 한곳에 모은 허브 컴포넌트다.
    /// 각 상태 클래스는 config를 직접 보지 않고 이 컴포넌트를 통해 값을 읽고 애니메이션을 요청한다.
    /// </summary>
    [RequireComponent(typeof(EnemyStateMachine))]
    public class EnemyCombat : MonoBehaviour, ICombatant
    {
        #region 필드

        [SerializeField] private EnemyCombatConfig config;

        [Header("Target")]
        [SerializeField] private PlayerCharacter player;

        [Header("Animation (visual only, no logic here)")]
        [SerializeField] private Animator animator;

        #endregion

        #region ICombatant

        public Transform Transform => transform;
        public CombatTeam Team => CombatTeam.Enemy;

        #endregion

        #region 설정값 패스스루

        public float IdleDuration => config.idleDuration;

        /// <summary>
        /// 플레이어가 가드 입력을 유지하고 있는지 여부. 실제 가드 성공 여부는 무기 접촉 시점에 판정된다.
        /// </summary>
        public bool IsPlayerGuarding => GetPlayer() != null && GetPlayer().IsGuarding;

        public string OpeningAttackStateName => config.openingAttackStateName;
        public float OpeningAttackRange => config.openingAttackRange;
        public Vector2 OpeningHitWindow => config.openingHitWindow;
        public float OpeningHitRadius => config.openingHitRadius;
        public float AttackRange => config.attackRange;
        public float DetectionRange => config.detectionRange;
        public float MoveSpeed => config.moveSpeed;
        public float RotationSpeed => config.rotationSpeed;
        public float PatrolRadius => config.patrolRadius;
        public float PatrolSpeed => config.patrolSpeed;

        public float CombatMinDistance => config.combatMinDistance;
        public float CombatMaxDistance => config.combatMaxDistance;
        /// <summary>관찰 위치를 고를 때 플레이어 정면 기준 좌우로 허용하는 각도(0~89도로 제한).</summary>
        public float CombatHalfAngle => Mathf.Clamp(config.combatHalfAngle, 0f, 89f);
        /// <summary>방향 변화가 이 각도 이내면 기존 관찰 목표를 유지하는 여유 각도(0~45도로 제한).</summary>
        public float CombatAngleTolerance => Mathf.Clamp(config.combatAngleTolerance, 0f, 45f);
        public float RepositionMinInterval => Mathf.Max(0.1f, config.repositionMinInterval);
        public float ObserveSpeed => config.observeSpeed;
        public float ObserveDuration => config.observeDuration;
        public float RepositionArriveThreshold => config.repositionArriveThreshold;
        /// <summary>목표 위치가 플레이어로부터 CombatMaxDistance보다 이만큼 더 멀어지면 목표를 다시 고른다.</summary>
        public float RepositionDriftThreshold => config.repositionDriftThreshold;

        public float BaseAttackWeight => config.baseAttackWeight;
        public float BaseContinueObserveWeight => config.baseContinueObserveWeight;
        public float BaseRepositionWeight => config.baseRepositionWeight;
        public float BaseRetreatWeight => config.baseRetreatWeight;
        public float BaseReapproachWeight => config.baseReapproachWeight;
        /// <summary>플레이어가 가드 중일 때 공격 결정 가중치에 곱하는 배율.</summary>
        public float GuardingAttackMultiplier => config.guardingAttackMultiplier;
        /// <summary>플레이어가 공격 중일 때 공격 결정 가중치에 곱하는 배율.</summary>
        public float AttackingAttackMultiplier => config.attackingAttackMultiplier;

        public float RetreatDuration => config.retreatDuration;
        public float RetreatSpeed => config.retreatSpeed;

        #endregion

        #region 런타임 상태

        /// <summary>플레이어가 지금 공격 애니메이션을 재생 중인지 여부. Decision 상태의 가중치 계산에서 참조한다.</summary>
        public bool IsPlayerAttacking => GetPlayer() != null && GetPlayer().IsAttacking;

        /// <summary>스폰 시점의 위치. 순찰 상태가 이 지점을 중심으로 돌아다닌다.</summary>
        public Vector3 HomePosition { get; private set; }
        /// <summary>현재 대상 플레이어의 Transform. 플레이어를 찾지 못하면 null이다.</summary>
        public Transform PlayerTransform => GetPlayer() != null ? GetPlayer().transform : null;

        /// <summary>
        /// 지금 진행 중인 공격이 강공격(콤보4 또는 오프닝 점프 공격)인지 여부. 체력/Rebound 시스템에서 참조하기 위한 값이다.
        /// </summary>
        public bool IsCurrentAttackStrong =>
            stateMachine.CurrentState == stateMachine.AttackState && stateMachine.AttackState.CurrentComboStepIsStrong;

        private EnemyStateMachine stateMachine;

        #endregion

        #region Unity 생명주기

        /// <summary>config가 비어 있으면 기본값 인스턴스로 대체하고, 상태 머신 참조와 순찰 기준 위치를 확보한다.</summary>
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

        /// <summary>활성화될 때 CombatRegistry에 적으로 등록해 다른 액터가 찾을 수 있게 한다.</summary>
        private void OnEnable()
        {
            CombatRegistry.Register(this);
        }

        /// <summary>비활성화될 때 CombatRegistry에서 등록을 해제한다.</summary>
        private void OnDisable()
        {
            CombatRegistry.Unregister(this);
        }

        #endregion

        #region 플레이어 탐색

        /// <summary>
        /// 대상 플레이어를 돌려준다. 인스펙터에서 지정되지 않았으면 CombatRegistry에서 가장 가까운 플레이어를 찾아 캐시한다.
        /// </summary>
        private PlayerCharacter GetPlayer()
        {
            if (player == null)
            {
                var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
                if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
            }
            return player;
        }

        #endregion

        #region 콤보 데이터와 애니메이션

        /// <summary>설정된 콤보 단계 중 index번째 단계 데이터를 돌려준다.</summary>
        public ComboStep GetComboStep(int index) => config.comboSteps[index];
        public int ComboStepCount => config.comboSteps.Length;

        /// <summary>대기 애니메이션으로 크로스페이드한다.</summary>
        public void PlayIdleAnim() => PlayAnimState(config.idleAnimStateName);
        /// <summary>추적용 달리기 애니메이션으로 크로스페이드한다.</summary>
        public void PlayMoveAnim() => PlayAnimState(config.moveAnimStateName);
        /// <summary>순찰용 걷기 애니메이션으로 크로스페이드한다.</summary>
        public void PlayPatrolAnim() => PlayAnimState(config.patrolAnimStateName);
        /// <summary>관찰 이동 블렌드 트리로 크로스페이드한다. 진입 직후에는 정지 상태로 보고 재생 속도를 1배로 둔다.</summary>
        public void PlayObserveAnim()
        {
            UpdateObservePlaybackSpeed(false);
            PlayAnimState(config.observeAnimStateName);
        }

        /// <summary>
        /// 관찰 상태에서 걷는 중이면 이동 속도에 맞춰 발걸음 재생 속도를 조절해 발 미끄러짐을 줄이고, 그 외에는 1배속으로 되돌린다.
        /// </summary>
        private void UpdateObservePlaybackSpeed(bool moving)
        {
            if (animator == null) return;
            bool observing = stateMachine != null && stateMachine.CurrentState == stateMachine.ObserveState;
            // Retreat도 ObserveMove 상태를 함께 쓰므로, 관찰 상태에서 걸을 때만 재생 속도를 조절한다.
            float playbackSpeed = observing && moving
                ? Mathf.Max(0f, config.observeSpeed) / Mathf.Max(0.01f, config.observeAnimationReferenceSpeed)
                    * Mathf.Max(0f, config.observeAnimationSpeedMultiplier)
                : 1f;
            animator.SetFloat("observePlaybackSpeed", playbackSpeed);
        }

        /// <summary>
        /// 관찰/후퇴 중 적 기준 로컬 이동 방향을 ObserveMove 블렌드 트리(moveX/moveZ)에 반영한다.
        /// 적은 항상 플레이어를 바라보므로, 옆이나 뒤로 움직이면 그 방향에 맞는 클립으로 자연스럽게 블렌드된다.
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

        /// <summary>지정한 애니메이터 상태로 설정된 크로스페이드 시간만큼 전환한다. 상태명이 비어 있으면 무시한다.</summary>
        public void PlayAnimState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
            {
                return;
            }

            animator.CrossFadeInFixedTime(stateName, config.crossFadeTime);
        }

        #endregion
    }
}
