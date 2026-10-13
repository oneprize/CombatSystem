using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// Everything the player modules read from the player's controls. PlayerCharacter looks
    /// for an implementation on its own GameObject, so a game can drive the character from
    /// its own input layer (or an AI / cutscene) instead of <see cref="PlayerInputReader"/>.
    /// "Pressed" members are true only on the frame the button went down; "Held" members
    /// are true while it stays down.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>x = right, y = forward. Keyboard gives -1/0/1 per axis (not normalized).</summary>
        Vector2 Move { get; }
        bool RunHeld { get; }
        bool JumpPressed { get; }
        bool DodgePressed { get; }
        /// <summary>Primary attack input.</summary>
        bool AttackPressed { get; }
        /// <summary>Attack button kept down (charges the power attack).</summary>
        bool AttackHeld { get; }
        bool GuardHeld { get; }
        bool SkillGPressed { get; }
        bool SkillAPressed { get; }
        bool SkillBPressed { get; }
        bool UpperAttackPressed { get; }
        /// <summary>Held together with Attack to trigger Skill C.</summary>
        bool SkillCModifierHeld { get; }
        /// <summary>Toggles lock-on.</summary>
        bool LockOnPressed { get; }
        /// <summary>Switches the lock-on target clockwise around the player.</summary>
        bool LockOnNextPressed { get; }
    }

    /// <summary>No input at all. Used when the character has no IPlayerInput component.</summary>
    public sealed class NullPlayerInput : IPlayerInput
    {
        public static readonly NullPlayerInput Instance = new NullPlayerInput();
        public Vector2 Move => Vector2.zero;
        public bool RunHeld => false;
        public bool JumpPressed => false;
        public bool DodgePressed => false;
        public bool AttackPressed => false;
        public bool AttackHeld => false;
        public bool GuardHeld => false;
        public bool SkillGPressed => false;
        public bool SkillAPressed => false;
        public bool SkillBPressed => false;
        public bool UpperAttackPressed => false;
        public bool SkillCModifierHeld => false;
        public bool LockOnPressed => false;
        public bool LockOnNextPressed => false;
    }
}
