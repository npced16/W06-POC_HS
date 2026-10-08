using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WhistlePOC
{
    // Owns Input System callbacks; gameplay consumes semantic events only.
    public sealed class DuelInputSystem : MonoBehaviour
    {
        public InputActionAsset actions;
        public event Action<Vector2> Move, Look;
        public event Action Attack, Parry, Scan, Dodge, Pause, Restart, Confirm;
        InputActionAsset instance;
        InputActionMap map;

        void Awake()
        {
            instance = Instantiate(actions); map = instance.FindActionMap("Duel", true);
            Bind("Move", OnMove, true); Bind("Look", OnLook, true);
            Bind("Attack", OnAttack); Bind("Parry", OnParry); Bind("Scan", OnScan); Bind("Dodge", OnDodge);
            Bind("Pause", OnPause); Bind("Restart", OnRestart); Bind("Confirm", OnConfirm);
        }
        void Bind(string name, Action<InputAction.CallbackContext> handler, bool cancel = false)
        {
            var action = map.FindAction(name, true); action.performed += handler;
            if (cancel) action.canceled += handler;
        }
        void OnEnable() { if (map != null) map.Enable(); }
        void OnDisable() { map?.Disable(); Move?.Invoke(Vector2.zero); Look?.Invoke(Vector2.zero); }
        void OnDestroy() { if (instance != null) Destroy(instance); }
        void OnMove(InputAction.CallbackContext context) => Move?.Invoke(context.ReadValue<Vector2>());
        void OnLook(InputAction.CallbackContext context) => Look?.Invoke(context.ReadValue<Vector2>());
        void OnAttack(InputAction.CallbackContext context) => Attack?.Invoke();
        void OnParry(InputAction.CallbackContext context) => Parry?.Invoke();
        void OnScan(InputAction.CallbackContext context) => Scan?.Invoke();
        void OnDodge(InputAction.CallbackContext context) => Dodge?.Invoke();
        void OnPause(InputAction.CallbackContext context) => Pause?.Invoke();
        void OnRestart(InputAction.CallbackContext context) => Restart?.Invoke();
        void OnConfirm(InputAction.CallbackContext context) => Confirm?.Invoke();
    }
}
