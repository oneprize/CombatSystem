using UnityEngine;

namespace CombatSystem.Combat
{
    /// <summary>
    /// 플레이어의 공격 사거리(attackRange) 밖에 있는 동안 플레이어 쪽으로 이동+회전한다.
    /// 사거리 안에 들어오면 바로 Attack으로 넘어간다 (발견→접근→공격).
    /// Decision이 "공격"/"재접근"을 선택했을 때도 이 상태를 그대로 재사용한다.
    /// </summary>
    public class EnemyChaseState : IEnemyState
    {
        private readonly EnemyStateMachine machine;
        private readonly EnemyCombat combat;

        public EnemyChaseState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine;
            this.combat = combat;
        }

        public void Enter()
        {
            combat.PlayMoveAnim();
        }

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

        public void Exit()
        {
        }
    }
}
