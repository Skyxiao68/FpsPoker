using UnityEngine;

public enum CombatStyle
{
    Rusher,
    RangedRoaming,
    DodgeCounter
}

[CreateAssetMenu(
    fileName = "FPSPersonality",
    menuName = "AI/FPS Personality"
)]
public class FPSPersonality : ScriptableObject
{
    [Header("Identity")]
    public string personalityName = "RedHat";

    [Tooltip("Broad archetype. Seeds sensible defaults for the " +
             "engagement values below.")]
    public CombatStyle combatStyle = CombatStyle.RangedRoaming;


    [Header("Perception")]

    [Tooltip("How far this AI can see a target.")]
    [Min(0f)]
    public float sightRange = 40f;

    [Tooltip("Total field of view cone, in degrees.")]
    [Range(10f, 360f)]
    public float fieldOfView = 110f;

    [Tooltip("Seconds the AI keeps chasing a target's last known " +
             "position after losing sight of it.")]
    [Min(0f)]
    public float memoryDuration = 4f;


    // =========================================================
    // AIM
    // =========================================================
    // This block is the difficulty dial. Everything here feeds
    // SetLookInput, so the AI is bound by the player's sensitivity
    // and pitch limits no matter how these are tuned.

    [Header("Aim")]

    [Tooltip("Max degrees per second the AI can swing its view. " +
             "Low values make it flickable and beatable.")]
    [Min(1f)]
    public float aimTurnSpeed = 220f;

    [Tooltip("Degrees of wobble constantly applied to the aim " +
             "point. Higher = sloppier.")]
    [Min(0f)]
    public float aimError = 2.5f;

    [Tooltip("How fast the aim wobble drifts.")]
    [Min(0f)]
    public float aimErrorFrequency = 1.5f;

    [Tooltip("Seconds between first seeing a target and starting " +
             "to track it.")]
    [Min(0f)]
    public float reactionTime = 0.28f;

    [Tooltip("0 = ignores target movement, 1 = perfectly leads a " +
             "moving target.")]
    [Range(0f, 1f)]
    public float targetLeadAccuracy = 0.4f;



    [Header("Engagement")]

    [Tooltip("Distance the AI tries to hold from its target.")]
    [Min(0f)]
    public float preferredRange = 18f;

    [Tooltip("Backs off if the target gets closer than this.")]
    [Min(0f)]
    public float minRange = 6f;

    [Tooltip("Degrees of aim error it tolerates before pulling " +
             "the trigger.")]
    [Min(0f)]
    public float fireAngleTolerance = 6f;

    [Tooltip("Seconds it holds the trigger per burst.")]
    [Min(0.02f)]
    public float burstDuration = 0.35f;

    [Tooltip("Seconds it waits between bursts. Higher = better " +
             "trigger discipline and less spray drift.")]
    [Min(0f)]
    public float burstCooldown = 0.45f;


    [Header("Movement")]

    [Tooltip("How far it strafes side to side while engaging. " +
             "0 = stands still and is easy to hit.")]
    [Range(0f, 1f)]
    public float strafeAmplitude = 0.7f;

    [Tooltip("How often it changes strafe direction.")]
    [Min(0f)]
    public float strafeFrequency = 0.8f;

    [Tooltip("Chance it sprints when repositioning.")]
    [Range(0f, 1f)]
    public float sprintWillingness = 0.5f;

    [Tooltip("Chance per second of jumping while engaging.")]
    [Range(0f, 3f)]
    public float jumpiness = 0.2f;

    [Tooltip("Seconds between path recalculations.")]
    [Min(0.05f)]
    public float repathInterval = 0.35f;


    [Header("Nerve")]

    [Tooltip("Health fraction below which it tries to break off " +
             "and evade. 0 = never retreats.")]
    [Range(0f, 1f)]
    public float evadeHealthThreshold = 0.3f;

    [Tooltip("Seconds it stays in evade before re-engaging.")]
    [Min(0f)]
    public float evadeDuration = 2.5f;

    [Tooltip("Pushes preferred range down and closes distance " +
             "faster. 1 = maximally aggressive.")]
    [Range(0f, 1f)]
    public float aggression = 0.5f;
}