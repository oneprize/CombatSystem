using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Data.Boss;
namespace CombatSystem.Boss
{
    /// <summary>
    /// 보스 패턴 1 투사체. 플레이어에게 가까워지면 한 번 슬로 모션 패리 창을 열고, 그 안에서 공격 입력이 들어오면
    /// 파편으로 부서지며 보스의 패리 누적을 올린다. 패리하지 못하고 맞으면 플레이어를 넉다운시킨다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BossProjectile : MonoBehaviour
    {
        #region 필드

        [SerializeField] private BossProjectileConfig config;
        private PlayerCharacter player;
        private BossCombat owner;
        private Rigidbody body;
        private Collider shell;
        private float remaining, savedScale, savedStep;
        private bool windowUsed, windowActive, resolved;
        /// <summary>현재 Time.timeScale을 바꿔 둔 투사체. 여러 투사체가 동시에 시간 배율을 덮어쓰지 않도록 한 개만 소유한다.</summary>
        private static BossProjectile timeOwner;
        public bool IsParryWindowOpen => windowActive;
        /// <summary>패리 창의 남은 실제 시간(초). 창이 닫혀 있으면 0.</summary>
        public float ParrySecondsRemaining => windowActive ? remaining : 0;
        /// <summary>패리·명중·충돌 중 하나로 이미 처리되었는지 여부. 보스 패턴 1 종료 조건으로 쓰인다.</summary>
        public bool Resolved => resolved;

        #endregion

        #region 초기화

        /// <summary>발사한 보스와 목표 플레이어를 연결한다. 생성 직후 BossCombat이 호출한다.</summary>
        public void Initialize(BossCombat boss, PlayerCharacter target) { owner=boss; player=target; }
        /// <summary>설정이 없으면 기본값으로 대체하고 리지드바디와 콜라이더를 캐시한다.</summary>
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BossProjectileConfig>();
                Debug.LogWarning("BossProjectile: no BossProjectileConfig assigned, using built-in defaults.", this);
            }
            body=GetComponent<Rigidbody>(); shell=GetComponent<SphereCollider>();
        }

        #endregion

        #region 패리 창

        /// <summary>
        /// 플레이어 쪽으로 다가오며 표면 거리가 패리 거리 이내가 되면 패리 창을 한 번만 연다.
        /// 창이 열려 있는 동안 투사체가 플레이어에 닿기 전에 끝나도록 남은 거리 기준의 안전 배율과 설정 배율 중 작은 값으로 시간을 늦춘다.
        /// </summary>
        private void Update()
        {
            if(resolved || player==null) { if(windowActive) RestoreTime(); return; }
            if(!windowUsed && timeOwner==null && Time.timeScale>.99f)
            {
                var c=player.GetComponent<CharacterController>();
                Vector3 target=c!=null?c.bounds.center:player.transform.position+Vector3.up;
                Vector3 delta=target-transform.position;
                float surfaceDistance=Mathf.Max(.05f,delta.magnitude-shell.bounds.extents.magnitude);
                if(surfaceDistance<=config.parryDistance && Vector3.Dot(body.linearVelocity,delta)>0)
                {
                    windowUsed=true; windowActive=true; remaining=config.parryDuration; timeOwner=this;
                    savedScale=Time.timeScale; savedStep=Time.fixedDeltaTime;
                    float safeScale=surfaceDistance/Mathf.Max(1,body.linearVelocity.magnitude)/(config.parryDuration*4);
                    float factor=Mathf.Max(.001f,Mathf.Min(config.slowScale,safeScale));
                    Time.timeScale=savedScale*factor; Time.fixedDeltaTime=savedStep*factor;
                }
            }
        }
        /// <summary>패리 창이 열린 동안 공격 입력을 받아 패리를 시도하고, 실제 시간 기준으로 창 시간이 다 되면 시간 배율을 복구한다.</summary>
        private void LateUpdate()
        {
            if(!windowActive || resolved) return;
            if(player!=null && !player.IsDodging && !player.IsHitReacting &&
                player.Controls.AttackPressed)
            { TryParry(); return; }
            remaining-=Time.unscaledDeltaTime;
            if(remaining<=0) RestoreTime();
        }
        /// <summary>
        /// 패리 창 안에서 플레이어의 보스 패리 입력을 소비해 투사체를 부순다. 플레이어를 투사체 쪽으로 돌리고 패리 연출을 재생한 뒤,
        /// 보스에 패리를 누적하고 메시 파편을 물리 조각으로 흩뿌린다.
        /// </summary>
        /// <returns>패리가 성립해 투사체가 파괴되었으면 true.</returns>
        public bool TryParry()
        {
            if(!windowActive || resolved || player==null || player.IsDodging || player.IsHitReacting)return false;
            if (!player.Combat.TryConsumeBossParry()) return false;
            Vector3 facing=transform.position-player.transform.position;
            facing.y=0;
            if(facing.sqrMagnitude>.0001f) player.transform.rotation=Quaternion.LookRotation(facing);
            player.Health.PlayProjectileParry();
            resolved=true; RestoreTime();
            player.PlayParryFeedback();
            if(owner!=null)owner.RegisterParry();
            shell.enabled=false; body.isKinematic=true;
            var fragments=GetComponentsInChildren<MeshFilter>();
            var colliders=new System.Collections.Generic.List<Collider>();
            foreach(var fragment in fragments)
            {
                if(fragment.transform==transform || fragment.sharedMesh==null)continue;
                var piece=fragment.gameObject;
                piece.transform.SetParent(null,true);
                var mc=piece.AddComponent<MeshCollider>(); mc.sharedMesh=fragment.sharedMesh; mc.convex=true;
                var rb=piece.AddComponent<Rigidbody>(); rb.mass=.15f;
                rb.interpolation=RigidbodyInterpolation.Interpolate;
                Vector3 outward=(fragment.GetComponent<Renderer>().bounds.center-transform.position).normalized;
                rb.AddForce((outward+Vector3.up*.3f)*config.fragmentImpulse,ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere*2,ForceMode.Impulse);
                foreach(var pc in player.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(mc,pc);
                if(owner!=null)foreach(var oc in owner.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(mc,oc);
                foreach(var other in colliders)Physics.IgnoreCollision(mc,other);
                colliders.Add(mc); Destroy(piece,config.fragmentLifetime);
            }
            Debug.Log("[Parry Success] Boss projectile shattered",player);
            Destroy(gameObject);
            return true;
        }

        #endregion

        #region 충돌과 시간 복구

        /// <summary>
        /// 플레이어와 부딪혔을 때 패리 입력이 살아 있으면 마지막으로 패리를 시도하고, 아니면 넉다운 피격을 준다.
        /// 다른 물체에 부딪히면 그대로 소멸한다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if(resolved)return;
            var hit=collision.collider.GetComponentInParent<PlayerCharacter>();
            if(hit!=null)
            {
                if(windowActive && player!=null && player.Combat.IsBossParryInputActive && TryParry())return;
                resolved=true; RestoreTime();
                hit.Health.ReceiveHit(3,owner!=null?owner.transform:transform,knockDown: true,allowBossParry: false);
            }
            else { resolved=true; RestoreTime(); }
            Destroy(gameObject);
        }
        /// <summary>패리 창을 닫고, 이 투사체가 시간 배율 소유자라면 원래 timeScale과 fixedDeltaTime으로 되돌린다.</summary>
        private void RestoreTime()
        {
            if(!windowActive)return;
            windowActive=false;
            if(timeOwner==this){Time.timeScale=savedScale;Time.fixedDeltaTime=savedStep;timeOwner=null;}
        }
        /// <summary>비활성화될 때 슬로 모션이 남지 않도록 시간 배율을 복구한다.</summary>
        private void OnDisable(){RestoreTime();}
        /// <summary>파괴될 때 슬로 모션이 남지 않도록 시간 배율을 복구한다.</summary>
        private void OnDestroy(){RestoreTime();}

        #endregion
    }
}
