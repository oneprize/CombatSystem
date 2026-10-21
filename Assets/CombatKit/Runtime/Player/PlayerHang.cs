using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHang : MonoBehaviour
    {
        public float grabRange=1.8f, maxGrabHeight=3.6f;
        public bool IsHanging {get;private set;}
        PlayerCharacter player;
        Animator animator;
        CharacterController controller;
        Transform left,right;
        HangRail rail;
        Collider railCollider;
        bool ignored;
        Vector3 grip;
        float elapsed,cooldown;
        bool idle;
        void Awake()
        {
            player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();controller=GetComponent<CharacterController>();
            if(animator.isHuman){left=animator.GetBoneTransform(HumanBodyBones.LeftHand);right=animator.GetBoneTransform(HumanBodyBones.RightHand);}
        }
        public bool Tick(float dt)
        {
            cooldown=Mathf.Max(0,cooldown-dt);
            if(IsHanging)
            {
                if(rail==null||player.IsDead||player.IsHitReacting){Release(false);return false;}
                elapsed+=dt;
                if(elapsed>.3f && (player.Controls.JumpPressed||player.Controls.DodgePressed)){Release(true);return true;}
                var state=animator.GetCurrentAnimatorStateInfo(0);
                if(!idle && state.IsName("JumpToHang") && state.normalizedTime>=.9f)
                {idle=true;animator.CrossFadeInFixedTime("Base Layer.HangingIdle",.12f,0,0);}
                return true;
            }
            if(cooldown>0||!player.Controls.JumpPressed||player.IsAttacking||player.IsDodging||player.IsHitReacting||player.IsLinkJumping||player.IsAirComboActive)return false;
            if(left==null||right==null||!animator.HasState(0,Animator.StringToHash("Base Layer.JumpToHang")))return false;
            HangRail best=null;float distance=grabRange;
            foreach(var candidate in FindObjectsByType<HangRail>(FindObjectsSortMode.None))
            {
                var point=candidate.ClosestPoint(transform.position);
                float height=point.y-controller.bounds.min.y;
                Vector3 flat=point-transform.position;flat.y=0;
                if(height<1.4f||height>maxGrabHeight||flat.magnitude>distance)continue;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll((left.position+right.position)*.5f,point-(left.position+right.position)*.5f,Vector3.Distance(point,(left.position+right.position)*.5f),~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(transform)&&hit.collider!=candidate.GetComponent<Collider>()){blocked=true;break;}
                if(blocked)continue;
                best=candidate;distance=flat.magnitude;
            }
            if(best==null)return false;
            rail=best;grip=rail.ClosestPoint(transform.position);railCollider=rail.GetComponent<Collider>();
            Vector3 facing=Vector3.Cross(rail.Axis,Vector3.up).normalized;
            if(Vector3.Dot(facing,grip-transform.position)<0)facing=-facing;
            if(facing.sqrMagnitude>.1f)transform.rotation=Quaternion.LookRotation(facing);
            player.Combat.CancelForDamage();player.AirCombo.OnDisable();
            IsHanging=true;idle=false;elapsed=0;
            if(railCollider!=null){ignored=!Physics.GetIgnoreCollision(controller,railCollider);if(ignored)Physics.IgnoreCollision(controller,railCollider,true);}
            animator.CrossFadeInFixedTime("Base Layer.JumpToHang",.08f,0,0);
            return true;
        }
        void LateUpdate()
        {
            if(!IsHanging||left==null||right==null||!controller.enabled)return;
            Vector3 target=rail.ClosestPoint(grip);
            Vector3 error=target-(left.position+right.position)*.5f;
            // Follow the animated hands progressively during the jump, then hold their midpoint at the rail.
            float blend=idle?1:Mathf.Clamp01(elapsed/1.2f);
            controller.Move(error*blend);
        }
        public void Release(bool animate)
        {
            if(!IsHanging)return;
            IsHanging=false;cooldown=.6f;
            if(ignored&&railCollider!=null)Physics.IgnoreCollision(controller,railCollider,false);
            ignored=false;rail=null;
            if(player!=null&&player.Locomotion!=null)player.Locomotion.ResumeAirDodge();
            if(animate)animator.CrossFadeInFixedTime("Base Layer.LinkJumpLoop",.1f,0,0);
        }
        void OnDisable(){Release(false);}
    }
}