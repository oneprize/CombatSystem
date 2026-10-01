using UnityEngine;
namespace ParrySystem.Boss
{
    public sealed class BossPattern2State : IBossState
    {
        private readonly BossStateMachine machine;
        private readonly BossCombat combat;
        private Vector3 start, target, jumpStart;
        private float travelled, chargeTime;
        private float elapsed;
        private bool landed;
        public string Name => "PATTERN 2";
        public BossPattern2State(BossStateMachine machine, BossCombat combat)
        { this.machine=machine; this.combat=combat; }
        public void Enter()
        {
            elapsed=0; travelled=0; chargeTime=0; combat.ChargeDistanceTravelled=0; landed=false; combat.ResetChargeHit();
            start=combat.transform.position;
            if(combat.PlayerTransform==null) { combat.Pattern2Phase="RECOVERY"; return; }
            // Initial facing; charge steers with a bounded radius, then the jump locks its target.
            target=combat.PlayerTransform.position;
            target.y=start.y;
            Vector3 direction=target-start; direction.y=0;
            float distance=direction.magnitude;
            direction=distance>.001f?direction/distance:combat.transform.forward;
            combat.transform.rotation=Quaternion.LookRotation(direction);
            combat.LockedChargeTarget=target;
            SetPhase("WINDUP");
        }
        public void Tick(float deltaTime)
        {
            elapsed+=deltaTime;
            switch(combat.Pattern2Phase)
            {
                case "WINDUP":
                    if(combat.BossAnimationProgress("Crouching",elapsed/Mathf.Max(.1f,combat.ChargeWindup))>=1) SetPhase("START SPRINT");
                    break;
                case "START SPRINT":
                    if(combat.BossAnimationProgress("StartSprint",elapsed/.5f)>=1) SetPhase("CHARGE");
                    break;
                case "CHARGE":
                    TickCharge(deltaTime);
                    break;
                case "JUMP START":
                    if(combat.BossAnimationProgress("JumpStart",elapsed/.18f)>=1) SetPhase("JUMP");
                    break;
                case "JUMP":
                    float progress=Mathf.Clamp01(elapsed/Mathf.Max(.1f,combat.JumpDuration));
                    Vector3 jump=Vector3.Lerp(jumpStart,target,progress);
                    jump.y+=4*combat.JumpHeight*progress*(1-progress);
                    if(!Move(jump))
                    {
                        // A blocked jump ends on the arena plane, without an impact through the obstacle.
                        Vector3 stop=combat.transform.position; stop.y=start.y; combat.transform.position=stop;
                        SetPhase("LANDING"); break;
                    }
                    if(progress>=1)
                    {
                        if(!landed) { landed=true; combat.ApplyLandingHit(); }
                        SetPhase("LANDING");
                    }
                    break;
                case "LANDING":
                    if(combat.BossAnimationProgress("JumpEnd",elapsed/.5f)>=1) SetPhase("RECOVERY");
                    break;
                case "RECOVERY":
                    if(elapsed>=combat.LandingRecovery) machine.ChangeState(machine.IdleState);
                    break;
            }
        }
        private void TickCharge(float deltaTime)
        {
            if(combat.PlayerTransform==null){SetPhase("RECOVERY");return;}
            float remaining=deltaTime;
            while(remaining>0 && combat.Pattern2Phase=="CHARGE")
            {
                if(travelled>=combat.MaxChargeDistance-.0001f || chargeTime>=combat.MaxChargeDuration-.0001f)
                {SetPhase("RECOVERY");return;}
                Vector3 aim=combat.PlayerTransform.position; aim.y=combat.transform.position.y;
                Vector3 toward=aim-combat.transform.position;
                if(toward.magnitude<=combat.TrackingStopDistance)
                {
                    target=aim; combat.LockedChargeTarget=target;
                    jumpStart=combat.transform.position; SetPhase("JUMP START");return;
                }
                float speed=Mathf.Max(.01f,combat.ChargeSpeed);
                float dt=Mathf.Min(remaining,.02f);
                dt=Mathf.Min(dt,combat.MaxChargeDuration-chargeTime);
                dt=Mathf.Min(dt,(combat.MaxChargeDistance-travelled)/speed);
                float step=speed*dt;
                var oldRotation=combat.transform.rotation;
                var rotation=Quaternion.RotateTowards(oldRotation,Quaternion.LookRotation(toward),
                    step/combat.MinTurnRadius*Mathf.Rad2Deg);
                Vector3 previous=combat.transform.position;
                Vector3 forward=Quaternion.Slerp(oldRotation,rotation,.5f)*Vector3.forward;
                combat.transform.rotation=rotation;
                if(!Move(previous+forward*step)){SetPhase("RECOVERY");return;}
                combat.ApplyChargeHit(previous,combat.transform.position);
                travelled+=step; chargeTime+=dt; remaining-=dt;
                combat.ChargeDistanceTravelled=travelled;
            }
        }
        private bool Move(Vector3 next)
        {
            Vector3 movement=next-combat.transform.position;
            if(movement.sqrMagnitude>.000001f)
            {
                foreach(var hit in Physics.SphereCastAll(combat.transform.position+Vector3.up,.5f,
                    movement.normalized,movement.magnitude,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(combat.transform) ||
                       hit.collider.GetComponentInParent<ParrySystem.Player.PlayerCombat>()!=null ||
                       hit.rigidbody!=null || Mathf.Abs(hit.normal.y)>.6f) continue;
                    return false;
                }
            }
            combat.transform.position=next;
            return true;
        }
        private void SetPhase(string phase){elapsed=0;combat.Pattern2Phase=phase;
            combat.PlayBossAnimation(phase=="WINDUP"?"Crouching":phase=="START SPRINT"?"StartSprint":phase=="CHARGE"?"Sprint":phase=="JUMP START"?"JumpStart":phase=="JUMP"?"JumpLoop":phase=="LANDING"?"JumpEnd":"Idle");}
        public void Exit(){combat.Pattern2Phase="-";combat.PlayBossAnimation("Idle");}
    }
}