using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerLedgeHang : MonoBehaviour
    {
        public float reach=1f,maxHeight=3.4f,shimmySpeed=1.2f;
        public bool IsHanging {get;private set;}
        PlayerCharacter player;Animator animator;CharacterController cc;
        Vector3 edge,normal;float timer,cooldown;bool entering;string motion;
        bool GrabPressed => UnityEngine.InputSystem.Keyboard.current!=null && UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame;
        void Awake(){player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();cc=GetComponent<CharacterController>();}
        bool Valid(Collider c){return !c.isTrigger&&!c.transform.IsChildOf(transform)&&
            c.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()==null&&
            c.GetComponentInParent<CombatSystem.Boss.BossCombat>()==null;}
        bool FindEdge(Vector3 origin,Vector3 forward,out Vector3 point,out Vector3 outward)
        {
            point=outward=Vector3.zero;
            RaycastHit wall;
            if(!Physics.Raycast(origin,forward,out wall,reach,~0,QueryTriggerInteraction.Ignore)||!Valid(wall.collider)||Mathf.Abs(wall.normal.y)>.2f)return false;
            RaycastHit top;
            Vector3 ray=wall.point-wall.normal*.2f+Vector3.up*maxHeight;
            if(!Physics.Raycast(ray,Vector3.down,out top,maxHeight+.5f,~0,QueryTriggerInteraction.Ignore)||!Valid(top.collider)||top.normal.y<.8f)return false;
            point=new Vector3(wall.point.x,top.point.y,wall.point.z);outward=wall.normal;
            // Require a continuous lip wide enough for both hands.
            Vector3 right=Vector3.Cross(Vector3.up,-outward);
            foreach(float side in new[]{-.24f,.24f})
            {
                RaycastHit support;
                if(!Physics.Raycast(point+right*side-outward*.15f+Vector3.up*.15f,Vector3.down,out support,.3f,~0,QueryTriggerInteraction.Ignore)||!Valid(support.collider))return false;
            }
            return true;
        }
        void Play(string state){if(motion==state)return;motion=state;animator.CrossFadeInFixedTime("Base Layer."+state,.15f,0,0);}
        public bool Tick(float dt)
        {
            if(!isActiveAndEnabled||!cc.enabled)return false;
            cooldown=Mathf.Max(0,cooldown-dt);
            if(IsHanging)
            {
                if(player.IsDead||player.IsHitReacting){Release(false);return false;}
                timer+=dt;
                if(timer>.25f&&player.Controls.JumpPressed){Release(true);return true;}
                if(entering)
                {
                    var state=animator.GetCurrentAnimatorStateInfo(0);
                    if((state.IsName("LedgeGrab")&&state.normalizedTime>=.9f)||timer>2f){entering=false;Play("LedgeIdle");}
                }
                else
                {
                    float x=player.Controls.Move.x;
                    Vector3 right=Vector3.Cross(Vector3.up,-normal), next,n;
                    if(Mathf.Abs(x)>.1f&&FindEdge(edge+normal*.55f-Vector3.up*.45f+right*x*shimmySpeed*dt,-normal,out next,out n)&&Vector3.Dot(n,normal)>.95f&&Mathf.Abs(next.y-edge.y)<.15f)
                    {edge=next;normal=n;Play(x<0?"LedgeLeft":"LedgeRight");}
                    else Play("LedgeIdle");
                }
                Vector3 target=edge+normal*(cc.radius+.08f)-Vector3.up*1.15f;
                cc.Move(Vector3.MoveTowards(transform.position,target,(entering?7f:4f)*dt)-transform.position);
                transform.rotation=Quaternion.LookRotation(-normal);
                return true;
            }
            if(cooldown>0||player.IsDead||player.IsAttacking||player.IsDodging||player.IsHitReacting||player.IsLinkJumping||player.IsAirComboActive||
                (player.Hang!=null&&player.Hang.IsHanging)||(player.WallRun!=null&&player.WallRun.IsWallRunning)||
                (player.WeaponStance!=null&&(player.WeaponStance.IsDrawn||player.WeaponStance.IsTransitioning)))return false;
            if(!GrabPressed)return false;
            Vector3 candidate,norm;
            if(!FindEdge(cc.bounds.center,transform.forward,out candidate,out norm))return false;
            float h=candidate.y-cc.bounds.min.y;
            if(h<1.3f||h>maxHeight)return false;
            edge=candidate;normal=norm;IsHanging=true;entering=true;timer=0;motion=null;
            player.Combat.CancelForDamage();player.AirCombo.OnDisable();Play("LedgeGrab");return true;
        }
        [Min(0f)] public float handSurfaceOffset=.035f;
        public void ApplyIK(int layer)
        {
            if(layer!=0||!IsHanging||!animator.isHuman)return;
            float weight=entering?Mathf.SmoothStep(0,1,Mathf.Clamp01((timer-.2f)/.8f)):1;
            FitHand(AvatarIKGoal.LeftHand,HumanBodyBones.LeftHand,weight);
            FitHand(AvatarIKGoal.RightHand,HumanBodyBones.RightHand,weight);
        }
        void FitHand(AvatarIKGoal goal,HumanBodyBones bone,float weight)
        {
            var hand=animator.GetBoneTransform(bone);
            if(hand==null)return;
            // Preserve the four-limb animation's hand height and side-to-side stepping.
            // Only correct depth against the actual wall surface, rather than pinning both hands to the top lip.
            Vector3 target=hand.position;
            float distance=Vector3.Dot(target-edge,normal);
            target+=normal*(handSurfaceOffset-distance);
            target.y=Mathf.Min(target.y,edge.y-.04f);
            Vector3 goalOffset=animator.GetIKPosition(goal)-hand.position;
            animator.SetIKPositionWeight(goal,weight);
            animator.SetIKPosition(goal,target+goalOffset);
        }
        public void Release(bool animate)
        {
            if(!IsHanging)return;IsHanging=false;cooldown=.7f;motion=null;
            if(player.Locomotion!=null)player.Locomotion.ResumeAirDodge();
            if(animate)animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop",.1f,0,0);
        }
        void OnDisable(){if(player!=null)Release(false);}
    }
}