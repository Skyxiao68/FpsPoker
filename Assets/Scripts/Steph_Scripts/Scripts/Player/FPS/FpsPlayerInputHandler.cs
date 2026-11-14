using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(FpsCharacterController))]
[DefaultExecutionOrder(-90)]
public class FpsPlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction lookAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction sprintAction;
    [SerializeField] private InputAction reloadAction;
    [SerializeField] private InputAction fireAction;

    private FpsCharacterController controller;
    private InputAction runtimeMoveAction;
    private InputAction runtimeLookAction;
    private InputAction runtimeJumpAction;
    private InputAction runtimeSprintAction;
    private InputAction runtimeReloadAction;
    private InputAction runtimeFireAction;
    private bool hasInputFocus;


    private void Awake()
    {
        ResolveController();
    }

    private void ResolveController()
    {
        if (controller == null)
            controller = GetComponent<FpsCharacterController>();
    }

    private void OnEnable()
    {
        // Fast Enter Play Mode can reuse the scene without another Awake.
        ResolveController();
        hasInputFocus = Application.isFocused;
        RebuildRuntimeActions();
        ClearControllerInput();
        if (hasInputFocus && Time.timeScale > 0f)
            CaptureCursor();
    }

    private void OnDisable()
    {
        ClearControllerInput();
        DisposeRuntimeActions();
    }

    private void OnApplicationFocus(bool focused)
    {
        hasInputFocus = focused;
        ClearControllerInput();
        if (!focused || !isActiveAndEnabled)
            return;

        ResolveController();
        RebuildRuntimeActions();
        if (Time.timeScale > 0f)
            CaptureCursor();
    }

    private void RebuildRuntimeActions()
    {
        DisposeRuntimeActions();
        // Keep inspector bindings as definitions. Fresh runtime actions avoid
        // carrying enabled/device state across play sessions or focus loss.
        runtimeMoveAction = CreateRuntimeAction(moveAction);
        runtimeLookAction = CreateRuntimeAction(lookAction);
        runtimeJumpAction = CreateRuntimeAction(jumpAction);
        runtimeSprintAction = CreateRuntimeAction(sprintAction);
        runtimeReloadAction = CreateRuntimeAction(reloadAction);
        runtimeFireAction = CreateRuntimeAction(fireAction);
    }

    private static InputAction CreateRuntimeAction(InputAction definition)
    {
        if (definition == null)
            return null;
        definition.Disable();
        InputAction action = definition.Clone();
        action.bindingMask = definition.bindingMask;
        action.Enable();
        return action;
    }

    private void DisposeRuntimeActions()
    {
        DisposeAction(ref runtimeMoveAction);
        DisposeAction(ref runtimeLookAction);
        DisposeAction(ref runtimeJumpAction);
        DisposeAction(ref runtimeSprintAction);
        DisposeAction(ref runtimeReloadAction);
        DisposeAction(ref runtimeFireAction);
    }

    private static void DisposeAction(ref InputAction action)
    {
        if (action == null)
            return;
        action.Disable();
        action.Dispose();
        action = null;
    }

    private void ClearControllerInput()
    {
        if (controller == null)
            return;
        controller.SetMoveInput(Vector2.zero);
        controller.SetLookInput(Vector2.zero);
        controller.SetSprint(false);
        controller.SetFireInput(false);
    }

    private static void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ResolveController();
        if (controller == null)
            return;
        if (!hasInputFocus || !controller.isActiveAndEnabled || Time.timeScale <= 0f)
        {
            ClearControllerInput();
            return;
        }

        // Unity releases the cursor when Escape is pressed. Clicking back
        // into the running game restores mouse look without restarting it.
        if (Cursor.lockState != CursorLockMode.Locked &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            CaptureCursor();

        controller.SetMoveInput(runtimeMoveAction != null
            ? runtimeMoveAction.ReadValue<Vector2>() : Vector2.zero);
        controller.SetLookInput(runtimeLookAction != null
            ? runtimeLookAction.ReadValue<Vector2>() : Vector2.zero);
        controller.SetSprint(runtimeSprintAction != null && runtimeSprintAction.IsPressed());
        if (runtimeJumpAction != null && runtimeJumpAction.triggered)
            controller.RequestJump();
        // Existing scene and prefab Reload actions have no binding yet.
        // R works now, while an inspector binding can still override it.
        if ((runtimeReloadAction != null && runtimeReloadAction.triggered) ||
            ((runtimeReloadAction == null || runtimeReloadAction.bindings.Count == 0) &&
             Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
            controller.RequestReload();
        controller.SetFireInput(runtimeFireAction != null && runtimeFireAction.IsPressed());
    }
}
