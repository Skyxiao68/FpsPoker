using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家当前持有的物品。跨场景保留。
/// 单例，由场景里一个常驻 GameObject 承载。
/// </summary>
[DefaultExecutionOrder(-200)]   // 比 Bridge（-100）更早，确保物品在筹码恢复前就绪
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Tooltip("调试用：进入游戏时自动持有这些物品")]
    [SerializeField]
    private List<ItemId> startingItems = new List<ItemId>();

    private readonly HashSet<ItemId> items = new HashSet<ItemId>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var id in startingItems)
            items.Add(id);

        Debug.Log($"[Inventory] 初始物品: {Describe()}");
    }

    public bool Has(ItemId id)
    {
        if (id == ItemId.None) return false;
        return items.Contains(id);
    }

    public void Add(ItemId id)
    {
        if (id == ItemId.None) return;
        if (items.Add(id))
            Debug.Log($"[Inventory] 获得: {id}");
    }

    public void Remove(ItemId id)
    {
        if (items.Remove(id))
            Debug.Log($"[Inventory] 移除: {id}");
    }

    public IReadOnlyCollection<ItemId> All => items;

    private string Describe()
    {
        if (items.Count == 0) return "(空)";
        return string.Join(", ", items);
    }
}