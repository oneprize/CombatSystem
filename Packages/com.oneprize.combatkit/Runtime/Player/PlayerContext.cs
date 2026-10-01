using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// Shared references every player module works with. Modules reach each other through
    /// <see cref="Character"/> instead of GetComponent.
    /// </summary>
    public sealed class PlayerContext
    {
        public PlayerCharacter Character { get; }
        public Transform Transform { get; }
        public Animator Animator { get; }
        public CharacterController Controller { get; }

        public PlayerContext(PlayerCharacter character)
        {
            Character = character;
            Transform = character.transform;
            Animator = character.GetComponent<Animator>();
            Controller = character.GetComponent<CharacterController>();
        }
    }
}
