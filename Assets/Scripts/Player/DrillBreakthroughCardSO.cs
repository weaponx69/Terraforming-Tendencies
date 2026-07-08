using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class DrillBreakthroughCardSO : BlueprintCardSO
        {
            public float gatherSpeedMultiplier = 1.5f;
    
            public override string GetCardGoal() => "MINING";
    
            public override void Apply()
            {
                BlueprintDraftManager.GatherSpeedMultiplier *= gatherSpeedMultiplier;
                Debug.Log($"[Blueprint] Drill Breakthrough: Gather speed multiplier now {BlueprintDraftManager.GatherSpeedMultiplier:F1}x");
            }
        }
}
