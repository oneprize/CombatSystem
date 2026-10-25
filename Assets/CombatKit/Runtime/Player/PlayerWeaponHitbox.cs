using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Data.Player;
namespace CombatSystem.Player
{
    /// <summary>
    /// 플레이어 무기에 붙는 히트박스. 현재 재생 중인 공격 애니메이션의 진행도가 히트 구간에 들어오면
    /// 공격 종류(지상/공중 콤보, 어퍼, 스킬, 발차기, 파워 공격 등)에 맞는 범위로 겹침 검사를 해 적과 보스에 피해를 준다.
    /// 같은 공격 안에서 한 대상은 한 번만 맞도록 공격 시퀀스/콤보 단계가 바뀔 때마다 명중 목록을 초기화한다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class PlayerWeaponHitbox : MonoBehaviour
    {
        #region 필드

        [SerializeField] private PlayerWeaponHitboxConfig config;
        /// <summary>카메라 등 연출이 실제 타격 판정과 같은 시점을 사용하도록 제공한다.</summary>
        public PlayerWeaponHitboxConfig Config => config;
        private Transform kickFoot;
        private Vector3 previousFootPosition;
        private float previousGroundProgress;
        private float previousAirProgress;
        private float previousUpperProgress;
        private float previousSkillCProgress;
        private PlayerCharacter player;
        private CombatModule combat => player.Combat;
        private Animator animator;
        private PlayerSwordWave swordWave;
        private BoxCollider box;
        private int sequence = -1, combo = -1;
        private readonly System.Collections.Generic.HashSet<EnemyDamageReaction> hits =
            new System.Collections.Generic.HashSet<EnemyDamageReaction>();
        private readonly System.Collections.Generic.HashSet<EnemyDamageReaction> secondSkillHits =
            new System.Collections.Generic.HashSet<EnemyDamageReaction>();
        private readonly System.Collections.Generic.HashSet<CombatSystem.Boss.BossCombat> bossHits =
            new System.Collections.Generic.HashSet<CombatSystem.Boss.BossCombat>();
        private readonly System.Collections.Generic.HashSet<CombatSystem.Boss.BossCombat> secondBossHits =
            new System.Collections.Generic.HashSet<CombatSystem.Boss.BossCombat>();

        #endregion

        #region 명중 처리 헬퍼

        /// <summary>
        /// 겹친 콜라이더가 보스라면 이번 공격(또는 두 번째 타격)에서 아직 맞지 않은 경우에만 피해를 준다.
        /// 파워 공격 해제 중이면 파워 배율을 적용한다.
        /// </summary>
        private void HitBoss(Collider other, bool secondStrike)
        {
            var boss = other.GetComponentInParent<CombatSystem.Boss.BossCombat>();
            var struck = secondStrike ? secondBossHits : bossHits;
            if (boss != null && struck.Add(boss)) boss.TryReceivePlayerHit(combat.IsReleasingPower ? combat.PowerDamageMultiplier : 1f);
        }
        /// <summary>적을 이번 공격의 명중 대상으로 기록한다. 이미 맞은 적이면 false를 반환해 중복 피해를 막는다.</summary>
        private bool Struck(System.Collections.Generic.HashSet<EnemyDamageReaction> set, EnemyDamageReaction enemy)
        {
            if (!set.Add(enemy)) return false;
            return true;
        }
        private float previousPowerProgress;
        private Vector3 previousPowerPosition;
        /// <summary>
        /// 파워 공격 해제 판정. 돌진 베기 구간에는 지난 프레임부터 이동한 경로를 캡슐로 쓸어 맞히고,
        /// 이어지는 내려찍기 구간에는 주변 전체를 구 범위로 맞힌다.
        /// </summary>
        private void UpdatePowerHits(AnimatorStateInfo state)
        {
            var power = player.Profile.powerAttack;
            Transform owner = player.transform;
            float progress = state.normalizedTime;
            float multiplier = combat.PowerDamageMultiplier;
            if (progress >= power.dashHitWindow.x && previousPowerProgress <= power.dashHitWindow.y)
            {
                HitWindowActive = true;
                foreach (var other in Physics.OverlapCapsule(previousPowerPosition, owner.position, power.dashHitRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && hits.Add(enemy)) enemy.ReceivePowerHit(owner, multiplier, false);
                    HitBoss(other, false);
                }
            }
            if (progress >= power.slamHitWindow.x && previousPowerProgress <= power.slamHitWindow.y)
            {
                HitWindowActive = true;
                foreach (var other in Physics.OverlapSphere(owner.position, power.slamHitRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && secondSkillHits.Add(enemy)) enemy.ReceivePowerHit(owner, multiplier, true);
                    HitBoss(other, true);
                }
            }
            previousPowerProgress = progress;
            previousPowerPosition = owner.position;
        }

        #endregion

        #region Unity 생명주기와 공격별 히트 판정

        /// <summary>설정과 플레이어/애니메이터 참조를 준비하고, 발차기 판정에 쓸 오른발 본을 찾는다.</summary>
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerWeaponHitboxConfig>();
                Debug.LogWarning("PlayerWeaponHitbox: no PlayerWeaponHitboxConfig assigned, using built-in defaults.", this);
            }
            player = GetComponentInParent<PlayerCharacter>();
            animator = player.GetComponent<Animator>();
            swordWave = player.GetComponent<PlayerSwordWave>();
            box = GetComponent<BoxCollider>();
            kickFoot = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            if (kickFoot != null) previousFootPosition = kickFoot.position;
        }
        /// <summary>이번 프레임에 공격의 히트 구간이 열려 있었는지 여부. 검기 등 다른 연출이 타이밍을 맞추는 데 쓴다.</summary>
        public bool HitWindowActive { get; private set; }
        /// <summary>
        /// 애니메이션 포즈가 확정된 뒤 매 프레임 히트 판정을 수행한다. 현재 공격 종류를 판별해 해당 히트 구간과
        /// 범위로 겹침 검사를 하고, 새 공격이 시작되면 명중 목록과 진행도 기록을 초기화한다.
        /// </summary>
        private void LateUpdate()
        {
            HitWindowActive = false;
            Vector3 footPosition = kickFoot != null ? kickFoot.position : transform.position;
            Vector3 footFrom = previousFootPosition;
            previousFootPosition = footPosition;
            if (!player.IsAttacking || player.IsGuarding || player.IsHitReacting || !box.enabled) return;
            if (player.AirCombo.IsAirDashing) return;
            if (combat.IsUsingPiercingSkill)
            {
                HitWindowActive = player.PiercingDash.IsDashing;
                return;
            }
            int index = combat.AttackComboIndex;
            // 새 공격(시퀀스) 또는 다음 콤보 단계로 넘어가면 명중 기록을 초기화한다.
            if (sequence != combat.AttackSequence || combo != index)
            {
                hits.Clear();
                footFrom = footPosition;
                previousGroundProgress = 0;
                previousUpperProgress = 0;
                previousSkillCProgress = 0;
                previousAirProgress = 0;
                bossHits.Clear();
                secondBossHits.Clear();
                secondSkillHits.Clear();
                previousPowerProgress = 0;
                previousPowerPosition = player.transform.position;
                sequence = combat.AttackSequence;
                combo = index;
            }
            string name = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            // 크로스페이드 중에는 다음 상태가 현재 공격이면 그 진행도를 기준으로 판정한다.
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(name)) state = next;
            }
            if (!state.IsName(name)) return;
            if (combat.IsParryAttempt || (combat.IsParryCounter && !combat.IsCounterAttacking)) return;
            if (combat.IsChargingPower || combat.IsSheathedReady) return;
            if (combat.IsReleasingPower) { UpdatePowerHits(state); return; }
            if (combat.IsUsingSkillC)
            {
                float progress=state.normalizedTime;
                bool active=progress>=config.skillCHitWindow.x && previousSkillCProgress<=config.skillCHitWindow.y;
                previousSkillCProgress=progress;
                if(!active)return;
                HitWindowActive=true;
                Transform owner=player.transform;
                foreach(var other in Physics.OverlapBox(owner.TransformPoint(config.skillCImpactCenter),config.skillCImpactSize*.5f,owner.rotation,~0,QueryTriggerInteraction.Ignore))
                {
                    var enemy=other.GetComponentInParent<EnemyDamageReaction>();
                    if(enemy!=null && Struck(hits, enemy))
                    {
                        enemy.ReceiveUpperHit(owner);
                        if (enemy.IsAirborneForCombo) combat.RegisterLaunch(enemy);
                    }
                    HitBoss(other,false);
                }
                return;
            }
            if (combat.IsUsingChaseAttack)
            {
                float progress = state.normalizedTime;
                bool active = progress >= config.chaseHitWindow.x && previousAirProgress <= config.chaseHitWindow.y;
                previousAirProgress = progress;
                if (!active) return;
                HitWindowActive = true;
                Transform owner = player.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(config.airImpactCenter),
                    config.airImpactSize * .5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && enemy.IsAirborneForCombo && Struck(hits, enemy))
                        enemy.ReceiveAirComboHit(owner, 0, false);
                    HitBoss(other, false);
                }
                return;
            }
            if (combat.IsUsingAirCombo)
            {
                int airIndex = combat.AirComboIndex;
                Vector2 hitRange = config.airHitWindows != null && airIndex < config.airHitWindows.Length
                    ? config.airHitWindows[airIndex] : new Vector2(.15f, .35f);
                float progress = state.normalizedTime;
                bool active = progress >= hitRange.x && previousAirProgress <= hitRange.y;
                previousAirProgress = progress;
                if (!active) return;
                HitWindowActive = true;
                Transform owner = player.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(config.airImpactCenter),
                    config.airImpactSize * .5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && enemy.IsAirborneForCombo && Struck(hits, enemy))
                        enemy.ReceiveAirComboHit(owner, airIndex, combat.IsAirComboFinisher);
                    HitBoss(other, false);
                }
                if (combat.IsAirComboFinisher && progress >= hitRange.y) combat.EndAirCombo();
                return;
            }
            if (combat.IsUsingUpperAttack)
            {
                float progress = state.normalizedTime;
                bool crossedWindow = progress >= config.upperHitWindow.x && previousUpperProgress <= config.upperHitWindow.y;
                previousUpperProgress = progress;
                if (!crossedWindow) return;
                HitWindowActive = true;
                Transform owner = player.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(config.upperImpactCenter),
                    config.upperImpactSize * .5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && Struck(hits, enemy)) enemy.ReceiveUpperHit(owner);
                    HitBoss(other, false);
                }
                return;
            }
            if (combat.IsUsingSkill)
            {
                float progress = state.normalizedTime;
                int strike = progress >= config.skillFirstHitWindow.x && progress <= config.skillFirstHitWindow.y ? 0 :
                    progress >= config.skillHitWindow.x && progress <= config.skillHitWindow.y ? 1 : -1;
                if (strike < 0) return;
                HitWindowActive = true;
                var struck = strike == 0 ? hits : secondSkillHits;
                Transform owner = player.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(config.skillImpactCenter),
                    config.skillImpactSize * 0.5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && Struck(struck, enemy)) enemy.ReceiveAerialHit(owner, strike);
                    HitBoss(other, strike == 1);
                }
                return;
            }
            // 지상 콤보의 발차기 단계는 무기 박스 대신 오른발이 이동한 경로를 캡슐로 쓸어 판정한다.
            if (combat.IsUsingGroundCombo && index == config.groundKickComboIndex && kickFoot != null)
            {
                float progress = state.normalizedTime;
                bool active = progress >= config.groundKickWindow.x && previousGroundProgress <= config.groundKickWindow.y;
                previousGroundProgress = progress;
                if (!active) return;
                HitWindowActive = true;
                foreach (var other in Physics.OverlapCapsule(footFrom, footPosition, config.groundKickRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    HitBoss(other, false);
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && Struck(hits, enemy)) enemy.ReceiveGroundComboHit(player.transform, index, combat.IsGroundComboFinisher);
                }
                return;
            }
            Vector2 window = combat.IsParryCounter ? player.Profile.parry.counterHitWindow : combat.IsSheathedCombo ? player.Profile.combat.sheathedHitWindow : combat.IsUsingEFollowUp ? player.Profile.combat.eFollowUpHitWindow : combat.IsUsingSkillA ? config.skillAHitWindow :
                combat.IsUsingGroundCombo && config.groundHitWindows != null && index < config.groundHitWindows.Length
                    ? config.groundHitWindows[index] : config.hitWindow;
            // 지상 콤보는 프레임이 히트 구간을 건너뛰어도 놓치지 않도록 이전 진행도와 함께 구간 통과 여부로 판정한다.
            if (combat.IsUsingGroundCombo)
            {
                bool active = combat.SourceAttackProgress(state) >= window.x && previousGroundProgress <= window.y;
                previousGroundProgress = combat.SourceAttackProgress(state);
                if (!active) return;
                HitWindowActive = true;
            }
            else if (state.normalizedTime < window.x || state.normalizedTime > window.y) return;
            HitWindowActive = true;
            var scale = transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            foreach (var other in Physics.OverlapBox(transform.TransformPoint(box.center),
                Vector3.Scale(box.size * 0.5f, scale), transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                // 납도 콤보 중에는 검기 컴포넌트가 칼날 명중을 대신 처리한다.
                if (combat.IsSheathedCombo && swordWave != null && swordWave.isActiveAndEnabled)
                {
                    swordWave.TryHitWithBlade(other);
                    continue;
                }
                var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                HitBoss(other, false);
                if (enemy != null && Struck(hits, enemy))
                {
                    if (combat.IsSheathedCombo) enemy.ReceiveGroundComboHit(player.transform, combat.SheathedComboStep % 2, false); else if (combat.IsUsingEFollowUp || combat.IsParryCounter) enemy.ReceiveGroundComboHit(player.transform, 1, false);
                    else if (combat.IsUsingSkillA) enemy.ReceiveSkillHit(player.transform);
                    else if (combat.IsUsingGroundCombo) enemy.ReceiveGroundComboHit(player.transform, index, combat.IsGroundComboFinisher);
                    else enemy.ReceiveHit();
                }
            }
        }

        #endregion
    }
}
