namespace CombatSystem.Combat
{
    /// <summary>EnemyStateMachine이 구동하는 적 AI 상태의 공통 인터페이스.</summary>
    public interface IEnemyState
    {
        /// <summary>상태에 진입할 때 한 번 호출되어 애니메이션·타이머 등 초기 설정을 한다.</summary>
        void Enter();
        /// <summary>상태가 활성인 동안 매 프레임 호출되어 행동을 진행하고 필요하면 다른 상태로 전이한다.</summary>
        void Tick(float deltaTime);
        /// <summary>다른 상태로 전이되기 직전에 호출되어 이 상태가 바꿔 둔 값을 정리한다.</summary>
        void Exit();
    }
}
