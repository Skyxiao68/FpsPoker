using UnityEngine;
using UnityEngine.InputSystem;

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

    private void Awake()
    {
        controller =GetComponent<FpsCharacterController>();
        Cursor.lockState = CursorLockMode.Locked;    
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        fireAction.Enable();
        reloadAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        fireAction.Disable();
        reloadAction.Disable();

        // If this component gets disabled mid-burst (weapon swap,
        // death, pause), make sure the trigger doesn't stay latched
        // held with nothing left to release it.
        if (controller != null)
            controller.SetFireInput(false);
    }

    private void Update()
    {
        controller.SetMoveInput(
            moveAction.ReadValue<Vector2>()
        );

        controller.SetLookInput(
            lookAction.ReadValue<Vector2>()
        );

        controller.SetSprint(
            sprintAction.IsPressed()
        );

        if (jumpAction.triggered)
        {
            controller.RequestJump();
        }

        if (reloadAction.triggered)
        {
            //
        }

        // when the button does.
        controller.SetFireInput(
            fireAction.IsPressed()
        );
    }
}