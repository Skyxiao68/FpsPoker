using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Tooltip("调试用：进入游戏时自动持有这些物品（每个 1 份）")]
    [SerializeField]
    private List<ItemId> startingItems = new List<ItemId>();

    // ★ 改成计数
    private readonly Dictionary<ItemId, int> itemCounts = new Dictionary<ItemId, int>();

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
        {
            if (id == ItemId.None)
                continue;
            if (!itemCounts.ContainsKey(id))
                itemCounts[id] = 0;
            itemCounts[id]++;
        }

        Debug.Log($"[Inventory] 初始物品: {Describe()}");
    }

    /// <summary>是否有至少 1 个</summary>
    public bool Has(ItemId id)
    {
        return id != ItemId.None && itemCounts.TryGetValue(id, out var n) && n > 0;
    }

    /// <summary>数量</summary>
    public int GetCount(ItemId id)
    {
        if (id == ItemId.None)
            return 0;
        return itemCounts.TryGetValue(id, out var n) ? n : 0;
    }

    /// <summary>加 1 个</summary>
    public void Add(ItemId id)
    {
        if (id == ItemId.None)
            return;
        if (!itemCounts.ContainsKey(id))
            itemCounts[id] = 0;
        itemCounts[id]++;
        Debug.Log($"[Inventory] 获得 {id}（共 {itemCounts[id]}）");
    }

    /// <summary>移除 1 个</summary>
    public void Remove(ItemId id)
    {
        if (id == ItemId.None)
            return;
        if (!itemCounts.ContainsKey(id))
            return;
        itemCounts[id]--;
        if (itemCounts[id] <= 0)
            itemCounts.Remove(id);
        Debug.Log($"[Inventory] 移除 {id}（剩 {GetCount(id)}）");
    }

    public IReadOnlyDictionary<ItemId, int> All => itemCounts;

    private string Describe()
    {
        if (itemCounts.Count == 0)
            return "(空)";
        var parts = new List<string>();
        foreach (var kv in itemCounts)
            parts.Add($"{kv.Key}x{kv.Value}");
        return string.Join(", ", parts);
    }
}
