using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CombatSystem.Player
{
    /// <summary>
    /// 납도 콤보의 베기마다 검기 투사체를 한 발씩 발사하고, 날아가는 동안 적/보스를 맞히거나 벽에 막히면 소멸시킨다.
    /// 칼날 자체의 명중도 같은 장부로 처리해 한 번의 베기에서 칼날과 검기가 같은 대상을 두 번 맞히지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class PlayerSwordWave : MonoBehaviour
    {
        #region 필드

        [SerializeField] private GameObject wavePrefab;
        [SerializeField, Min(.1f)] private float speed = 20f;
        [SerializeField, Min(.1f)] private float range = 100f;
        [SerializeField, Min(.01f)] private float size = .65f;
        [SerializeField] private Vector3 spawnOffset = new Vector3(0,.35f,.8f);
        private PlayerCharacter player;
        private Animator animator;
        private int emittedSequence = -1;
        private readonly List<GameObject> live = new List<GameObject>();
        /// <summary>지금까지 발사한 검기 수(디버그/테스트 확인용 누적 카운트).</summary>
        public int FiredCount { get; private set; }

        /// <summary>한 번의 베기(공격 시퀀스)에서 이미 맞힌 대상을 기록하는 명중 장부.</summary>
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

        #endregion

        #region 명중 판정

        /// <summary>현재 공격 시퀀스의 명중 장부를 반환한다. 새 베기가 시작됐으면 새 장부를 만든다.</summary>
        private AttackHits CurrentHits()
        {
            if(currentHits == null || currentHits.Sequence != player.Combat.AttackSequence)
                currentHits = new AttackHits { Sequence = player.Combat.AttackSequence, ComboStep = player.Combat.SheathedComboStep };
            return currentHits;
        }
        /// <summary>
        /// 납도 콤보의 칼날이 닿은 대상을 현재 베기의 장부로 명중 처리한다.
        /// 칼날과 검기 투사체는 베기 단위 장부를 공유하며, 날아가는 검기는 다음 베기가 시작돼도 자기 장부를 유지한다.
        /// </summary>
        public void TryHitWithBlade(Collider target) { ApplyHit(target, CurrentHits()); }

        /// <summary>대상이 보스 또는 살아 있는 적이고 이 장부에서 아직 맞지 않았으면 피해를 준다.</summary>
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
        /// <summary>플레이어 자신, 검기 자신, 적, 보스를 제외한 콜라이더를 검기를 막는 장애물(벽 등)로 판정한다.</summary>
        private bool IsObstacle(Collider collider, GameObject wave)
        {
            return !collider.transform.IsChildOf(transform) && !collider.transform.IsChildOf(wave.transform) &&
                collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() == null &&
                collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() == null;
        }
        /// <summary>박스 겹침 검사를 할당 없이 수행한다. 버퍼가 가득 차면 크기를 늘려 결과 누락을 막는다.</summary>
        private int Overlap(Vector3 center, Vector3 half, Quaternion rotation)
        {
            int count;
            while((count = Physics.OverlapBoxNonAlloc(center, half, overlapBuffer, rotation, ~0, QueryTriggerInteraction.Ignore)) == overlapBuffer.Length)
                System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
            return count;
        }
        /// <summary>박스 캐스트를 할당 없이 수행한다. 버퍼가 가득 차면 크기를 늘려 결과 누락을 막는다.</summary>
        private int Sweep(Vector3 center, Vector3 half, Vector3 direction, Quaternion rotation, float distance)
        {
            int count;
            while((count = Physics.BoxCastNonAlloc(center, half, direction, castBuffer, rotation, distance, ~0, QueryTriggerInteraction.Ignore)) == castBuffer.Length)
                System.Array.Resize(ref castBuffer, castBuffer.Length * 2);
            return count;
        }

        #endregion

        #region 검기 발사와 비행

        /// <summary>플레이어와 애니메이터 참조를 캐시한다.</summary>
        private void Awake(){player=GetComponent<PlayerCharacter>();animator=GetComponent<Animator>();}
        /// <summary>
        /// 납도 콤보 베기의 히트 구간 시작 시점에 맞춰 베기당 한 번 검기를 생성한다.
        /// 프리팹에 딸린 데모용 스크립트/조명을 끄고 파티클을 재시작한 뒤 비행 코루틴을 시작한다.
        /// </summary>
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
            // 진행 방향 축으로만 굴려서 첫 번째 베기 검기를 비행 경로 변화 없이 가로로 눕힌다.
            Quaternion rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(0,0,combat.SheathedComboStep==0?90f:0f);
            var wave=Instantiate(wavePrefab,transform.TransformPoint(spawnOffset),rotation);
            wave.name="Sheathed Sword Wave";
            wave.transform.localScale=wavePrefab.transform.localScale*size;
            // 임포트한 데모 에셋의 이동/데칼 스크립트는 전투 로직과 무관하므로 끈다.
            foreach(var script in wave.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var light in wave.GetComponentsInChildren<Light>(true))light.enabled=false;
            foreach(var ps in wave.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                // 세로로 선 두 번째 검기에는 지면 충돌 자국이나 물결 효과가 없어야 한다.
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
        /// <summary>파티클이 검기 프리팹 안의 지면 효과(Decal/Puddle) 계층에 속하는지 확인한다.</summary>
        private static bool IsGroundEffect(Transform effect,Transform root)
        {
            for(var current=effect;current!=null && current!=root;current=current.parent)
                if(current.name=="Decal" || current.name=="Puddle")return true;
            return false;
        }
        /// <summary>
        /// 검기를 사거리만큼 직진시키며 매 프레임 겹침/스윕 검사로 대상을 맞힌다.
        /// 벽에 막히면 즉시 제거하고, 사거리 끝에 닿으면 파티클이 사라질 때까지(최대 2초) 기다렸다가 제거한다.
        /// </summary>
        private IEnumerator Fly(GameObject wave,Vector3 direction,AttackHits attack)
        {
            Vector3 half = attack.ComboStep == 0 ? horizontalHitHalfExtents : verticalHitHalfExtents;
            Quaternion hitRotation = Quaternion.LookRotation(direction);
            float travelled=0;
            bool hitWall=false;
            // 발사 위치 오프셋 때문에 가까운 벽 너머에 검기가 생성되는 것을 막는다.
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
                // 물리 쿼리 결과 순서와 상관없이 장애물을 대상보다 먼저 판정한다.
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
        /// <summary>비행 중인 검기를 모두 제거하고 발사/명중 기록을 초기화한다.</summary>
        private void OnDisable(){StopAllCoroutines();foreach(var wave in live)if(wave!=null)Destroy(wave);live.Clear();emittedSequence=-1;currentHits=null;}

        #endregion
    }
}
