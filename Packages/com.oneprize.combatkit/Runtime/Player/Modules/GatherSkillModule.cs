using System.Collections.Generic;
using UnityEngine;
using CombatSystem.Combat;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>Skill I: pulls visible enemies in a forward cone into a row in front of the player.</summary>
    public sealed class GatherSkillModule : PlayerModule
    {
        private PlayerProfile.GatherSkillSettings config;
        private int sequence = -1;
        private bool fired;
        public int LastGatherCount { get; private set; }

        protected override void OnInitialize()
        {
            config = Player.Profile.gatherSkill;
        }

        public void LateTick()
        {
            var combat = Player.Combat;
            if (!combat.IsUsingSkillI) return;
            if (sequence != combat.AttackSequence) { sequence = combat.AttackSequence; fired = false; LastGatherCount = 0; }
            if (fired) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0)) { var next = animator.GetNextAnimatorStateInfo(0); if (next.IsName("Base Layer.Skill_I")) state = next; }
            if (!state.IsName("Base Layer.Skill_I") || state.normalizedTime < config.recallProgress) return;
            fired = true;
            var origin = transform.position;
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var targets = new List<EnemyDamageReaction>();
            foreach (var combatant in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                var enemy = combatant.Transform.GetComponent<EnemyDamageReaction>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0) continue;
                var delta = enemy.transform.position - origin;
                if (Mathf.Abs(delta.y) > config.heightTolerance) continue;
                delta.y = 0;
                if (delta.sqrMagnitude > config.range * config.range || Vector3.Angle(forward, delta) > config.halfAngle) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<EnemyDamageReaction>() == null) { blocked = true; break; }
                if (!blocked) targets.Add(enemy);
            }
            // Preserve left-to-right order so pulled enemies do not cross and block one another.
            targets.Sort((a, b) => Vector3.Dot(a.transform.position - origin, right).CompareTo(Vector3.Dot(b.transform.position - origin, right)));
            foreach (var enemy in targets)
            {
                int slot = LastGatherCount; int column = slot % 5; int row = slot / 5;
                int columns = Mathf.Min(5, targets.Count - row * 5);
                float side = (column - (columns - 1) * .5f) * config.spacing;
                Vector3 destination = origin + forward * (config.gatherDistance + row * config.spacing) + right * side;
                destination.y = enemy.transform.position.y;
                if (enemy.BeginGather(destination, transform, config.pullDuration)) LastGatherCount++;
            }
        }
    }
}
