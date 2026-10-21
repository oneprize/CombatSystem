using UnityEngine;
using CombatSystem.Player;
using CombatSystem.Core;
using CombatSystem.Data.Boss;
namespace CombatSystem.Boss {
/// <summary>
/// 보스의 체력·패리 누적·그로기 판정과 패턴 1(투사체)·패턴 2(돌진/점프) 실행에 필요한 데이터와 동작을 모아 둔 전투 컴포넌트.
/// 보스 상태 머신의 각 상태가 이 컴포넌트를 통해 플레이어 정보 조회, 애니메이션 재생, 타격 판정을 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossCombat : MonoBehaviour, ICombatant {
#region 플레이어 참조

[SerializeField] private PlayerCharacter player;
[SerializeField] private BossCombatConfig coreConfig;
public Transform Transform => transform;
public CombatTeam Team => CombatTeam.Enemy;
/// <summary>인스펙터에서 플레이어가 지정되지 않았으면 가장 가까운 플레이어 팀 전투원을 찾아 캐시한 뒤 반환한다.</summary>
private PlayerCharacter GetPlayer()
{
    if (player == null)
    {
        var nearest = CombatRegistry.FindNearest(CombatTeam.Player, transform.position);
        if (nearest != null) player = nearest.Transform.GetComponent<PlayerCharacter>();
    }
    return player;
}

#endregion

#region 패리와 그로기

private float groggyEndsAt;
private bool groggyActive;
/// <summary>현재 그로기 사이클에서 누적된 패리 횟수. 그로기가 끝나면 0으로 초기화된다.</summary>
public int ParryCount { get; private set; }
public int ParriesToGroggy => Mathf.Max(1, coreConfig.parriesToGroggy);
/// <summary>패리가 충분히 누적되어 다음 기회에 그로기 상태로 진입할 수 있는지 여부.</summary>
public bool GroggyReady => ParryCount >= ParriesToGroggy;
public float GroggySecondsRemaining => groggyActive ? Mathf.Max(0, groggyEndsAt - Time.time) : 0;
public bool IsGroggy => groggyActive && GroggySecondsRemaining > 0;
/// <summary>보스는 그로기 중에만 플레이어 공격으로 피해를 입는다.</summary>
public bool CanTakeDamage => IsGroggy && CurrentHealth > 0;
/// <summary>플레이어의 패리 성공을 누적한다. 그로기 중이거나 이미 그로기 준비가 끝났으면 더 쌓지 않는다.</summary>
public void RegisterParry()
{
    if (groggyActive || CurrentHealth <= 0 || GroggyReady) return;
    ParryCount++;
    Debug.Log("[Boss Parry] " + ParryCount + "/" + ParriesToGroggy, this);
}
/// <summary>그로기 상태를 시작해 설정된 시간 동안 피해를 받을 수 있게 하고, 날아가는 중인 투사체는 제거한다.</summary>
public void BeginGroggy()
{
    groggyActive = true;
    groggyEndsAt = Time.time + Mathf.Max(.1f, coreConfig.groggyDuration);
    if (ActiveProjectile != null && !ActiveProjectile.Resolved) Destroy(ActiveProjectile.gameObject);
}
/// <summary>그로기를 끝내고 패리 누적을 초기화해 다음 그로기 사이클을 시작한다.</summary>
public void EndGroggy() { groggyActive = false; ParryCount = 0; }
/// <summary>플레이어 공격을 받아 피해를 적용한다. 그로기 중이 아니면 무시한다.</summary>
/// <returns>피해가 실제로 적용되었으면 true.</returns>
public bool TryReceivePlayerHit(float damageMultiplier = 1f)
{
    if (!CanTakeDamage) return false;
    CurrentHealth = Mathf.Max(0, CurrentHealth - coreConfig.damagePerPlayerHit * Mathf.Max(0f, damageMultiplier));
    return true;
}

#endregion

#region 상태 조회

public float PatternCooldown => coreConfig.patternCooldown;
/// <summary>현재 날아가고 있는 패턴 1 투사체. 그로기 진입 시 정리 대상이 된다.</summary>
public BossProjectile ActiveProjectile { get; private set; }
/// <summary>플레이어가 수평 거리 기준으로 탐지 범위 안에 있는지 여부.</summary>
public bool PlayerDetected { get { if(PlayerTransform==null)return false; var d=PlayerTransform.position-transform.position; d.y=0; return d.sqrMagnitude<=DetectionRange*DetectionRange; } }
public float MaxHealth => Mathf.Max(1,coreConfig.maxHealth);
public float CurrentHealth {get; private set;}
public float DetectionRange => coreConfig.detectionRange;
public float AttackRange => coreConfig.attackRange;
public Transform PlayerTransform { get { var p = GetPlayer(); return p != null ? p.transform : null; } }
public bool IsPlayerGuarding { get { var p = GetPlayer(); return p != null && p.IsGuarding; } }
public bool IsPlayerAttacking { get { var p = GetPlayer(); return p != null && p.IsAttacking; } }

#endregion

#region Unity 생명주기

/// <summary>누락된 설정 에셋을 기본값 인스턴스로 대체하고 체력을 최대치로 채운다.</summary>
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
/// <summary>전투원 레지스트리에 등록해 다른 시스템이 보스를 탐색할 수 있게 한다.</summary>
private void OnEnable() { CombatRegistry.Register(this); }

#endregion

#region 패턴 1 - 투사체

[Header("Pattern 1 - Projectile")]
[SerializeField] private Rigidbody projectilePrefab;
[SerializeField] private Transform projectileSpawnPoint;
[SerializeField] private BossPattern1Config pattern1Config;
public float ThrowReleaseProgress => pattern1Config.throwReleaseProgress;
private bool throwArmed;
public bool ThrowReleased { get; private set; }
public Vector3 LastThrowOrigin { get; private set; }
/// <summary>던지기 애니메이션이 릴리스 지점에 도달하면 투사체를 발사하도록 예약한다.</summary>
public void ArmThrow() { throwArmed=true; ThrowReleased=false; }
/// <summary>예약된 투사체 발사를 취소한다. 패턴이 중단되거나 컴포넌트가 비활성화될 때 사용한다.</summary>
public void CancelThrow() { throwArmed=false; }
/// <summary>예약된 던지기가 Throw 애니메이션의 릴리스 진행도에 도달했는지 확인하고 투사체를 발사한다.</summary>
private void LateUpdate()
{
    // 애니메이터 평가 이후에 샘플링해야 이번 프레임의 손 위치에서 투사체가 생성된다.
    if (!throwArmed || BossAnimationProgress("Throw", -1f) < pattern1Config.throwReleaseProgress) return;
    throwArmed=false; ThrowReleased=true; ThrowProjectile();
}
/// <summary>레지스트리에서 등록을 해제하고 대기 중인 던지기를 취소한다.</summary>
private void OnDisable() { CombatRegistry.Unregister(this); CancelThrow(); }
/// <summary>
/// 손(또는 지정된 생성 지점)에서 플레이어 몸통을 향해 포물선 투사체를 발사한다.
/// 발사 시점의 조준을 고정하고, 예상 비행 시간 동안의 중력 낙하를 보정한 초기 속도를 준다.
/// </summary>
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
    // 조준은 발사 순간 한 번만 고정한다. 예상 비행 시간 뒤 그 지점에 닿도록 중력을 보정한다.
    float flightTime = Mathf.Max(.1f, Vector3.Distance(origin, target) / pattern1Config.projectileSpeed);
    Vector3 velocity = (target - origin) / flightTime - .5f * Physics.gravity * flightTime;
    var body = Instantiate(projectilePrefab, origin, Quaternion.identity);
    var ball = body.gameObject;
    ball.name = "Boss Pattern1 Projectile";
    // 프리팹에 저작된 투사체 스케일을 그대로 유지한다(여기서 스케일을 덮어쓰지 않음).
    var ballCollider = ball.GetComponent<Collider>();
    foreach (var ownerCollider in GetComponentsInChildren<Collider>())
        Physics.IgnoreCollision(ballCollider, ownerCollider);
    ActiveProjectile = ball.GetComponent<BossProjectile>();
    if(ActiveProjectile!=null) ActiveProjectile.Initialize(this, playerCombat);
    body.linearVelocity = velocity;
    Destroy(ball, pattern1Config.projectileLifetime);
}

