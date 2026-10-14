using UnityEngine;
using UnityEngine.AI;

namespace CombatSystem.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        private NavMeshAgent agent;
        private EnemyStateMachine machine;
        private Vector3 destination;
        private bool hasDestination, warned;
        private float nextRepath, nextAttach;
        private NavMeshPath path;
        public Vector3 MoveDirection => Ready && agent.desiredVelocity.sqrMagnitude > .001f
            ? agent.desiredVelocity.normalized : Vector3.zero;
        private bool Ready => agent != null && agent.enabled && agent.isOnNavMesh;

        private void Awake()
        {
            path = new NavMeshPath();
            agent = GetComponent<NavMeshAgent>();
            machine = GetComponent<EnemyStateMachine>();
            agent.updateRotation = false;
            agent.autoBraking = false;
            agent.enabled = false;
        }

        public void Suspend()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (Ready) { agent.ResetPath(); agent.velocity = Vector3.zero; }
            agent.enabled = false;
            hasDestination = false;
        }

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

        public bool CanAttack(Transform target)
        {
            return target != null && Mathf.Abs(target.position.y - transform.position.y) < 1.5f &&
                ClearLine(transform.position + Vector3.up, target.position + Vector3.up, target);
        }

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

        private void LateUpdate()
        {
            if (machine == null || !machine.isActiveAndEnabled) Suspend();
        }
        private void OnDisable() { Suspend(); }
    }
}
