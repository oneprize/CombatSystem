using UnityEngine;
namespace CombatSystem.Boss
{
    /// <summary>
    /// 보스 패턴 2: 웅크림 예비 동작 → 질주 시작 → 회전 반경이 제한된 추적 돌진 → 근접 시 점프 공격 → 착지 → 회복 순으로 진행한다.
    /// 돌진은 최대 거리/시간에 도달하거나 장애물에 막히면 바로 회복 단계로 넘어간다.
    /// </summary>
    public sealed class BossPattern2State : IBossState
    {
        #region 필드

        private readonly BossStateMachine machine;
        private readonly BossCombat combat;
        private Vector3 start, target, jumpStart;
        private float travelled, chargeTime;
        private float elapsed;
        private bool landed;
        public string Name => "PATTERN 2";

        #endregion

        #region 상태 진입과 단계 진행

        /// <summary>상태 전이에 쓸 상태 머신과 이동·판정을 수행할 전투 컴포넌트를 연결한다.</summary>
        public BossPattern2State(BossStateMachine machine, BossCombat combat)
        { this.machine=machine; this.combat=combat; }
        /// <summary>진행 값을 초기화하고 플레이어를 향해 돌아선 뒤 예비 동작 단계를 시작한다. 플레이어가 없으면 곧바로 회복 단계로 둔다.</summary>
        public void Enter()
        {
            elapsed=0; travelled=0; chargeTime=0; combat.ChargeDistanceTravelled=0; landed=false; combat.ResetChargeHit();
            start=combat.transform.position;
            if(combat.PlayerTransform==null) { combat.Pattern2Phase="RECOVERY"; return; }
            // 시작 방향만 여기서 정한다. 돌진 중에는 제한된 회전 반경으로 조향하고, 점프 시점에 목표를 고정한다.
            target=combat.PlayerTransform.position;
            target.y=start.y;
            Vector3 direction=target-start; direction.y=0;
            float distance=direction.magnitude;
            direction=distance>.001f?direction/distance:combat.transform.forward;
            combat.transform.rotation=Quaternion.LookRotation(direction);
            combat.LockedChargeTarget=target;
            SetPhase("WINDUP");
        }
        /// <summary>현재 단계 이름에 따라 애니메이션 완료를 기다리거나 돌진/점프 이동을 진행하고 다음 단계로 넘긴다.</summary>
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
                        // 점프가 막히면 아레나 평면에 내려놓고 끝낸다. 장애물 너머로 착지 충격을 주지 않기 위함이다.
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

        #endregion

        #region 돌진 이동

        /// <summary>
        /// 플레이어를 향해 최소 회전 반경 제약을 지키며 돌진한다. 정확도를 위해 최대 0.02초 단위로 쪼개 이동·타격 판정을 하고,
        /// 추적 정지 거리 안에 들어오면 목표를 고정해 점프 준비 단계로 넘어간다.
        /// </summary>
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
        /// <summary>
        /// 구체 캐스트로 경로상의 벽을 검사한 뒤 보스를 이동시킨다. 자기 자신, 플레이어, 리지드바디, 바닥/천장에 가까운 면은 장애물로 보지 않는다.
        /// </summary>
        /// <returns>막히지 않고 이동했으면 true.</returns>
        private bool Move(Vector3 next)
        {
            Vector3 movement=next-combat.transform.position;
            if(movement.sqrMagnitude>.000001f)
            {
                foreach(var hit in Physics.SphereCastAll(combat.transform.position+Vector3.up,.5f,
                    movement.normalized,movement.magnitude,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(combat.transform) ||
                       hit.collider.GetComponentInParent<CombatSystem.Player.PlayerCharacter>()!=null ||
                       hit.rigidbody!=null || Mathf.Abs(hit.normal.y)>.6f) continue;
                    return false;
                }
            }
            combat.transform.position=next;
            return true;
        }

        #endregion

        #region 단계 전환

        /// <summary>단계 경과 시간을 초기화하고 단계 이름에 대응하는 보스 애니메이션을 재생한다.</summary>
        private void SetPhase(string phase){elapsed=0;combat.Pattern2Phase=phase;
            combat.PlayBossAnimation(phase=="WINDUP"?"Crouching":phase=="START SPRINT"?"StartSprint":phase=="CHARGE"?"Sprint":phase=="JUMP START"?"JumpStart":phase=="JUMP"?"JumpLoop":phase=="LANDING"?"JumpEnd":"Idle");}
        /// <summary>패턴 2 단계 표시를 지우고 대기 애니메이션으로 복귀한다.</summary>
        public void Exit(){combat.Pattern2Phase="-";combat.PlayBossAnimation("Idle");}

        #endregion
    }
}