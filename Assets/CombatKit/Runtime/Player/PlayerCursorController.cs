using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CombatSystem.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCursorController : MonoBehaviour
    {
        [SerializeField] private bool lockOnStart = true;
        [SerializeField] private bool escapeToUnlock = true;
        [SerializeField] private bool clickToRelock = true;
        private readonly HashSet<Object> menus = new HashSet<Object>();
        private bool wantsLock, escaped, started;
        public bool IsLocked => Cursor.lockState == CursorLockMode.Locked;

        private void Start() { started = true; wantsLock = lockOnStart; Apply(); }
        private void OnEnable() { if (started) Apply(); }

        public void LockCursor() { wantsLock = true; escaped = false; Apply(); }
        public void UnlockCursor() { wantsLock = false; escaped = false; Apply(); }

        // UI callers retain the gameplay lock preference; multiple open menus can coexist.
        public void SetMenuOpen(Object owner, bool open)
        {
            if (owner == null) return;
            if (open) menus.Add(owner); else menus.Remove(owner);
            Apply();
        }

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

        private void Apply()
        {
            bool locked = isActiveAndEnabled && Application.isFocused && wantsLock && menus.Count == 0;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        private void OnApplicationFocus(bool focused) { if (started) Apply(); }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
