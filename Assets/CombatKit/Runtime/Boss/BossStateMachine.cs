using UnityEngine;
using System.Collections.Generic;
namespace CombatSystem.Boss {
/// <summary>
/// 보스의 대기·패턴 1·패턴 2·그로기 상태를 관리하는 상태 머신. 패턴을 번갈아 선택하고,
/// 모니터 UI에서 볼 수 있도록 상태 진입/실행/종료 기록을 남긴다.
/// </summary>
[DisallowMultipleComponent,RequireComponent(typeof(BossCombat))]
public sealed class BossStateMachine : MonoBehaviour {
/// <summary>모니터 로그 한 줄. 같은 상태의 연속 EXECUTE는 Count로 묶어 기록한다.</summary>
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
/// <summary>상태 이름을 반환하되 상태가 없으면 "-"로 표시한다.</summary>
public static string StateName(IBossState state)=>state!=null?state.Name:"-";
/// <summary>같은 BossCombat을 공유하는 각 상태 인스턴스를 생성한다.</summary>
private void Awake(){GroggyState=new BossGroggyState(this,GetComponent<BossCombat>());IdleState=new BossIdleState(this,GetComponent<BossCombat>());Pattern1State=new BossPattern1State(this,GetComponent<BossCombat>());Pattern2State=new BossPattern2State(this,GetComponent<BossCombat>());}
private bool useSecondPattern;
/// <summary>패턴 1과 패턴 2를 번갈아 가며 다음 패턴 상태로 전이한다.</summary>
public void SelectNextPattern()
{
    var next=useSecondPattern ? (IBossState)Pattern2State : Pattern1State;
    useSecondPattern=!useSecondPattern;
    ChangeState(next);
}
/// <summary>아직 상태가 없으면 대기 상태로 시작한다.</summary>
private void Start(){if(CurrentState==null)ChangeState(IdleState);}
/// <summary>그로기 진입 조건을 우선 확인한 뒤 현재 상태를 Tick한다. 보스가 쓰러지면 더 이상 진행하지 않는다.</summary>
private void Update()
{
    var combat = GetComponent<BossCombat>();
    if (combat.CurrentHealth <= 0) return;
    // 패턴 Tick 안에서 전이하면 재진입 문제가 생기므로 그로기 전이 요청은 Tick 바깥에서 처리한다.
    if (combat.GroggyReady && CurrentState != GroggyState) ChangeState(GroggyState);
    if(CurrentState!=null){Record(CurrentState,"EXECUTE");CurrentState.Tick(Time.deltaTime);}
}
/// <summary>모니터 로그에 상태 단계를 기록한다. 연속된 EXECUTE는 한 줄로 합치고 최대 64개까지만 보관한다.</summary>
private void Record(IBossState state,string phase){
if(phase=="EXECUTE" && log.Count>0){var last=log[log.Count-1];if(last.State==state.Name && last.Phase==phase){last.Count++;return;}}
log.Add(new MonitorEntry{State=state.Name,Phase=phase,Time=Time.time});if(log.Count>64)log.RemoveAt(0);
}
/// <summary>현재 상태를 종료하고 다음 상태로 진입한다. 플레이 중이 아니거나 같은 상태로의 전이는 무시한다.</summary>
public void ChangeState(IBossState next){
if(!Application.isPlaying || next==null || next==CurrentState)return;
if(CurrentState!=null){CurrentState.Exit();Record(CurrentState,"EXIT");}
PreviousStateName=StateName(CurrentState);CurrentState=next;StateEnteredAt=Time.time;Record(next,"ENTER");next.Enter();
}
} }
