using UnityEngine;
using UnityEngine.InputSystem;

public class FpsPlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction lookAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction sprintAction;

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
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
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
    }
}
