using System.Collections.Generic;
using UnityEngine;
using ParrySystem.Combat;
namespace ParrySystem.Player
{
    [RequireComponent(typeof(CharacterController),typeof(Animator))]
    public sealed class PlayerPiercingDash : MonoBehaviour
    {
        [SerializeField,Min(0)] private float distance=5f;
        [SerializeField,Min(.01f)] private float duration=.22f;
        [SerializeField,Range(0,1)] private float startProgress=15f/45f;
        [SerializeField,Min(.05f)] private float hitRadius=.7f;
        [Header("Skill B (R)")]
        [SerializeField,Min(0)] private float skillBDistance=5f;
        [SerializeField,Min(.01f)] private float skillBDuration=.28f;
        [SerializeField,Range(0,1)] private float skillBStartProgress=.32f;
        private PlayerCombat combat;
        private CharacterController character;
        private Animator animator;
        private int sequence=-1;
        private bool started,finished;
        private float elapsed;
        private Vector3 direction;
        private readonly List<Collider> ignored=new List<Collider>();
        private readonly HashSet<EnemyDamageReaction> struck=new HashSet<EnemyDamageReaction>();
        private readonly HashSet<ParrySystem.Boss.BossCombat> bosses=new HashSet<ParrySystem.Boss.BossCombat>();
        public bool IsDashing => started&&!finished;
        void Awake(){combat=GetComponent<PlayerCombat>();character=GetComponent<CharacterController>();animator=GetComponent<Animator>();}
        void Update()
        {
            if(!combat.IsUsingPiercingSkill){RestoreCollisions();started=false;finished=false;}
        }
        public void TickAnimation()
        {
            if(!combat.IsUsingPiercingSkill || !character.enabled)return;
            if(sequence!=combat.AttackSequence){
                RestoreCollisions();sequence=combat.AttackSequence;
                started=finished=false;elapsed=0;struck.Clear();bosses.Clear();
                direction=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            }
            if(finished)return;
            float dashDistance = combat.IsUsingSkillB ? skillBDistance : distance;
            float dashDuration = Mathf.Max(.01f, combat.IsUsingSkillB ? skillBDuration : duration);
            float begin = combat.IsUsingSkillB ? skillBStartProgress : startProgress;
            string animationState = "Base Layer." + combat.CurrentAttackStateName;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(animator.IsInTransition(0)){
                var next=animator.GetNextAnimatorStateInfo(0);
                if(next.IsName(animationState))state=next;
            }
            if(!state.IsName(animationState) || state.normalizedTime<begin)return;
            if(!started){
                started=true;
                foreach(var enemy in FindObjectsByType<EnemyDamageReaction>(FindObjectsSortMode.None))
                    foreach(var col in enemy.GetComponentsInChildren<Collider>())
                        if(col.enabled&&!col.isTrigger&&!Physics.GetIgnoreCollision(character,col)){
                            Physics.IgnoreCollision(character,col,true);ignored.Add(col);
                        }
            }
            if(Time.deltaTime<=0)return;
            float step=Mathf.Min(Time.deltaTime,dashDuration-elapsed);
            Vector3 from=transform.position;
            character.Move(direction*(dashDistance/dashDuration*step));
            Vector3 to=transform.position;
            Physics.SyncTransforms();
            // Sweep the traveled segment, including low-FPS movement, once per enemy.
            foreach(var col in Physics.OverlapCapsule(from+Vector3.down*.1f,to+Vector3.down*.1f,hitRadius,~0,QueryTriggerInteraction.Ignore)){
                var enemy=col.GetComponentInParent<EnemyDamageReaction>();
                if(enemy!=null&&struck.Add(enemy))enemy.ReceivePiercingHit(transform,direction);
                var boss=col.GetComponentInParent<ParrySystem.Boss.BossCombat>();
                if(boss!=null&&bosses.Add(boss))boss.TryReceivePlayerHit();
            }
            elapsed+=step;
            if(elapsed>=dashDuration || Vector3.Dot(to-from,direction)<dashDistance/dashDuration*step*.1f){
                finished=true;RestoreCollisions();
            }
        }
        void RestoreCollisions()
        {
            // Clear overlap before restoring collision if damage/cancel stopped inside an enemy.
            foreach(var col in ignored)if(col!=null){
                if(character!=null&&character.enabled&&col.enabled){
                    Vector3 away;float depth;
                    if(Physics.ComputePenetration(character,transform.position,transform.rotation,
                        col,col.transform.position,col.transform.rotation,out away,out depth))
                        character.Move(away*(depth+.03f));
                }
                Physics.IgnoreCollision(character,col,false);
            }
            ignored.Clear();
        }
        void OnDisable(){RestoreCollisions();started=false;finished=false;}
    }
}
