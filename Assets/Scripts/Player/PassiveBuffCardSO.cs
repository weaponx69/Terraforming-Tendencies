using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class PassiveBuffCardSO : BlueprintCardSO
        {
            public enum BuffType { GatherSpeed, PowerGeneration }
            public BuffType buffType;
            public float multiplier = 1.2f;
    
            public override string GetCardGoal()
            {
                if (buffType == BuffType.GatherSpeed) return "MINING";
                if (buffType == BuffType.PowerGeneration) return "POWER";
                return "PASSIVE BUFF";
            }
    
            public override void Apply()
            {
                if (buffType == BuffType.GatherSpeed)
                {
                    BlueprintDraftManager.GatherSpeedMultiplier *= multiplier;
                    Debug.Log($"[Blueprint] Active gather speed multiplier is now: {BlueprintDraftManager.GatherSpeedMultiplier}");
                }
                else if (buffType == BuffType.PowerGeneration)
                {
                    BlueprintDraftManager.PowerGenMultiplier *= multiplier;
                    Debug.Log($"[Blueprint] Active power generation multiplier is now: {BlueprintDraftManager.PowerGenMultiplier}");
                }
            }
        }
}
