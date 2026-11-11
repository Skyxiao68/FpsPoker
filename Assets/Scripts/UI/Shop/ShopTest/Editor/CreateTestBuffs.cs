using System.IO;
using PokerShop;
using UnityEditor;
using UnityEngine;

public static class CreateTestBuffs
{
    const string Dir = "Assets/ShopData/TestBuffs";

    class Buff
    {
        public string id, name, description, category, suit;
        public int price, maxOwned, amount;
        public EnhancementType type;
        public Buff(string id, string name, string description, string category,
                    EnhancementType type, int amount, string suit, int price, int maxOwned)
        {
            this.id = id; this.name = name; this.description = description; this.category = category;
            this.type = type; this.amount = amount; this.suit = suit;
            this.price = price; this.maxOwned = maxOwned;
        }
    }

    static readonly Buff[] Buffs =
    {
        new Buff("extra_card",     "Extra Starting Card", "Start the round with one extra card.",  "Cards",      EnhancementType.ExtraStartingCard, 1,  "",         80,  1),
        new Buff("card_swap",      "Card Swap",           "Swap one card from your hand.",         "Cards",      EnhancementType.CardSwap,          1,  "",         65,  1),
        new Buff("double_swap",    "Double Swap",         "Swap up to two cards from your hand.",  "Cards",      EnhancementType.CardSwap,          2,  "",         110, 1),
        new Buff("cold_read",      "Cold Read",           "See both of the enemy's cards.",        "Cards",      EnhancementType.CardSwap,          2,  "",         65,  1),
        new Buff("spade_tattoo",   "Spade Tattoo",        "Spades also feed HP (70%).",            "Suits",      EnhancementType.SuitShift,         70, "spades",   65,  1),
        new Buff("red_shift",      "Red Shift",           "Diamonds also feed ATK (50%).",         "Suits",      EnhancementType.SuitShift,         50, "diamonds", 65,  1),
        new Buff("flat_bonus",     "Flat Bonus",          "Every hand type gains +0.10 multiplier.","Cards",     EnhancementType.HandMultiplier,    10, "",         65,  1),
        new Buff("lucky_high",     "Lucky High",          "High card pays 1.30; others lose 0.05.","Cards",      EnhancementType.HandMultiplier,    30, "",         110, 1),
        new Buff("pressure",       "Pressure",            "Your raises scare them more.",          "Table",      EnhancementType.Pressure,          8,  "",         25,  1),
        new Buff("bait",           "Bait",                "Your checks invite a bet.",             "Table",      EnhancementType.Pressure,          8,  "",         65,  1),
        new Buff("precog",         "Precog",              "See the deck top every hand.",          "Cards",      EnhancementType.CardSwap,          3,  "",         110, 1),
        new Buff("monochrome",     "Monochrome",          "Whole hand counts as one suit.",        "Suits",      EnhancementType.SuitShift,         100,"all",      110, 1),
        new Buff("draw_six",       "Draw Six",            "A third hole card; best five of eight.", "Cards",     EnhancementType.ExtraStartingCard, 1,  "",         110, 1),
        new Buff("pair_up",        "Pair Up",             "A single pair settles as two pair.",    "Cards",      EnhancementType.HandMultiplier,    10, "",         65,  1),
        new Buff("momentum",       "Momentum",            "Each raise adds +0.05 (max +0.15).",    "Table",      EnhancementType.HandMultiplier,    5,  "",         25,  1),
        new Buff("four_flush",     "Four Flush",          "Four of a suit counts as a flush.",     "Suits",      EnhancementType.SuitShift,         4,  "all",      110, 1),
        new Buff("chop_breaker",   "Chop Breaker",        "A split counts as your win.",           "Table",      EnhancementType.Pressure,          0,  "",         25,  1),
        new Buff("suit_stack",     "Suit Stack",          "Each extra card of a suit x1.3 (max x2).","Suits",    EnhancementType.SuitShift,         30, "all",      65,  1),
        new Buff("boost_hearts",   "Hearts Boost",        "Hearts are more likely to be dealt.",   "Suits",      EnhancementType.SuitBoost,         1,  "hearts",   70,  1),
        new Buff("boost_spades",   "Spades Boost",        "Spades are more likely to be dealt.",   "Suits",      EnhancementType.SuitBoost,         1,  "spades",   70,  1),
        new Buff("boost_diamonds", "Diamonds Boost",      "Diamonds are more likely to be dealt.", "Suits",      EnhancementType.SuitBoost,         1,  "diamonds", 70,  1),
        new Buff("boost_clubs",    "Clubs Boost",         "Clubs are more likely to be dealt.",    "Suits",      EnhancementType.SuitBoost,         1,  "clubs",    70,  1),
        new Buff("insurance_25",   "Basic Insurance",     "Reduce losses from a defeat by 25%.",   "Protection", EnhancementType.Insurance,         25, "",         60,  1),
        new Buff("insurance_50",   "Full Insurance",      "Reduce losses from a defeat by 50%.",   "Protection", EnhancementType.Insurance,         50, "",         110, 1),
    };

    static T GetOrCreate<T>(string path) where T : ScriptableObject
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
            def.type = b.type; def.amount = b.amount; def.suit = b.suit;
            EditorUtility.SetDirty(def);

            var item = GetOrCreate<ShopItem>($"{Dir}/{b.id}.asset");
            item.id = b.id; item.displayName = b.name; item.description = b.description;
            item.category = b.category; item.price = b.price; item.maxOwned = b.maxOwned;
            item.payload = def;
            EditorUtility.SetDirty(item);

            catalog.items.Add(item);
        }

        foreach (string id in new[] { "deadeye", "quickstep", "iron_heart" })
        {
            var item = AssetDatabase.LoadAssetAtPath<ShopItem>($"{Dir}/{id}.asset");
            if (item != null)
                catalog.items.Add(item);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = catalog;
        Debug.Log($"Created {Buffs.Length} test buffs in {Dir}");
    }
}
