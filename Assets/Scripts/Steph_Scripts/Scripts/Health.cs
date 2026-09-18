using System;
using UnityEngine;

[RequireComponent(typeof(FpsCharacterController))]
public class Health : MonoBehaviour, IDamagable
{
    [Header("Settings")]
    [Min(1)]
    [SerializeField] private int maxHealth = 100;

    [Header("On Death")]
    [Tooltip("Disabled the instant this dies. For the player, " +
             "that's the input script; for an enemy, FPSAIEngine.")]
    [SerializeField] private Behaviour[] disableOnDeath;

    private FpsCharacterController controller;

    public int MaxHealth => maxHealth;

    public int CurrentHealth { get; private set; }

    public float NormalizedHealth =>
        maxHealth > 0
            ? (float)CurrentHealth / maxHealth
            : 0f;

    public bool IsDead { get; private set; }

    /// <summary>(amountTaken, currentHealthAfter)</summary>
    public event Action<int, int> OnDamaged;

    public event Action<Health> OnDeath;

    private void Awake()
    {
        controller = GetComponent<FpsCharacterController>();
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        CurrentHealth =
            Mathf.Max(CurrentHealth - amount, 0);

        OnDamaged?.Invoke(amount, CurrentHealth);

        print(CurrentHealth+" health remaining");

        if (CurrentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        CurrentHealth =
            Mathf.Min(CurrentHealth + amount, maxHealth);
    }


    public void ResetHealth()
    {
        IsDead = false;
        CurrentHealth = maxHealth;

        SetDisabledScripts(false);
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        if (controller != null)
        {
            controller.SetMoveInput(Vector2.zero);
            controller.SetLookInput(Vector2.zero);
            controller.SetSprint(false);
            controller.SetFireInput(false);
        }

        SetDisabledScripts(true);

        OnDeath?.Invoke(this);
    }

    private void SetDisabledScripts(bool disabled)
    {
        if (disableOnDeath == null)
            return;

        foreach (Behaviour behaviour in disableOnDeath)
        {
            if (behaviour != null)
                behaviour.enabled = !disabled;
        }
    }

}