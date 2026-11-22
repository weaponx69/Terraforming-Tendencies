using System.Collections.Generic;
using GameDevTV.RTS.Prologue;
using GameDevTV.RTS.UI;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Prologue research hire: opens the pause research ladder to pick a Tier-2 project.
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

            var pause = Object.FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Exclude);
            if (pause != null)
            {
                pause.OpenResearchPick();
                PrologueHUD.Instance?.ShowNote(new List<string>
                {
                    "Choose a research project from the pause ladder."
                });
                return;
            }

            // Fallback if pause UI is missing: auto-pick first locked Tier-2 id.
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
                $"Research started: {PrologueResearch.DisplayName(pick)} ({PrologueData.ResearchWeeks(pick)} wk)."
            });
        }

        public override int GetMaterialsPlayCost() => 0;
    }
}
