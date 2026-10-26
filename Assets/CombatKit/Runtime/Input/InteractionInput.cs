using UnityEngine.InputSystem;

namespace CombatSystem.Core
{
    /// <summary>게임 내 상호작용은 F로 통일한다. 공격/이동 입력과 별도로 사용한다.</summary>
    public static class InteractionInput
    {
        public const Key InteractionKey = Key.F;
        public const string DisplayKey = "F";
        public static bool Held => Keyboard.current != null && Keyboard.current[InteractionKey].isPressed;
        public static bool Pressed => Keyboard.current != null && Keyboard.current[InteractionKey].wasPressedThisFrame;
    }
}
