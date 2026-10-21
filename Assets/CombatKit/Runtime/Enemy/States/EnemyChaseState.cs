using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 추적 상태. 플레이어가 공격 사거리(attackRange, 오프닝 공격 전에는 openingAttackRange) 밖에 있는 동안
    /// 플레이어 쪽으로 이동·회전하고, 사거리 안에서 공격이 가능하면 바로 Attack으로 넘어간다(발견→접근→공격).
    /// Decision이 "공격"/"재접근"을 선택했을 때도 이 상태를 그대로 재사용한다.
    /// </summary>
    public class EnemyChaseState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        /// <summary>상태 머신과 EnemyCombat 참조를 받는다.</summary>
        public EnemyChaseState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        /// <summary>달리기 애니메이션을 재생한다.</summary>
        public void Enter()
        {
            combat.PlayMoveAnim();
        }

        /// <summary>
        /// 사거리 안이고 시야가 트여 있으면 공격 상태로 전환하고, 아니면 NavMesh 경로를 따라 플레이어에게 접근한다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            Transform player = combat.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Transform self = machine.transform;
            Vector3 toPlayer = player.position - self.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            float attackRange = machine.AttackState.OpeningAttackPending ? combat.OpeningAttackRange : combat.AttackRange;
            if (distance <= attackRange && machine.Navigation.CanAttack(player))
            {
                machine.ChangeState(machine.AttackState);
                return;
            }

            machine.Navigation.MoveTo(player.position, combat.MoveSpeed, Mathf.Max(.1f, attackRange * .8f));
            Vector3 direction = machine.Navigation.MoveDirection;
            if (direction.sqrMagnitude < .001f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRotation, combat.RotationSpeed * deltaTime);

        }

        /// <summary>정리할 것이 없다.</summary>
        public void Exit()
        {
        }
    }
}
