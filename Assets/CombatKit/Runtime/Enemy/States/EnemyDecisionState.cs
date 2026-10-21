using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 관찰이 끝나면 진입 즉시 플레이어 상태를 보고 다음 행동을 가중치 랜덤으로 고르는 순간적인 "판단" 상태다.
    /// 이동·애니메이션 로직은 없고, 고른 결과에 맞는 상태로 바로 넘겨주기만 한다.
    ///
    /// - 공격 / 재접근  -> Chase (거리를 좁히다 attackRange 안에 들어오면 Chase가 Attack으로 넘긴다)
    /// - 계속 관찰      -> Observe (기존 목표 지점 유지, 관찰 시간만 리셋)
    /// - 재배치         -> Observe (새 목표 지점을 강제로 고르게 함)
    /// - 후퇴           -> Retreat
    ///
    /// 플레이어가 가드 중이면 공격 가중치를 낮추고, 공격 중이면 맞부딪히지 않도록 약간 낮춘다.
    /// </summary>
    public class EnemyDecisionState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyDecisionState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>플레이어의 가드·공격 여부로 공격 가중치를 보정한 뒤 가중치 랜덤으로 다음 상태를 골라 바로 전이한다.</summary>
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
                machine.ChangeState(machine.ObserveState); // 재배치
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

        /// <summary>Enter에서 곧바로 전이하므로 매 프레임 할 일이 없다.</summary>
        public void Tick(float deltaTime)
        {
        }

        /// <summary>정리할 것이 없다.</summary>
        public void Exit()
        {
        }
    }
}
