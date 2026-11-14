using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class CombatHitbox : MonoBehaviour
{
    [SerializeField] private Health owner;
    [SerializeField] private bool head;

    public Health Owner => owner;
    public bool IsHeadshot => head;

    public void Configure(Health target, bool isHead)
    {
        owner = target;
        head = isHead;
        GetComponent<Collider>().isTrigger = true;
    }
}
