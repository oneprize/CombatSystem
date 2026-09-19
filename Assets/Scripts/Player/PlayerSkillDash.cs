using System.Collections.Generic;
using UnityEngine;
using ParrySystem.Combat;
using ParrySystem.Boss;

namespace ParrySystem.Player
{
    [RequireComponent(typeof(CharacterController), typeof(Animator), typeof(PlayerCombat))]
    public sealed class PlayerSkillDash : MonoBehaviour
    {
        [Header("E skill - Skill_A (45 frames at 30 fps)")]
        [SerializeField, Range(0f, 1f)] private float slashStart = 14f / 45f;
        [SerializeField, Range(0f, 1f)] private float slashEnd = 17f / 45f;
        [SerializeField, Range(0f, 20f)] private float dashDistance = 4f;
        [SerializeField] private Vector3 hitSize = new Vector3(1.8f, 2.2f, 1.2f);

        private CharacterController character;
        private Animator animator;
        private PlayerCombat combat;
        private Vector3 direction;
        private float appliedDistance;
        private bool active, slashing;
        private readonly List<Collider> ignored = new List<Collider>();
        private readonly HashSet<EnemyDamageReaction> enemiesHit = new HashSet<EnemyDamageReaction>();
        private readonly HashSet<BossCombat> bossesHit = new HashSet<BossCombat>();

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            combat = GetComponent<PlayerCombat>();
        }

        public void Begin()
        {
            Cancel();
            direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            appliedDistance = 0f;
            enemiesHit.Clear();
            bossesHit.Clear();
            active = true;
        }

        private void Update()
        {
            if (active && (!combat.IsUsingSkillA || combat.IsHitReacting ||
                !character.enabled || !animator.isActiveAndEnabled))
                Cancel();
        }

        // Called after Animator evaluation instead of applying the clip's root displacement.
        public void ApplyMotion()
        {
            if (!active || !isActiveAndEnabled || !combat.IsUsingSkillA ||
                combat.IsHitReacting || !character.enabled)
            {
                Cancel();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Base Layer.Skill_A")) state = next;
            }
            if (!state.IsName("Base Layer.Skill_A")) return;
            Advance(state.normalizedTime);
        }

        private void Advance(float progress)
        {
            if (progress < slashStart) return;
            if (!slashing)
            {
                slashing = true;
                IgnoreEnemyBodies();
                HitAtCurrentPosition();
            }
            float fraction = Mathf.InverseLerp(slashStart, Mathf.Max(slashStart + .001f, slashEnd), progress);
            float target = Mathf.Clamp(dashDistance, 0f, 20f) * fraction;
            float remaining = Mathf.Max(0f, target - appliedDistance);
            // Substeps cover the entire travelled path even if a frame skips the slash window.
            int steps = Mathf.CeilToInt(remaining / .2f);
            for (int i = 0; i < steps; i++)
            {
                character.Move(direction * (remaining / steps));
                HitAtCurrentPosition();
            }
            appliedDistance = Mathf.Max(appliedDistance, target);
        }

        private void IgnoreEnemyBodies()
        {
            foreach (var enemy in FindObjectsByType<EnemyDamageReaction>(FindObjectsSortMode.None))
                IgnoreBodies(enemy.GetComponentsInChildren<Collider>());
            foreach (var boss in FindObjectsByType<BossCombat>(FindObjectsSortMode.None))
                IgnoreBodies(boss.GetComponentsInChildren<Collider>());
        }

        private void IgnoreBodies(Collider[] bodies)
        {
            foreach (var body in bodies)
            {
                if (body == character || body.isTrigger || !body.enabled ||
                    Physics.GetIgnoreCollision(character, body)) continue;
                Physics.IgnoreCollision(character, body, true);
                ignored.Add(body);
            }
        }

        private void HitAtCurrentPosition()
        {
            Vector3 center = character.bounds.center + direction * .6f;
            Vector3 half = new Vector3(Mathf.Max(.05f, hitSize.x), Mathf.Max(.05f, hitSize.y),
                Mathf.Max(.05f, hitSize.z)) * .5f;
            foreach (var other in Physics.OverlapBox(center, half,
                Quaternion.LookRotation(direction), ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = other.GetComponentInParent<EnemyDamageReaction>();
                if (enemy != null && enemiesHit.Add(enemy)) enemy.ReceiveSkillHit(transform);
                var boss = other.GetComponentInParent<BossCombat>();
                if (boss != null && bossesHit.Add(boss)) boss.TryReceivePlayerHit();
            }
        }

        public void Cancel()
        {
            foreach (var body in ignored)
                if (body != null && character != null)
                    Physics.IgnoreCollision(character, body, false);
            ignored.Clear();
            active = slashing = false;
        }

        private void OnDisable() { Cancel(); }
        private void OnDestroy() { Cancel(); }
    }
}
