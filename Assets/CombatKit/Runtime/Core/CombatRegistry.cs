using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// Lightweight runtime directory of active combatants. Replaces hand-dragged inspector
    /// references and scene-wide FindObjectOfType lookups: a spawned actor registers itself
    /// in OnEnable/OnDisable, and anything that needs to find "the player" or "an enemy"
    /// queries this instead of knowing about a specific GameObject. Works regardless of
    /// spawn order or how many actors exist per team.
    /// </summary>
    public static class CombatRegistry
    {
        private static readonly List<ICombatant> players = new List<ICombatant>();
        private static readonly List<ICombatant> enemies = new List<ICombatant>();

        private static List<ICombatant> ListFor(CombatTeam team) => team == CombatTeam.Player ? players : enemies;

        public static void Register(ICombatant combatant)
        {
            if (combatant == null) return;
            var list = ListFor(combatant.Team);
            if (!list.Contains(combatant)) list.Add(combatant);
        }

        public static void Unregister(ICombatant combatant)
        {
            if (combatant == null) return;
            ListFor(combatant.Team).Remove(combatant);
        }

        /// <summary>All currently registered combatants for a team. Entries whose Transform
        /// has been destroyed are pruned lazily.</summary>
        public static IReadOnlyList<ICombatant> GetAll(CombatTeam team)
        {
            var list = ListFor(team);
            list.RemoveAll(c => c == null || c.Transform == null);
            return list;
        }

        public static ICombatant FindNearest(CombatTeam team, Vector3 from)
        {
            ICombatant nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var combatant in GetAll(team))
            {
                float sqr = (combatant.Transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = combatant;
                }
            }
            return nearest;
        }
    }
}
