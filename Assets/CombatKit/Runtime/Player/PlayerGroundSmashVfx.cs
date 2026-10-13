using UnityEngine;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class PlayerGroundSmashVfx : MonoBehaviour
    {
        [SerializeField] private PlayerWeaponHitboxConfig hitConfig;
        [SerializeField] private GameObject effectPrefab;
        [SerializeField, Min(.01f)] private float effectScale = .32f;
        [SerializeField, Min(.1f)] private float effectLifetime = 4f;
        [SerializeField, Range(0f, 1f)] private float effectStartTime = .0842f;
        [SerializeField] private LayerMask groundLayers = ~0;
        private PlayerCharacter player;
        private Animator animator;
        private int sequence = -1;
        private bool played;
        public int PlayedCount { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerCharacter>();
            animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            if (player == null || player.Combat == null || animator == null ||
                hitConfig == null || effectPrefab == null) return;
            var combat = player.Combat;
            if (sequence != combat.AttackSequence)
            {
                sequence = combat.AttackSequence;
                played = false;
            }
            if (played || !combat.IsUsingSkill || !combat.IsAttacking || player.IsHitReacting) return;
            string stateName = "Base Layer." + combat.CurrentAttackStateName;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName(stateName)) state = next;
            }
            if (!state.IsName(stateName) || state.normalizedTime < hitConfig.skillHitWindow.x) return;
            // The Q skill's second strike owns this effect, once per attack.
            played = true;
            SpawnImpact();
        }

        private void SpawnImpact()
        {
            Vector3 center = transform.TransformPoint(hitConfig.skillImpactCenter);
            var hits = Physics.RaycastAll(center + Vector3.up * 3f, Vector3.down, 8f,
                groundLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit ground = default;
            foreach (var hit in hits)
            {
                if (hit.normal.y < .5f || hit.distance >= nearest ||
                    hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<CombatSystem.Combat.EnemyDamageReaction>() != null ||
                    hit.collider.GetComponentInParent<CombatSystem.Boss.BossCombat>() != null) continue;
                nearest = hit.distance;
                ground = hit;
            }
            if (float.IsPositiveInfinity(nearest)) return;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, ground.normal).normalized;
            // Keep world placement outside the FBX's animated root transform.
            var anchor = new GameObject("Q Ground Smash");
            anchor.transform.SetPositionAndRotation(ground.point + ground.normal * .015f,
                Quaternion.LookRotation(forward, ground.normal));
            anchor.transform.localScale = Vector3.one * effectScale;
            var effect = Instantiate(effectPrefab, anchor.transform, false);
            var effectAnimator = effect.GetComponent<Animator>();
            if (effectAnimator != null)
            {
                // Skip the FBX's anticipation so the rock burst starts on contact.
                effectAnimator.Play("GroundSmash", 0, effectStartTime);
                effectAnimator.Update(0f);
            }
            PlayedCount++;
            Destroy(anchor, effectLifetime);
        }
    }
}
