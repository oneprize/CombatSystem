using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Core;
using CombatSystem.Data.Boss;
namespace CombatSystem.Boss {
[DisallowMultipleComponent]
public sealed class BossCombat : MonoBehaviour, ICombatant {
[SerializeField] private PlayerCharacter player;
[SerializeField] private BossCombatConfig coreConfig;
public Transform Transform => transform;
public CombatTeam Team => CombatTeam.Enemy;
private PlayerCharacter GetPlayer()
{
    if (player == null)
    {
        var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
        if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
    }
    return player;
}
private float groggyEndsAt;
private bool groggyActive;
public int ParryCount { get; private set; }
public int ParriesToGroggy => Mathf.Max(1, coreConfig.parriesToGroggy);
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
    groggyEndsAt = Time.time + Mathf.Max(.1f, coreConfig.groggyDuration);
    if (ActiveProjectile != null && !ActiveProjectile.Resolved) Destroy(ActiveProjectile.gameObject);
}
public void EndGroggy() { groggyActive = false; ParryCount = 0; }
public bool TryReceivePlayerHit(float damageMultiplier = 1f)
{
    if (!CanTakeDamage) return false;
    CurrentHealth = Mathf.Max(0, CurrentHealth - coreConfig.damagePerPlayerHit * Mathf.Max(0f, damageMultiplier));
    return true;
}
public float PatternCooldown => coreConfig.patternCooldown;
public BossProjectile ActiveProjectile { get; private set; }
public bool PlayerDetected { get { if(PlayerTransform==null)return false; var d=PlayerTransform.position-transform.position; d.y=0; return d.sqrMagnitude<=DetectionRange*DetectionRange; } }
public float MaxHealth => Mathf.Max(1,coreConfig.maxHealth);
public float CurrentHealth {get; private set;}
public float DetectionRange => coreConfig.detectionRange;
public float AttackRange => coreConfig.attackRange;
public Transform PlayerTransform { get { var p = GetPlayer(); return p != null ? p.transform : null; } }
public bool IsPlayerGuarding { get { var p = GetPlayer(); return p != null && p.IsGuarding; } }
public bool IsPlayerAttacking { get { var p = GetPlayer(); return p != null && p.IsAttacking; } }
private void Awake(){
if (coreConfig == null)
{
    coreConfig = ScriptableObject.CreateInstance<BossCombatConfig>();
    Debug.LogWarning("BossCombat: no BossCombatConfig assigned, using built-in defaults.", this);
}
if (pattern1Config == null)
{
    pattern1Config = ScriptableObject.CreateInstance<BossPattern1Config>();
    Debug.LogWarning("BossCombat: no BossPattern1Config assigned, using built-in defaults.", this);
}
if (pattern2Config == null)
{
    pattern2Config = ScriptableObject.CreateInstance<BossPattern2Config>();
    Debug.LogWarning("BossCombat: no BossPattern2Config assigned, using built-in defaults.", this);
}
CurrentHealth=MaxHealth;}
private void OnEnable() { CombatRegistry.Register(this); }

[Header("Pattern 1 - Projectile")]
[SerializeField] private Rigidbody projectilePrefab;
[SerializeField] private Transform projectileSpawnPoint;
[SerializeField] private BossPattern1Config pattern1Config;
public float ThrowReleaseProgress => pattern1Config.throwReleaseProgress;
private bool throwArmed;
public bool ThrowReleased { get; private set; }
public Vector3 LastThrowOrigin { get; private set; }
public void ArmThrow() { throwArmed=true; ThrowReleased=false; }
public void CancelThrow() { throwArmed=false; }
private void LateUpdate()
{
    // Sample after Animator evaluation so the spawn uses this frame's hand pose.
    if (!throwArmed || BossAnimationProgress("Throw", -1f) < pattern1Config.throwReleaseProgress) return;
    throwArmed=false; ThrowReleased=true; ThrowProjectile();
}
private void OnDisable() { CombatRegistry.Unregister(this); CancelThrow(); }
public void ThrowProjectile()
{
    var playerCombat = GetPlayer();
    if (playerCombat == null) return;
    if (projectilePrefab == null) { Debug.LogWarning("Boss projectile prefab is not assigned.", this); return; }
    var animator = GetComponent<Animator>();
    Transform hand = projectileSpawnPoint != null ? projectileSpawnPoint :
        animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
    Vector3 origin = hand != null ? hand.position : transform.position + transform.rotation * pattern1Config.projectileSpawnOffset;
    LastThrowOrigin = origin;
    var character = playerCombat.GetComponent<CharacterController>();
    Vector3 target = character != null ? character.bounds.center : playerCombat.transform.position + Vector3.up;
    // Snapshot aim once. Compensate gravity to reach that point after the estimated flight time.
    float flightTime = Mathf.Max(.1f, Vector3.Distance(origin, target) / pattern1Config.projectileSpeed);
    Vector3 velocity = (target - origin) / flightTime - .5f * Physics.gravity * flightTime;
    var body = Instantiate(projectilePrefab, origin, Quaternion.identity);
    var ball = body.gameObject;
    ball.name = "Boss Pattern1 Projectile";
    ball.transform.localScale = Vector3.one * pattern1Config.projectileDiameter;
    var ballCollider = ball.GetComponent<Collider>();
    foreach (var ownerCollider in GetComponentsInChildren<Collider>())
        Physics.IgnoreCollision(ballCollider, ownerCollider);
    ActiveProjectile = ball.GetComponent<BossProjectile>();
    if(ActiveProjectile!=null) ActiveProjectile.Initialize(this, playerCombat);
    body.linearVelocity = velocity;
    Destroy(ball, pattern1Config.projectileLifetime);
}


[Header("Pattern 2 - Charge and Jump")]
[SerializeField] private BossPattern2Config pattern2Config;
public float MaxChargeDistance => Mathf.Max(.1f,pattern2Config.maxChargeDistance);
public float MinTurnRadius => Mathf.Max(.1f,pattern2Config.minTurnRadius);
public float TrackingStopDistance => Mathf.Max(.1f,pattern2Config.trackingStopDistance);
public float MaxChargeDuration => Mathf.Max(.1f,pattern2Config.maxChargeDuration);
public float ChargeDistanceTravelled { get; set; }
public float ChargeWindup => pattern2Config.chargeWindup;
public float ChargeSpeed => pattern2Config.chargeSpeed;
public float JumpStartDistance => pattern2Config.jumpStartDistance;
public float JumpDuration => pattern2Config.jumpDuration;
public float JumpHeight => pattern2Config.jumpHeight;
public float LandingRecovery => pattern2Config.landingRecovery;
public string Pattern2Phase { get; set; } = "-";
public Vector3 LockedChargeTarget { get; set; }
public void ApplyLandingHit()
{
    var playerCombat = GetPlayer();
    if(playerCombat==null)return;
    var character=playerCombat.GetComponent<CharacterController>();
    Vector3 nearest=character!=null?character.bounds.ClosestPoint(transform.position):playerCombat.transform.position;
    Vector3 delta=nearest-transform.position;
    if(Mathf.Abs(delta.y)>pattern2Config.impactHeight)return;
    delta.y=0;
    if(delta.sqrMagnitude<=pattern2Config.impactRadius*pattern2Config.impactRadius)
    {
        playerCombat.Health.ReceiveHit(3,transform,true,true);
    }
}
private void OnDrawGizmosSelected()
{
    if(!Application.isPlaying || Pattern2Phase=="-")return;
    Gizmos.color=Color.red;
    Gizmos.DrawWireSphere(LockedChargeTarget,pattern2Config.impactRadius);
    Gizmos.DrawLine(transform.position,LockedChargeTarget);
}

private bool chargeHitConsumed;
public float JumpTakeoffTime => pattern2Config.jumpTakeoffTime;
public float JumpLandingTime => Mathf.Max(pattern2Config.jumpTakeoffTime+.05f,pattern2Config.jumpLandingTime);
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
    var playerCombat = GetPlayer();
    if(chargeHitConsumed || playerCombat==null)return;
    foreach(var collider in Physics.OverlapCapsule(from+Vector3.up*pattern2Config.chargeHitHeight,to+Vector3.up*pattern2Config.chargeHitHeight,
        pattern2Config.chargeHitRadius,~0,QueryTriggerInteraction.Ignore))
    {
        var hitPlayer=collider.GetComponentInParent<PlayerCharacter>();
        if(hitPlayer==null || hitPlayer!=playerCombat)continue;
        if(hitPlayer.Health.ReceiveHit(3,transform,true,true))chargeHitConsumed=true;
        break;
    }
}
} }
