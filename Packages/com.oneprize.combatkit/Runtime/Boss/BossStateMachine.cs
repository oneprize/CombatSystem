using UnityEngine;
using System.Collections.Generic;
namespace CombatSystem.Boss {
[DisallowMultipleComponent,RequireComponent(typeof(BossCombat))]
public sealed class BossStateMachine : MonoBehaviour {
public sealed class MonitorEntry {public string State,Phase;public float Time;public int Count=1;}
private readonly List<MonitorEntry> log=new List<MonitorEntry>();
public IReadOnlyList<MonitorEntry> MonitorLog => log;
public IBossState CurrentState {get;private set;}
public float StateEnteredAt {get;private set;}
public string PreviousStateName {get;private set;}="-";
public BossIdleState IdleState {get;private set;}
public BossPattern1State Pattern1State {get;private set;}
public BossPattern2State Pattern2State {get;private set;}
public BossGroggyState GroggyState {get;private set;}
public static string StateName(IBossState state)=>state!=null?state.Name:"-";
private void Awake(){GroggyState=new BossGroggyState(this,GetComponent<BossCombat>());IdleState=new BossIdleState(this,GetComponent<BossCombat>());Pattern1State=new BossPattern1State(this,GetComponent<BossCombat>());Pattern2State=new BossPattern2State(this,GetComponent<BossCombat>());}
private bool useSecondPattern;
public void SelectNextPattern()
{
    var next=useSecondPattern ? (IBossState)Pattern2State : Pattern1State;
    useSecondPattern=!useSecondPattern;
    ChangeState(next);
}
private void Start(){if(CurrentState==null)ChangeState(IdleState);}
private void Update()
{
    var combat = GetComponent<BossCombat>();
    if (combat.CurrentHealth <= 0) return;
    // Process the request outside a pattern Tick to avoid reentrant state transitions.
    if (combat.GroggyReady && CurrentState != GroggyState) ChangeState(GroggyState);
    if(CurrentState!=null){Record(CurrentState,"EXECUTE");CurrentState.Tick(Time.deltaTime);}
}
private void Record(IBossState state,string phase){
if(phase=="EXECUTE" && log.Count>0){var last=log[log.Count-1];if(last.State==state.Name && last.Phase==phase){last.Count++;return;}}
log.Add(new MonitorEntry{State=state.Name,Phase=phase,Time=Time.time});if(log.Count>64)log.RemoveAt(0);
}
public void ChangeState(IBossState next){
if(!Application.isPlaying || next==null || next==CurrentState)return;
if(CurrentState!=null){CurrentState.Exit();Record(CurrentState,"EXIT");}
PreviousStateName=StateName(CurrentState);CurrentState=next;StateEnteredAt=Time.time;Record(next,"ENTER");next.Enter();
}
} }