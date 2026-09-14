using UnityEngine;

namespace ParrySystem.Combat
{
    /// <summary>
    /// 플레이어가 감지 범위(detectionRange) 밖에 있는 동안 스폰 지점(HomePosition) 주변을
    /// patrolRadius 반경 안에서 천천히 배회한다. 감지 범위 안에 들어오면 즉시 Chase로 전환한다.
    /// </summary>
    public class EnemyPatrolState : IEnemyState
    {
        private const float WaypointArriveDistance = 0.3f;

        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private Vector3 waypoint;

        public EnemyPatrolState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            combat.PlayPatrolAnim();
            PickNewWaypoint();
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

            Transform self = machine.transform;
            Vector3 toWaypoint = waypoint - self.position;
            toWaypoint.y = 0f;

            if (toWaypoint.magnitude <= WaypointArriveDistance)
            {
                PickNewWaypoint();
                return;
            }

            Vector3 direction = toWaypoint.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);
            self.position += direction * combat.PatrolSpeed * deltaTime;
        }

        public void Exit()
        {
        }

        private void PickNewWaypoint()
        {
            Vector2 offset = Random.insideUnitCircle * combat.PatrolRadius;
            Vector3 home = combat.HomePosition;
            waypoint = new Vector3(home.x + offset.x, home.y, home.z + offset.y);
        }
    }
}
