using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSlopeSlide : MonoBehaviour
    {
        [Range(0,60)] public float minimumAngle=18f;
        public float maxSpeed=6f, acceleration=8f;
        [Header("슬라이딩 자세 접지")]
        [Tooltip("앉은 자세의 골반 관절과 경사면 사이 거리(m).")]
        [Min(.05f)] public float seatedHipHeight=.18f;
        [Tooltip("지지하는 발 관절과 경사면 사이 거리(m).")]
        [Min(0f)] public float slideFootHeight=.1f;
        [Range(0f,60f)] public float maximumPoseTilt=45f;
        public bool IsSliding {get;private set;}
        PlayerCharacter player; CharacterController controller; Animator animator;float speed;
        bool starting,ending;float exitElapsed;
        Transform hips,leftFoot,rightFoot;
        Vector3 poseNormal=Vector3.up;
        bool hasPoseNormal;
        void Awake()
        {
            player=GetComponent<PlayerCharacter>();controller=GetComponent<CharacterController>();animator=GetComponent<Animator>();
            if(animator.isHuman)
            {
                hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
        }
        public bool Tick(float dt)
        {
            if(!isActiveAndEnabled||!controller.enabled)return false;
            var c=player.Controls;
            if(player.IsDead||player.IsHitReacting||player.IsAttacking||player.IsDodging||player.IsAirComboActive||player.IsLinkJumping||
               (player.Hang!=null&&player.Hang.IsHanging)||c.JumpPressed||c.DodgePressed||c.AttackPressed||
               c.SkillAPressed||c.ParryPressed||c.SkillGPressed||c.UpperAttackPressed)
            {End(false);return false;}
            if(ending){exitElapsed+=dt;controller.Move(Vector3.down*2f*dt);var st=animator.GetCurrentAnimatorStateInfo(0);if((st.IsName("SlopeSlideEnd")&&st.normalizedTime>=1)||exitElapsed>.8f){ending=false;IsSliding=false;player.Locomotion.ResumeAirDodge();animator.CrossFadeInFixedTime(controller.isGrounded?"Base Layer.Idle":"Base Layer.LinkJumpLoop",.12f,0,0);}return true;}
            if(starting){var st=animator.GetCurrentAnimatorStateInfo(0);if(st.IsName("SlopeSlideStart")&&st.normalizedTime>=.9f){starting=false;animator.CrossFadeInFixedTime("Base Layer.SlopeSlide",.1f,0,0);}}
            RaycastHit floor=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.SphereCastAll(controller.bounds.center,.15f,Vector3.down,controller.bounds.extents.y+.2f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)||hit.normal.y<.1f||
                   hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null||
                   hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                if(hit.distance<nearest){floor=hit;nearest=hit.distance;}
            }
            float angle=nearest<float.PositiveInfinity?Vector3.Angle(floor.normal,Vector3.up):0;
            if(angle<(IsSliding?minimumAngle-2:minimumAngle)||angle>70){End(true);return ending;}
            Vector3 downhill=Vector3.ProjectOnPlane(Vector3.down,floor.normal).normalized;
            var cam=player.CameraTransform;Vector3 forward=Vector3.ProjectOnPlane(cam!=null?cam.forward:transform.forward,Vector3.up).normalized;
            Vector3 input=forward*c.Move.y+Vector3.Cross(Vector3.up,forward)*c.Move.x;
            if(Vector3.Dot(input,Vector3.ProjectOnPlane(downhill,Vector3.up).normalized)<-.25f){End(true);return ending;}
            if(!IsSliding)
            {
                if(!animator.HasState(0,Animator.StringToHash("Base Layer.SlopeSlide")))return false;
                IsSliding=true;speed=1.5f;starting=true;ending=false;
                animator.CrossFadeInFixedTime("Base Layer.SlopeSlideStart",.15f,0,0);
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
            starting=false;
            if(animate&&!player.IsDead&&!player.IsHitReacting){if(!ending){ending=true;exitElapsed=0;animator.CrossFadeInFixedTime("Base Layer.SlopeSlideEnd",.08f,0,0);}return;}
            ending=false;IsSliding=false;speed=0;
            player.Locomotion.ResumeAirDodge();
            if(animate&&!player.IsDead&&!player.IsHitReacting)animator.CrossFadeInFixedTime(controller.isGrounded?"Base Layer.Idle":"Base Layer.LinkJumpLoop",.15f,0,0);
        }

        static float GroundPoseWeight(AnimatorStateInfo state)
        {
            if(state.IsName("Base Layer.SlopeSlide"))return 1f;
            if(state.IsName("Base Layer.SlopeSlideStart"))
                return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.55f,.95f,state.normalizedTime));
            if(state.IsName("Base Layer.SlopeSlideEnd"))
                return 1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(state.normalizedTime/.45f));
            return 0f;
        }

        bool TryGetPoseFloor(Vector3 point,float scale,out RaycastHit floor)
        {
            floor=default;float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(point+Vector3.up*scale,Vector3.down,controller.height*scale+1.5f,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(transform)||hit.normal.y<.3f||
                   hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyStateMachine>()!=null||
                   hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                if(hit.distance<nearest){floor=hit;nearest=hit.distance;}
            }
            return !float.IsPositiveInfinity(nearest);
        }

        float FootGroundLift(Vector3 foot,float scale,float weight)
        {
            RaycastHit floor;
            if(!TryGetPoseFloor(foot,scale,out floor))return 0f;
            return Mathf.Max(0f,(slideFootHeight*scale*weight-Vector3.Dot(foot-floor.point,floor.normal))/floor.normal.y);
        }

        /// <summary>기존 IK 패스에서 골반과 지지 발을 경사면에 맞춘다. 캡슐과 이동 궤적은 변경하지 않는다.</summary>
        public void ApplyGroundPose(int layerIndex,Vector3 precedingBodyOffset)
        {
            if(layerIndex!=0)return;
            if(!isActiveAndEnabled||!IsSliding||!controller.enabled||!animator.isHuman||hips==null||leftFoot==null||rightFoot==null)
            {hasPoseNormal=false;return;}
            float weight=GroundPoseWeight(animator.GetCurrentAnimatorStateInfo(0));
            if(animator.IsInTransition(0))
                weight=Mathf.Lerp(weight,GroundPoseWeight(animator.GetNextAnimatorStateInfo(0)),
                    Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
            if(weight<=.001f)return;

            // 앞선 몸 높이 보정은 아직 Transform 뼈 위치에 반영되지 않았으므로 한 번만 포함한다.
            Vector3 hip=hips.position+precedingBodyOffset;
            float scale=Mathf.Abs(transform.lossyScale.y);
            RaycastHit floor;
            if(!TryGetPoseFloor(hip,scale,out floor)||controller.bounds.min.y-controller.skinWidth*scale-floor.point.y>.4f*scale)return;
            poseNormal=hasPoseNormal?Vector3.RotateTowards(poseNormal,floor.normal,6f*Time.deltaTime,0f):floor.normal;
            hasPoseNormal=true;
            Vector3 downhill=Vector3.ProjectOnPlane(Vector3.down,poseNormal).normalized;
            Vector3 forward=Vector3.ProjectOnPlane(downhill.sqrMagnitude>.01f?downhill:transform.forward,Vector3.up).normalized;
            Vector3 axis=Vector3.Cross(Vector3.up,forward);

            // 원본 앉은 클립의 다리 각도를 사용해 몸을 눕힌다. 골반만 내리면서 발이 바닥을 뚫는 것을 막는다.
            Vector3 left=leftFoot.position-hips.position,right=rightFoot.position-hips.position;
            Vector3 support=Vector3.Dot(left,poseNormal)<Vector3.Dot(right,poseNormal)?left:right;
            float along=Vector3.Dot(support,forward),length=new Vector2(along,support.y).magnitude;
            float pitch=0f;
            if(along>.05f*scale&&length>.1f*scale)
            {
                float currentAngle=Mathf.Atan2(support.y,along)*Mathf.Rad2Deg;
                float slopeAngle=Mathf.Atan2(downhill.y,Vector3.Dot(downhill,forward))*Mathf.Rad2Deg;
                float contactAngle=Mathf.Asin(Mathf.Clamp((slideFootHeight-seatedHipHeight)*scale/length,-.8f,.8f))*Mathf.Rad2Deg;
                pitch=Mathf.Clamp(currentAngle-slopeAngle-contactAngle,-maximumPoseTilt,maximumPoseTilt);
            }
            Quaternion tilt=Quaternion.AngleAxis(pitch*weight,axis);
            float heightCorrection=(seatedHipHeight*scale-Vector3.Dot(hip-floor.point,poseNormal))/Mathf.Max(.3f,poseNormal.y);
            heightCorrection=Mathf.Clamp(heightCorrection,-.9f*scale,.3f*scale)*weight;
            // 경사로 끝에서는 발 아래의 평지가 골반 아래 경사면의 연장선보다 높다.
            // 실제 발 위치도 검사해 바닥 관통 없이 일어서는 동작으로 이어 준다.
            Vector3 correctedHip=hip+Vector3.up*heightCorrection;
            float footLift=Mathf.Max(FootGroundLift(correctedHip+tilt*left,scale,weight),FootGroundLift(correctedHip+tilt*right,scale,weight));
            heightCorrection+=Mathf.Min(footLift,Mathf.Max(0f,-heightCorrection)+.3f*scale);
            animator.bodyPosition=hip+tilt*(animator.bodyPosition-hip)+Vector3.up*heightCorrection;
            animator.bodyRotation=tilt*animator.bodyRotation;
        }
        void OnDisable(){if(player!=null)End(false);}
    }
}
