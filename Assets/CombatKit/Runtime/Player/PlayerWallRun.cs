using UnityEngine;
namespace CombatSystem.Player
{
    /// <summary>
    /// 공중에서 달리기 입력으로 옆벽에 붙어 벽을 따라 달린다.
    /// 진입은 벽 간격·진행 방향 입력·실제 수평 속도·앞쪽 장애물·벽 연속성(활주로)을 모두 통과하고
    /// 같은 벽이 짧은 시간 계속 감지되어야 확정된다. 달리는 동안에는 실제 면을 다시 확인하고,
    /// 벽이 끊기거나 모서리에 막히면 종료 모션 없이 곧바로 공중 이동으로 넘긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWallRun : MonoBehaviour
    {
        #region 설정

        [Min(.1f)] public float speed=7f, wallCheckDistance=1f;
        [Range(.1f,2f)] public float maxDuration=2f;
        [Min(0)] public float downwardSpeed=.7f, reattachDelay=.5f;
        public float jumpAwaySpeed=3.5f,jumpUpSpeed=4.5f;
        [Range(0f,1f)] public float startSpeedFraction=.15f;
        [Min(0f)] public float startWallApproachSpeed=1.5f;

        [Header("진입 검증")]
        [Tooltip("캡슐 표면과 벽 사이 최대 간격(m). 멀리 떠 있는 벽에 붙어 허공에서 달리지 않게 한다.")]
        [Min(0f)] public float maxAttachGap=.3f;
        [Tooltip("진행 방향으로 벽면이 최소 이만큼 이어져야 진입한다(m). 얇은 벽 끝이나 기둥을 스치는 경우 제외.")]
        [Min(0f)] public float minRunwayLength=1.5f;
        [Tooltip("진행 방향 앞이 이 거리(m)만큼 비어 있어야 진입한다. 세 벽이 만나는 모서리 차단.")]
        [Min(0f)] public float frontClearance=1f;
        [Tooltip("벽을 따라가는 입력 성분의 최소 크기(정규화 전). 벽으로 돌진하는 각도의 진입을 막는다.")]
        [Range(0f,1f)] public float minAlongWallInput=.5f;
        [Tooltip("수평 이동 속도가 이 값(m/s) 이상일 때만 진입한다. 기본값은 걷기(3)보다 크고 달리기(6)보다 작다.")]
        [Min(0f)] public float minHorizontalSpeed=4f;
        [Tooltip("같은 벽이 이 시간(초) 동안 계속 감지되어야 진입을 확정한다. 한순간 스친 접촉 제외.")]
        [Min(0f)] public float candidateConfirmTime=.08f;
        [Tooltip("점프·더블 점프·벽 점프가 실제로 발동한 뒤 이 시간(초) 동안은 새 벽에 붙지 않는다.")]
        [Min(0f)] public float jumpEntryLockout=.2f;

        [Header("달리는 동안")]
        [Tooltip("이 시간(초) 뒤의 위치에서 벽이 끊기거나 막혀 있으면 미리 이탈한다.")]
        [Min(0f)] public float lookAheadTime=.1f;
        [Tooltip("콜라이더 이음매 등에서 벽면을 놓쳐도 이 시간(초)까지는 유지한다.")]
        [Min(0f)] public float wallLostGrace=.06f;
        [Tooltip("벽 달리기를 이 시간(초) 이상 유지해야 공중 점프 한 번을 되돌려 준다.")]
        [Min(0f)] public float minRunTimeForJumpRefresh=.25f;
        [Tooltip("이보다 짧게 붙었다가 자발적으로 놓으면 종료 모션 없이 공중 이동으로 넘긴다.")]
        [Min(0f)] public float minRunTimeForEndAnim=.2f;

        [Header("연속 부착")]
        [Tooltip("켜면 방금 떠난 벽과 방향이 충분히 다른 벽(통로 반대편 등)에는 재부착 대기시간 없이 붙는다.")]
        public bool allowOppositeWallChain=false;
        [Tooltip("착지하기 전까지 붙을 수 있는 최대 횟수.")]
        [Min(1)] public int maxAttachesPerAirtime=2;

        [Header("디버그")]
        [Tooltip("선택했을 때 마지막 진입/유지 검사 레이와 결과를 Scene 뷰에 그린다.")]
        public bool drawDebug=false;

        #endregion

        #region 상태

        public bool IsWallRunning {get;private set;}
        PlayerCharacter player; CharacterController controller; Animator animator;
        Vector3 normal, direction;
        Collider currentWall, departedWall;
        Vector3 contactPoint, departedNormal, departedPoint;
        float elapsed,cooldown;
        int side;
        bool starting, ending, waitForLanding; float exitElapsed;
        Vector3 exitVelocity;
        bool jumpRefreshed;
        float lostTime;
        int attachesThisAirtime;
        // 진입 후보: 같은 평면이 candidateConfirmTime 동안 계속 유효해야 확정한다.
        bool hasCandidate; Vector3 candidateNormal, candidatePoint; float candidateTime;
        string lastReject="";
        readonly System.Collections.Generic.List<(Vector3 from, Vector3 to, bool ok)> debugRays=new System.Collections.Generic.List<(Vector3, Vector3, bool)>();

        string StatePrefix => side<0?"WallRunLeft":"WallRunRight";
        float Radius => controller.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));
        float ProbeReach => Radius+maxAttachGap+.2f;

        #endregion

        void Awake(){player=GetComponent<PlayerCharacter>();controller=GetComponent<CharacterController>();animator=GetComponent<Animator>();}

        #region 검사

        Vector3 InputDirection()
        {
            var input=player.Controls.Move;
            var cam=player.CameraTransform;
            Vector3 forward=cam!=null?cam.forward:transform.forward;
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            return (forward*input.y+Vector3.Cross(Vector3.up,forward)*input.x).normalized;
        }

        bool IsIgnoredSurface(Collider c)
        {
            return c.transform.IsChildOf(transform)||
                c.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null||
                c.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null;
        }

        bool FindWall(Vector3 castDirection,out RaycastHit closest,bool selectingWall=false)
        {
            closest=default;float nearest=float.PositiveInfinity;
            foreach(var candidate in Physics.SphereCastAll(controller.bounds.center,.12f,castDirection,wallCheckDistance,~0,QueryTriggerInteraction.Ignore))
            {
                var hit=candidate;
                if(selectingWall)
                {
                    // Sphere casts return rounded edge normals at collider seams. Confirm the actual face.
                    RaycastHit face;
                    if(!hit.collider.Raycast(new Ray(controller.bounds.center,castDirection),out face,wallCheckDistance+.12f))continue;
                    hit=face;
                }
                if(Mathf.Abs(hit.normal.y)>.2f||IsIgnoredSurface(hit.collider))continue;
                if(selectingWall&&waitForLanding&&IsDepartedSurface(hit))continue;
                if(hit.distance<nearest){nearest=hit.distance;closest=hit;}
            }
            return nearest<float.PositiveInfinity;
        }

        /// <summary>origin에서 벽 쪽으로 레이를 쏴 실제 벽면을 찾는다. 바닥·천장·적은 벽으로 보지 않는다.</summary>
        bool ProbeFace(Vector3 origin,Vector3 towardWall,float maxDistance,out RaycastHit hit)
        {
            bool found=false;hit=default;float nearest=float.PositiveInfinity;
            foreach(var h in Physics.RaycastAll(origin,towardWall,maxDistance,~0,QueryTriggerInteraction.Ignore))
            {
                if(h.distance>=nearest||IsIgnoredSurface(h.collider))continue;
                nearest=h.distance;hit=h;found=true;
            }
            bool ok=found&&Mathf.Abs(hit.normal.y)<=.2f;
            if(drawDebug)debugRays.Add((origin,origin+towardWall*(found?hit.distance:maxDistance),ok));
            return ok;
        }

        /// <summary>법선과 평면 위치로 같은 벽면인지 판단한다. 같은 평면을 이루는 인접 콜라이더도 같은 벽으로 본다.</summary>
        static bool SamePlane(Vector3 normalA,Vector3 pointA,Vector3 normalB,Vector3 pointB)
        {
            return Vector3.Dot(normalA,normalB)>.98f&&Mathf.Abs(Vector3.Dot(pointB-pointA,normalA))<.2f;
        }

        /// <summary>진행 방향으로 minRunwayLength만큼 허리·가슴 높이 모두에서 같은 벽면이 이어지는지 확인한다.</summary>
        bool HasRunway(Vector3 wallNormal,Vector3 wallPoint,Vector3 runDir)
        {
            Vector3 center=controller.bounds.center;
            float h=controller.bounds.extents.y;
            float[] heights={h*.45f,-h*.2f}; // 가슴, 허리
            for(float d=0f;;d=Mathf.Min(d+.5f,minRunwayLength))
            {
                foreach(float y in heights)
                {
                    RaycastHit probe;
                    if(!ProbeFace(center+runDir*d+Vector3.up*y,-wallNormal,ProbeReach,out probe))return false;
                    if(!SamePlane(wallNormal,wallPoint,probe.normal,probe.point))return false;
                }
                if(d>=minRunwayLength)return true;
            }
        }

        /// <summary>진행 방향으로 캡슐을 쏴서 앞이 막혔는지 본다. 달리고 있는 벽 자체에 스치는 충돌은 무시한다.</summary>
        bool PathBlocked(Vector3 runDir,Vector3 wallNormal,float distance)
        {
            var b=controller.bounds;
            float half=Mathf.Max(0f,b.extents.y-Radius);
            foreach(var hit in Physics.CapsuleCastAll(b.center+Vector3.up*half,b.center-Vector3.up*half,Radius*.9f,runDir,distance,~0,QueryTriggerInteraction.Ignore))
            {
                if(IsIgnoredSurface(hit.collider)||hit.distance<=0f)continue;
                if(Vector3.Dot(hit.normal,wallNormal)>.7f)continue;  // 지금 붙은 벽
                if(hit.normal.y>.5f)continue;                         // 바닥, 완만한 경사
                if(drawDebug)debugRays.Add((b.center,hit.point,false));
                return true;
            }
            return false;
        }

        bool IsDepartedSurface(RaycastHit hit)
        {
            float alignment=Vector3.Dot(hit.normal,departedNormal);
            if(hit.collider==departedWall&&alignment>.7f)return true;
            // Adjacent colliders on the same flat wall must not refresh the jump allowance.
            return SamePlane(departedNormal,departedPoint,hit.normal,hit.point);
        }
        void RememberDepartedWall()
        {
            departedWall=currentWall;departedNormal=normal;departedPoint=contactPoint;
        }

        #endregion

        #region 갱신

        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled) { End(false); return false; }
            if(drawDebug)debugRays.Clear();
            cooldown=Mathf.Max(0,cooldown-dt);
            bool blocked=player.IsDead||player.IsHitReacting||player.IsAttacking||player.IsDodging||player.IsLinkJumping||player.IsAirComboActive||
                (player.Hang!=null&&player.Hang.IsHanging)||(player.WeaponStance!=null&&player.WeaponStance.IsTransitioning);
            if(blocked){End(false);ClearCandidate();return false;}
            if(ending)return TickEnding(dt);
            if(controller.isGrounded)
            {
                attachesThisAirtime=0;
                if(waitForLanding){waitForLanding=false;departedWall=null;}
            }
            Vector3 input=InputDirection();
            if(IsWallRunning)return TickRunning(dt,input);
            return TryAttach(dt,input);
        }

        bool TickEnding(float dt)
        {
            // A fresh jump may interrupt the non-jump wall exit animation too.
            if(player.Controls.JumpPressed){FinishExit(exitVelocity.y);return false;}
            exitElapsed+=dt;exitVelocity.y+=Physics.gravity.y*dt;
            var flags=controller.Move(exitVelocity*dt);
            if((flags&CollisionFlags.Above)!=0)exitVelocity.y=Mathf.Min(0,exitVelocity.y);
            bool landed=controller.isGrounded&&exitVelocity.y<=0f;
            if(landed)exitVelocity.y=-2f;
            var exit=animator.GetCurrentAnimatorStateInfo(0);
            bool endFinished=exit.IsName(StatePrefix+"End")&&exit.normalizedTime>=1f;
            if(landed||endFinished||exitElapsed>1.2f)FinishExit(exitVelocity.y);
            return true;
        }

        bool TickRunning(float dt,Vector3 input)
        {
            elapsed+=dt;
            if(player.Controls.JumpPressed){JumpOffWall();return true;}
            if(!jumpRefreshed&&elapsed>=minRunTimeForJumpRefresh)
            {
                // 충분히 달린 벽 달리기만 공중 점프 한 번을 되돌려 준다.
                jumpRefreshed=true;
                player.Locomotion.RefreshAirJump();
            }
            // Inspector/기존 씬 값에 관계없이 2초 상한을 적용하고 누적 소수점 오차로 한 프레임 늘어나지 않게 한다.
            if(elapsed+.0001f>=Mathf.Clamp(maxDuration,.1f,2f)){FinishExit(-downwardSpeed);return true;}
            // 착지하면 이동 모듈이 착지를 처리하도록 바로 넘긴다.
            if(controller.isGrounded){FinishExit(-2f);return true;}

            float startProgress=1f;
            if(starting)
            {
                var st=animator.GetCurrentAnimatorStateInfo(0);
                if(animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(StatePrefix+"Start"))
                    st=animator.GetNextAnimatorStateInfo(0);
                startProgress=st.IsName(StatePrefix+"Start")?Mathf.Clamp01(st.normalizedTime):0f;
                if(startProgress>=1f)
                {
                    starting=false;
                    animator.CrossFadeInFixedTime("Base Layer."+StatePrefix,.1f,0,0);
                }
            }

            // 자발적 종료: 벽 지지는 남아 있으므로 필요하면 종료 모션을 쓴다.
            if(!player.Controls.RunHeld||input.sqrMagnitude<.1f||player.Controls.DodgePressed){End(true);return ending;}
            Vector3 tangent=Vector3.Cross(Vector3.up,normal).normalized;
            if(Vector3.Dot(tangent,input)<0)tangent=-tangent;
            if(Vector3.Dot(tangent,input)<.35f){End(true);return ending;}

            // 지지 확인: 실제 면 레이로 확인하고, 법선이 크게 바뀌면 새 벽으로 갈아타지 않고 이탈한다.
            RaycastHit hit;
            Vector3 center=controller.bounds.center;
            if(ProbeFace(center,-normal,ProbeReach,out hit)&&Vector3.Dot(normal,hit.normal)>=.7f)
            {
                lostTime=0f;
                normal=hit.normal;currentWall=hit.collider;contactPoint=hit.point;
                tangent=Vector3.Cross(Vector3.up,normal).normalized;
                if(Vector3.Dot(tangent,input)<0)tangent=-tangent;
            }
            else
            {
                lostTime+=dt;
                if(lostTime>wallLostGrace){LoseWall("벽면 소실");return true;}
            }
            direction=tangent;

            // 진행 예측: 잠시 뒤 위치에서 벽이 끊기거나 앞이 막히면 미리 이탈한다.
            float ahead=Mathf.Max(speed*lookAheadTime,.2f);
            RaycastHit aheadHit;
            if(!ProbeFace(center+direction*ahead,-normal,ProbeReach,out aheadHit)||!SamePlane(normal,contactPoint,aheadHit.normal,aheadHit.point))
            {LoseWall("앞쪽 벽 끊김");return true;}
            if(PathBlocked(direction,normal,ahead+Radius*.5f)){LoseWall("모서리 차단");return true;}

            transform.rotation=Quaternion.LookRotation(direction);
            Vector3 before=transform.position;
            // CharacterController resolves wall/floor collisions; only a small inward pressure maintains contact.
            float blend=Mathf.SmoothStep(0f,1f,startProgress);
            float moveSpeed=starting?Mathf.Lerp(speed*startSpeedFraction,speed,blend):speed;
            // Ease into contact during Start; the controller resolves the wall collision.
            float inwardSpeed=starting?Mathf.Lerp(startWallApproachSpeed,.6f,blend):.6f;
            float fallSpeed=starting?downwardSpeed*blend:downwardSpeed;
            controller.Move((direction*moveSpeed-normal*inwardSpeed-Vector3.up*fallSpeed)*dt);
            if(Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude<moveSpeed*dt*.15f){LoseWall("진행 막힘");return true;}

            // 이동 직후에도 벽 지지가 남아 있는지 다시 확인한다.
            if(!ProbeFace(controller.bounds.center,-normal,ProbeReach,out hit))
            {
                lostTime+=dt;
                if(lostTime>wallLostGrace){LoseWall("이동 후 벽면 소실");return true;}
            }
            return true;
        }

        bool TryAttach(float dt,Vector3 input)
        {
            if(controller.isGrounded||!player.Controls.RunHeld||input.sqrMagnitude<.1f){ClearCandidate();return false;}
            // 점프 입력이 들어온 프레임은 이동 모듈이 점프를 처리하도록 진입을 포기한다.
            if(player.Controls.JumpPressed){Reject("점프 입력 우선");return false;}
            if(Time.time-player.Locomotion.LastJumpTime<jumpEntryLockout){Reject("점프 직후");return false;}
            if(attachesThisAirtime>=maxAttachesPerAirtime){Reject("공중 부착 횟수 초과");return false;}
            // CharacterController.velocity는 한 프레임의 마지막 Move만 반영한다(이동 모듈은 수평/수직을 따로 Move한다).
            // 이동 모듈이 공중 잡기용으로 넘겨주는 운동량을 기준으로 실제로 달리던 중인지 본다.
            Vector3 planarVelocity=Vector3.ProjectOnPlane(player.Locomotion.TraversalVelocity,Vector3.up);
            if(planarVelocity.magnitude<minHorizontalSpeed){Reject("수평 속도 부족");return false;}

            RaycastHit left,right;Vector3 rightDir=Vector3.Cross(Vector3.up,input);
            bool hasLeft=FindWall(-rightDir,out left,true),hasRight=FindWall(rightDir,out right,true);
            if(!hasLeft&&!hasRight){ClearCandidate();return false;}
            bool useLeft=hasLeft&&(!hasRight||left.distance<right.distance);
            var chosen=useLeft?left:right;

            // 재부착 대기시간은 모든 벽에 적용한다. 옵션이 켜져 있으면 방향이 크게 다른 벽만 예외로 둔다.
            bool differentWall=Vector3.Dot(chosen.normal,departedNormal)<.3f;
            if(cooldown>0f&&!(allowOppositeWallChain&&waitForLanding&&differentWall)){Reject("재부착 대기");return false;}
            // FindWall의 면 확인 레이는 캡슐 중심에서 출발하므로 거리에서 반지름을 빼면 표면 간격이다.
            if(chosen.distance-Radius>maxAttachGap){Reject("벽과 너무 멂");return false;}
            Vector3 alongWall=Vector3.ProjectOnPlane(input,chosen.normal);
            alongWall.y=0f;
            if(alongWall.magnitude<minAlongWallInput){Reject("벽 방향 입력 부족");return false;}
            Vector3 runDir=alongWall.normalized;
            if(PathBlocked(runDir,chosen.normal,frontClearance)){Reject("앞이 막힘");return false;}
            if(!HasRunway(chosen.normal,chosen.point,runDir)){Reject("벽 길이 부족");return false;}
            string state=useLeft?"WallRunLeft":"WallRunRight";
            if(!animator.HasState(0,Animator.StringToHash("Base Layer."+state))){ClearCandidate();return false;}

            // 같은 벽면이 짧은 시간 계속 감지되어야 확정한다. 그동안은 일반 공중 이동과 점프 모션이 유지된다.
            if(hasCandidate&&SamePlane(candidateNormal,candidatePoint,chosen.normal,chosen.point))candidateTime+=dt;
            else{hasCandidate=true;candidateTime=0f;}
            candidateNormal=chosen.normal;candidatePoint=chosen.point;
            if(candidateTime<candidateConfirmTime){lastReject="후보 확인 중";return false;}

            Attach(chosen,runDir,useLeft,state);
            return true;
        }

        void Attach(RaycastHit wall,Vector3 runDir,bool useLeft,string state)
        {
            ClearCandidate();
            normal=wall.normal;currentWall=wall.collider;contactPoint=wall.point;direction=runDir;
            elapsed=0;lostTime=0;jumpRefreshed=false;attachesThisAirtime++;
            side=useLeft?-1:1;IsWallRunning=true;starting=false;ending=false;waitForLanding=false;
            lastReject="";
            // 점프 횟수는 그대로 두고 공중 이동만 끝낸다. 추가 점프는 minRunTimeForJumpRefresh 이후에 돌려준다.
            player.Locomotion.OnWallRunAttached();
            transform.rotation=Quaternion.LookRotation(direction);
            // Every wall attachment enters the running loop directly.
            animator.CrossFadeInFixedTime("Base Layer."+state,.1f,0,0);
        }

        void ClearCandidate(){hasCandidate=false;candidateTime=0f;}
        void Reject(string reason){ClearCandidate();lastReject=reason;}

        #endregion

        #region 이탈

        /// <summary>
        /// 벽 달리기를 끝낸다. animate=true는 자발적 종료로, 충분히 달렸으면 종료 모션을 재생한다.
        /// animate=false는 다른 동작(피격, 공격, 사망 등)에 즉시 양보한다.
        /// </summary>
        public void End(bool animate)
        {
            if(!IsWallRunning)return;
            if(animate&&!player.IsDead&&!player.IsHitReacting)
            {
                if(player.Controls.JumpPressed){JumpOffWall();return;}
                // 짧게 붙었다 놓은 경우에는 종료 모션 없이 바로 공중 이동으로 넘긴다(모션 깜빡임 방지).
                if(!ending&&elapsed<minRunTimeForEndAnim){FinishExit(-downwardSpeed);return;}
                RememberDepartedWall();
                cooldown=reattachDelay;starting=false;
                if(!ending)
                {
                    ending=true;exitElapsed=0;
                    exitVelocity=direction*speed*.5f+normal-Vector3.up*downwardSpeed;
                    animator.CrossFadeInFixedTime("Base Layer."+StatePrefix+"End",.1f,0,0);
                }
                return;
            }
            RememberDepartedWall();
            cooldown=reattachDelay;starting=false;
            ending=false;IsWallRunning=false;waitForLanding=!controller.isGrounded;
            player.Locomotion.ResumeWallRelease();
        }

        /// <summary>벽이 끊기거나 모서리에 막혀 지지를 잃었을 때. 종료 클립을 기다리지 않고 공중 이동으로 넘긴다.</summary>
        void LoseWall(string reason)
        {
            lastReject=reason;
            FinishExit(-downwardSpeed);
        }

        void JumpOffWall()
        {
            RememberDepartedWall();
            Vector3 launch=direction*speed*.5f+normal*jumpAwaySpeed+Vector3.up*jumpUpSpeed;
            cooldown=reattachDelay;
            starting=false;ending=false;IsWallRunning=false;
            waitForLanding=true;
            // General air movement owns input immediately; the remaining jump count carries over from the wall run.
            player.Locomotion.BeginWallJump(launch);
        }

        void FinishExit(float verticalSpeed)
        {
            RememberDepartedWall();
            cooldown=reattachDelay;
            starting=false;ending=false;IsWallRunning=false;
            waitForLanding=!controller.isGrounded;
            // Locomotion selects JumpLoop in flight or JumpEnd on contact, and keeps the jump count.
            player.Locomotion.ResumeWallExit(verticalSpeed);
        }

        void OnDisable(){if(player!=null)End(false);}

        #endregion

        #region 디버그

        /// <summary>마지막으로 진입을 거절했거나 이탈한 이유. 튜닝용.</summary>
        public string LastDecision=>lastReject;

        void OnDrawGizmosSelected()
        {
            if(!drawDebug)return;
            foreach(var ray in debugRays)
            {
                Gizmos.color=ray.ok?Color.green:Color.red;
                Gizmos.DrawLine(ray.from,ray.to);
            }
        }

        #endregion
    }
}
