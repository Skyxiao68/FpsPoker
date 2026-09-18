using TMPro;
using UnityEngine;

public class FPS_HUD : MonoBehaviour
{
    public Health health;
    public TextMeshProUGUI healthText;
    void OnEnable()
    {
        health.OnDamaged += UpdateHealth;
    }
    void UpdateHealth(int amount, int currentHealth)
    {
        healthText.text = currentHealth.ToString();
    }
}
