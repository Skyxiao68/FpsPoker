using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterHitboxes : MonoBehaviour
{
    [SerializeField] private CombatHitbox[] hitboxes;

    public bool HasActiveHitboxes
    {
        get
        {
            if (!isActiveAndEnabled || hitboxes == null) return false;
            foreach (CombatHitbox hitbox in hitboxes)
                if (hitbox != null && hitbox.isActiveAndEnabled && hitbox.GetComponent<Collider>().enabled)
                    return true;
            return false;
        }
    }

    public void Configure(CombatHitbox[] regions)
    {
        hitboxes = regions;
    }
}
