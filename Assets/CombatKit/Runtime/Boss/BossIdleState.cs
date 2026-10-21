namespace CombatSystem.Boss {
/// <summary>패턴 사이의 대기 상태. 쿨다운이 끝나고 플레이어가 탐지 범위 안에 있으면 다음 패턴을 고른다.</summary>
public sealed class BossIdleState : IBossState {
private readonly BossStateMachine machine; private readonly BossCombat combat; private float wait;
/// <summary>패턴 선택을 요청할 상태 머신과 쿨다운·탐지 정보를 제공할 전투 컴포넌트를 연결한다.</summary>
public BossIdleState(BossStateMachine machine,BossCombat combat){this.machine=machine;this.combat=combat;}
public string Name => "IDLE";
/// <summary>패턴 쿨다운 타이머를 다시 시작한다.</summary>
public void Enter(){wait= combat.PatternCooldown;}
/// <summary>쿨다운이 끝난 뒤 플레이어를 탐지하면 다음 패턴으로 전이한다.</summary>
public void Tick(float deltaTime){wait-=deltaTime;if(wait<=0 && combat.PlayerDetected)machine.SelectNextPattern();}
/// <summary>정리할 상태가 없어 아무 것도 하지 않는다.</summary>
public void Exit(){}
} }