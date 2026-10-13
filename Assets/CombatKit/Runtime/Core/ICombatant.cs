using UnityEngine;

namespace CombatSystem.Core
{
    /// <summary>
    /// Implemented by the top-level combat component of a spawned actor (player, enemy, boss)
    /// so it can register with <see cref="CombatRegistry"/> and be discovered by other actors
    /// without a hand-wired scene reference.
    /// </summary>
    public interface ICombatant
    {
        Transform Transform { get; }
        CombatTeam Team { get; }
    }
}
