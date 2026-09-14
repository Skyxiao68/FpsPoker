using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyAIParameters
{
    public string enemyName = "Enemy";
    [Range(0f, 1f)] public float raiseTendency = 0.5f;
    [Range(0f, 1f)] public float foldTendency = 0.3f;
    public string combatStyle = "Melee";
}

public static class EnemyPokerAI
{
    
    public static EnemyAction Decide(
        EnemyAIParameters parameters,
        HandResult enemyHand,
        bool playerRaised,
        int raiseAmount,
        int enemyChips)
    {
        
        if (playerRaised && enemyChips < raiseAmount)
        {
            Debug.Log($"{parameters.enemyName} 筹码不足，弃牌");
            return EnemyAction.Fold;
        }

       
        float handStrengthMod = 0f;
        if (enemyHand.handType >= HandType.ThreeOfAKind)      handStrengthMod = 0.3f;
        else if (enemyHand.handType >= HandType.TwoPair)      handStrengthMod = 0.15f;
        else if (enemyHand.handType == HandType.HighCard)     handStrengthMod = -0.2f;

        float foldMod = 0f;
        if (enemyHand.handType == HandType.HighCard)          foldMod = 0.3f;
        else if (enemyHand.handType == HandType.OnePair)      foldMod = 0.1f;

        
        if (playerRaised)
        {
            float effectiveFold = Mathf.Clamp01(parameters.foldTendency + foldMod + 0.2f);
            if (Random.value < effectiveFold)
            {
                Debug.Log($"{parameters.enemyName} 面对加注，弃牌（fold 概率 {effectiveFold:F2}）");
                return EnemyAction.Fold;
            }

            
            Debug.Log($"{parameters.enemyName} 匹配加注 {raiseAmount}");
            return EnemyAction.Raise;
        }

        
        float effectiveRaise = Mathf.Clamp01(parameters.raiseTendency + handStrengthMod);
        if (Random.value < effectiveRaise)
        {
            Debug.Log($"{parameters.enemyName} 主动加注（raise 概率 {effectiveRaise:F2}）");
            return EnemyAction.Raise;
        }

        Debug.Log($"{parameters.enemyName} Check");
        return EnemyAction.Check;
    }
}