using UnityEngine;

namespace CombatSystem.Combat
{
    public class EnemyIdleState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private float elapsed;

        public EnemyIdleState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            elapsed = 0f;
            combat.PlayIdleAnim();
        }

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

        public void Exit()
        {
        }
    }
}
