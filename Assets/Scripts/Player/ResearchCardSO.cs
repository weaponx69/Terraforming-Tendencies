using System.Collections.Generic;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.UI;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Prologue research hire: starts a week-long project (Tier-2 unlocks).
    /// Pause-tree pick is Task 7; v1 auto-picks the first locked id.
    /// </summary>
    [CreateAssetMenu(fileName = "ResearchCard", menuName = "Cards/Research Card")]
    public class ResearchCardSO : BlueprintCardSO
    {
        public override string GetCardGoal() => "RESEARCH";

        public override bool CanApply()
        {
            if (!PrologueController.IsActive) return false;
            var c = PrologueController.Instance?.Colony;
            if (c?.Research == null) return false;
            return c.Research.Active == PrologueResearchId.None
                && c.Research.HasLockedProject();
        }

        public override void Apply()
        {
            var c = PrologueController.Instance?.Colony;
            if (c?.Research == null) return;

            // v1: auto-pick first locked Tier-2 id; pause-tree pick is Task 7.
            var pick = PrologueResearchId.FasterBuild;
            if (c.Research.IsUnlocked(pick))
                pick = PrologueResearchId.LongerPowerReach;
            if (c.Research.IsUnlocked(pick))
                pick = PrologueResearchId.ExtraScanType;

            if (!c.Research.TryBegin(pick, out var reason))
            {
                PrologueHUD.Instance?.ShowNote(new List<string> { reason ?? "Cannot research" });
                return;
            }

            PrologueHUD.Instance?.ShowNote(new List<string>
            {
                $"Research started: {pick} ({PrologueData.ResearchWeeks(pick)} wk)."
            });
        }

        public override int GetMaterialsPlayCost() => 0;
    }
}
