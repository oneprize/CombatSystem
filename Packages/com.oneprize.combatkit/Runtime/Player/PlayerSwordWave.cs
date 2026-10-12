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
        [SerializeField, Min(.1f)] private float range = 100f;
        [SerializeField, Min(.01f)] private float size = .65f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0,.35f,.8f);
        private PlayerCharacter player;
        private Animator animator;
        private int emittedSequence = -1;
        private readonly List<GameObject> live = new List<GameObject>();
        public int FiredCount { get; private set; }

        private sealed class AttackHits
        {
            public int Sequence, ComboStep;
            public readonly HashSet<UnityEngine.Object> Targets = new HashSet<UnityEngine.Object>();
        }
        private AttackHits currentHits;
        private RaycastHit[] castBuffer = new RaycastHit[32];
        private Collider[] overlapBuffer = new Collider[32];
        [SerializeField] private Vector3 horizontalHitHalfExtents = new Vector3(1.3f, .25f, .25f);
        [SerializeField] private Vector3 verticalHitHalfExtents = new Vector3(.25f, 1.1f, .25f);

        private AttackHits CurrentHits()
        {
            if(currentHits == null || currentHits.Sequence != player.Combat.AttackSequence)
                currentHits = new AttackHits { Sequence = player.Combat.AttackSequence, ComboStep = player.Combat.SheathedComboStep };
            return currentHits;
        }
        // Both the blade and its projectile claim targets in the same per-swing ledger.
        // In-flight waves retain their ledger even when a later swing starts.
        public void TryHitWithBlade(Collider target) { ApplyHit(target, CurrentHits()); }

        private void ApplyHit(Collider collider, AttackHits attack)
        {
            var boss = collider.GetComponentInParent<CombatSystem.Boss.BossCombat>();
            if(boss != null)
            {
                if(attack.Targets.Add(boss)) boss.TryReceivePlayerHit();
                return;
            }
            var enemy = collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>();
            if(enemy != null && enemy.CurrentHealth > 0f && attack.Targets.Add(enemy))
                enemy.ReceiveGroundComboHit(transform, attack.ComboStep % 2, false);
        }
        private bool IsObstacle(Collider collider, GameObject wave)
        {
            return !collider.transform.IsChildOf(transform) && !collider.transform.IsChildOf(wave.transform) &&
                collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() == null &&
                collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() == null;
        }
        private int Overlap(Vector3 center, Vector3 half, Quaternion rotation)
        {
            int count;
            while((count = Physics.OverlapBoxNonAlloc(center, half, overlapBuffer, rotation, ~0, QueryTriggerInteraction.Ignore)) == overlapBuffer.Length)
                System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
            return count;
        }
        private int Sweep(Vector3 center, Vector3 half, Vector3 direction, Quaternion rotation, float distance)
        {
            int count;
            while((count = Physics.BoxCastNonAlloc(center, half, direction, castBuffer, rotation, distance, ~0, QueryTriggerInteraction.Ignore)) == castBuffer.Length)
                System.Array.Resize(ref castBuffer, castBuffer.Length * 2);
            return count;
        }

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
            live.Add(wave);FiredCount++;StartCoroutine(Fly(wave,direction,CurrentHits()));
        }
        private static bool IsGroundEffect(Transform effect,Transform root)
        {
            for(var current=effect;current!=null && current!=root;current=current.parent)
                if(current.name=="Decal" || current.name=="Puddle")return true;
            return false;
        }
        private IEnumerator Fly(GameObject wave,Vector3 direction,AttackHits attack)
        {
            Vector3 half = attack.ComboStep == 0 ? horizontalHitHalfExtents : verticalHitHalfExtents;
            Quaternion hitRotation = Quaternion.LookRotation(direction);
            float travelled=0;
            bool hitWall=false;
            // Prevent the muzzle offset from spawning a projectile through a nearby wall.
            Vector3 muzzle = wave.transform.position + Vector3.up * half.y;
            Vector3 origin = transform.position; origin.y = muzzle.y;
            Vector3 launch = muzzle - origin;
            int launchCount = Sweep(origin, half, launch.normalized, hitRotation, launch.magnitude);
            int initialCount = Overlap(origin, half, hitRotation);
            for(int i=0;i<initialCount;i++) if(IsObstacle(overlapBuffer[i],wave)) { travelled=range; hitWall=true; }
            for(int i=0;i<launchCount;i++) if(IsObstacle(castBuffer[i].collider,wave)) { travelled=range; hitWall=true; }
            while(wave!=null&&travelled<range)
            {
                float step=Mathf.Min(speed*Time.deltaTime,range-travelled);
                bool blocked=false;
                Vector3 center = wave.transform.position + Vector3.up * half.y;
                int overlaps = Overlap(center, half, hitRotation);
                // Resolve blockers before targets, independently of physics query ordering.
                for(int i=0;i<overlaps;i++)
                    if(IsObstacle(overlapBuffer[i], wave)) { blocked=true; step=0f; break; }
                if(!blocked)
                {
                    int count = Sweep(center, half, direction, hitRotation, step);
                    for(int i=0;i<count;i++)
                        if(IsObstacle(castBuffer[i].collider, wave))
                        { step=Mathf.Min(step,castBuffer[i].distance); blocked=true; }
                    for(int i=0;i<overlaps;i++) ApplyHit(overlapBuffer[i], attack);
                    for(int i=0;i<count;i++)
                        if(!IsObstacle(castBuffer[i].collider,wave) && (!blocked || castBuffer[i].distance < step))
                            ApplyHit(castBuffer[i].collider, attack);
                }
                wave.transform.position+=direction*step;travelled+=step;
                if(blocked){hitWall=true;break;}
                yield return null;
            }
            if(wave!=null)
            {
                if(hitWall){live.Remove(wave);Destroy(wave);yield break;}
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
        private void OnDisable(){StopAllCoroutines();foreach(var wave in live)if(wave!=null)Destroy(wave);live.Clear();emittedSequence=-1;currentHits=null;}
    }
}
