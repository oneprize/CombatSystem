using UnityEngine;

namespace ParrySystem.Combat
{
    /// <summary>
    /// Decision이 "후퇴"를 선택했을 때 잠깐 플레이어 반대 방향으로 물러난다.
    /// 물러나는 동안에도 플레이어를 계속 바라본다 (경계하며 뒷걸음질).
    /// retreatDuration이 지나면 그 자리에서 다시 Observe로 전환해 새 전투 위치를 잡는다.
    /// </summary>
    public class EnemyRetreatState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        private float elapsed;

        public EnemyRetreatState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            elapsed = 0f;
            combat.PlayObserveAnim();
        }

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

            // 물러나는 동안에도 계속 플레이어를 바라본다.
            Quaternion targetRotation = Quaternion.LookRotation(towardPlayer);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);

            Vector3 awayFromPlayer = -towardPlayer;
            self.position += awayFromPlayer * combat.RetreatSpeed * deltaTime;

            Vector3 localMove = self.InverseTransformDirection(awayFromPlayer);
            combat.SetObserveMoveDirection(localMove.x, localMove.z);

            elapsed += deltaTime;
            if (elapsed >= combat.RetreatDuration)
            {
                machine.ObserveState.ForceReposition();
                machine.ChangeState(machine.ObserveState);
            }
        }

        public void Exit()
        {
        }
    }
}
