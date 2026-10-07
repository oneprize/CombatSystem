using UnityEngine;
namespace CombatSystem.Boss
{
    public sealed class BossGroggyState : IBossState
    {
        private readonly BossStateMachine machine;
        private readonly BossCombat combat;
        public string Name => "GROGGY";
        public BossGroggyState(BossStateMachine machine, BossCombat combat)
        { this.machine = machine; this.combat = combat; }
        public void Enter()
        {
            combat.BeginGroggy();
            combat.PlayBossAnimation("Crouching");
        }
        public void Tick(float deltaTime)
        {
            if (combat.GroggySecondsRemaining <= 0)
                machine.ChangeState(machine.IdleState);
        }
        public void Exit()
        {
            combat.EndGroggy();
            combat.PlayBossAnimation("Idle");
        }
    }
}
