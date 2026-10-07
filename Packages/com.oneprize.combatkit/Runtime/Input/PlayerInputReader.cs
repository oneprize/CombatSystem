using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Player
{
    /// <summary>
    /// Default <see cref="IPlayerInput"/>: reads the "Combat" action map of an InputActionAsset.
    /// Every property reads the action directly instead of caching per frame, so
    /// WasPressedThisFrame keeps exactly the timing of the old Input.GetKeyDown /
    /// Keyboard.current checks (shared by combat modules). Rebind keys in the asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string mapName = "Combat";

        private InputActionMap map;
        private InputAction move, run, jump, dodge, attack, guard;
        private InputAction skillG, skillA, skillB, skillI, upperAttack, skillCModifier;
        private InputAction lockOn, lockOnNext;

        public Vector2 Move => move != null ? move.ReadValue<Vector2>() : Vector2.zero;
        public bool RunHeld => Held(run);
        public bool JumpPressed => Pressed(jump);
        public bool DodgePressed => Pressed(dodge);
        public bool AttackPressed => Pressed(attack);
        public bool AttackHeld => Held(attack);
        public bool GuardHeld => Held(guard);
        public bool SkillGPressed => Pressed(skillG);
        public bool SkillAPressed => Pressed(skillA);
        public bool SkillBPressed => Pressed(skillB);
        public bool SkillIPressed => Pressed(skillI);
        public bool UpperAttackPressed => Pressed(upperAttack);
        public bool SkillCModifierHeld => Held(skillCModifier);
        public bool LockOnPressed => Pressed(lockOn);
        public bool LockOnNextPressed => Pressed(lockOnNext);

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("PlayerInputReader: no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }
            map = actions.FindActionMap(mapName, true);
            move = map.FindAction("Move", true);
            run = map.FindAction("Run", true);
            jump = map.FindAction("Jump", true);
            dodge = map.FindAction("Dodge", true);
            attack = map.FindAction("Attack", true);
            guard = map.FindAction("Guard", true);
            skillG = map.FindAction("SkillG", true);
            skillA = map.FindAction("SkillA", true);
            skillB = map.FindAction("SkillB", true);
            skillI = map.FindAction("SkillI", true);
            upperAttack = map.FindAction("UpperAttack", true);
            skillCModifier = map.FindAction("SkillCModifier", true);
            lockOn = map.FindAction("LockOn", true);
            lockOnNext = map.FindAction("LockOnNext", true);
        }

        private void OnEnable() { map?.Enable(); }
        private void OnDisable() { map?.Disable(); }

        private bool Pressed(InputAction action) => enabled && action != null && action.WasPressedThisFrame();
        private bool Held(InputAction action) => enabled && action != null && action.IsPressed();
    }
}
