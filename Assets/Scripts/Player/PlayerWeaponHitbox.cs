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
        private float previousUpperProgress;
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
        }
        private void LateUpdate()
        {
            if (!combat.isAttacking || combat.IsGuarding || combat.IsHitReacting || !box.enabled) return;
            int index = combat.AttackComboIndex;
            if (sequence != combat.AttackSequence || combo != index)
            {
                hits.Clear();
                previousUpperProgress = 0;
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
            if (combat.IsUsingUpperAttack)
            {
                float progress = state.normalizedTime;
                bool crossedWindow = progress >= upperHitWindow.x && previousUpperProgress <= upperHitWindow.y;
                previousUpperProgress = progress;
                if (!crossedWindow) return;
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
            Vector2 window = combat.IsUsingSkillA ? skillAHitWindow : index == 1 ? combo2HitWindow : index == 2 ? combo3HitWindow : hitWindow;
            if (state.normalizedTime < window.x || state.normalizedTime > window.y) return;
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
                    else enemy.ReceiveHit();
                }
            }
        }
    }
}