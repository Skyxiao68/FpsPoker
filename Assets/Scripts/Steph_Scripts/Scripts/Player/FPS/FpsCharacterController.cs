using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FpsCharacterController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject cameraParent;

    [Tooltip("The weapon this character is holding. Both the " +
             "player and the AI fire through this controller, so " +
             "neither one needs to touch the Weapon directly.")]
    [SerializeField] private Weapon weapon;

    [Header("Movement Settings")]
    [SerializeField] private FpsMovementSettings movementSettings;

    private CharacterController characterController;

    private Vector2 moveInput;
    private Vector2 lookInput;

    private bool sprintRequested;
    private bool jumpRequested;

    // Vertical camera rotation.
    private float rotationX;

    private Vector3 velocity;

    public float currentSpeed;

    public Vector3 Velocity => velocity;

    public float HorizontalSpeed =>
        new Vector3(
            velocity.x,
            0f,
            velocity.z
        ).magnitude;

    public bool IsGrounded =>
        characterController.isGrounded;

    /// <summary>
    /// The camera's transform, including any applied recoil pitch.
    /// Use this (not a weapon's own firePoint) as the source of
    /// truth for where a hitscan shot is actually aimed.
    /// </summary>
    public Transform CameraTransform =>
        cameraParent != null
            ? cameraParent.transform
            : transform;

    /// <summary>
    /// How many degrees of rotation one unit of look input produces
    /// this frame. An AI uses this to convert a desired turn in
    /// degrees into the same look input a mouse would generate, so
    /// it's bound by the same sensitivity the player is.
    /// </summary>
    public float LookDegreesPerInputUnit =>
        movementSettings != null
            ? movementSettings.lookSensitivity * 0.1f
            : 0.1f;

    public float LookPitchLimit =>
        movementSettings != null
            ? movementSettings.lookXLimit
            : 80f;

    public bool IsMoving =>
        HorizontalSpeed > 0.01f;

    public bool IsSprinting =>
        sprintRequested &&
        moveInput.y > 0f;


    // =========================================================
    // RECOIL
    // =========================================================

    private Vector2 recoilTarget;
    private Vector2 currentRecoil;

    /// <summary>
    /// Adds weapon recoil.
    /// Vertical = upward camera kick.
    /// Horizontal = sideways camera kick.
    /// </summary>
    public void AddRecoil(
        float vertical,
        float horizontal)
    {
        recoilTarget.y += vertical;
        recoilTarget.x += horizontal;
    }

    public void ResetRecoil()
    {
        recoilTarget = Vector2.zero;
        currentRecoil = Vector2.zero;
    }


    // =========================================================
    // MOVEMENT API
    // =========================================================

    public void SetMoveInput(Vector2 input)
    {
        moveInput =
            Vector2.ClampMagnitude(
                input,
                1f
            );
    }

    public void SetMoveDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude > 1f)
            worldDirection.Normalize();

        Vector3 localDirection =
            transform.InverseTransformDirection(
                worldDirection
            );

        moveInput =
            new Vector2(
                localDirection.x,
                localDirection.z
            );

        moveInput =
            Vector2.ClampMagnitude(
                moveInput,
                1f
            );
    }

    public void SetSprint(bool sprint)
    {
        sprintRequested = sprint;
    }

    public void RequestJump()
    {
        jumpRequested = true;
    }

    public void SetLookInput(Vector2 input)
    {
        lookInput = input;
    }


    // =========================================================
    // WEAPON API
    // =========================================================

    private bool fireRequested;

    public Weapon EquippedWeapon => weapon;

    public FpsMovementSettings MovementSettings => movementSettings;

    /// <summary>
    /// Swaps in a new settings asset at runtime. Same reasoning as
    /// Weapon.SetSettings - pass a cloned copy, never the shared
    /// asset itself, once you've modified any field on it.
    /// </summary>
    public void SetMovementSettings(FpsMovementSettings settings)
    {
        movementSettings = settings;
    }

    public bool HasWeapon => weapon != null;

    public bool IsFiring => fireRequested;

    /// <summary>
    /// Holds or releases the trigger. This is the only way either
    /// the player or the AI is allowed to shoot - both go through
    /// here, so fire rate, spray pattern and recoil behave
    /// identically for both.
    /// </summary>
    public void SetFireInput(bool held)
    {
        fireRequested = held;
    }

    /// <summary>
    /// Swaps the held weapon at runtime. Releases the old trigger
    /// so a burst can't leak across the swap.
    /// </summary>
    public void EquipWeapon(Weapon newWeapon)
    {
        if (weapon != null)
            weapon.SetTriggerHeld(false);

        weapon = newWeapon;
        fireRequested = false;
    }

    private void HandleWeapon()
    {
        if (weapon == null)
            return;

        weapon.SetTriggerHeld(fireRequested);
    }

    public void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation =
            Quaternion.LookRotation(
                direction.normalized
            );
    }

    public void FacePosition(Vector3 position)
    {
        Vector3 direction =
            position -
            transform.position;

        FaceDirection(direction);
    }

    public void Stop()
    {
        velocity.x = 0f;
        velocity.z = 0f;

        moveInput = Vector2.zero;
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();
    }

    private void Update()
    {
        ApplyFriction();

        HandleMovement();

        HandleJump();

        HandleLook();

        HandleRecoil();

        HandleWeapon();

        characterController.Move(
            velocity *
            Time.deltaTime
        );

        currentSpeed =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            ).magnitude;

        jumpRequested = false;
    }


    // =========================================================
    // FRICTION
    // =========================================================

    private void ApplyFriction()
    {
        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        float speed =
            horizontalVelocity.magnitude;

        if (speed < 0.01f)
            return;

        if (!characterController.isGrounded)
            return;

        float control =
            Mathf.Max(
                speed,
                movementSettings.stopSpeed
            );

        float drop =
            control *
            movementSettings.friction *
            Time.deltaTime;

        float newSpeed =
            Mathf.Max(
                speed - drop,
                0f
            );

        newSpeed /= speed;

        velocity.x *= newSpeed;
        velocity.z *= newSpeed;
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        Vector3 wishDirection =
            transform.forward *
            moveInput.y +
            transform.right *
            moveInput.x;

        if (wishDirection.sqrMagnitude > 1f)
            wishDirection.Normalize();

        if (wishDirection.sqrMagnitude < 0.001f)
            return;

        bool grounded =
            characterController.isGrounded;

        bool sprinting =
            sprintRequested &&
            moveInput.y > 0f;

        float acceleration;

        if (grounded)
        {
            acceleration =
                sprinting
                    ? movementSettings.sprintAcceleration
                    : movementSettings.walkAcceleration;
        }
        else
        {
            acceleration =
                movementSettings.airAcceleration;
        }

        float movementMaxSpeed =
            sprinting
                ? movementSettings.moveSpeed *
                  movementSettings.sprintMultiplier
                : movementSettings.moveSpeed;

        float groundMaxSpeed =
            Mathf.Min(
                movementMaxSpeed,
                movementSettings.maxSpeed
            );

        float allowedSpeed =
            grounded
                ? groundMaxSpeed
                : movementSettings.airMaxSpeed;

        Vector3 horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        float currentWishSpeed =
            Vector3.Dot(
                horizontalVelocity,
                wishDirection
            );

        float speedOffset =
            allowedSpeed -
            currentWishSpeed;

        if (speedOffset > 0f)
        {
            float speedToAdd =
                acceleration *
                Time.deltaTime;

            speedToAdd =
                Mathf.Min(
                    speedToAdd,
                    speedOffset
                );

            velocity.x +=
                speedToAdd *
                wishDirection.x;

            velocity.z +=
                speedToAdd *
                wishDirection.z;
        }

        horizontalVelocity =
            new Vector3(
                velocity.x,
                0f,
                velocity.z
            );

        float horizontalSpeed =
            horizontalVelocity.magnitude;

        if (horizontalSpeed > allowedSpeed)
        {
            horizontalVelocity =
                horizontalVelocity.normalized *
                allowedSpeed;

            velocity.x =
                horizontalVelocity.x;

            velocity.z =
                horizontalVelocity.z;
        }
    }


    // =========================================================
    // JUMP
    // =========================================================

    private void HandleJump()
    {
        if (characterController.isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (jumpRequested)
            {
                velocity.y =
                    Mathf.Sqrt(
                        movementSettings.jumpHeight *
                        2f *
                        movementSettings.gravity
                    );
            }
        }
        else
        {
            velocity.y -=
                movementSettings.gravity *
                Time.deltaTime;
        }
    }


    // =========================================================
    // LOOK
    // =========================================================

    private void HandleLook()
    {
        // Mouse vertical movement.
        rotationX -=
            lookInput.y *
            movementSettings.lookSensitivity *
            0.1f;

        // Hard vertical look limit.
        rotationX =
            Mathf.Clamp(
                rotationX,
                -movementSettings.lookXLimit,
                movementSettings.lookXLimit
            );

        // Horizontal look.
        transform.Rotate(
            Vector3.up *
            lookInput.x *
            movementSettings.lookSensitivity *
            0.1f
        );

        ApplyCameraRotation();
    }


    // =========================================================
    // RECOIL
    // =========================================================

    private void HandleRecoil()
    {
        // Smoothly move current recoil toward
        // the recoil target.
        currentRecoil =
            Vector2.Lerp(
                currentRecoil,
                recoilTarget,
                movementSettings.recoilSnappiness *
                Time.deltaTime
            );

        // Gradually return recoil target to zero.
        recoilTarget =
            Vector2.Lerp(
                recoilTarget,
                Vector2.zero,
                movementSettings.recoilRecovery *
                Time.deltaTime
            );

        // -----------------------------------------------------
        // HORIZONTAL RECOIL
        // -----------------------------------------------------

        if (Mathf.Abs(currentRecoil.x) > 0.001f)
        {
            transform.Rotate(
                Vector3.up *
                currentRecoil.x
            );
        }

        // -----------------------------------------------------
        // VERTICAL RECOIL
        // -----------------------------------------------------

        float recoilPitch =
            currentRecoil.y;

        // Calculate what the camera pitch would be
        // after applying recoil.
        float targetPitch =
            rotationX -
            recoilPitch;

        // Prevent recoil from going beyond
        // the vertical look limit.
        targetPitch =
            Mathf.Clamp(
                targetPitch,
                -movementSettings.lookXLimit,
                movementSettings.lookXLimit
            );


        float allowedRecoil =
            rotationX -
            targetPitch;

        // Apply only the allowed amount.
        float recoilDifference =
            recoilPitch -
            allowedRecoil;

        rotationX -=
            recoilDifference;

        // Clamp as an additional safety measure.
        rotationX =
            Mathf.Clamp(
                rotationX,
                -movementSettings.lookXLimit,
                movementSettings.lookXLimit
            );

        ApplyCameraRotation();
    }



    private void ApplyCameraRotation()
    {
        if (cameraParent == null)
            return;

        cameraParent.transform.localRotation =
            Quaternion.Euler(
                rotationX,
                0f,
                0f
            );
    }
}