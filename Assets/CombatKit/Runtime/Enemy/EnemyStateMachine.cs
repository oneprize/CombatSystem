using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 일반 적 AI의 상태 머신. 대기·순찰·추적·공격·관찰·결정·후퇴 상태 인스턴스를 만들어 들고, 매 프레임 현재 상태를 Tick한다.
    /// 디버그 창과 모니터 UI를 위해 상태 진입·실행·종료 기록도 남긴다.
    /// </summary>
    [RequireComponent(typeof(EnemyCombat), typeof(EnemyNavigation))]
    public class EnemyStateMachine : MonoBehaviour
    {
        #region 모니터 기록

        /// <summary>모니터용 상태 기록 한 줄. 같은 상태의 연속 실행(EXECUTE)은 Count로 합쳐진다.</summary>
        public sealed class MonitorEntry
        {
            public string State, Phase;
            public float Time;
            public int Count = 1;
        }
        private readonly System.Collections.Generic.List<MonitorEntry> monitorLog =
            new System.Collections.Generic.List<MonitorEntry>();
        /// <summary>최근 상태 기록(최대 64개). FSM 그래프 창 등 디버그 도구에서 읽는다.</summary>
        public System.Collections.Generic.IReadOnlyList<MonitorEntry> MonitorLog => monitorLog;
        public float StateEnteredAt { get; private set; }
        public string PreviousStateName { get; private set; } = "-";
        /// <summary>상태 객체를 모니터 표시용 짧은 대문자 이름(예: CHASE, 피격 리액션은 HIT)으로 바꾼다.</summary>
        public static string StateName(IEnemyState state)
        {
            if (state == null) return "-";
            var groggy = state as EnemyGroggyState;
            if (groggy != null) return groggy.IsDeflecting ? "PARRY DEFLECT" : groggy.IsLarge ? "BIG GROGGY" : "GROGGY";
            return state is EnemyDamageReaction ? "HIT" :
                state.GetType().Name.Replace("Enemy", "").Replace("State", "").ToUpperInvariant();
        }
        /// <summary>상태 진입·실행·종료를 모니터 기록에 추가한다. 같은 상태의 연속 실행은 한 줄로 합친다.</summary>
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

        #endregion

        #region 상태 참조

        public EnemyNavigation Navigation { get; private set; }
        public IEnemyState CurrentState { get; private set; }

        public EnemyIdleState IdleState { get; private set; }
        public EnemyPatrolState PatrolState { get; private set; }
        public EnemyChaseState ChaseState { get; private set; }
        public EnemyAttackState AttackState { get; private set; }
        public EnemyObserveState ObserveState { get; private set; }
        public EnemyDecisionState DecisionState { get; private set; }
        public EnemyRetreatState RetreatState { get; private set; }
        public EnemyGroggyState GroggyState { get; private set; }

        #endregion

        #region Unity 생명주기

        /// <summary>내비게이션 참조를 잡고 모든 AI 상태 인스턴스를 생성한다.</summary>
        private void Awake()
        {
            Navigation = GetComponent<EnemyNavigation>();
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

        /// <summary>다른 컴포넌트 초기화가 끝난 뒤 대기 상태에서 AI를 시작한다.</summary>
        private void Start()
        {
            ChangeState(IdleState);
        }

        /// <summary>현재 상태를 기록하고 한 프레임 진행시킨다.</summary>
        private void Update()
        {
            var executing = CurrentState;
            if (executing != null)
            {
                Record(executing, "EXECUTE");
                executing.Tick(Time.deltaTime);
            }
        }

        #endregion

        #region 상태 전이

        /// <summary>
        /// 현재 상태를 종료하고 다음 상태로 전이한다. 전이할 때마다 NavMesh 이동을 일단 멈추며,
        /// 훈련용 더미는 피격 리액션 외의 모든 전이를 더미 전용 대기 상태로 바꾼다. 같은 상태로의 전이는 무시한다.
        /// </summary>
        public void ChangeState(IEnemyState next)
        {
            var dummy = GetComponent<CombatSystem.Training.PracticeDummy>();
            if (dummy != null && !(next is EnemyDamageReaction) && !(next is EnemyGroggyState)) next = dummy.Idle;
            if (next == null || next == CurrentState)
            {
                return;
            }

            if (CurrentState != null)
            {
                CurrentState.Exit();
                Record(CurrentState, "EXIT");
            }
            Navigation.Suspend();
            PreviousStateName = StateName(CurrentState);
            CurrentState = next;
            StateEnteredAt = Time.time;
            Record(CurrentState, "ENTER");
            CurrentState.Enter();
        }

        #endregion
    }
}
