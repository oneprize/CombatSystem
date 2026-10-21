using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 공격 뒤 곧바로 다시 붙지 않고, 플레이어 정면 부채꼴 안의 전투 위치(combatMinDistance~combatMaxDistance)를
    /// 하나 골라 플레이어를 바라본 채 그 지점으로 이동하며 관찰하는 상태다.
    /// 목표 지점은 (1) 도착했을 때, (2) 플레이어 기준 거리·각도를 크게 벗어났을 때만 다시 고르므로
    /// 플레이어가 움직여도 목표가 매 프레임 따라 움직이지 않는다.
    /// observeDuration이 지나면 Decision 상태로 넘어가 다음 행동을 정하고, 감지 범위를 벗어나면 추적한다.
    /// </summary>
    public class EnemyObserveState : IEnemyState
    {
        #region 필드

        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        private float elapsed;
        /// <summary>현재 목표 지점을 고른 뒤 지난 시간. 목표 재선택 최소 간격 판정에 쓴다.</summary>
        private float targetAge;
        private bool hasTarget;
        private Vector3 targetPosition;

        #endregion

        #region 상태 진입과 진행

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyObserveState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>
        /// 관찰 시간을 초기화하고, 기존 목표가 없거나 무효하거나 이미 도착한 지점이면 새 목표를 고른 뒤
        /// 목표 방향에 맞춘 관찰 이동 애니메이션을 시작한다.
        /// </summary>
        public void Enter()
        {
            elapsed = 0f;

            if (!hasTarget || (combat.PlayerTransform != null && IsTargetInvalid(combat.PlayerTransform)))
            {
                PickNewTargetPosition();
            }
            Vector3 initialDirection = targetPosition - machine.transform.position;
            initialDirection.y = 0;
            if (initialDirection.sqrMagnitude <= combat.RepositionArriveThreshold * combat.RepositionArriveThreshold)
            {
                PickNewTargetPosition();
                initialDirection = targetPosition - machine.transform.position;
                initialDirection.y = 0;
            }
            Vector3 local = machine.transform.InverseTransformDirection(initialDirection.normalized);
            combat.PlayObserveAnim();
            combat.SetObserveMoveDirection(local.x, local.z);
        }

        /// <summary>
        /// Decision이 "재배치"를 선택했을 때 호출해, 다음 Enter()에서 새 목표 지점을 고르게 만든다.
        /// 호출하지 않으면("계속 관찰") 기존 목표 지점을 유지한 채 관찰 시간만 다시 흐른다.
        /// </summary>
        public void ForceReposition()
        {
            hasTarget = false;
        }

        /// <summary>
        /// 플레이어를 바라보며 목표 지점으로 이동하고, 필요하면 목표를 다시 고른다.
        /// 감지 범위를 벗어나면 추적 상태로, 관찰 시간이 끝나면 Decision 상태로 넘어간다.
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
            float distanceToPlayer = toPlayer.magnitude;

            if (distanceToPlayer > combat.DetectionRange)
            {
                machine.ChangeState(machine.ChaseState);
                return;
            }

            targetAge += deltaTime;
            // 저장된 월드 좌표를 유지하고, 크게 벗어났을 때만 최소 간격을 두고 다시 고른다.
            // 적이 아직 목표로 이동 중이라는 이유만으로 목표를 계속 다시 뽑지는 않는다.
            if ((!hasTarget && targetAge >= .3f) || (hasTarget && targetAge >= combat.RepositionMinInterval && IsTargetInvalid(player)))
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

            // 도착했을 때는 무효 목표 재선택 대기 시간을 기다리지 않고,
            // 같은 프레임에 다음 지점을 골라 바로 이동을 이어 간다.
            if (hasTarget && distanceToTarget <= Mathf.Max(combat.RepositionArriveThreshold, .5f))
            {
                PickNewTargetPosition();
                toTarget = targetPosition - self.position;
                toTarget.y = 0f;
                distanceToTarget = toTarget.magnitude;
            }

            Vector3 localMove = Vector3.zero;
            if (hasTarget && distanceToTarget > .001f)
            {
                machine.Navigation.MoveTo(targetPosition, combat.ObserveSpeed, .05f);
                Vector3 moveDir = machine.Navigation.MoveDirection;

                localMove = self.InverseTransformDirection(moveDir);
            }

            combat.SetObserveMoveDirection(localMove.x, localMove.z);

            elapsed += deltaTime;
            if (elapsed >= combat.ObserveDuration)
            {
                machine.ChangeState(machine.DecisionState);
            }
        }

        /// <summary>정리할 것이 없다. 목표 지점은 다음 관찰에서 재사용할 수 있도록 유지한다.</summary>
        public void Exit()
        {
        }

        #endregion

        #region 목표 위치 선택

        /// <summary>플레이어의 수평 정면 방향을 구한다. 정면이 수직에 가까우면 월드 정면을 쓴다.</summary>
        private static Vector3 GetPlanarForward(Transform player)
        {
            Vector3 forward = player.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// 현재 목표 지점이 플레이어 기준 허용 거리(여유 포함)나 정면 각도(여유 포함)를 벗어났는지 판정한다.
        /// </summary>
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

        /// <summary>
        /// 플레이어 정면 부채꼴 안에서 NavMesh로 도달 가능한 후보를 샘플링해, 현재 위치에서 충분히 떨어진 지점을 새 목표로 고른다.
        /// 유효한 후보가 하나도 없으면 목표를 비우고 이동을 멈춘다.
        /// </summary>
        private void PickNewTargetPosition()
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            // forward는 목표를 고르는 순간에만 사용한다. 이후 플레이어의 회전·이동에 목표를 붙이지 않는다.
            Vector3 forward = GetPlanarForward(player);
            float minimumTravel = Mathf.Max(.8f, combat.RepositionArriveThreshold + .2f);
            Vector3 best = player.position + forward * combat.CombatMaxDistance;
            float bestDistance = -1f;
            // 샘플링 횟수를 제한해 이미 도착한 지점을 다시 고르는 일을 피한다.
            // 마지막 몇 번은 부채꼴 모서리를 시도해, 무한 재시도 없이 결정적인 대체 후보를 확보한다.
            for (int i = 0; i < 16; i++)
            {
                float angle = i < 12 ? Random.Range(-combat.CombatHalfAngle, combat.CombatHalfAngle)
                    : (i % 2 == 0 ? -combat.CombatHalfAngle : combat.CombatHalfAngle);
                float distance = i < 12 ? Random.Range(combat.CombatMinDistance, combat.CombatMaxDistance)
                    : (i < 14 ? combat.CombatMinDistance : combat.CombatMaxDistance);
                Vector3 candidate = player.position + Quaternion.Euler(0f, angle, 0f) * forward * distance;
                candidate.y = machine.transform.position.y;
                Vector3 valid;
                if (!machine.Navigation.TryTarget(candidate, out valid)) continue;
                candidate = valid;
                Vector3 sector = candidate - player.position; sector.y = 0f;
                if (sector.magnitude < combat.CombatMinDistance || sector.magnitude > combat.CombatMaxDistance ||
                    Vector3.Angle(forward, sector) > combat.CombatHalfAngle) continue;
                float travel = Vector3.Distance(candidate, machine.transform.position);
                if (travel > bestDistance) { bestDistance = travel; best = candidate; }
                if (travel >= minimumTravel) { best = candidate; break; }
            }
            if (bestDistance < 0f) { hasTarget = false; targetAge = 0f; machine.Navigation.Suspend(); return; }
            targetPosition = best;

            hasTarget = true;
            targetAge = 0f;
        }

        #endregion
    }
}
