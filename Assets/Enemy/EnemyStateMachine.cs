using UnityEngine;

namespace ParrySystem.Combat
{
    [RequireComponent(typeof(EnemyCombat))]
    public class EnemyStateMachine : MonoBehaviour
    {
        public sealed class MonitorEntry
        {
            public string State, Phase;
            public float Time;
            public int Count = 1;
        }
        private readonly System.Collections.Generic.List<MonitorEntry> monitorLog =
            new System.Collections.Generic.List<MonitorEntry>();
        public System.Collections.Generic.IReadOnlyList<MonitorEntry> MonitorLog => monitorLog;
        public float StateEnteredAt { get; private set; }
        public string PreviousStateName { get; private set; } = "-";
        public static string StateName(IEnemyState state)
        {
            if (state == null) return "-";
            return state is EnemyDamageReaction ? "HIT" :
                state.GetType().Name.Replace("Enemy", "").Replace("State", "").ToUpperInvariant();
        }
        private void Record(IEnemyState state, string phase)
        {
            string name = StateName(state);
            if (phase == "EXECUTE" && monitorLog.Count > 0)
            {
                var last = monitorLog[monitorLog.Count - 1];
                if (last.State == name && last.Phase == phase) { last.Count++; return; }
            }
            monitorLog.Add(new MonitorEntry { State = name, Phase = phase, Time = Time.time });
            if (monitorLog.Count > 64) monitorLog.RemoveAt(0);
        }
        public IEnemyState CurrentState { get; private set; }

        public EnemyIdleState IdleState { get; private set; }
        public EnemyPatrolState PatrolState { get; private set; }
        public EnemyChaseState ChaseState { get; private set; }
        public EnemyAttackState AttackState { get; private set; }
        public EnemyObserveState ObserveState { get; private set; }
        public EnemyDecisionState DecisionState { get; private set; }
        public EnemyRetreatState RetreatState { get; private set; }
        public EnemyGroggyState GroggyState { get; private set; }

        private void Awake()
        {
            var combat = GetComponent<EnemyCombat>();

            IdleState = new EnemyIdleState(this, combat);
            PatrolState = new EnemyPatrolState(this, combat);
            ChaseState = new EnemyChaseState(this, combat);
            AttackState = new EnemyAttackState(this, combat);
            ObserveState = new EnemyObserveState(this, combat);
            DecisionState = new EnemyDecisionState(this, combat);
            RetreatState = new EnemyRetreatState(this, combat);
            GroggyState = new EnemyGroggyState(this, combat);
        }

        private void Start()
        {
            ChangeState(IdleState);
        }

        private void Update()
        {
            var executing = CurrentState;
            if (executing != null)
            {
                Record(executing, "EXECUTE");
                executing.Tick(Time.deltaTime);
            }
        }

        public void ChangeState(IEnemyState next)
        {
            var dummy = GetComponent<ParrySystem.Training.PracticeDummy>();
            if (dummy != null && !(next is EnemyDamageReaction)) next = dummy.Idle;
            if (next == null || next == CurrentState)
            {
                return;
            }

            if (CurrentState != null)
            {
                CurrentState.Exit();
                Record(CurrentState, "EXIT");
            }
            PreviousStateName = StateName(CurrentState);
            CurrentState = next;
            StateEnteredAt = Time.time;
            Record(CurrentState, "ENTER");
            CurrentState.Enter();
        }
    }
}
