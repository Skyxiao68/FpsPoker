using UnityEngine;

/// <summary>Controls the barrier and the short preparation phase of an FPS duel.</summary>
public class CombatStartSequence : MonoBehaviour
{
    [Min(0f)] [SerializeField] private float preparationSeconds = 3f;

    public static CombatStartSequence Active { get; private set; }
    public bool IsPreparing { get; private set; }
    public float RemainingSeconds => IsPreparing
        ? Mathf.Max(0f, releaseTime - Time.time) : 0f;

    private float releaseTime;
    private bool begun;
    private FpsCharacterController player;
    private FpsCharacterController enemy;
    private FPSAIEngine enemyAI;

    private void OnEnable()
    {
        Active = this;
        begun = false;
        IsPreparing = false;
        player = null;
        enemy = null;
        enemyAI = null;
    }

    private void Start()
    {
        // The barrier also works when this scene is played without a
        // CombatSceneController. Begin can attach fighters later that frame.
        if (!begun)
            Begin(null, null);
    }

    // CombatSceneController supplies the newly spawned enemy before its first Update.
    public void Begin(FpsCharacterController playerController, GameObject enemyObject)
    {
        if (!begun)
        {
            begun = true;
            releaseTime = Time.time + Mathf.Max(0f, preparationSeconds);
            IsPreparing = true;
        }

        if (!IsPreparing)
            return;

        if (player == null && playerController != null)
        {
            player = playerController;
            SetWeaponLock(player, true);
            if (enemyAI != null)
                enemyAI.SetPreDuel(true, player.transform);
        }

        if (enemy == null && enemyObject != null)
        {
            enemy = enemyObject.GetComponentInChildren<FpsCharacterController>();
            enemyAI = enemyObject.GetComponentInChildren<FPSAIEngine>();
            SetWeaponLock(enemy, true);
            if (enemyAI != null)
                enemyAI.SetPreDuel(true, player != null ? player.transform : null);
        }

        if (preparationSeconds <= 0f)
            Finish();
    }

    private void Update()
    {
        if (IsPreparing && Time.time >= releaseTime)
            Finish();
    }

    private void Finish()
    {
        if (!IsPreparing)
            return;

        IsPreparing = false;
        if (enemyAI != null)
            enemyAI.SetPreDuel(false, null);
        SetWeaponLock(player, false);
        SetWeaponLock(enemy, false);
        gameObject.SetActive(false);
    }

    public bool ContainsCollider(Collider candidate)
    {
        return candidate != null && candidate.transform.IsChildOf(transform);
    }

    private static void SetWeaponLock(FpsCharacterController controller, bool locked)
    {
        if (controller == null || controller.EquippedWeapon == null)
            return;

        controller.SetFireInput(false);
        controller.EquippedWeapon.SetCombatLocked(locked);
    }

    private void OnDisable()
    {
        // Also release the fighters if the barrier is removed externally.
        if (IsPreparing)
        {
            IsPreparing = false;
            if (enemyAI != null)
                enemyAI.SetPreDuel(false, null);
            SetWeaponLock(player, false);
            SetWeaponLock(enemy, false);
        }

        if (Active == this)
            Active = null;
    }
}
