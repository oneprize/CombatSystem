using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSlopeSlide : MonoBehaviour
    {
        [Range(0,60)] public float minimumAngle=18f;
        public float maxSpeed=6f, acceleration=8f;
        public bool IsSliding {get;private set;}
        PlayerCharacter player; CharacterController controller; Animator animator;float speed;
        void Awake(){player=GetComponent<PlayerCharacter>();controller=GetComponent<CharacterController>();animator=GetComponent<Animator>();}
        public bool Tick(float dt)
        {
            if(!isActiveAndEnabled||!controller.enabled)return false;
            var c=player.Controls;
            if(player.IsDead||player.IsHitReacting||player.IsAttacking||player.IsDodging||player.IsAirComboActive||player.IsLinkJumping||
               (player.Hang!=null&&player.Hang.IsHanging)||c.JumpPressed||c.DodgePressed||c.AttackPressed||
               c.SkillAPressed||c.SkillBPressed||c.SkillGPressed||c.UpperAttackPressed)
            {End(false);return false;}
            RaycastHit floor=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(controller.bounds.center,.15f,Vector3.down,controller.bounds.extents.y+.2f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)||hit.normal.y<.1f||
                   hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null||
                   hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                if(hit.distance<nearest){floor=hit;nearest=hit.distance;}
            }
            float angle=nearest<float.PositiveInfinity?Vector3.Angle(floor.normal,Vector3.up):0;
            if(angle<(IsSliding?minimumAngle-2:minimumAngle)||angle>70){End(true);return false;}
            Vector3 downhill=Vector3.ProjectOnPlane(Vector3.down,floor.normal).normalized;
            var cam=player.CameraTransform;Vector3 forward=Vector3.ProjectOnPlane(cam!=null?cam.forward:transform.forward,Vector3.up).normalized;
            Vector3 input=forward*c.Move.y+Vector3.Cross(Vector3.up,forward)*c.Move.x;
            if(Vector3.Dot(input,Vector3.ProjectOnPlane(downhill,Vector3.up).normalized)<-.25f){End(true);return false;}
            if(!IsSliding)
            {
                if(!animator.HasState(0,Animator.StringToHash("Base Layer.SlopeSlide")))return false;
                IsSliding=true;speed=1.5f;
                animator.CrossFadeInFixedTime("Base Layer.SlopeSlide",.15f,0,0);
            }
            speed=Mathf.MoveTowards(speed,maxSpeed,acceleration*dt);
            Vector3 facing=Vector3.ProjectOnPlane(downhill,Vector3.up);
            if(facing.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-12*dt));
            controller.Move((downhill*speed-Vector3.up*1.5f)*dt);
            return true;
        }
        public void End(bool animate)
        {
            if(!IsSliding)return;
            IsSliding=false;speed=0;
            player.Locomotion.ResumeAirDodge();
            if(animate&&!player.IsDead&&!player.IsHitReacting)animator.CrossFadeInFixedTime(controller.isGrounded?"Base Layer.Idle":"Base Layer.LinkJumpLoop",.15f,0,0);
        }
        void OnDisable(){if(player!=null)End(false);}
    }
}