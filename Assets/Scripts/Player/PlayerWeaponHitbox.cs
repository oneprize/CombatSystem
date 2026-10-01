using UnityEngine;
using ParrySystem.Combat;
namespace ParrySystem.Player
{
    [RequireComponent(typeof(BoxCollider))]
    public class PlayerWeaponHitbox : MonoBehaviour
    {
        [SerializeField] private Vector2 hitWindow = new Vector2(0.25f, 0.75f);
        [SerializeField] private Vector2 combo2HitWindow = new Vector2(0.1f, 0.25f);
        [SerializeField] private Vector2 combo3HitWindow = new Vector2(0.25f, 0.5f);
        [SerializeField] private Vector2 skillFirstHitWindow = new Vector2(0.10f, 0.18f);
        [SerializeField] private Vector2 skillHitWindow = new Vector2(0.47f, 0.57f);
        [SerializeField] private Vector3 skillImpactCenter = new Vector3(0f, -0.1f, 1.2f);
        [SerializeField] private Vector3 skillImpactSize = new Vector3(1.8f, 2.2f, 2.4f);
        [SerializeField] private Vector2 skillAHitWindow = new Vector2(15f / 45f, 16f / 45f);
        [Header("Upper attack")]
        [SerializeField] private Vector2 upperHitWindow = new Vector2(9f / 75f, 16f / 75f);
        [SerializeField] private Vector3 upperImpactCenter = new Vector3(0, -.15f, 1.2f);
        [SerializeField] private Vector3 upperImpactSize = new Vector3(1.4f, 2.2f, 2f);
        [Header("Air combo")]
        [SerializeField] private Vector2[] airHitWindows = {
            // Attach clips omit recovery frames; preserve the hit times in seconds.
            new Vector2(6f/24f,14f/24f), new Vector2(4.5f/18f,10.5f/18f), new Vector2(6.3f/25f,14.7f/25f),
            new Vector2(6f/23f,14f/23f), new Vector2(5.4f/22f,12.6f/22f), new Vector2(6.75f/30f,18f/30f) };
        [SerializeField] private Vector3 airImpactCenter = new Vector3(0, -.1f, 1.25f);
        [SerializeField] private Vector3 airImpactSize = new Vector3(1.8f, 2.5f, 2.5f);
        [Header("Ground 5 combo")]
        [SerializeField] private Vector2[] groundHitWindows = {
            new Vector2(.2f,.55f), new Vector2(.2f,.55f), new Vector2(.2f,.55f),
            new Vector2(.2f,.55f), new Vector2(.2f,.55f) };
        [Header("Ground combo 3 - right-foot kick")]
        [SerializeField] private Vector2 groundKickWindow = new Vector2(.25f, .46f);
        [SerializeField, Min(.01f)] private float groundKickRadius = .3f;
        private Transform kickFoot;
        private Vector3 previousFootPosition;
        private float previousGroundProgress;
        private float previousAirProgress;
        private float previousUpperProgress;
        [Header("Skill C launcher")]
        [SerializeField] private Vector2 skillCHitWindow = new Vector2(.30f,.44f);
        [SerializeField] private Vector3 skillCImpactCenter = new Vector3(0f,-.1f,1.1f);
        [SerializeField] private Vector3 skillCImpactSize = new Vector3(1.6f,2.4f,2.2f);
        private float previousSkillCProgress;
        private PlayerCombat combat;
        private Animator animator;
        private BoxCollider box;
        private int sequence = -1, combo = -1;
        private readonly System.Collections.Generic.HashSet<EnemyDamageReaction> hits =
            new System.Collections.Generic.HashSet<EnemyDamageReaction>();
        private readonly System.Collections.Generic.HashSet<EnemyDamageReaction> secondSkillHits =
            new System.Collections.Generic.HashSet<EnemyDamageReaction>();
        private readonly System.Collections.Generic.HashSet<ParrySystem.Boss.BossCombat> bossHits =
            new System.Collections.Generic.HashSet<ParrySystem.Boss.BossCombat>();
        private readonly System.Collections.Generic.HashSet<ParrySystem.Boss.BossCombat> secondBossHits =
            new System.Collections.Generic.HashSet<ParrySystem.Boss.BossCombat>();
        private void HitBoss(Collider other, bool secondStrike)
        {
            var boss = other.GetComponentInParent<ParrySystem.Boss.BossCombat>();
            var struck = secondStrike ? secondBossHits : bossHits;
            if (boss != null && struck.Add(boss)) boss.TryReceivePlayerHit();
        }
        private void Awake()
        {
            combat = GetComponentInParent<PlayerCombat>();
            animator = combat.GetComponent<Animator>();
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
            if (!combat.isAttacking || combat.IsGuarding || combat.IsHitReacting || !box.enabled) return;
            if (combat.IsUsingSkillI) return;
            if (combat.IsUsingPiercingSkill)
            {
                var dash = combat.GetComponent<PlayerPiercingDash>();
                HitWindowActive = dash != null && dash.IsDashing;
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
                bool active=progress>=skillCHitWindow.x && previousSkillCProgress<=skillCHitWindow.y;
                previousSkillCProgress=progress;
                if(!active)return;
                HitWindowActive=true;
                Transform owner=combat.transform;
                foreach(var other in Physics.OverlapBox(owner.TransformPoint(skillCImpactCenter),skillCImpactSize*.5f,owner.rotation,~0,QueryTriggerInteraction.Ignore))
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
                Vector2 hitRange = airHitWindows != null && airIndex < airHitWindows.Length
                    ? airHitWindows[airIndex] : new Vector2(.15f, .35f);
                float progress = state.normalizedTime;
                bool active = progress >= hitRange.x && previousAirProgress <= hitRange.y;
                previousAirProgress = progress;
                if (!active) return;
                HitWindowActive = true;
                Transform owner = combat.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(airImpactCenter),
                    airImpactSize * .5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
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
                bool crossedWindow = progress >= upperHitWindow.x && previousUpperProgress <= upperHitWindow.y;
                previousUpperProgress = progress;
                if (!crossedWindow) return;
                HitWindowActive = true;
                Transform owner = combat.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(upperImpactCenter),
                    upperImpactSize * .5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
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
                int strike = progress >= skillFirstHitWindow.x && progress <= skillFirstHitWindow.y ? 0 :
                    progress >= skillHitWindow.x && progress <= skillHitWindow.y ? 1 : -1;
                if (strike < 0) return;
                HitWindowActive = true;
                var struck = strike == 0 ? hits : secondSkillHits;
                Transform owner = combat.transform;
                foreach (var other in Physics.OverlapBox(owner.TransformPoint(skillImpactCenter),
                    skillImpactSize * 0.5f, owner.rotation, ~0, QueryTriggerInteraction.Ignore))
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
                bool active = progress >= groundKickWindow.x && previousGroundProgress <= groundKickWindow.y;
                previousGroundProgress = progress;
                if (!active) return;
                HitWindowActive = true;
                foreach (var other in Physics.OverlapCapsule(footFrom, footPosition, groundKickRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    HitBoss(other, false);
                    var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                    if (enemy != null && hits.Add(enemy)) enemy.ReceiveGroundComboHit(combat.transform, index);
                }
                return;
            }
            Vector2 window = combat.IsUsingSkillA ? skillAHitWindow :
                combat.IsUsingGroundCombo && groundHitWindows != null && index < groundHitWindows.Length
                    ? groundHitWindows[index] : hitWindow;
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
                    if (combat.IsUsingSkillA) enemy.ReceiveSkillHit(combat.transform);
                    else if (combat.IsUsingGroundCombo) enemy.ReceiveGroundComboHit(combat.transform, index);
                    else enemy.ReceiveHit();
                }
            }
        }
    }
}