
using UnityEngine;

[CreateAssetMenu(
    fileName = "FpsMovementSettings",
    menuName = "Character/FPS Movement Settings"
)]
public class FpsMovementSettings : ScriptableObject
{
    [Header("Movement")]

    [Min(0f)]
    public float moveSpeed = 5f;

    [Min(0f)]
    public float sprintMultiplier = 1.8f;

    [Min(0f)]
    public float walkAcceleration = 45f;

    [Min(0f)]
    public float sprintAcceleration = 65f;

    [Min(0f)]
    public float friction = 8f;

    [Min(0f)]
    public float stopSpeed = 1f;


    [Header("Air Movement")]

    [Min(0f)]
    public float airAcceleration = 5f;

    [Min(0f)]
    public float airMaxSpeed = 6f;


    [Header("Speed Limits")]

    [Min(0f)]
    public float maxSpeed = 9f;


    [Header("Jumping & Gravity")]

    [Min(0f)]
    public float jumpHeight = 2f;

    [Min(0f)]
    public float gravity = 15f;


    [Header("Look Settings")]

    [Min(0f)]
    public float lookSensitivity = 0.1f;

    [Min(0f)]
    public float lookXLimit = 80f;


    [Header("Recoil")]

    [Tooltip("How quickly recoil moves toward its target.")]
    [Min(0f)]
    public float recoilSnappiness = 20f;

    [Tooltip("How quickly recoil returns toward zero.")]
    [Min(0f)]
    public float recoilRecovery = 10f;
}


