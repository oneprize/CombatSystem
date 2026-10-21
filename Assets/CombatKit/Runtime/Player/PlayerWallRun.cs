using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallRun : MonoBehaviour
    {
        [Min(.1f)] public float speed=7f, wallCheckDistance=1f, maxDuration=1.8f;
        [Min(0)] public float downwardSpeed=.7f, reattachDelay=.5f;
        public bool IsWallRunning {get;private set;}
        PlayerCharacter player; CharacterController controller; Animator animator;
        Vector3 normal, direction;
        float elapsed,cooldown;
        int side;
        void Awake(){player=GetComponent<PlayerCharacter>();controller=GetComponent<CharacterController>();animator=GetComponent<Animator>();}
        Vector3 InputDirection()
        {
            var input=player.Controls.Move;
            var cam=player.CameraTransform;
            Vector3 forward=cam!=null?cam.forward:transform.forward;
            forward=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            return (forward*input.y+Vector3.Cross(Vector3.up,forward)*input.x).normalized;
        }
        bool FindWall(Vector3 castDirection,out RaycastHit closest)
        {
            closest=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(controller.bounds.center,.12f,castDirection,wallCheckDistance,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)||Mathf.Abs(hit.normal.y)>.2f ||
                   hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null ||
                   hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                if(hit.distance<nearest){nearest=hit.distance;closest=hit;}
            }
            return nearest<float.PositiveInfinity;
        }
        public bool Tick(float dt)
        {
            if (!isActiveAndEnabled) { End(false); return false; }
            if (!isActiveAndEnabled) { End(false); return false; }
            if (!isActiveAndEnabled) { End(false); return false; }
            if (!isActiveAndEnabled) { End(false); return false; }
            if (!isActiveAndEnabled) { End(false); return false; }
            if (!isActiveAndEnabled) { End(false); return false; }
            cooldown=Mathf.Max(0,cooldown-dt);
            bool blocked=player.IsDead||player.IsHitReacting||player.IsAttacking||player.IsDodging||player.IsLinkJumping||player.IsAirComboActive||
                (player.Hang!=null&&player.Hang.IsHanging)||(player.WeaponStance!=null&&player.WeaponStance.IsTransitioning);
            if(blocked){End(false);return false;}
            Vector3 input=InputDirection();
            if(IsWallRunning)
            {
                elapsed+=dt;
                RaycastHit hit;
                if(controller.isGrounded||!player.Controls.RunHeld||input.sqrMagnitude<.1f||elapsed>=maxDuration||
                    player.Controls.JumpPressed||player.Controls.DodgePressed||!FindWall(-normal,out hit))
                {End(true);return false;}
                if(Vector3.Dot(normal,hit.normal)<.7f){End(true);return false;}
                normal=hit.normal;
                Vector3 tangent=Vector3.Cross(Vector3.up,normal).normalized;
                if(Vector3.Dot(tangent,input)<0)tangent=-tangent;
                if(Vector3.Dot(tangent,input)<.35f){End(true);return false;}
                direction=tangent;
                transform.rotation=Quaternion.LookRotation(direction);
                Vector3 before=transform.position;
                // CharacterController resolves wall/floor collisions; only a small inward pressure maintains contact.
                controller.Move((direction*speed-normal*.6f-Vector3.up*downwardSpeed)*dt);
                if(Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude<speed*dt*.15f)End(true);
                return true;
            }
            if(cooldown>0||controller.isGrounded||!player.Controls.RunHeld||input.sqrMagnitude<.1f)return false;
            RaycastHit left,right;Vector3 rightDir=Vector3.Cross(Vector3.up,input);
            bool hasLeft=FindWall(-rightDir,out left),hasRight=FindWall(rightDir,out right);
            if(!hasLeft&&!hasRight)return false;
            bool useLeft=hasLeft&&(!hasRight||left.distance<right.distance);
            var chosen=useLeft?left:right;
            direction=Vector3.ProjectOnPlane(input,chosen.normal).normalized;
            if(direction.sqrMagnitude<.1f)return false;
            string state=useLeft?"WallRunLeft":"WallRunRight";
            if(!animator.HasState(0,Animator.StringToHash("Base Layer."+state)))return false;
            normal=chosen.normal;elapsed=0;side=useLeft?-1:1;IsWallRunning=true;
            transform.rotation=Quaternion.LookRotation(direction);
            animator.CrossFadeInFixedTime("Base Layer."+state,.1f,0,0);
            return true;
        }
        public void End(bool animate)
        {
            if(!IsWallRunning)return;
            IsWallRunning=false;cooldown=reattachDelay;
            player.Locomotion.ResumeAirDodge();
            if(animate&&!player.IsDead&&!player.IsHitReacting)animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop",.1f,0,0);
        }
        void OnDisable(){if(player!=null)End(false);}
    }
}