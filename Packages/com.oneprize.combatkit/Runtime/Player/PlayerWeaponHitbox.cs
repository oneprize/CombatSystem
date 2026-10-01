using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Data.Player;
namespace CombatSystem.Player
{
    [RequireComponent(typeof(BoxCollider))]
    public class PlayerWeaponHitbox : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponHitboxConfig config;
        private Transform kickFoot;
        private Vector3 previousFootPosition;
        private float previousGroundProgress;
        private float previousAirProgress;
        private float previousUpperProgress;
        private float previousSkillCProgress;
        private PlayerCharacter player;
        private CombatModule combat => player.Combat;
        private Animator animator;
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
        private void HitBoss(Collider other, bool secondStrike)
        {
            var boss = other.GetComponentInParent<CombatSystem.Boss.BossCombat>();
            var struck = secondStrike ? secondBossHits : bossHits;
            if (boss != null && struck.Add(boss)) boss.TryReceivePlayerHit();
        }
        private void Awake()
        {
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PlayerWeaponHitboxConfig>();
                Debug.LogWarning("PlayerWeaponHitbox: no PlayerWeaponHitboxConfig assigned, using built-in defaults.", this);
            }
            player = GetComponentInParent<PlayerCharacter>();
            animator = player.GetComponent<Animator>();
            box = GetComponent<BoxCollider>();
            kickFoot = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
            if (kickFoot != null) previousFootPosition = kickFoot.position;
        }
        public bool HitWindowActive { get; private set; }
        private void LateUpdate()
        {
            HitWindowActive = false;
            Vector3 footPosition = kickFoot != null ? kickFoot.position : transform.position;
            Vector3 footFrom = previousFootPosition;
            previousFootPosition = footPosition;
            if (!player.IsAttacking || player.IsGuarding || player.IsHitReacting || !box.enabled) return;
            if (combat.IsUsingSkillI) return;
            if (combat.IsUsingPiercingSkill)
            {
                HitWindowActive = player.PiercingDash.IsDashing;
                return;
            }
            int index = combat.AttackComboIndex;
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
                sequence = combat.AttackSequence;
                combo = index;
            }
            string name = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(name)) state = next;
            }
            if (!state.IsName(name)) return;
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
                    if(enemy!=null && hits.Add(enemy))enemy.ReceiveUpperHit(owner);
                    HitBoss(other,false);
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
                    if (enemy != null && enemy.IsAirborneForCombo && hits.Add(enemy))
                        enemy.ReceiveAirComboHit(owner, airIndex);
                    HitBoss(other, false);
                }
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
                    if (enemy != null && hits.Add(enemy)) enemy.ReceiveUpperHit(owner);
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
                    if (enemy != null && struck.Add(enemy)) enemy.ReceiveAerialHit(owner, strike);
                    HitBoss(other, strike == 1);
                }
                return;
            }
            if (combat.IsUsingGroundCombo && index == 2 && kickFoot != null)
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
                    if (enemy != null && hits.Add(enemy)) enemy.ReceiveGroundComboHit(player.transform, index);
                }
                return;
            }
            Vector2 window = combat.IsUsingSkillA ? config.skillAHitWindow :
                combat.IsUsingGroundCombo && config.groundHitWindows != null && index < config.groundHitWindows.Length
                    ? config.groundHitWindows[index] : config.hitWindow;
            if (combat.IsUsingGroundCombo)
            {
                bool active = state.normalizedTime >= window.x && previousGroundProgress <= window.y;
                previousGroundProgress = state.normalizedTime;
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
                var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                HitBoss(other, false);
                if (enemy != null && hits.Add(enemy))
                {
                    if (combat.IsUsingSkillA) enemy.ReceiveSkillHit(player.transform);
                    else if (combat.IsUsingGroundCombo) enemy.ReceiveGroundComboHit(player.transform, index);
                    else enemy.ReceiveHit();
                }
            }
        }
    }
}