using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CombatSystem.Player
{
    /// <summary>
    /// 게임플레이 중 마우스 커서 잠금을 관리한다. ESC로 잠금을 풀고 화면 클릭으로 다시 잠그며,
    /// 열린 메뉴가 하나라도 있거나 창 포커스를 잃으면 커서를 풀어 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCursorController : MonoBehaviour
    {
        #region 필드

        [SerializeField] private bool lockOnStart = true;
        [SerializeField] private bool escapeToUnlock = true;
        [SerializeField] private bool clickToRelock = true;
        private readonly HashSet<Object> menus = new HashSet<Object>();
        private bool wantsLock, escaped, started;
        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        #endregion

        #region 공개 API

        /// <summary>시작 시 설정에 따라 잠금 여부를 정하고 적용한다.</summary>
        private void Start() { started = true; wantsLock = lockOnStart; Apply(); }
        /// <summary>다시 켜졌을 때 현재 잠금 요청 상태를 재적용한다(Start 이전에는 건너뜀).</summary>
        private void OnEnable() { if (started) Apply(); }

        /// <summary>게임플레이용 커서 잠금을 요청한다(열린 메뉴가 없을 때만 실제로 잠긴다).</summary>
        public void LockCursor() { wantsLock = true; escaped = false; Apply(); }
        /// <summary>게임플레이용 커서 잠금을 해제한다.</summary>
        public void UnlockCursor() { wantsLock = false; escaped = false; Apply(); }

        /// <summary>
        /// UI가 메뉴 열림/닫힘을 알린다. 게임플레이 잠금 요청은 그대로 유지되며,
        /// 여러 메뉴가 동시에 열려 있을 수 있으므로 소유자별로 추적해 모두 닫혀야 다시 잠긴다.
        /// </summary>
        public void SetMenuOpen(Object owner, bool open)
        {
            if (owner == null) return;
            if (open) menus.Add(owner); else menus.Remove(owner);
            Apply();
        }

        #endregion

        #region 내부 로직

        /// <summary>파괴된 메뉴 소유자를 정리하고, ESC 입력 시 잠금 해제, ESC로 풀린 상태에서 UI 밖 클릭 시 재잠금을 처리한다.</summary>
        private void Update()
        {
            menus.RemoveWhere(owner => owner == null);
            if (escapeToUnlock && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                wantsLock = false; escaped = true; Apply();
            }
            else if (escaped && clickToRelock && menus.Count == 0 &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                LockCursor();
        }

        /// <summary>컴포넌트 활성, 창 포커스, 잠금 요청, 열린 메뉴 유무를 종합해 실제 커서 잠금/표시 상태를 결정한다.</summary>
        private void Apply()
        {
            bool locked = isActiveAndEnabled && Application.isFocused && wantsLock && menus.Count == 0;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        /// <summary>창 포커스가 바뀌면 잠금 상태를 재계산한다.</summary>
        private void OnApplicationFocus(bool focused) { if (started) Apply(); }
        /// <summary>꺼질 때는 커서를 반드시 풀고 보이게 한다.</summary>
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        #endregion
    }
}
