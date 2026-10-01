using UnityEngine;
using UnityEngine.InputSystem;
using ParrySystem.Player;
namespace ParrySystem.Boss
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BossProjectile : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float parryDistance = 4f;
        [SerializeField, Min(.1f)] private float parryDuration = 2f;
        [SerializeField, Range(.001f,1)] private float slowScale = .03f;
        [SerializeField, Min(0)] private float fragmentImpulse = 4f;
        [SerializeField, Min(.1f)] private float fragmentLifetime = 4f;
        private PlayerCombat player;
        private BossCombat owner;
        private Rigidbody body;
        private Collider shell;
        private float remaining, savedScale, savedStep;
        private bool windowUsed, windowActive, resolved;
        private static BossProjectile timeOwner;
        public bool IsParryWindowOpen => windowActive;
        public float ParrySecondsRemaining => windowActive ? remaining : 0;
        public bool Resolved => resolved;
        public void Initialize(BossCombat boss, PlayerCombat target) { owner=boss; player=target; }
        private void Awake() { body=GetComponent<Rigidbody>(); shell=GetComponent<SphereCollider>(); }
        private void Update()
        {
            if(resolved || player==null) { if(windowActive) RestoreTime(); return; }
            if(!windowUsed && timeOwner==null && Time.timeScale>.99f)
            {
                var c=player.GetComponent<CharacterController>();
                Vector3 target=c!=null?c.bounds.center:player.transform.position+Vector3.up;
                Vector3 delta=target-transform.position;
                float surfaceDistance=Mathf.Max(.05f,delta.magnitude-shell.bounds.extents.magnitude);
                if(surfaceDistance<=parryDistance && Vector3.Dot(body.linearVelocity,delta)>0)
                {
                    windowUsed=true; windowActive=true; remaining=parryDuration; timeOwner=this;
                    savedScale=Time.timeScale; savedStep=Time.fixedDeltaTime;
                    float safeScale=surfaceDistance/Mathf.Max(1,body.linearVelocity.magnitude)/(parryDuration*4);
                    float factor=Mathf.Max(.001f,Mathf.Min(slowScale,safeScale));
                    Time.timeScale=savedScale*factor; Time.fixedDeltaTime=savedStep*factor;
                }
            }
        }
        private void LateUpdate()
        {
            if(!windowActive || resolved) return;
            if(player!=null && !player.IsDodging && !player.IsHitReacting &&
                Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame)
            { TryParry(); return; }
            remaining-=Time.unscaledDeltaTime;
            if(remaining<=0) RestoreTime();
        }
        public bool TryParry()
        {
            if(!windowActive || resolved || player==null || player.IsDodging || player.IsHitReacting)return false;
            Vector3 facing=transform.position-player.transform.position;
            facing.y=0;
            if(facing.sqrMagnitude>.0001f) player.transform.rotation=Quaternion.LookRotation(facing);
            player.TryConsumeParry();
            var reaction=player.GetComponent<PlayerDamageReaction>();
            if(reaction!=null)reaction.PlayProjectileParry();
            resolved=true; RestoreTime();
            var feedback = player.GetComponent<ParryFeedback>();
            if (feedback != null) feedback.Play();
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
                rb.AddForce((outward+Vector3.up*.3f)*fragmentImpulse,ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere*2,ForceMode.Impulse);
                foreach(var pc in player.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(mc,pc);
                if(owner!=null)foreach(var oc in owner.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(mc,oc);
                foreach(var other in colliders)Physics.IgnoreCollision(mc,other);
                colliders.Add(mc); Destroy(piece,fragmentLifetime);
            }
            Debug.Log("[Parry Success] Boss projectile shattered",player);
            Destroy(gameObject);
            return true;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if(resolved)return;
            var hit=collision.collider.GetComponentInParent<PlayerDamageReaction>();
            if(hit!=null)
            {
                if(windowActive && player!=null && player.IsParryInputActive && TryParry())return;
                resolved=true; RestoreTime();
                hit.ReceiveHit(3,owner!=null?owner.transform:transform,false,true);
            }
            else { resolved=true; RestoreTime(); }
            Destroy(gameObject);
        }
        private void RestoreTime()
        {
            if(!windowActive)return;
            windowActive=false;
            if(timeOwner==this){Time.timeScale=savedScale;Time.fixedDeltaTime=savedStep;timeOwner=null;}
        }
        private void OnDisable(){RestoreTime();}
        private void OnDestroy(){RestoreTime();}
    }
}