namespace ParrySystem.Combat
{
    /// <summary>
    /// 패링당해서 경직된 상태. staggerDuration 동안 아무것도 못 하다가 Idle로 돌아간다.
    /// </summary>
    public class EnemyGroggyState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;
        private float elapsed;

        public EnemyGroggyState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            elapsed = 0f;
            combat.SetParryWindowOpen(false);
            combat.PlayGroggyAnim();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (elapsed >= combat.StaggerDuration)
            {
                machine.ChangeState(machine.IdleState);
            }
        }

        public void Exit()
        {
            combat.ResetGroggyGauge();
        }
    }
}