#endregion

#region 패턴 2 - 돌진과 점프

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
/// <summary>디버그 표시용 패턴 2 진행 단계 이름. 패턴 2가 진행 중이 아니면 "-".</summary>
public string Pattern2Phase { get; set; } = "-";
/// <summary>돌진 시작 시 고정한 목표 지점. 점프 착지 위치이자 기즈모 표시 기준이 된다.</summary>
public Vector3 LockedChargeTarget { get; set; }
/// <summary>점프 착지 순간 충격 반경과 높이 범위 안에 있는 플레이어에게 넉다운 피격을 준다.</summary>
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
        playerCombat.Health.ReceiveHit(3,transform,knockDown: true);
    }
}

#endregion

#region 디버그 기즈모

/// <summary>패턴 2 진행 중 고정된 목표 지점과 착지 충격 반경을 씬 뷰에 표시한다.</summary>
private void OnDrawGizmosSelected()
{
    if(!Application.isPlaying || Pattern2Phase=="-")return;
    Gizmos.color=Color.red;
    Gizmos.DrawWireSphere(LockedChargeTarget,pattern2Config.impactRadius);
    Gizmos.DrawLine(transform.position,LockedChargeTarget);
}

#endregion

#region 돌진 판정과 애니메이션

private bool chargeHitConsumed;
public float JumpTakeoffTime => pattern2Config.jumpTakeoffTime;
/// <summary>착지 시점. 이륙 시점보다 최소 0.05초 뒤가 되도록 보정한다.</summary>
public float JumpLandingTime => Mathf.Max(pattern2Config.jumpTakeoffTime+.05f,pattern2Config.jumpLandingTime);
/// <summary>돌진 1회당 한 번만 피격되도록 막는 플래그를 새 돌진 시작 시 초기화한다.</summary>
public void ResetChargeHit(){chargeHitConsumed=false;}
/// <summary>Base Layer에 해당 상태가 있을 때만 크로스페이드로 보스 애니메이션을 재생한다.</summary>
public void PlayBossAnimation(string state)
{
    var animator=GetComponent<Animator>();
    if(animator!=null && animator.runtimeAnimatorController!=null && animator.HasState(0,Animator.StringToHash("Base Layer."+state)))
        animator.CrossFadeInFixedTime("Base Layer."+state,.12f,0,0);
}
/// <summary>
/// 지정한 애니메이터 상태의 정규화 진행도를 반환한다. 전환 중이면 다음 상태 기준으로 본다.
/// 애니메이터나 상태가 없으면 fallback을, 해당 상태가 재생 중이 아니면 0을 반환한다.
/// </summary>
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
/// <summary>이번 프레임 돌진 이동 구간을 캡슐로 검사해 플레이어에게 넉다운 피격을 준다. 돌진 1회당 한 번만 적중한다.</summary>
public void ApplyChargeHit(Vector3 from,Vector3 to)
{
    var playerCombat = GetPlayer();
    if(chargeHitConsumed || playerCombat==null)return;
    foreach(var collider in Physics.OverlapCapsule(from+Vector3.up*pattern2Config.chargeHitHeight,to+Vector3.up*pattern2Config.chargeHitHeight,
        pattern2Config.chargeHitRadius,~0,QueryTriggerInteraction.Ignore))
    {
        var hitPlayer=collider.GetComponentInParent<PlayerCharacter>();
        if(hitPlayer==null || hitPlayer!=playerCombat)continue;
        if(hitPlayer.Health.ReceiveHit(3,transform,knockDown: true))chargeHitConsumed=true;
        break;
    }
}

#endregion
} }
