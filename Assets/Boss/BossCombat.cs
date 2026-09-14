using UnityEngine;
using ParrySystem.Player;
namespace ParrySystem.Boss {
[DisallowMultipleComponent]
public sealed class BossCombat : MonoBehaviour {
[SerializeField] private PlayerCombat player;
[SerializeField,Min(1)] private float maxHealth=500;
[SerializeField,Min(0)] private float detectionRange=20, attackRange=15;
[SerializeField, Min(.1f)] private float patternCooldown = 3f;
[Header("Groggy")]
[SerializeField, Min(1)] private int parriesToGroggy = 4;
[SerializeField, Min(.1f)] private float groggyDuration = 10f;
[SerializeField, Min(0)] private float damagePerPlayerHit = 10f;
private float groggyEndsAt;
private bool groggyActive;
public int ParryCount { get; private set; }
public int ParriesToGroggy => Mathf.Max(1, parriesToGroggy);
public bool GroggyReady => ParryCount >= ParriesToGroggy;
public float GroggySecondsRemaining => groggyActive ? Mathf.Max(0, groggyEndsAt - Time.time) : 0;
public bool IsGroggy => groggyActive && GroggySecondsRemaining > 0;
public bool CanTakeDamage => IsGroggy && CurrentHealth > 0;
public void RegisterParry()
{
    if (groggyActive || CurrentHealth <= 0 || GroggyReady) return;
    ParryCount++;
    Debug.Log("[Boss Parry] " + ParryCount + "/" + ParriesToGroggy, this);
}
public void BeginGroggy()
{
    groggyActive = true;
    groggyEndsAt = Time.time + Mathf.Max(.1f, groggyDuration);
    if (ActiveProjectile != null && !ActiveProjectile.Resolved) Destroy(ActiveProjectile.gameObject);
}
public void EndGroggy() { groggyActive = false; ParryCount = 0; }
public bool TryReceivePlayerHit()
{
    if (!CanTakeDamage) return false;
    CurrentHealth = Mathf.Max(0, CurrentHealth - damagePerPlayerHit);
    return true;
}
public float PatternCooldown => patternCooldown;
public BossProjectile ActiveProjectile { get; private set; }
public bool PlayerDetected { get { if(PlayerTransform==null)return false; var d=PlayerTransform.position-transform.position; d.y=0; return d.sqrMagnitude<=DetectionRange*DetectionRange; } }
public float MaxHealth => Mathf.Max(1,maxHealth);
public float CurrentHealth {get; private set;}
public float DetectionRange => detectionRange;
public float AttackRange => attackRange;
public Transform PlayerTransform => player!=null?player.transform:null;
public bool IsPlayerGuarding => player!=null && player.IsGuarding;
public bool IsPlayerAttacking => player!=null && player.isAttacking;
private void Awake(){CurrentHealth=MaxHealth;if(player==null)player=FindFirstObjectByType<PlayerCombat>();}

[Header("Pattern 1 - Projectile")]
[SerializeField] private Rigidbody projectilePrefab;
[SerializeField] private Transform projectileSpawnPoint;
[SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0, 1.5f, 1.2f);
[SerializeField, Min(.1f)] private float projectileSpeed = 12f;
[SerializeField, Min(.05f)] private float projectileDiameter = .6f;
[SerializeField, Min(.1f)] private float projectileLifetime = 8f;
[SerializeField, Range(0f,1f)] private float throwReleaseProgress = .55f;
public float ThrowReleaseProgress => throwReleaseProgress;
public void ThrowProjectile()
{
    if (PlayerTransform == null) return;
    if (projectilePrefab == null) { Debug.LogWarning("Boss projectile prefab is not assigned.", this); return; }
    Vector3 origin = projectileSpawnPoint != null ? projectileSpawnPoint.position :
        transform.position + transform.rotation * projectileSpawnOffset;
    var character = player.GetComponent<CharacterController>();
    Vector3 target = character != null ? character.bounds.center : PlayerTransform.position + Vector3.up;
    // Snapshot aim once. Compensate gravity to reach that point after the estimated flight time.
    float flightTime = Mathf.Max(.1f, Vector3.Distance(origin, target) / projectileSpeed);
    Vector3 velocity = (target - origin) / flightTime - .5f * Physics.gravity * flightTime;
    var body = Instantiate(projectilePrefab, origin, Quaternion.identity);
    var ball = body.gameObject;
    ball.name = "Boss Pattern1 Projectile";
    ball.transform.localScale = Vector3.one * projectileDiameter;
    var ballCollider = ball.GetComponent<Collider>();
    foreach (var ownerCollider in GetComponentsInChildren<Collider>())
        Physics.IgnoreCollision(ballCollider, ownerCollider);
    ActiveProjectile = ball.GetComponent<BossProjectile>();
    if(ActiveProjectile!=null) ActiveProjectile.Initialize(this, player);
    body.linearVelocity = velocity;
    Destroy(ball, projectileLifetime);
}


[Header("Pattern 2 - Charge and Jump")]
[SerializeField, Min(0)] private float chargeWindup = .7f;
[SerializeField, Min(.1f)] private float chargeSpeed = 12f;
[SerializeField, Min(0)] private float jumpStartDistance = 3f;
[SerializeField, Min(.1f)] private float jumpDuration = .8f;
[SerializeField, Min(0)] private float jumpHeight = 3f;
[SerializeField, Min(0)] private float landingRecovery = 1f;
[SerializeField, Min(.1f)] private float impactRadius = 3f;
[SerializeField, Min(.1f)] private float impactHeight = 2.5f;
[Header("Charge Tracking")]
[SerializeField, Min(.1f)] private float maxChargeDistance = 20f;
[SerializeField, Min(.1f)] private float minTurnRadius = 12f;
[SerializeField, Min(.1f)] private float trackingStopDistance = 5f;
[SerializeField, Min(.1f)] private float maxChargeDuration = 1.5f;
public float MaxChargeDistance => Mathf.Max(.1f,maxChargeDistance);
public float MinTurnRadius => Mathf.Max(.1f,minTurnRadius);
public float TrackingStopDistance => Mathf.Max(.1f,trackingStopDistance);
public float MaxChargeDuration => Mathf.Max(.1f,maxChargeDuration);
public float ChargeDistanceTravelled { get; set; }
public float ChargeWindup => chargeWindup;
public float ChargeSpeed => chargeSpeed;
public float JumpStartDistance => jumpStartDistance;
public float JumpDuration => jumpDuration;
public float JumpHeight => jumpHeight;
public float LandingRecovery => landingRecovery;
public string Pattern2Phase { get; set; } = "-";
public Vector3 LockedChargeTarget { get; set; }
public void ApplyLandingHit()
{
    if(player==null)return;
    var character=player.GetComponent<CharacterController>();
    Vector3 nearest=character!=null?character.bounds.ClosestPoint(transform.position):player.transform.position;
    Vector3 delta=nearest-transform.position;
    if(Mathf.Abs(delta.y)>impactHeight)return;
    delta.y=0;
    if(delta.sqrMagnitude<=impactRadius*impactRadius)
    {
        var reaction=player.GetComponent<PlayerDamageReaction>();
        if(reaction!=null)reaction.ReceiveHit(3,transform,true,true);
    }
}
private void OnDrawGizmosSelected()
{
    if(!Application.isPlaying || Pattern2Phase=="-")return;
    Gizmos.color=Color.red;
    Gizmos.DrawWireSphere(LockedChargeTarget,impactRadius);
    Gizmos.DrawLine(transform.position,LockedChargeTarget);
}

[Header("Pattern 2 Animation")]
[SerializeField, Range(0, .9f)] private float jumpTakeoffTime = .2f;
[SerializeField, Range(.1f, 1)] private float jumpLandingTime = .75f;
[SerializeField, Min(.1f)] private float chargeHitRadius = 2f;
[SerializeField] private float chargeHitHeight = 2f;
private bool chargeHitConsumed;
public float JumpTakeoffTime => jumpTakeoffTime;
public float JumpLandingTime => Mathf.Max(jumpTakeoffTime+.05f,jumpLandingTime);
public void ResetChargeHit(){chargeHitConsumed=false;}
public void PlayBossAnimation(string state)
{
    var animator=GetComponent<Animator>();
    if(animator!=null && animator.runtimeAnimatorController!=null && animator.HasState(0,Animator.StringToHash("Base Layer."+state)))
        animator.CrossFadeInFixedTime("Base Layer."+state,.12f,0,0);
}
public float BossAnimationProgress(string state, float fallback)
{
    var animator=GetComponent<Animator>();
    if(animator==null || animator.runtimeAnimatorController==null)return fallback;
    if(!animator.HasState(0,Animator.StringToHash("Base Layer."+state)))return fallback;
    if(animator.IsInTransition(0)){
        var next=animator.GetNextAnimatorStateInfo(0);
        if(next.IsName(state))return next.normalizedTime;
    }
    var current=animator.GetCurrentAnimatorStateInfo(0);
    return current.IsName(state)?current.normalizedTime:0;
}
public void ApplyChargeHit(Vector3 from,Vector3 to)
{
    if(chargeHitConsumed || player==null)return;
    foreach(var collider in Physics.OverlapCapsule(from+Vector3.up*chargeHitHeight,to+Vector3.up*chargeHitHeight,
        chargeHitRadius,~0,QueryTriggerInteraction.Ignore))
    {
        var reaction=collider.GetComponentInParent<PlayerDamageReaction>();
        if(reaction==null || reaction.gameObject!=player.gameObject)continue;
        if(reaction.ReceiveHit(3,transform,true,true))chargeHitConsumed=true;
        break;
    }
}
} }