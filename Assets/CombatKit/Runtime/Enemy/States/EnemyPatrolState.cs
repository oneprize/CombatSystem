using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 순찰 상태. 플레이어가 감지 범위(detectionRange) 밖에 있는 동안 스폰 지점(HomePosition) 주변
    /// patrolRadius 반경 안을 천천히 배회하고, 감지 범위 안에 들어오면 즉시 Chase로 전환한다.
    /// </summary>
    public class EnemyPatrolState : IEnemyState
    {
        private const float WaypointArriveDistance = 0.3f;

        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private Vector3 waypoint;

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyPatrolState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>걷기 애니메이션을 재생하고 첫 순찰 지점을 고른다.</summary>
        public void Enter()
        {
            combat.PlayPatrolAnim();
            PickNewWaypoint();
        }

        /// <summary>
        /// 플레이어를 감지하면 추적으로 전환하고, 아니면 순찰 지점으로 이동·회전한다.
        /// 지점에 도착했거나 경로를 만들 수 없으면 새 지점을 고른다.
        /// </summary>
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

            if (!machine.Navigation.MoveTo(waypoint, combat.PatrolSpeed, .1f)) { PickNewWaypoint(); return; }
            Vector3 direction = machine.Navigation.MoveDirection;
            if (direction.sqrMagnitude < .001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);

        }

        /// <summary>정리할 것이 없다.</summary>
        public void Exit()
        {
        }

        /// <summary>스폰 지점을 중심으로 순찰 반경 안의 임의 지점을 다음 순찰 지점으로 정한다.</summary>
        private void PickNewWaypoint()
        {
            Vector2 offset = Random.insideUnitCircle * combat.PatrolRadius;
            Vector3 home = combat.HomePosition;
            waypoint = new Vector3(home.x + offset.x, home.y, home.z + offset.y);
        }
    }
}
