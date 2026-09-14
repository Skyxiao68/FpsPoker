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

    // Original model rotation
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

    private void Update()
    {
        bool isFiring =
            Input.GetButton("Fire1");

        HandleRecoilRecovery(isFiring);

        if (isFiring)
        {
            Fire();
        }
    }

    // =========================================================
    // FIRE
    // =========================================================

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

        // Restart the spray pattern from the beginning once
        // enough time has passed without firing. This only resets
        // which pattern index the next shot uses - the actual
        // recoilTarget/visualRecoilTarget offsets are NOT snapped
        // here, they decay gradually on their own in
        // HandleRecoilRecovery whenever the trigger isn't held.
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
        // Aim from the camera position/orientation, then apply
        // the spray offset on top of it (see GetSprayDirection).
        // We don't aim from firePoint.forward because it only
        // follows the player's yaw, not the camera's pitch.
        Transform aimTransform =
            characterController != null
                ? characterController.CameraTransform
                : firePoint;

        Vector3 origin =
            aimTransform.position;

        // The spray pattern offsets where the shot goes, not
        // where the camera is pointed. recoilTarget accumulates
        // the pattern values (see ApplyRecoil) and decays on its
        // own in HandleRecoilRecovery — it never touches any
        // transform's actual rotation.
        Vector3 direction =
            GetSprayDirection(aimTransform);

        Vector3 hitPoint =
            origin +
            direction *
            weaponSettings.range;

        // =====================================================
        // HITSCAN
        // =====================================================

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

        // =====================================================
        // MUZZLE FLASH
        // =====================================================

        SpawnMuzzleFlash();

        // =====================================================
        // BULLET TRAIL
        // =====================================================
        // Tracer still starts at the visible muzzle so it doesn't
        // appear to come out of the camera, it just ends wherever
        // the camera-based trace (including recoil) actually hit.

        SpawnBulletTrail(
            firePoint.position,
            hitPoint
        );

        // =====================================================
        // RECOIL
        // =====================================================

        ApplyRecoil();
    }

    // =========================================================
    // SPRAY OFFSET
    // =========================================================

    /// <summary>
    /// Rotates the aim transform's forward vector by the current
    /// accumulated spray offset. This never modifies any actual
    /// transform - it's a pure direction calculation used only
    /// for this shot's raycast, so it has zero effect on where
    /// the camera or player is actually facing.
    /// </summary>
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

    // =========================================================
    // HIT
    // =========================================================

    private void HandleHit(RaycastHit hit)
    {
        Debug.Log(
            $"Hit: {hit.collider.name}"
        );

        /*
        // Add your damage system here.

        IDamageable damageable =
            hit.collider.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(
                weaponSettings.damage
            );
        }
        */

        SpawnImpactDecal(hit);
    }

    // =========================================================
    // RECOIL
    // =========================================================

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

        // Same impulse drives the visual kick, but unlike
        // recoilTarget this one is allowed to decay every frame
        // (see HandleRecoilRecovery) so the model actually settles
        // back down between shots instead of holding the kicked
        // pose for the whole burst. It's also scaled independently
        // so the kick can look bigger/smaller without changing the
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

    // =========================================================
    // RECOIL RECOVERY
    // =========================================================

    private void HandleRecoilRecovery(bool isFiring)
    {
        // The aim/spray offset gradually recovers as soon as the
        // trigger isn't held - no more waiting for recoilResetTime
        // then snapping straight to zero. While the trigger IS
        // held, it stays put so the pattern keeps applying to
        // each shot.
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

        // Smoothly move the cosmetic weapon-model kick toward
        // its own visual target.
        currentRecoil =
            Vector3.Lerp(
                currentRecoil,
                visualRecoilTarget,
                weaponSettings.recoilSnappiness *
                Time.deltaTime
            );

        // The visual target always decays back to neutral, at
        // recoilRecovery speed - scaled by the same multiplier
        // used to size the kick, so a bigger kick doesn't take
        // longer to settle than a normal one. This one decays
        // regardless of isFiring, since it's purely cosmetic and
        // should always be springing back toward center between
        // individual shots.
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

        // Apply recoil relative to the original rotation.
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

    // =========================================================
    // RESET
    // =========================================================

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

    // =========================================================
    // MUZZLE FLASH
    // =========================================================

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlash == null)
            return;

        // Stop Action is set to None on the particle system, so
        // Play() just restarts the burst cleanly even if it's
        // still mid-flash from the previous shot during rapid
        // automatic fire.
        muzzleFlash.Play();
    }

    // =========================================================
    // BULLET TRAIL
    // =========================================================

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

    // =========================================================
    // DECAL
    // =========================================================

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