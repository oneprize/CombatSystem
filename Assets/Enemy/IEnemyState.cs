namespace ParrySystem.Combat
{
    public interface IEnemyState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
