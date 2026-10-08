using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CombatSystem.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class PlayerSwordWave : MonoBehaviour
    {
        [SerializeField] private GameObject wavePrefab;
        [SerializeField, Min(.1f)] private float speed = 20f;
        [SerializeField, Min(.1f)] private float range = 12f;
        [SerializeField, Min(.01f)] private float size = .65f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0,.35f,.8f);
        private PlayerCharacter player;
        private Animator animator;
        private int emittedSequence = -1;
        private readonly List<GameObject> live = new List<GameObject>();
        public int FiredCount { get; private set; }
        private void Awake(){player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();}
        private void LateUpdate()
        {
            if(wavePrefab==null||player.Combat==null||player.IsHitReacting)return;
            var combat=player.Combat;
            if(!combat.IsSheathedCombo||emittedSequence==combat.AttackSequence)return;
            string name="Base Layer."+combat.CurrentAttackStateName;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(animator.IsInTransition(0)){var next=animator.GetNextAnimatorStateInfo(0);if(next.IsName(name))state=next;}
            if(!state.IsName(name)||state.normalizedTime<player.Profile.combat.sheathedHitWindow.x)return;
            emittedSequence=combat.AttackSequence;
            Vector3 direction=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            // Roll around travel direction so the first slash lies sideways without changing its flight.
            Quaternion rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,combat.SheathedComboStep==0?90f:0f);
            var wave=Instantiate(wavePrefab,transform.TransformPoint(spawnOffset),rotation);
            wave.name="Sheathed Sword Wave";
            wave.transform.localScale=wavePrefab.transform.localScale*size;
            // The imported demo movement/decal scripts are not part of combat gameplay.
            foreach(var script in wave.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var light in wave.GetComponentsInChildren<Light>(true))light.enabled=false;
            foreach(var ps in wave.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                // The upright second wave has no ground impacts or water ripples.
                if(combat.SheathedComboStep==1 && IsGroundEffect(ps.transform,wave.transform))
                {
                    ps.gameObject.SetActive(false);
                    continue;
                }
                var main=ps.main;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.stopAction=ParticleSystemStopAction.None;
                ps.Play(false);
            }
            live.Add(wave);FiredCount++;StartCoroutine(Fly(wave,direction));
        }
        private static bool IsGroundEffect(Transform effect,Transform root)
        {
            for(var current=effect;current!=null && current!=root;current=current.parent)
                if(current.name=="Decal" || current.name=="Puddle")return true;
            return false;
        }
        private IEnumerator Fly(GameObject wave,Vector3 direction)
        {
            float travelled=0;
            while(wave!=null&&travelled<range)
            {
                float step=Mathf.Min(speed*Time.deltaTime,range-travelled);
                bool blocked=false;
                foreach(var hit in Physics.SphereCastAll(wave.transform.position,.15f,direction,step,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(transform)||hit.transform.IsChildOf(wave.transform)||
                        hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>()!=null||
                        hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>()!=null)continue;
                    step=Mathf.Min(step,hit.distance);blocked=true;
                }
                wave.transform.position+=direction*step;travelled+=step;
                if(blocked)break;
                yield return null;
            }
            if(wave!=null)
            {
                float fade=0;
                foreach(var ps in wave.GetComponentsInChildren<ParticleSystem>(true))
                {
                    fade=Mathf.Max(fade,ps.main.startLifetime.constantMax);
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                }
                yield return new WaitForSeconds(Mathf.Min(fade,2f));
                live.Remove(wave);Destroy(wave);
            }
        }
        private void OnDisable(){StopAllCoroutines();foreach(var wave in live)if(wave!=null)Destroy(wave);live.Clear();emittedSequence=-1;}
    }
}
