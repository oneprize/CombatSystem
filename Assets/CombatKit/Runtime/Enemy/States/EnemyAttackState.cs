using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 적의 공격 상태. 첫 공격은 오프닝 점프 공격으로 한 번만 쓰고, 이후에는 콤보 1~4(1~3 약공격, 4 강공격)를 순서대로 이어 간다.
    /// 단계 사이의 실제 애니메이션 전환은 Animator Controller의 Exit Time 트랜지션이 담당하므로,
    /// 이 클래스는 진입 시 이어 갈 콤보 클립으로 한 번만 크로스페이드하고 이후에는 애니메이터 진행률로
    /// 현재 단계와 종료를 판정한다(애니메이션 이벤트는 사용하지 않는다).
    /// </summary>
    public class EnemyAttackState : IEnemyState
    {
        #region 필드와 프로퍼티

        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private readonly Animator animator;

        // 피격으로 끊겨도 리셋하지 않는다. 콤보 전체를 끊기지 않고 마쳤을 때만 0으로 돌아간다.
        private int comboIndex;

        /// <summary>오프닝 점프 공격을 이미 사용했는지 여부(적마다 한 번만 사용).</summary>
        private bool openingUsed;
        /// <summary>오프닝 점프 공격의 루트 모션 배율. 플레이어와의 거리에 맞춰 도약 거리를 줄인다.</summary>
        public float OpeningMotionScale { get; private set; } = 1f;
        public bool IsOpeningAttack { get; private set; }
        public bool OpeningAttackPending => !openingUsed;
        public int ComboIndex => comboIndex;
        /// <summary>공격 상태에 진입할 때마다 증가하는 번호. 히트박스가 공격 단위로 중복 타격을 초기화하는 데 쓴다.</summary>
        public int AttackSequence { get; private set; }
        /// <summary>현재 공격이 강공격인지 여부. 오프닝 점프 공격도 강공격으로 취급한다.</summary>
        public bool CurrentComboStepIsStrong => IsOpeningAttack || CurrentStep.isStrongAttack;

        private ComboStep CurrentStep => combat.GetComboStep(comboIndex);

        #endregion

        #region 상태 진입과 진행

        /// <summary>상태 머신과 EnemyCombat 참조를 받고 애니메이터를 캐시한다.</summary>
        public EnemyAttackState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
            animator = machine.GetComponent<Animator>();
        }

        /// <summary>
        /// 공격을 시작한다. 오프닝 공격을 아직 안 썼으면 거리 기반 도약 배율을 계산해 오프닝 공격을, 아니면 이어 갈 콤보 단계를 재생한다.
        /// </summary>
        public void Enter()
        {
            AttackSequence++;
            IsOpeningAttack = !openingUsed;
            if (IsOpeningAttack)
            {
                openingUsed = true;
                Vector3 offset = combat.PlayerTransform == null ? Vector3.zero : combat.PlayerTransform.position - machine.transform.position;
                offset.y = 0f;
                OpeningMotionScale = Mathf.Clamp01((offset.magnitude - 1.1f) / 4.1f);
            }
            // 공격을 시작하는 순간 플레이어 쪽으로 즉시 방향을 맞춘다. Chase/Decision에서 넘어오며
            // 덜 돈 각도가 남아 있어도 첫 프레임부터 정면으로 공격하게 하기 위함이다.
            FacePlayerInstant();
            // 애니메이션은 여기서 한 번만 크로스페이드하고, 단계 사이 전환은 Animator Controller가 처리한다.
            combat.PlayAnimState(IsOpeningAttack ? combat.OpeningAttackStateName : CurrentStep.animStateName);
        }

        /// <summary>
        /// 오프닝 공격은 클립이 끝나면 관찰 상태로 넘어간다. 콤보 공격은 플레이어를 따라 회전하면서
        /// 애니메이터가 재생 중인 단계를 추적하고, 마지막 단계를 완주하면 콤보를 초기화하고 관찰 상태로 넘어간다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (IsOpeningAttack)
            {
                // 도약 방향은 고정한다. 플레이어가 도약 공격을 회피할 수 있게 하기 위함이다.
                AnimatorStateInfo opening;
                if (TryGetOpeningProgress(out opening) && opening.normalizedTime >= 1f)
                {
                    comboIndex = 0;
                    machine.ChangeState(machine.ObserveState);
                }
                return;
            }
            // 콤보가 진행되는 내내(단계가 바뀌는 순간에도) 플레이어가 움직이면 계속 따라 바라본다.
            FacePlayerTowards(deltaTime);

            // 크로스페이드 중에는 다음 상태를 우선해 화면의 콤보와 판정을 동기화한다.
            AnimatorStateInfo animation;
            int playingCombo;
            if (!TryGetAttackProgress(out animation, out playingCombo))
            {
                return;
            }

            comboIndex = playingCombo;
            float progress = animation.normalizedTime;

            // 중간 단계는 애니메이터 전환을 기다리고, 마지막 단계만 완주한 뒤 종료한다.
            if (comboIndex == combat.ComboStepCount - 1 && progress >= 1f)
            {
                comboIndex = 0;
                machine.ChangeState(machine.ObserveState);
            }
        }

        #endregion

        #region 애니메이션 진행 판정

        /// <summary>오프닝 공격 애니메이터 상태 정보를 얻는다. 전환 중이면 다음 상태를 우선한다.</summary>
        /// <returns>오프닝 공격 중이고 해당 상태가 재생(또는 전환 대상) 중이면 true.</returns>
        public bool TryGetOpeningProgress(out AnimatorStateInfo state)
        {
            state = default(AnimatorStateInfo);
            if (!IsOpeningAttack || animator == null) return false;
            string name = "Base Layer." + combat.OpeningAttackStateName;
            if (animator.IsInTransition(0))
            {
                state = animator.GetNextAnimatorStateInfo(0);
                if (state.IsName(name)) return true;
            }
            state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(name);
        }

        /// <summary>
        /// 재생 중인 콤보 단계와 그 애니메이터 상태 정보를 얻는다. 전환 중이면 다음 상태를 우선하며,
        /// 현재 진행 단계보다 앞선 단계로 되돌아간 것으로는 인정하지 않는다.
        /// </summary>
        /// <returns>현재 단계 이상의 콤보 상태가 재생 중이면 true.</returns>
        private bool TryGetAttackProgress(out AnimatorStateInfo animation, out int index)
        {
            animation = default(AnimatorStateInfo);
            index = -1;
            if (animator == null || !animator.isActiveAndEnabled ||
                animator.runtimeAnimatorController == null)
            {
                return false;
            }

            if (animator.IsInTransition(0))
            {
                animation = animator.GetNextAnimatorStateInfo(0);
                index = FindCombo(animation);
                if (index >= comboIndex)
                {
                    return true;
                }
            }

            animation = animator.GetCurrentAnimatorStateInfo(0);
            index = FindCombo(animation);
            return index >= comboIndex;
        }

        /// <summary>애니메이터 상태가 몇 번째 콤보 단계인지 찾는다(레이어 접두사 유무 모두 허용). 없으면 -1.</summary>
        private int FindCombo(AnimatorStateInfo animation)
        {
            for (int i = 0; i < combat.ComboStepCount; i++)
            {
                string stateName = combat.GetComboStep(i).animStateName;
                if (animation.IsName(stateName) ||
                    animation.IsName("Base Layer." + stateName))
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region 상태 종료와 방향 전환

        /// <summary>오프닝 공격 플래그만 해제한다. 콤보 단계는 유지해 다음 공격에서 이어 간다.</summary>
        public void Exit()
        {
            IsOpeningAttack = false;
            // comboIndex는 여기서 리셋하지 않는다. 피격으로 끊겨도 다음 공격에서 이어 간다.
        }

        /// <summary>플레이어 쪽으로 즉시 수평 회전한다.</summary>
        private void FacePlayerInstant()
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Vector3 toPlayer = player.position - machine.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            machine.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }

        /// <summary>설정된 회전 속도로 플레이어 쪽을 향해 서서히 회전한다.</summary>
        private void FacePlayerTowards(float deltaTime)
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Vector3 toPlayer = player.position - machine.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized);
            machine.transform.rotation = Quaternion.RotateTowards(machine.transform.rotation, targetRotation, combat.RotationSpeed * deltaTime);
        }

        #endregion
    }
}
