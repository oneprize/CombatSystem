using UnityEngine;

namespace ParrySystem.Combat
{
    /// <summary>
    /// 공격 뒤 곧바로 다시 붙지 않고, 플레이어 주변의 전투 위치(combatMinDistance~combatMaxDistance)를
    /// 하나 골라 그 지점으로 이동하며 관찰한다. 플레이어를 매 프레임 직접 쫓아가지 않고,
    /// (1) 목표에 도착했을 때, (2) 거리/정면 각도를 크게 벗어났을 때만
    /// 새 목표 지점을 고른다 - 그래서 플레이어가 움직인다고 목표가 매 프레임 따라오지 않는다.
    /// observeDuration이 지나면 Decision으로 넘어가 다음 행동을 정한다.
    /// </summary>
    public class EnemyObserveState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        private float elapsed;
        private float targetAge;
        private bool hasTarget;
        private Vector3 targetPosition;

        public EnemyObserveState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            elapsed = 0f;
            combat.PlayObserveAnim();

            if (!hasTarget || (combat.PlayerTransform != null && IsTargetInvalid(combat.PlayerTransform)))
            {
                PickNewTargetPosition();
            }
        }

        /// <summary>
        /// Decision이 "위치 변경"을 선택했을 때 호출한다 - 다음 Enter()에서 새 목표 지점을 고르게 만든다.
        /// 호출하지 않으면("계속 관찰") 기존 목표 지점을 그대로 유지한 채 관찰 시간만 다시 흐른다.
        /// </summary>
        public void ForceReposition()
        {
            hasTarget = false;
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
            float distanceToPlayer = toPlayer.magnitude;

            if (distanceToPlayer > combat.DetectionRange)
            {
                machine.ChangeState(machine.ChaseState);
                return;
            }

            targetAge += deltaTime;
            // 저장된 월드 좌표를 유지한다. 크게 벗어났을 때만, 최소 간격을 두고 재선택한다.
            // 적이 아직 복귀 중이라는 이유만으로 목표를 계속 다시 뽑지는 않는다.
            if (!hasTarget || (targetAge >= combat.RepositionMinInterval && IsTargetInvalid(player)))
            {
                PickNewTargetPosition();
            }

            // 이동 방향과 무관하게 항상 플레이어를 바라본다.
            Vector3 towardPlayer = distanceToPlayer > 0.001f ? toPlayer / distanceToPlayer : self.forward;
            Quaternion targetRotation = Quaternion.LookRotation(towardPlayer);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);

            // 플레이어가 아니라 "골라둔 전투 위치"를 향해 이동한다.
            Vector3 toTarget = targetPosition - self.position;
            toTarget.y = 0f;
            float distanceToTarget = toTarget.magnitude;

            Vector3 localMove;
            if (distanceToTarget > combat.RepositionArriveThreshold)
            {
                // moveDir은 항상 단위 벡터라 ObserveSpeed를 아무리 낮춰도 블렌드 트리에는
                // "이 방향으로 이동 중"이라는 신호가 그대로 들어간다 (실제 이동 거리로 판단하면
                // 속도가 느릴 때 한 프레임 이동량이 너무 작아져서 Idle로 오인될 수 있다).
                Vector3 moveDir = toTarget / distanceToTarget;
                self.position += moveDir * Mathf.Min(combat.ObserveSpeed * deltaTime, distanceToTarget);
                localMove = self.InverseTransformDirection(moveDir);
            }
            else
            {
                // 목표 지점에 도착했다 - 맴도는 느낌을 위해 새 지점을 하나 더 고른다.
                if (targetAge >= combat.RepositionMinInterval)
                {
                    PickNewTargetPosition();
                }
                localMove = Vector3.zero;
            }

            combat.SetObserveMoveDirection(localMove.x, localMove.z);

            elapsed += deltaTime;
            if (elapsed >= combat.ObserveDuration)
            {
                machine.ChangeState(machine.DecisionState);
            }
        }

        public void Exit()
        {
        }

        private static Vector3 GetPlanarForward(Transform player)
        {
            Vector3 forward = player.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private bool IsTargetInvalid(Transform player)
        {
            Vector3 offset = targetPosition - player.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float drift = Mathf.Max(0f, combat.RepositionDriftThreshold);
            if (distance > combat.CombatMaxDistance + drift ||
                distance < Mathf.Max(0f, combat.CombatMinDistance - drift))
            {
                return true;
            }

            return Vector3.Angle(GetPlanarForward(player), offset) >
                combat.CombatHalfAngle + combat.CombatAngleTolerance;
        }

        private void PickNewTargetPosition()
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            // forward는 목표를 선택하는 순간에만 사용한다. 이후 회전/이동에 목표를 붙이지 않는다.
            Vector3 forward = GetPlanarForward(player);
            float angle = Random.Range(-combat.CombatHalfAngle, combat.CombatHalfAngle);
            float distance = Random.Range(combat.CombatMinDistance, combat.CombatMaxDistance);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * forward * distance;

            targetPosition = player.position + offset;
            targetPosition.y = machine.transform.position.y;
            hasTarget = true;
            targetAge = 0f;
        }
    }
}
