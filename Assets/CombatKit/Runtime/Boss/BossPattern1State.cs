namespace CombatSystem.Boss
{
    /// <summary>보스 패턴 1: 플레이어를 바라보며 투사체를 던지고, 투사체가 처리될 때까지 기다린 뒤 대기 상태로 돌아간다.</summary>
    public sealed class BossPattern1State : IBossState
    {
        private readonly BossCombat combat;
        private readonly BossStateMachine machine;
        /// <summary>상태 전이에 쓸 상태 머신과 던지기를 수행할 전투 컴포넌트를 연결한다.</summary>
        public BossPattern1State(BossStateMachine machine, BossCombat combat)
        { this.machine = machine; this.combat = combat; }
        public string Name => "PATTERN 1";
        private bool released, animationDone;
        private float elapsed;
        /// <summary>플레이어 쪽으로 돌아선 뒤 던지기를 예약하고 Throw 애니메이션을 시작한다.</summary>
        public void Enter() { released=false; animationDone=false; elapsed=0; FacePlayer(); combat.ArmThrow(); combat.PlayBossAnimation("Throw"); }
        /// <summary>
        /// 투사체를 놓기 전까지 플레이어를 계속 조준하고, 애니메이션 종료와 투사체 처리(패리/명중/소멸)가 모두 끝나면 대기 상태로 전이한다.
        /// 애니메이터가 없을 때는 경과 시간을 클립 길이로 나눈 값을 진행도로 대신 쓴다.
        /// </summary>
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
        /// <summary>수평면 기준으로 보스를 플레이어 방향으로 즉시 회전시킨다.</summary>
        private void FacePlayer()
        {
            if(combat.PlayerTransform==null)return;
            var direction=combat.PlayerTransform.position-combat.transform.position;
            direction.y=0;
            if(direction.sqrMagnitude>.0001f)
                combat.transform.rotation=UnityEngine.Quaternion.LookRotation(direction);
        }
        /// <summary>패턴이 중단되어도 투사체가 늦게 발사되지 않도록 던지기 예약을 취소하고 대기 애니메이션으로 돌린다.</summary>
        public void Exit() { combat.CancelThrow(); combat.PlayBossAnimation("Idle"); }
    }
}
