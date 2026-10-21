using System.IO;
using PokerShop;
using UnityEditor;
using UnityEngine;

public static class CreateTestBuffs
{
    const string Dir = "Assets/ShopData/TestBuffs";

    class Buff
    {
        public string id,
            name,
            description,
            category,
            suit,
            tier;
        public int price,
            maxOwned,
            amount;
        public EnhancementType type;

        public Buff(
            string id,
            string name,
            string description,
            string category,
            EnhancementType type,
            int amount,
            string suit,
            int price,
            int maxOwned,
            string tier = "bronze"
        )
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.category = category;
            this.type = type;
            this.amount = amount;
            this.suit = suit;
            this.price = price;
            this.maxOwned = maxOwned;
            this.tier = tier;
        }
    }

    static readonly Buff[] Buffs =
    {
        // ---- Bronze ----
        new Buff(
            "card_swap",
            "Card Swap",
            "Swap one card from your hand.",
            "Cards",
            EnhancementType.CardSwap,
            1,
            "",
            50,
            2,
            "bronze"
        ),
        new Buff(
            "cheat_glove",
            "Cheat Glove",
            "See one of the enemy's cards.",
            "Cards",
            EnhancementType.CardSwap,
            1,
            "",
            60,
            1,
            "bronze"
        ),
        new Buff(
            "boost_hearts",
            "Hearts Boost",
            "Hearts are more likely to be dealt.",
            "Suits",
            EnhancementType.SuitBoost,
            1,
            "hearts",
            70,
            1,
            "bronze"
        ),
        new Buff(
            "boost_spades",
            "Spades Boost",
            "Spades are more likely to be dealt.",
            "Suits",
            EnhancementType.SuitBoost,
            1,
            "spades",
            70,
            1,
            "bronze"
        ),
        new Buff(
            "boost_diamonds",
            "Diamonds Boost",
            "Diamonds are more likely to be dealt.",
            "Suits",
            EnhancementType.SuitBoost,
            1,
            "diamonds",
            70,
            1,
            "bronze"
        ),
        new Buff(
            "boost_clubs",
            "Clubs Boost",
            "Clubs are more likely to be dealt.",
            "Suits",
            EnhancementType.SuitBoost,
            1,
            "clubs",
            70,
            1,
            "bronze"
        ),
        // ---- Silver ----
        new Buff(
            "extra_card",
            "Extra Starting Card",
            "Start the round with one extra card.",
            "Cards",
            EnhancementType.ExtraStartingCard,
            1,
            "",
            100,
            1,
            "silver"
        ),
        new Buff(
            "double_swap",
            "Double Swap",
            "Swap up to two cards from your hand.",
            "Cards",
            EnhancementType.CardSwap,
            2,
            "",
            110,
            1,
            "silver"
        ),
        new Buff(
            "insurance_25",
            "Basic Insurance",
            "Reduce losses from a defeat by 25%.",
            "Protection",
            EnhancementType.Insurance,
            25,
            "",
            90,
            1,
            "silver"
        ),
        // ---- Gold ----
        new Buff(
            "queen_of_hearts",
            "Queen of Hearts",
            "Hearts now grant Attack instead of HP.",
            "Suits",
            EnhancementType.SuitBoost,
            1,
            "hearts",
            200,
            1,
            "gold"
        ),
        new Buff(
            "insurance_50",
            "Full Insurance",
            "Reduce losses from a defeat by 50%.",
            "Protection",
            EnhancementType.Insurance,
            50,
            "",
            220,
            1,
            "gold"
        ),
    };

    static T GetOrCreate<T>(string path)
        where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    [MenuItem("PokerShopDemo/Create Test Buffs")]
    public static void Create()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        var catalog = GetOrCreate<ShopCatalog>(Dir + "/TestCatalog.asset");
        catalog.items.Clear();

        foreach (var b in Buffs)
        {
            var def = GetOrCreate<EnhancementDef>($"{Dir}/{b.id}_def.asset");
            def.type = b.type;
            def.amount = b.amount;
            def.suit = b.suit;
            
            EditorUtility.SetDirty(def);

            var item = GetOrCreate<ShopItem>($"{Dir}/{b.id}.asset");
            item.id = b.id;
            item.displayName = b.name;
            item.description = b.description;
            item.category = b.category;
            item.price = b.price;
            item.maxOwned = b.maxOwned;
            item.payload = def;
            item.tier = b.tier;
            EditorUtility.SetDirty(item);

            catalog.items.Add(item);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = catalog;
        Debug.Log($"Created {Buffs.Length} test buffs in {Dir}");
    }
}
