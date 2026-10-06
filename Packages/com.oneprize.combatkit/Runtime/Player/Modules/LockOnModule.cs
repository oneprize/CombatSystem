using UnityEngine;
using CombatSystem.Core;
using CombatSystem.Data.Player;

namespace CombatSystem.Player
{
    /// <summary>
    /// Lock-on target selection. Z toggles a lock on the nearest enemy, the mouse wheel
    /// cycles clockwise around the player, and a defeated or out-of-range target hands the
    /// lock to the nearest remaining enemy. The camera and attack facing read <see cref="Target"/>.
    /// </summary>
    public sealed class LockOnModule : PlayerModule
    {
        private PlayerProfile.LockOnSettings config;

        /// <summary>The locked enemy, or null when lock-on is off.</summary>
        public ICombatant Target { get; private set; }
        public bool IsLocked => Target != null;

        protected override void OnInitialize()
        {
            config = Player.Profile.lockOn;
        }

        public void Tick()
        {
            var controls = Player.Controls;
            if (controls.LockOnPressed)
            {
                Target = IsLocked ? null : FindNearest(null, config.acquireRange);
                return;
            }
            if (!IsLocked) return;
            if (!IsValid(Target, config.keepRange))
            {
                // Defeated, disabled or too far: keep fighting the nearest remaining enemy.
                Target = FindNearest(Target, config.acquireRange);
                return;
            }
            if (controls.LockOnNextPressed) Target = FindClockwise(Target) ?? Target;
        }

        private ICombatant FindNearest(ICombatant exclude, float range)
        {
            ICombatant best = null;
            float bestSqr = range * range;
            foreach (var enemy in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                if (enemy == exclude || !IsValid(enemy, range)) continue;
                float sqr = Flat(enemy.Transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                best = enemy;
            }
            return best;
        }

        /// <summary>
        /// The enemy at the smallest clockwise angle (seen from above) from the current target,
        /// measured around the player, so repeated presses visit every enemy in a stable order.
        /// </summary>
        private ICombatant FindClockwise(ICombatant current)
        {
            Vector3 from = Flat(current.Transform.position - transform.position);
            ICombatant best = null;
            float bestAngle = float.PositiveInfinity;
            foreach (var enemy in CombatRegistry.GetAll(CombatTeam.Enemy))
            {
                if (enemy == current || !IsValid(enemy, config.acquireRange)) continue;
                float angle = Vector3.SignedAngle(from, Flat(enemy.Transform.position - transform.position), Vector3.up);
                if (angle <= 0f) angle += 360f; // positive = clockwise in Unity's left-handed, y-up space
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                best = enemy;
            }
            return best;
        }

        private bool IsValid(ICombatant enemy, float range)
        {
            if (enemy == null || enemy.Transform == null || !enemy.Transform.gameObject.activeInHierarchy) return false;
            var health = enemy.Transform.GetComponent<CombatSystem.Combat.EnemyDamageReaction>();
            if (health != null && health.CurrentHealth <= 0f) return false;
            return Flat(enemy.Transform.position - transform.position).sqrMagnitude <= range * range;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public override void OnDisable() { Target = null; }
    }
}
