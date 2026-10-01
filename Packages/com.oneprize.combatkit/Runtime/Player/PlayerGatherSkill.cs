using UnityEngine;
using ParrySystem.Combat;
namespace ParrySystem.Player
{
    public sealed class PlayerGatherSkill : MonoBehaviour
    {
        [SerializeField,Range(0,1)] private float recallProgress=.36f;
        [SerializeField,Min(0)] private float range=5f;
        [SerializeField,Range(0,90)] private float halfAngle=30f;
        [SerializeField,Min(.05f)] private float pullDuration=.35f;
        [SerializeField,Min(.5f)] private float gatherDistance=1.5f;
        [SerializeField,Min(.1f)] private float spacing=.9f;
        [SerializeField,Min(0)] private float heightTolerance=2f;
        private PlayerCombat combat;
        private Animator animator;
        private int sequence=-1;
        private bool fired;
        public int LastGatherCount { get; private set; }
        void Awake(){combat=GetComponent<PlayerCombat>();animator=GetComponent<Animator>();}
        void LateUpdate()
        {
            if(!combat.IsUsingSkillI)return;
            if(sequence!=combat.AttackSequence){sequence=combat.AttackSequence;fired=false;LastGatherCount=0;}
            if(fired)return;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(animator.IsInTransition(0)){var next=animator.GetNextAnimatorStateInfo(0);if(next.IsName("Base Layer.Skill_I"))state=next;}
            if(!state.IsName("Base Layer.Skill_I")||state.normalizedTime<recallProgress)return;
            fired=true;
            var origin=transform.position;
            var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            var targets=new System.Collections.Generic.List<EnemyDamageReaction>();
            foreach(var enemy in FindObjectsByType<EnemyDamageReaction>(FindObjectsSortMode.None)){
                if(!enemy.isActiveAndEnabled||enemy.CurrentHealth<=0)continue;
                var delta=enemy.transform.position-origin;
                if(Mathf.Abs(delta.y)>heightTolerance)continue;
                delta.y=0;
                if(delta.sqrMagnitude>range*range||Vector3.Angle(forward,delta)>halfAngle)continue;
                bool blocked=false;
                foreach(var hit in Physics.RaycastAll(origin,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.collider.transform.IsChildOf(transform)&&hit.collider.GetComponentInParent<EnemyDamageReaction>()==null){blocked=true;break;}
                if(!blocked)targets.Add(enemy);
            }
            // Preserve left-to-right order so pulled enemies do not cross and block one another.
            targets.Sort((a,b)=>Vector3.Dot(a.transform.position-origin,right).CompareTo(Vector3.Dot(b.transform.position-origin,right)));
            foreach(var enemy in targets){
                int slot=LastGatherCount;int column=slot%5;int row=slot/5;
                int columns=Mathf.Min(5,targets.Count-row*5);
                float side=(column-(columns-1)*.5f)*spacing;
                Vector3 destination=origin+forward*(gatherDistance+row*spacing)+right*side;
                destination.y=enemy.transform.position.y;
                if(enemy.BeginGather(destination,transform,pullDuration))LastGatherCount++;
            }
        }
    }
}
