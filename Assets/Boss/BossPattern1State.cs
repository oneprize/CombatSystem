namespace ParrySystem.Boss
{
    public sealed class BossPattern1State : IBossState
    {
        private readonly BossCombat combat;
        private readonly BossStateMachine machine;
        public BossPattern1State(BossStateMachine machine, BossCombat combat)
        { this.machine = machine; this.combat = combat; }
        public string Name => "PATTERN 1";
        private bool released, animationDone;
        private float elapsed;
        public void Enter() { released=false; animationDone=false; elapsed=0; FacePlayer(); combat.ArmThrow(); combat.PlayBossAnimation("Throw"); }
        public void Tick(float deltaTime)
        {
            if (!combat.ThrowReleased) FacePlayer();
            elapsed+=deltaTime;
            float progress=combat.BossAnimationProgress("Throw",elapsed/3.833333f);
            released = combat.ThrowReleased;
            if(!animationDone && progress>=1){animationDone=true;combat.PlayBossAnimation("Idle");}
            if (released && animationDone && (combat.ActiveProjectile == null || combat.ActiveProjectile.Resolved))
                machine.ChangeState(machine.IdleState);
        }
        private void FacePlayer()
        {
            if(combat.PlayerTransform==null)return;
            var direction=combat.PlayerTransform.position-combat.transform.position;
            direction.y=0;
            if(direction.sqrMagnitude>.0001f)
                combat.transform.rotation=UnityEngine.Quaternion.LookRotation(direction);
        }
        public void Exit() { combat.CancelThrow(); combat.PlayBossAnimation("Idle"); }
    }
}
