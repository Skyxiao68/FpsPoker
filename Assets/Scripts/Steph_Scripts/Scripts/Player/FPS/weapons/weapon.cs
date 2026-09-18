using System.Collections;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private WeaponSettings weaponSettings;

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform weaponModel;
    [SerializeField] private TrailRenderer bulletTrail;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private FpsCharacterController characterController;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask hitMask = ~0;

    // Fire timing
    private float nextFireTime;

    // Spray pattern
    private int shotIndex;
    private float lastShotTime;

    // Recoil
    private Vector3 recoilTarget;
    private Vector3 visualRecoilTarget;
    private Vector3 currentRecoil;

    private Quaternion originalModelRotation;

    public WeaponSettings Settings => weaponSettings;

    public Transform FirePoint => firePoint;

    public bool CanFire =>
        Time.time >= nextFireTime;

    private void Awake()
    {
        if (characterController == null)
        {
            characterController =
                GetComponentInParent<FpsCharacterController>();
        }

        if (weaponModel != null)
        {
            originalModelRotation =
                weaponModel.localRotation;
        }
    }



    private bool triggerHeld;


    public void SetTriggerHeld(bool held)
    {
        triggerHeld = held;
    }

    public bool IsTriggerHeld => triggerHeld;

    private void Update()
    {
        HandleRecoilRecovery(triggerHeld);

        if (triggerHeld)
        {
            Fire();
        }
    }


    public void Fire()
    {
        if (!CanFire)
            return;

        if (weaponSettings == null)
        {
            Debug.LogWarning(
                $"{name}: No WeaponSettings assigned."
            );

            return;
        }

        if (firePoint == null)
        {
            Debug.LogWarning(
                $"{name}: No Fire Point assigned."
            );

            return;
        }


        if (Time.time - lastShotTime >
            weaponSettings.recoilResetTime)
        {
            shotIndex = 0;
        }

        lastShotTime = Time.time;

        nextFireTime =
            Time.time +
            weaponSettings.fireRate;

        PerformFire();
    }

    private void PerformFire()
    {

        Transform aimTransform =
            characterController != null
                ? characterController.CameraTransform
                : firePoint;

        Vector3 origin =
            aimTransform.position;


        Vector3 direction =
            GetSprayDirection(aimTransform);

        Vector3 hitPoint =
            origin +
            direction *
            weaponSettings.range;


        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            weaponSettings.range,
            hitMask,
            QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;

            HandleHit(hit);
        }


        SpawnMuzzleFlash();



        SpawnBulletTrail(
            firePoint.position,
            hitPoint
        );


        ApplyRecoil();
    }


    private Vector3 GetSprayDirection(Transform aimTransform)
    {
        // recoilTarget.x = accumulated horizontal (yaw) offset
        // recoilTarget.y = accumulated vertical (pitch) offset
        Quaternion offsetRotation =
            Quaternion.AngleAxis(
                recoilTarget.x,
                aimTransform.up
            ) *
            Quaternion.AngleAxis(
                -recoilTarget.y,
                aimTransform.right
            );

        return offsetRotation * aimTransform.forward;
    }


    private void HandleHit(RaycastHit hit)
    {
        IDamagable damagable =
            hit.collider.GetComponentInParent<IDamagable>();

        if (damagable != null)
        {
            damagable.TakeDamage(
                weaponSettings.damage
            );
        }

        SpawnImpactDecal(hit);
    }


    private void ApplyRecoil()
    {
        float vertical =
            GetVerticalRecoil();

        float horizontal =
            GetHorizontalRecoil();

        // Small random variation.
        horizontal += Random.Range(
            -weaponSettings.recoilRandomness,
            weaponSettings.recoilRandomness
        );

        // Add to the recoil target.
        //
        // X = horizontal
        // Y = vertical
        //
        // Z can be used for weapon roll.
        recoilTarget.x += horizontal;
        recoilTarget.y += vertical;

        // actual bullet spray.
        visualRecoilTarget.x +=
            horizontal *
            weaponSettings.visualHorizontalKickMultiplier;

        visualRecoilTarget.y +=
            vertical *
            weaponSettings.visualVerticalKickMultiplier;

        shotIndex++;
    }

    private float GetVerticalRecoil()
    {
        if (weaponSettings.verticalRecoilPattern == null ||
            weaponSettings.verticalRecoilPattern.Length == 0)
        {
            return 0f;
        }

        int index =
            Mathf.Min(
                shotIndex,
                weaponSettings.verticalRecoilPattern.Length - 1
            );

        return weaponSettings.verticalRecoilPattern[index];
    }

    private float GetHorizontalRecoil()
    {
        if (weaponSettings.horizontalRecoilPattern == null ||
            weaponSettings.horizontalRecoilPattern.Length == 0)
        {
            return 0f;
        }

        int index =
            Mathf.Min(
                shotIndex,
                weaponSettings.horizontalRecoilPattern.Length - 1
            );

        return weaponSettings.horizontalRecoilPattern[index];
    }

    private void HandleRecoilRecovery(bool isFiring)
    {

        if (!isFiring)
        {
            recoilTarget.x =
                Mathf.Lerp(
                    recoilTarget.x,
                    0f,
                    weaponSettings.recoilRecovery *
                    Time.deltaTime
                );

            recoilTarget.y =
                Mathf.Lerp(
                    recoilTarget.y,
                    0f,
                    weaponSettings.recoilRecovery *
                    Time.deltaTime
                );
        }

        if (weaponModel == null)
            return;

        currentRecoil =
            Vector3.Lerp(
                currentRecoil,
                visualRecoilTarget,
                weaponSettings.recoilSnappiness *
                Time.deltaTime
            );

        visualRecoilTarget.x =
            Mathf.Lerp(
                visualRecoilTarget.x,
                0f,
                weaponSettings.recoilRecovery *
                weaponSettings.visualHorizontalKickMultiplier *
                Time.deltaTime
            );

        visualRecoilTarget.y =
            Mathf.Lerp(
                visualRecoilTarget.y,
                0f,
                weaponSettings.recoilRecovery *
                weaponSettings.visualVerticalKickMultiplier *
                Time.deltaTime
            );

        Quaternion recoilRotation =
            Quaternion.Euler(
                -currentRecoil.y,
                currentRecoil.x,
                0f
            );

        weaponModel.localRotation =
            originalModelRotation *
            recoilRotation;
    }



    public void ResetRecoil()
    {
        shotIndex = 0;

        recoilTarget = Vector3.zero;
        visualRecoilTarget = Vector3.zero;
        currentRecoil = Vector3.zero;

        if (weaponModel != null)
        {
            weaponModel.localRotation =
                originalModelRotation;
        }
    }


    private void SpawnMuzzleFlash()
    {
        if (muzzleFlash == null)
            return;

        muzzleFlash.Play();
    }

    private void SpawnBulletTrail(
        Vector3 startPosition,
        Vector3 endPosition)
    {
        if (bulletTrail == null)
            return;

        TrailRenderer trail =
            Instantiate(
                bulletTrail,
                startPosition,
                Quaternion.identity
            );

        StartCoroutine(
            MoveTrail(
                trail,
                startPosition,
                endPosition
            )
        );
    }

    private IEnumerator MoveTrail(
        TrailRenderer trail,
        Vector3 startPosition,
        Vector3 endPosition)
    {
        float distance =
            Vector3.Distance(
                startPosition,
                endPosition
            );

        float duration =
            distance /
            weaponSettings.trailSpeed;

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    time / duration
                );

            trail.transform.position =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    t
                );

            yield return null;
        }

        trail.transform.position =
            endPosition;

        yield return new WaitForSeconds(
            trail.time
        );

        Destroy(
            trail.gameObject
        );
    }


    private void SpawnImpactDecal(
        RaycastHit hit)
    {
        if (weaponSettings.impactDecal == null)
            return;

        Quaternion rotation =
            Quaternion.LookRotation(
                hit.normal
            );

        GameObject decal =
            Instantiate(
                weaponSettings.impactDecal,
                hit.point +
                hit.normal * 0.001f,
                rotation
            );

        Destroy(
            decal,
            weaponSettings.decalLifetime
        );
    }
}