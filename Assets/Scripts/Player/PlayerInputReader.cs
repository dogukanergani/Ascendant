using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInputReader : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] string actionMapName = "Player";

    InputActionMap map;
    InputAction moveAction;
    InputAction attackAction;

    public event System.Action AttackPressed;

    public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

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
        attackAction = map.FindAction("Attack", true);
        attackAction.performed += OnAttackPerformed;
        map.Enable();
    }

    void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.performed -= OnAttackPerformed;
        }

        map?.Disable();
    }

    void OnAttackPerformed(InputAction.CallbackContext context)
    {
        AttackPressed?.Invoke();
    }
}
