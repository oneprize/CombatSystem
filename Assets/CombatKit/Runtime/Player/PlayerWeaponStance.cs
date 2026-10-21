using UnityEngine;
using UnityEngine.InputSystem;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerWeaponStance : MonoBehaviour
    {
        public Transform weaponRoot;
        public Vector3 holsterPosition = new Vector3(-.22f,0,-.1f);
        public Vector3 holsterEuler = new Vector3(0,0,130);
        [Range(0,1)] public float drawAttachTime=.25f, sheathAttachTime=.65f;
        public bool IsDrawn {get; private set;} = true;
        public bool IsTransitioning {get; private set;}
        PlayerCharacter player; Animator animator; Transform hand,hips;
        Vector3 handPos,handScale; Quaternion handRot;
        bool drawing,switched; float elapsed; int pending;
        void Awake()
        {
            player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();
            if(weaponRoot==null)foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="WeaponRoot"){weaponRoot=t;break;}
            if(weaponRoot==null)return;
            hand=weaponRoot.parent;handPos=weaponRoot.localPosition;handRot=weaponRoot.localRotation;handScale=weaponRoot.localScale;
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);
        }
        int Request()
        {
            var c=player.Controls;
            if(c.SkillAPressed)return 2;if(c.SkillBPressed)return 3;if(c.SkillGPressed)return 4;
            if(c.UpperAttackPressed)return 5;
            if(c.AttackPressed)return c.SkillCModifierHeld?6:1;
            return 0;
        }
        public bool Tick(float dt)
        {
            if(weaponRoot==null||hips==null)return false;
            if(player.IsDead||player.IsHitReacting){Cancel();return false;}
            if(IsTransitioning)
            {
                int request=Request();if(request!=0)pending=request;
                elapsed+=dt;
                string name=drawing?"DrawSword":"SheathSword";
                var state=animator.GetCurrentAnimatorStateInfo(0);
                if(animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).IsName(name))state=animator.GetNextAnimatorStateInfo(0);
                if(state.IsName(name)&&!switched&&state.normalizedTime>=(drawing?drawAttachTime:sheathAttachTime))
                {Attach(drawing);switched=true;}
                if((state.IsName(name)&&state.normalizedTime>=1)||elapsed>3)
                {
                    Attach(drawing);IsTransitioning=false;
                    if(!drawing&&pending!=0){StartTransition(true);return true;}
                    animator.CrossFadeInFixedTime("Base Layer.Idle",.12f,0,0);
                    int next=pending;pending=0;
                    if(next==1)player.Combat.TryAttack();
                    else if(next==2)player.Combat.TrySkillA();
                    else if(next==3)player.Combat.TrySkillB();
                    else if(next==4)player.Combat.TrySkill();
                    else if(next==5)player.Combat.TryUpperAttack();
                    else if(next==6)player.Combat.TrySkillC();
                }
                return true;
            }
            if(player.IsAttacking||player.IsDodging||player.IsLinkJumping||player.IsAirComboActive||
                (player.Hang!=null&&player.Hang.IsHanging)||!player.Locomotion.IsGrounded)return false;
            int action=Request();
            if(!IsDrawn&&action!=0){pending=action;StartTransition(true);return IsTransitioning;}
            if(Keyboard.current!=null&&Keyboard.current.tKey.wasPressedThisFrame)
            {pending=0;StartTransition(!IsDrawn);return IsTransitioning;}
            return false;
        }
        public void StartTransition(bool draw)
        {
            if(weaponRoot==null||IsTransitioning)return;
            string state=draw?"DrawSword":"SheathSword";
            if(!animator.HasState(0,Animator.StringToHash("Base Layer."+state)))return;
            drawing=draw;switched=false;elapsed=0;IsTransitioning=true;
            animator.CrossFadeInFixedTime("Base Layer."+state,.1f,0,0);
        }
        void Attach(bool drawn)
        {
            IsDrawn=drawn;weaponRoot.SetParent(drawn?hand:hips,false);
            weaponRoot.localPosition=drawn?handPos:holsterPosition;
            weaponRoot.localRotation=drawn?handRot:Quaternion.Euler(holsterEuler);
            weaponRoot.localScale=handScale;
        }
        public void Cancel(){IsTransitioning=false;pending=0;}
        void OnDisable(){Cancel();if(weaponRoot!=null&&hand!=null)Attach(true);}
    }
}