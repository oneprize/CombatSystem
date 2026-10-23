using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallRun : MonoBehaviour
    {
        [Min(.1f)] public float speed=7f, wallCheckDistance=1f, maxDuration=2f;
        [Min(0)] public float downwardSpeed=.7f, reattachDelay=.5f;
        public bool IsWallRunning {get;private set;}
        PlayerCharacter player; CharacterController controller; Animator animator;
        Vector3 normal, direction;
        Collider currentWall, departedWall;
        Vector3 contactPoint, departedNormal, departedPoint;
        float elapsed,cooldown;
        int side;
        bool starting, ending, waitForLanding; float exitElapsed;
        Vector3 exitVelocity;
        public float jumpAwaySpeed=3.5f,jumpUpSpeed=4.5f;
        [Range(0f,1f)] public float startSpeedFraction=.15f;
        [Min(0f)] public float startWallApproachSpeed=1.5f;
        string StatePrefix => side<0?"WallRunLeft":"WallRunRight";
        void Awake(){player=GetComponent<PlayerCharacter>();controller=GetComponent<CharacterController>();animator=GetComponent<Animator>();}
        Vector3 InputDirection()
        {
            var input=player.Controls.Move;
            var cam=player.CameraTransform;
            Vector3 forward=cam!=null?cam.forward:transform.forward;
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            return (forward*input.y+Vector3.Cross(Vector3.up,forward)*input.x).normalized;
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
                if(hit.transform.IsChildOf(transform)||Mathf.Abs(hit.normal.y)>.2f ||
                   hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null ||
                   hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                if(selectingWall&&waitForLanding&&IsDepartedSurface(hit))continue;
                if(hit.distance<nearest){nearest=hit.distance;closest=hit;}
            }
            return nearest<float.PositiveInfinity;
        }
        bool IsDepartedSurface(RaycastHit hit)
        {
            float alignment=Vector3.Dot(hit.normal,departedNormal);
            if(hit.collider==departedWall&&alignment>.7f)return true;
            // Adjacent colliders on the same flat wall must not refresh the jump allowance.
            return alignment>.98f&&Mathf.Abs(Vector3.Dot(hit.point-departedPoint,departedNormal))<.2f;
        }
        void RememberDepartedWall()
        {
            departedWall=currentWall;departedNormal=normal;departedPoint=contactPoint;
        }
        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled) { End(false); return false; }
            cooldown=Mathf.Max(0,cooldown-dt);
            bool blocked=player.IsDead||player.IsHitReacting||player.IsAttacking||player.IsDodging||player.IsLinkJumping||player.IsAirComboActive||
                (player.Hang!=null&&player.Hang.IsHanging)||(player.WeaponStance!=null&&player.WeaponStance.IsTransitioning);
            if(blocked){End(false);return false;}
            if(ending)
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
            if(waitForLanding&&controller.isGrounded){waitForLanding=false;departedWall=null;}
            Vector3 input=InputDirection();
            if(IsWallRunning)
            {
                elapsed+=dt;
                if(player.Controls.JumpPressed){JumpOffWall();return true;}
                if(elapsed>=Mathf.Min(maxDuration,2f))
                {
                    FinishExit(-downwardSpeed);
                    return true;
                }
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
                RaycastHit hit;
                if(controller.isGrounded||!player.Controls.RunHeld||input.sqrMagnitude<.1f||
                    player.Controls.DodgePressed||!FindWall(-normal,out hit))
                {End(true);return ending;}
                if(Vector3.Dot(normal,hit.normal)<.7f){End(true);return ending;}
                normal=hit.normal;currentWall=hit.collider;contactPoint=hit.point;
                Vector3 tangent=Vector3.Cross(Vector3.up,normal).normalized;
                if(Vector3.Dot(tangent,input)<0)tangent=-tangent;
                if(Vector3.Dot(tangent,input)<.35f){End(true);return ending;}
                direction=tangent;
                transform.rotation=Quaternion.LookRotation(direction);
                Vector3 before=transform.position;
                // CharacterController resolves wall/floor collisions; only a small inward pressure maintains contact.
                float blend=Mathf.SmoothStep(0f,1f,startProgress);
                float moveSpeed=starting?Mathf.Lerp(speed*startSpeedFraction,speed,blend):speed;
                // Ease into contact during Start; the controller resolves the wall collision.
                float inwardSpeed=starting?Mathf.Lerp(startWallApproachSpeed,.6f,blend):.6f;
                float fallSpeed=starting?downwardSpeed*blend:downwardSpeed;
                controller.Move((direction*moveSpeed-normal*inwardSpeed-Vector3.up*fallSpeed)*dt);
                if(Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude<moveSpeed*dt*.15f)End(true);
                return true;
            }
            // A different wall may be caught immediately in the air; only the departed surface stays blocked.
            if((cooldown>0&&!waitForLanding)||controller.isGrounded||!player.Controls.RunHeld||input.sqrMagnitude<.1f)return false;
            RaycastHit left,right;Vector3 rightDir=Vector3.Cross(Vector3.up,input);
            bool hasLeft=FindWall(-rightDir,out left,true),hasRight=FindWall(rightDir,out right,true);
            if(!hasLeft&&!hasRight)return false;
            bool useLeft=hasLeft&&(!hasRight||left.distance<right.distance);
            var chosen=useLeft?left:right;
            direction=Vector3.ProjectOnPlane(input,chosen.normal).normalized;
            if(direction.sqrMagnitude<.1f)return false;
            string state=useLeft?"WallRunLeft":"WallRunRight";
            if(!animator.HasState(0,Animator.StringToHash("Base Layer."+state)))return false;
            // Every wall attachment enters the running loop directly.
            normal=chosen.normal;currentWall=chosen.collider;contactPoint=chosen.point;
            elapsed=0;side=useLeft?-1:1;IsWallRunning=true;starting=false;ending=false;waitForLanding=false;
            player.Locomotion.OnWallRunAttached();
            transform.rotation=Quaternion.LookRotation(direction);
            animator.CrossFadeInFixedTime("Base Layer."+state,.1f,0,0);
            return true;
        }
        public void End(bool animate)
        {
            if(!IsWallRunning)return;
            RememberDepartedWall();
            cooldown=reattachDelay;starting=false;
            if(animate&&!player.IsDead&&!player.IsHitReacting)
            {
                if(player.Controls.JumpPressed){JumpOffWall();return;}
                if(!ending)
                {
                    ending=true;exitElapsed=0;
                    exitVelocity=direction*speed*.5f+normal-Vector3.up*downwardSpeed;
                    animator.CrossFadeInFixedTime("Base Layer."+StatePrefix+"End",.1f,0,0);
                }
                return;
            }
            ending=false;IsWallRunning=false;waitForLanding=!controller.isGrounded;
            player.Locomotion.ResumeAirDodge();
            if(animate&&!player.IsDead&&!player.IsHitReacting)animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop",.1f,0,0);
        }
        void JumpOffWall()
        {
            RememberDepartedWall();
            Vector3 launch=direction*speed*.5f+normal*jumpAwaySpeed+Vector3.up*jumpUpSpeed;
            cooldown=reattachDelay;
            starting=false;ending=false;IsWallRunning=false;
            waitForLanding=true;
            // General air movement owns input immediately, with one additional jump remaining.
            player.Locomotion.BeginWallJump(launch);
        }
        void FinishExit(float verticalSpeed)
        {
            RememberDepartedWall();
            cooldown=reattachDelay;
            starting=false;ending=false;IsWallRunning=false;
            waitForLanding=!controller.isGrounded;
            // Locomotion selects JumpLoop in flight or JumpEnd on contact. Do not overwrite it here.
            player.Locomotion.ResumeWallExit(verticalSpeed);
        }
        void OnDisable(){if(player!=null)End(false);}
    }
}