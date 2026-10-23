using UnityEngine;

namespace CombatSystem.Combat
{
    public sealed class EnemyGroggyState : IEnemyState
    {
        readonly EnemyStateMachine machine;
        readonly EnemyCombat combat;
        readonly Animator animator;
        float elapsed;
        bool deflecting;
        public bool IsLarge { get; private set; }
        public bool IsDeflecting => deflecting;
        public float Remaining { get; private set; }

        public EnemyGroggyState(EnemyStateMachine machine, EnemyCombat combat)
        {
            this.machine = machine; this.combat = combat;
            animator = machine.GetComponent<Animator>();
        }

        public void Configure(bool large) { IsLarge = large; }
        public void Enter()
        {
            deflecting = true; elapsed = 0;
            Remaining = IsLarge ? combat.LargeGroggyDuration : combat.ShortGroggyDuration;
            animator.CrossFadeInFixedTime("Base Layer.ParryDeflect", .04f, 0, 0f);
        }

        public void Tick(float dt)
        {
            elapsed += dt;
            if (deflecting)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("ParryDeflect"))
                    state = animator.GetNextAnimatorStateInfo(0);
                if ((state.IsName("ParryDeflect") && state.normalizedTime >= combat.ParryDeflectEndProgress) || elapsed > 1f)
                {
                    deflecting = false;
                    animator.CrossFadeInFixedTime(IsLarge ? "Base Layer.BigGroggy" : "Base Layer.Groggy", .08f, 0, 0f);
                }
                return;
            }
            Remaining = Mathf.Max(0, Remaining - dt);
            if (Remaining <= 0) machine.ChangeState(machine.ObserveState);
        }

        public void Exit()
        {
            if (IsLarge) combat.ClearParryGauge();
            Remaining = 0;
        }
    }
}
