using System;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputReader : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] string actionMapName = "Player";

    InputActionMap map;
    InputAction moveAction;
    InputAction jumpAction;

    public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public bool JumpHeld { get; private set; }
    public event Action JumpPressed;

#if UNITY_EDITOR
    void Reset()
    {
        actions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
    }
#endif

    void OnEnable()
    {
        if (actions == null)
        {
            Debug.LogError("PlayerInputReader: InputActionAsset is not assigned.", this);
            enabled = false;
            return;
        }

        map = actions.FindActionMap(actionMapName, true);
        moveAction = map.FindAction("Move", true);
        jumpAction = map.FindAction("Jump", true);

        jumpAction.performed += OnJumpPerformed;
        jumpAction.canceled += OnJumpCanceled;
        map.Enable();
    }

    void OnDisable()
    {
        if (map == null)
        {
            return;
        }

        jumpAction.performed -= OnJumpPerformed;
        jumpAction.canceled -= OnJumpCanceled;
        map.Disable();
        JumpHeld = false;
    }

    void OnJumpPerformed(InputAction.CallbackContext context)
    {
        JumpHeld = true;
        JumpPressed?.Invoke();
    }

    void OnJumpCanceled(InputAction.CallbackContext context)
    {
        JumpHeld = false;
    }
}
