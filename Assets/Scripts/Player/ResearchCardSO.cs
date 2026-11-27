using System.Collections.Generic;
using GameDevTV.RTS.MainGame;
using GameDevTV.RTS.UI;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Research card — opens the in-game research panel (no pause).
    /// </summary>
    [CreateAssetMenu(fileName = "ResearchCard", menuName = "Cards/Research Card")]
    public class ResearchCardSO : BlueprintCardSO
    {
        public override string GetCardGoal() => "RESEARCH";

        public override bool CanApply()
        {
            if (!MainGameController.IsActive) return false;
            var c = MainGameController.Instance?.Colony;
            if (c?.Research == null) return false;
            return c.Research.Active == MainGameResearchId.None
                && c.Research.HasLockedProject();
        }

        public override void Apply()
        {
            var c = MainGameController.Instance?.Colony;
            if (c?.Research == null) return;

            var hud = MainGameHUD.Instance;
            hud?.EnsureResearchPicker();
            if (MainGameResearchPickerUI.Instance != null)
            {
                MainGameResearchPickerUI.Instance.Open();
                return;
            }

            // Fallback if HUD picker is missing:
            var pick = MainGameResearchId.FasterBuild;
            if (c.Research.IsUnlocked(pick))
                pick = MainGameResearchId.LongerPowerReach;
            if (c.Research.IsUnlocked(pick))
                pick = MainGameResearchId.ExtraScanType;
            if (c.Research.IsUnlocked(pick))
                pick = MainGameResearchId.ExtendedSurvey;

            if (!c.Research.TryBegin(pick, out var reason))
            {
                MainGameHUD.Instance?.ShowNote(new List<string> { reason ?? "Cannot research" });
                return;
            }

            MainGameHUD.Instance?.ShowNote(new List<string>
            {
                $"Research started: {MainGameResearch.DisplayName(pick)} ({MainGameData.ResearchWeeks(pick)} wk)."
            });
        }

        public override int GetMaterialsPlayCost() => 0;
    }
}
