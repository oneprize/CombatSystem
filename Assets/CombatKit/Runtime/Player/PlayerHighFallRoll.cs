using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHighFallRoll : MonoBehaviour
    {
        [Min(0)] public float minimumFallHeight=6f;
        public bool IsRolling {get;private set;}
        public float LastFallHeight {get;private set;}
        PlayerCharacter player;Animator animator;CharacterController controller;
        bool airborne;float apex,airTime,elapsed;
        void Awake(){player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();controller=GetComponent<CharacterController>();}
        public bool Tick(float dt)
        {
            if(!isActiveAndEnabled||!controller.enabled)return false;
            if(player.IsDead||player.IsHitReacting){Cancel();return false;}
            if(IsRolling)
            {
                elapsed+=dt;
                var state=animator.GetCurrentAnimatorStateInfo(0);
                controller.Move(Vector3.down*2f*dt);
                if((state.IsName("HighFallRoll")&&state.normalizedTime>=1)||elapsed>2)
                {
                    IsRolling=false;player.Locomotion.OnCombatLanded();
                    animator.CrossFadeInFixedTime("Base Layer.Idle",.15f,0,0);
                }
                return true;
            }
            if(player.IsAttacking||player.IsAirComboActive||player.IsLinkJumping||player.IsDodging||
               (player.Hang!=null&&player.Hang.IsHanging)||(player.SlopeSlide!=null&&player.SlopeSlide.IsSliding))
            {airborne=false;airTime=0;return false;}
            float feet=controller.bounds.min.y;
            if(!controller.isGrounded)
            {
                if(!airborne){airborne=true;apex=feet;airTime=0;}
                apex=Mathf.Max(apex,feet);airTime+=dt;return false;
            }
            if(!airborne)return false;
            LastFallHeight=apex-feet;airborne=false;
            var settings=player.Profile.locomotion;
            float doubleJumpHeight=settings.jumpForce*settings.jumpForce/Mathf.Max(.01f,Mathf.Abs(settings.gravity));
            if(airTime<.15f||LastFallHeight<Mathf.Max(minimumFallHeight,doubleJumpHeight+1f))return false;
            if(!animator.HasState(0,Animator.StringToHash("Base Layer.HighFallRoll")))return false;
            player.Locomotion.OnCombatLanded();IsRolling=true;elapsed=0;
            animator.CrossFadeInFixedTime("Base Layer.HighFallRoll",.08f,0,0);return true;
        }
        public void ApplyRootMotion(Vector3 delta)
        {
            if(!IsRolling||!controller.enabled)return;
            delta.y=0;controller.Move(delta);
        }
        public void Cancel(){IsRolling=false;airborne=false;airTime=0;}
        void OnDisable(){Cancel();}
    }
}