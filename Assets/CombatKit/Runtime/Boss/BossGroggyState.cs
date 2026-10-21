using UnityEngine;
namespace CombatSystem.Boss
{
    /// <summary>패리가 충분히 누적되면 진입하는 보스 그로기 상태. 지속 시간 동안 웅크린 채 피해를 받을 수 있다.</summary>
    public sealed class BossGroggyState : IBossState
    {
        private readonly BossStateMachine machine;
        private readonly BossCombat combat;
        public string Name => "GROGGY";
        /// <summary>상태 전이에 쓸 상태 머신과 그로기 판정을 담당하는 전투 컴포넌트를 연결한다.</summary>
        public BossGroggyState(BossStateMachine machine, BossCombat combat)
        { this.machine = machine; this.combat = combat; }
        /// <summary>그로기 타이머를 시작하고 웅크림 애니메이션을 재생한다.</summary>
        public void Enter()
        {
            combat.BeginGroggy();
            combat.PlayBossAnimation("Crouching");
        }
        /// <summary>그로기 시간이 다 되면 대기 상태로 돌아간다.</summary>
        public void Tick(float deltaTime)
        {
            if (combat.GroggySecondsRemaining <= 0)
                machine.ChangeState(machine.IdleState);
        }
        /// <summary>그로기를 종료해 패리 누적을 초기화하고 대기 애니메이션으로 복귀한다.</summary>
        public void Exit()
        {
            combat.EndGroggy();
            combat.PlayBossAnimation("Idle");
        }
    }
}
