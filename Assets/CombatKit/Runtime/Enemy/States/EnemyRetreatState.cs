using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 후퇴 상태. Decision이 "후퇴"를 선택했을 때 잠깐 플레이어 반대 방향으로 물러나며,
    /// 그동안에도 플레이어를 계속 바라본다(경계하며 뒷걸음질).
    /// retreatDuration이 지나면 그 자리에서 Observe로 전환해 새 전투 위치를 잡는다.
    /// </summary>
    public class EnemyRetreatState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        private float elapsed;

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyRetreatState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>후퇴 시간을 초기화하고 관찰 이동 블렌드 트리를 재생한다(뒷걸음질 모션 재사용).</summary>
        public void Enter()
        {
            elapsed = 0f;
            combat.PlayObserveAnim();
        }

        /// <summary>
        /// 플레이어를 바라본 채 반대 방향으로 이동하고, 이동 방향을 블렌드 트리에 반영한다.
        /// 후퇴 시간이 끝나면 재배치를 요청하고 관찰 상태로 넘어간다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Transform self = machine.transform;
            Vector3 toPlayer = player.position - self.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 towardPlayer = distance > 0.001f ? toPlayer / distance : self.forward;

            Quaternion targetRotation = Quaternion.LookRotation(towardPlayer);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);

            Vector3 awayFromPlayer = -towardPlayer;
            machine.Navigation.MoveTo(self.position + awayFromPlayer * 2f, combat.RetreatSpeed, .1f);

            Vector3 localMove = self.InverseTransformDirection(machine.Navigation.MoveDirection);
            combat.SetObserveMoveDirection(localMove.x, localMove.z);

            elapsed += deltaTime;
            if (elapsed >= combat.RetreatDuration)
            {
                machine.ObserveState.ForceReposition();
                machine.ChangeState(machine.ObserveState);
            }
        }

        /// <summary>정리할 것이 없다.</summary>
        public void Exit()
        {
        }
    }
}
