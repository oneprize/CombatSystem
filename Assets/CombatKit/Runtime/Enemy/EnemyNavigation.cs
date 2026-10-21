using UnityEngine;
using UnityEngine.AI;

namespace CombatSystem.Combat
{
    /// <summary>
    /// NavMeshAgent를 경로 계산 전용으로 감싸는 적 이동 보조 컴포넌트다. 에이전트는 필요할 때만 NavMesh에 붙이고,
    /// 회전·제동은 끈 채 원하는 이동 방향만 상태 클래스에 제공한다. 넉백 등 다른 이동이 필요하면 Suspend로 분리한다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        #region 필드

        private NavMeshAgent agent;
        private EnemyStateMachine machine;
        private Vector3 destination;
        /// <summary>유효한 목적지 경로를 가지고 있는지, NavMesh 부재 경고를 이미 출력했는지 여부.</summary>
        private bool hasDestination, warned;
        /// <summary>경로 재계산과 NavMesh 재부착을 너무 자주 시도하지 않도록 하는 다음 허용 시각.</summary>
        private float nextRepath, nextAttach;
        private NavMeshPath path;
        /// <summary>에이전트가 원하는 이동 방향(정규화). 경로가 없거나 NavMesh에 없으면 0 벡터다.</summary>
        public Vector3 MoveDirection => Ready && agent.desiredVelocity.sqrMagnitude > .001f
            ? agent.desiredVelocity.normalized : Vector3.zero;
        private bool Ready => agent != null && agent.enabled && agent.isOnNavMesh;

        #endregion

        #region NavMesh 연결

        /// <summary>에이전트가 회전·제동을 직접 하지 않도록 설정하고, 실제로 필요해질 때까지 비활성 상태로 둔다.</summary>
        private void Awake()
        {
            path = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            machine = GetComponent<EnemyStateMachine>();
            agent.updateRotation = false;
            agent.autoBraking = false;
            agent.enabled = false;
        }

        /// <summary>경로를 지우고 에이전트를 비활성화해 NavMesh 이동을 멈춘다. 넉백·공격처럼 다른 방식으로 움직일 때 호출한다.</summary>
        public void Suspend()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (Ready) { agent.ResetPath(); agent.velocity = Vector3.zero; }
            agent.enabled = false;
            hasDestination = false;
        }

        /// <summary>
        /// 발밑 가까이에 도달 가능한 NavMesh가 있으면 에이전트를 활성화해 그 위치로 붙인다.
        /// 실패하면 일정 간격 뒤에 다시 시도하며, NavMesh가 없다는 경고는 한 번만 출력한다.
        /// </summary>
        /// <returns>에이전트가 NavMesh 위에서 사용 가능한 상태면 true.</returns>
        private bool Attach()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (Ready) return true;
            if (Time.time < nextAttach) return false;
            nextAttach = Time.time + .3f;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, 1.5f, agent.areaMask) ||
                Mathf.Abs(hit.position.y - transform.position.y) > .65f ||
                !ClearLine(transform.position + Vector3.up * .6f, hit.position + Vector3.up * .6f, null))
            {
                if (!warned) { Debug.LogWarning("Enemy needs a reachable baked NavMesh near its feet: " + name, this); warned = true; }
                return false;
            }
            agent.enabled = true;
            if (!agent.Warp(hit.position)) { agent.enabled = false; return false; }
            agent.updateRotation = false;
            agent.autoBraking = false;
            nextAttach = 0f;
            warned = false;
            return true;
        }

        #endregion

        #region 경로 이동

        /// <summary>후보 위치를 NavMesh 위로 보정하고, 현재 위치에서 끝까지 이어지는 경로가 있는지 확인한다.</summary>
        /// <returns>완전한 경로가 있으면 true이며, 이때 target은 NavMesh 위로 보정된 위치다.</returns>
        public bool TryTarget(Vector3 candidate, out Vector3 target)
        {
            target = candidate;
            if (!Attach()) return false;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 2f, agent.areaMask) ||
                Mathf.Abs(hit.position.y - candidate.y) > 2f) return false;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            target = hit.position;
            return true;
        }

        /// <summary>
        /// 목표 지점으로의 이동을 요청한다. 목표가 충분히 바뀌었거나 경로가 없을 때만 재계산 간격을 지켜 경로를 갱신한다.
        /// </summary>
        /// <returns>유효한 경로를 따라 이동 중이면 true, 도달할 수 없으면 false.</returns>
        public bool MoveTo(Vector3 target, float speed, float stopDistance)
        {
            if (!Attach()) return false;
            agent.speed = speed;
            agent.stoppingDistance = stopDistance;
            if ((!hasDestination || (target - destination).sqrMagnitude > .04f || !agent.hasPath) &&
                Time.time >= nextRepath)
            {
                nextRepath = Time.time + .2f;
                Vector3 valid;
                if (!TryTarget(target, out valid))
                {
                    agent.ResetPath(); hasDestination = false;
                    return false;
                }
                destination = target;
                agent.SetPath(path);
                hasDestination = true;
            }
            return hasDestination;
        }

        #endregion

        #region 공격 가능 판정

        /// <summary>대상이 비슷한 높이에 있고 사이를 가로막는 지형이 없어 공격을 시작해도 되는지 판정한다.</summary>
        public bool CanAttack(Transform target)
        {
            return target != null && Mathf.Abs(target.position.y - transform.position.y) < 1.5f &&
                ClearLine(transform.position + Vector3.up, target.position + Vector3.up, target);
        }

        /// <summary>두 지점 사이에 자신·대상·다른 적·플레이어를 제외한 장애물이 없는지 검사한다.</summary>
        private bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            foreach (var hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || (target != null && hit.transform.IsChildOf(target)) ||
                    hit.collider.GetComponentInParent<EnemyStateMachine>() != null ||
                    hit.collider.GetComponentInParent<CombatSystem.Player.PlayerCharacter>() != null) continue;
                return false;
            }
            return true;
        }

        #endregion

        #region 자동 정지

        /// <summary>상태 머신이 꺼져 있으면(사망 등) NavMesh 이동을 멈춘다.</summary>
        private void LateUpdate()
        {
            if (machine == null || !machine.isActiveAndEnabled) Suspend();
        }
        /// <summary>비활성화될 때 NavMesh 이동을 멈춘다.</summary>
        private void OnDisable() { Suspend(); }

        #endregion
    }
}
