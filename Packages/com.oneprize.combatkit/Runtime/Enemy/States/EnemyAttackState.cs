using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 콤보 1~4를 순서대로 이어간다 (1~3 약공격, 4 강공격).
    /// 스텝 사이의 실제 애니메이션 전환은 Animator Controller의 Exit Time 트랜지션이 담당한다.
    /// 이 클래스는 Attack 상태에 처음 진입할 때 이어갈 콤보에 맞는 클립으로 한 번만 CrossFade하고,
    /// 이후에는 애니메이션을 건드리지 않은 채 Animator 진행률로 강공격 여부와 종료를 판정한다
    /// (애니메이션 이벤트 사용 안 함).
    /// </summary>
    public class EnemyAttackState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private readonly Animator animator;

        // 피격으로 끊겨도 리셋하지 않는다 - 전체 콤보를 안 끊기고 다 마쳤을 때만 0으로 돌아간다.
        private int comboIndex;

        public int ComboIndex => comboIndex;
        public int AttackSequence { get; private set; }
        public bool CurrentComboStepIsStrong => CurrentStep.isStrongAttack;

        private ComboStep CurrentStep => combat.GetComboStep(comboIndex);

        public EnemyAttackState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
            animator = machine.GetComponent<Animator>();
        }

        public void Enter()
        {
            AttackSequence++;
            // 공격을 시작하는 순간 바로 플레이어 쪽으로 각도를 딱 맞춘다 (Chase/Decision에서 넘어오며
            // 아직 다 돌지 못한 각도가 남아있어도 첫 프레임부터 정면으로 공격하도록).
            FacePlayerInstant();
            // 애니메이션은 여기서 딱 한 번만 CrossFade하고, 스텝 사이 전환은 Animator Controller가 처리한다.
            combat.PlayAnimState(CurrentStep.animStateName);
        }

        public void Tick(float deltaTime)
        {
            // 콤보가 진행되는 내내(스텝이 바뀌는 순간에도) 플레이어가 움직이면 계속 따라 바라본다.
            FacePlayerTowards(deltaTime);

            // CrossFade 중에는 다음 상태를 우선하여 화면의 콤보와 판정을 동기화한다.
            AnimatorStateInfo animation;
            int playingCombo;
            if (!TryGetAttackProgress(out animation, out playingCombo))
            {
                return;
            }

            comboIndex = playingCombo;
            float progress = animation.normalizedTime;

            // 중간 콤보는 Animator 전환을 기다리고 마지막 콤보만 완주 후 종료한다.
            if (comboIndex == combat.ComboStepCount - 1 && progress >= 1f)
            {
                comboIndex = 0;
                machine.ChangeState(machine.ObserveState);
            }
        }

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

        public void Exit()
        {
            // comboIndex는 여기서 리셋하지 않는다 - 피격으로 끊겨도 다음 공격에서 이어간다.
        }

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
    }
}
