using UnityEngine;
using UnityEngine.InputSystem;

namespace CombatSystem.Player
{
    /// <summary>
    /// 기본 <see cref="IPlayerInput"/> 구현. InputActionAsset의 "Combat" 액션 맵을 읽는다.
    /// 프레임 단위로 캐시하지 않고 프로퍼티마다 액션을 직접 읽으므로, WasPressedThisFrame이
    /// 기존 Input.GetKeyDown / Keyboard.current 검사(전투 모듈들이 함께 쓰던 방식)와 정확히 같은 타이밍을 유지한다.
    /// 키 재배치는 에셋에서 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        #region 필드

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string mapName = "Combat";

        private InputActionMap map;
        private InputAction move, run, jump, dodge, attack, guard;
        private InputAction skillG, skillA, skillB, upperAttack, skillCModifier;
        private InputAction lockOn, lockOnNext;

        #endregion

        #region 프로퍼티

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
        public bool UpperAttackPressed => Pressed(upperAttack);
        public bool SkillCModifierHeld => Held(skillCModifier);
        public bool LockOnPressed => Pressed(lockOn);
        public bool LockOnNextPressed => Pressed(lockOnNext);

        #endregion

        #region Unity 생명주기

        /// <summary>
        /// 액션 맵과 각 액션을 이름으로 찾아 캐시한다. 에셋이 없으면 에러를 남기고 컴포넌트를 끄며,
        /// 맵/액션 이름이 없으면 예외를 던져 설정 오류를 바로 드러낸다.
        /// </summary>
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
            upperAttack = map.FindAction("UpperAttack", true);
            skillCModifier = map.FindAction("SkillCModifier", true);
            lockOn = map.FindAction("LockOn", true);
            lockOnNext = map.FindAction("LockOnNext", true);
        }

        /// <summary>컴포넌트가 켜지면 액션 맵을 활성화한다.</summary>
        private void OnEnable() { map?.Enable(); }
        /// <summary>컴포넌트가 꺼지면 액션 맵을 비활성화한다.</summary>
        private void OnDisable() { map?.Disable(); }

        #endregion

        #region 내부 로직

        /// <summary>컴포넌트가 켜져 있고 이번 프레임에 액션이 눌렸는지 확인한다.</summary>
        private bool Pressed(InputAction action) => enabled && action != null && action.WasPressedThisFrame();
        /// <summary>컴포넌트가 켜져 있고 액션을 누르고 있는 중인지 확인한다.</summary>
        private bool Held(InputAction action) => enabled && action != null && action.IsPressed();

        #endregion
    }
}
