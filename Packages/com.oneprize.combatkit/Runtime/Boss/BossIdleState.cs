namespace CombatSystem.Boss {
public sealed class BossIdleState : IBossState {
private readonly BossStateMachine machine; private readonly BossCombat combat; private float wait;
public BossIdleState(BossStateMachine machine,BossCombat combat){this.machine=machine;this.combat=combat;}
public string Name => "IDLE";
public void Enter(){wait= combat.PatternCooldown;}
public void Tick(float deltaTime){wait-=deltaTime;if(wait<=0 && combat.PlayerDetected)machine.SelectNextPattern();}
public void Exit(){}
} }