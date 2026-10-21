using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 대기 상태. AI 시작 시 진입하며, 플레이어를 감지하면 추적으로, idleDuration이 지나면 순찰로 넘어간다.
    /// </summary>
    public class EnemyIdleState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private float elapsed;

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyIdleState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>대기 시간을 초기화하고 대기 애니메이션을 재생한다.</summary>
        public void Enter()
        {
            elapsed = 0f;
            combat.PlayIdleAnim();
        }

        /// <summary>플레이어가 감지 범위 안이면 추적으로, 대기 시간이 끝나면 순찰로 전환한다.</summary>
        public void Tick(float deltaTime)
        {
            Transform player = combat.PlayerTransform;
            if (player != null)
            {
                Vector3 toPlayer = player.position - machine.transform.position;
                toPlayer.y = 0f;

                if (toPlayer.magnitude <= combat.DetectionRange)
                {
                    machine.ChangeState(machine.ChaseState);
                    return;
                }
            }

            elapsed += deltaTime;

            if (elapsed >= combat.IdleDuration)
            {
                machine.ChangeState(machine.PatrolState);
            }
        }

        /// <summary>정리할 것이 없다.</summary>
        public void Exit()
        {
        }
    }
}
