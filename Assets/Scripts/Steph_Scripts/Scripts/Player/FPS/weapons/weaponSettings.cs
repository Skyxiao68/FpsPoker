using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponSettings",
    menuName = "Weapons/Weapon Settings"
)]
public class WeaponSettings : ScriptableObject
{
    [Header("Identity")]
    public string weaponName;


    [Header("Damage")]
    [Min(0)]
    public int damage = 10;

    [Min(0f)]
    public float range = 1000f;


    [Header("Fire Rate")]
    [Min(0f)]
    public float fireRate = 0.1f;
    [Header("fireMode")]
    public bool isAutomatic ;

    [Header("Ammo")]
    [Min(1)]
    public int magazineSize = 30;

    [Min(0f)]
    public float reloadTime = 2f;


    [Header("Bullet Trail")]
    [Min(1f)]
    public float trailSpeed = 300f;


    [Header("Impact")]
    public GameObject impactDecal;

    [Min(0f)]
    public float decalLifetime = 10f;


    [Header("Recoil")]

    [Tooltip("How quickly the weapon moves toward the recoil position.")]
    [Min(0f)]
    public float recoilSnappiness = 25f;

    [Tooltip("How quickly recoil returns to zero.")]
    [Min(0f)]
    public float recoilRecovery = 8f;

    [Tooltip("Small random horizontal variation.")]
    [Min(0f)]
    public float recoilRandomness = 0.05f;

    [Tooltip("Time without firing before the spray pattern resets.")]
    [Min(0f)]
    public float recoilResetTime = 0.25f;

    [Header("Visual Kick")]

    [Tooltip("Scales how big the gun model's vertical kick looks, without affecting the actual bullet spray/aim offset.")]
    [Min(0f)]
    public float visualVerticalKickMultiplier = 1f;

    [Tooltip("Scales how big the gun model's horizontal kick looks, without affecting the actual bullet spray/aim offset.")]
    [Min(0f)]
    public float visualHorizontalKickMultiplier = 1f;


    [Header("Spray Pattern")]

    [Tooltip("Vertical recoil per shot.")]
    public float[] verticalRecoilPattern;

    [Tooltip("Horizontal recoil per shot.")]
    public float[] horizontalRecoilPattern;


    [Header("Movement")]

    [Range(0f, 1f)]
    public float movementMultiplier = 1f;
}