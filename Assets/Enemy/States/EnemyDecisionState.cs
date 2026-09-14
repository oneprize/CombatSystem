using UnityEngine;

namespace ParrySystem.Combat
{
    /// <summary>
    /// Observe가 끝나면 한 프레임 안에서 플레이어 상태를 보고 다음 행동을 가중치 랜덤으로 고른다.
    /// 실제 이동/애니메이션 로직은 없고, 고른 결과에 맞춰 다른 상태로 바로 넘겨주기만 하는
    /// 순간적인 "판단" 상태다.
    ///
    /// - 공격 / 재접근  -> Chase (사거리를 좁히다 attackRange 안에 들어오면 Chase가 알아서 Attack으로 넘어간다)
    /// - 계속 관찰      -> Observe (기존 목표 지점 유지, 관찰 시간만 리셋)
    /// - 위치 변경      -> Observe (새 목표 지점을 강제로 고르게 함)
    /// - 후퇴          -> Retreat
    ///
    /// 패링 시스템을 고려해서, 플레이어가 가드 중이면 공격 가중치를 크게 낮추고
    /// (공격했다가 무조건 패링당할 위험이 크므로), 공격 중이면 살짝 낮춘다(안 부딪히게 관망).
    /// </summary>
    public class EnemyDecisionState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        public EnemyDecisionState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            float attackWeight = combat.BaseAttackWeight;
            if (combat.IsPlayerGuarding)
            {
                attackWeight *= combat.GuardingAttackMultiplier;
            }
            if (combat.IsPlayerAttacking)
            {
                attackWeight *= combat.AttackingAttackMultiplier;
            }

            float continueObserveWeight = combat.BaseContinueObserveWeight;
            float repositionWeight = combat.BaseRepositionWeight;
            float retreatWeight = combat.BaseRetreatWeight;
            float reapproachWeight = combat.BaseReapproachWeight;

            float total = attackWeight + continueObserveWeight + repositionWeight + retreatWeight + reapproachWeight;
            float roll = Random.value * total;

            if (roll < attackWeight)
            {
                machine.ChangeState(machine.ChaseState); // 공격
                return;
            }
            roll -= attackWeight;

            if (roll < continueObserveWeight)
            {
                machine.ChangeState(machine.ObserveState); // 계속 관찰 (기존 목표 지점 유지)
                return;
            }
            roll -= continueObserveWeight;

            if (roll < repositionWeight)
            {
                machine.ObserveState.ForceReposition();
                machine.ChangeState(machine.ObserveState); // 위치 변경
                return;
            }
            roll -= repositionWeight;

            if (roll < retreatWeight)
            {
                machine.ChangeState(machine.RetreatState); // 후퇴
                return;
            }

            machine.ChangeState(machine.ChaseState); // 재접근
        }

        public void Tick(float deltaTime)
        {
        }

        public void Exit()
        {
        }
    }
}
