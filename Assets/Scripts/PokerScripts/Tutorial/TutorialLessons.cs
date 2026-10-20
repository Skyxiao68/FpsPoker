using System.Collections.Generic;

/// <summary>一课要玩家做什么。</summary>
public enum TutorialGate
{
    // 只读：点 GOT IT 就走。
    GotIt,

    // 玩家必须自己点 CHECK 才能过（教程不替他点）。
    ClickCheck,

    // 自动打到摊牌，然后把两家牌摆出来讲，点 GOT IT 结算并继续。
    Showdown,

    // 最后一课：敌人只有 8 筹码，付不起 10 的 buy-in，讲"本关结束"，点 FINISH。
    Finish,
}

/// <summary>一课的全部数据：发牌、讲什么、怎么过。</summary>
public class TutorialLesson
{
    public string title;

    // 提示卡正文。换行用 \n，最多 3 行。
    public string body;

    // 依次抽出的牌：玩家第一张、敌人第一张、玩家第二张、敌人第二张，
    // 之后是公共牌（翻牌 3、转牌 1、河牌 1）。
    public List<Card> drawOrder = new List<Card>();

    // 这一课开局给敌人多少筹码（正常是 200，最后一课是 8）。
    public int enemyChips = 200;

    public TutorialGate gate = TutorialGate.GotIt;

    // 自动推进到哪一步为止：None/Flop/Showdown。
    public string autoTo = "None";

    // 这一课要高亮什么：空 / "check" / "playerCards" / "chips"。
    public string highlight = "";
}

/// <summary>
/// 五课的剧本数据。改文案只动这个文件，逻辑在 TutorialDirector 里。
/// </summary>
public static class TutorialLessons
{
    private static Card C(Rank rank, Suit suit)
    {
        Card card = new Card();
        card.Rank = rank;
        card.Suit = suit;
        return card;
    }

    public static List<TutorialLesson> Build()
    {
        List<TutorialLesson> lessons = new List<TutorialLesson>();

        // 1. 牌面：点数 + 花色，花色决定战斗属性。
        TutorialLesson l1 = new TutorialLesson();
        l1.title = "Your cards";
        l1.body = "Your cards are your combat stats:\n"
                + "<color=#A0A0A0>♠ ATK</color>   <color=#FF8080>♥ HP</color>   "
                + "<color=#90E0FF>♣ ROF</color>   <color=#FFD060>♦ SPD</color>\n"
                + "Win or lose the hand, they stay with you — and only count in the duel.";
        l1.highlight = "playerCards";
        l1.drawOrder.Add(C(Rank.Queen, Suit.Clubs));
        l1.drawOrder.Add(C(Rank.Eight, Suit.Diamonds));
        l1.drawOrder.Add(C(Rank.Queen, Suit.Spades));
        l1.drawOrder.Add(C(Rank.Five, Suit.Hearts));
        l1.gate = TutorialGate.GotIt;
        lessons.Add(l1);

        // 2. 三个动作：只有 Check 能点。
        TutorialLesson l2 = new TutorialLesson();
        l2.title = "Three moves";
        l2.body = "FOLD — drop the hand; you lose the pot, not your chips.\n"
                + "CHECK / MATCH — pass for free, or put in what they bet.\n"
                + "RAISE — put in extra chips; they must match it or fold. Only CHECK is live now.";
        l2.highlight = "check";
        l2.drawOrder.Add(C(Rank.Queen, Suit.Clubs));
        l2.drawOrder.Add(C(Rank.Eight, Suit.Diamonds));
        l2.drawOrder.Add(C(Rank.Queen, Suit.Spades));
        l2.drawOrder.Add(C(Rank.Five, Suit.Hearts));
        l2.gate = TutorialGate.ClickCheck;
        lessons.Add(l2);

        // 3. 最好的五张：翻牌后看 Best five 行。
        TutorialLesson l3 = new TutorialLesson();
        l3.title = "The best five";
        l3.body = "Two cards plus the table: only the best five count.\n"
                + "The line under your hand names it and lights those cards.\n"
                + "Here you have Three Queens — Three of a Kind, +35% stats!";
        l3.drawOrder.Add(C(Rank.Queen, Suit.Clubs));
        l3.drawOrder.Add(C(Rank.Eight, Suit.Diamonds));
        l3.drawOrder.Add(C(Rank.Queen, Suit.Spades));
        l3.drawOrder.Add(C(Rank.Five, Suit.Hearts));
        l3.drawOrder.Add(C(Rank.Queen, Suit.Diamonds));
        l3.drawOrder.Add(C(Rank.Nine, Suit.Hearts));
        l3.drawOrder.Add(C(Rank.Four, Suit.Spades));
        l3.gate = TutorialGate.GotIt;
        l3.autoTo = "Flop";
        lessons.Add(l3);

        // 4. 摊牌与底池。
        TutorialLesson l4 = new TutorialLesson();
        l4.title = "Showdown and the pot";
        l4.body = "Both hands are revealed — but the pot is not decided yet.\n"
                + "You still have to win the Duel to take it.\n"
                + "In this lesson the pot is yours either way.";
        l4.drawOrder.Add(C(Rank.Queen, Suit.Clubs));
        l4.drawOrder.Add(C(Rank.Eight, Suit.Diamonds));
        l4.drawOrder.Add(C(Rank.Queen, Suit.Spades));
        l4.drawOrder.Add(C(Rank.Five, Suit.Hearts));
        l4.drawOrder.Add(C(Rank.Queen, Suit.Diamonds));
        l4.drawOrder.Add(C(Rank.Nine, Suit.Hearts));
        l4.drawOrder.Add(C(Rank.Four, Suit.Spades));
        l4.drawOrder.Add(C(Rank.Two, Suit.Clubs));
        l4.drawOrder.Add(C(Rank.Three, Suit.Diamonds));
        l4.gate = TutorialGate.Showdown;
        l4.autoTo = "Showdown";
        lessons.Add(l4);

        // 5. 本关怎么结束。
        TutorialLesson l5 = new TutorialLesson();
        l5.title = "How a level ends";
        l5.body = "A level ends when the opponent cannot cover the next buy-in.\n"
                + "This one has 8 chips — below the 10 buy-in — so the level is already over.\n"
                + "If your chips cannot afford the buy-in, you lose the game.";
        l5.highlight = "chips";
        l5.drawOrder.Add(C(Rank.Queen, Suit.Clubs));
        l5.drawOrder.Add(C(Rank.Eight, Suit.Diamonds));
        l5.drawOrder.Add(C(Rank.Queen, Suit.Spades));
        l5.drawOrder.Add(C(Rank.Five, Suit.Hearts));
        l5.enemyChips = 8;
        l5.gate = TutorialGate.Finish;
        lessons.Add(l5);

        return lessons;
    }
}
