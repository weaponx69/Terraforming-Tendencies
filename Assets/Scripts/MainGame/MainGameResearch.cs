using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.MainGame
{
    public enum MainGameResearchTier { Tier1 = 1, Tier2 = 2, Tier3 = 3 }

    public enum MainGameResearchId
    {
        None = 0,
        FasterBuild = 1,
        LongerPowerReach = 2,
        ExtraScanType = 3,
        /// <summary>Survey paint overage costs 25% fewer colony days per extra hex.</summary>
        ExtendedSurvey = 4,
    }

    public class MainGameResearch
    {
        readonly HashSet<MainGameResearchId> unlocked = new();
        public MainGameResearchId Active { get; private set; }
        public int WeeksRemaining { get; private set; }

        public bool IsTierUnlocked(MainGameResearchTier tier) =>
            tier == MainGameResearchTier.Tier1 ||
            (tier == MainGameResearchTier.Tier2 && unlocked.Count > 0);

        public bool IsUnlocked(MainGameResearchId id) =>
            id == MainGameResearchId.None || unlocked.Contains(id);

        /// <summary>True when at least one Tier-2 research id is still locked (hand seating).</summary>
        public bool HasLockedProject() =>
            !unlocked.Contains(MainGameResearchId.FasterBuild)
            || !unlocked.Contains(MainGameResearchId.LongerPowerReach)
            || !unlocked.Contains(MainGameResearchId.ExtraScanType)
            || !unlocked.Contains(MainGameResearchId.ExtendedSurvey);

        public bool TryBegin(MainGameResearchId id, out string reason)
        {
            reason = null;
            if (id == MainGameResearchId.None) { reason = "No project"; return false; }
            if (unlocked.Contains(id)) { reason = "Already unlocked"; return false; }
            if (Active != MainGameResearchId.None) { reason = "Research already underway"; return false; }
            Active = id;
            WeeksRemaining = MainGameData.ResearchWeeks(id);
            return true;
        }

        /// <summary>Pause / HUD label for a research id.</summary>
        public static string DisplayName(MainGameResearchId id) => id switch
        {
            MainGameResearchId.FasterBuild => "Faster Build",
            MainGameResearchId.LongerPowerReach => "Longer Power Reach",
            MainGameResearchId.ExtraScanType => "Extra Scan Type",
            MainGameResearchId.ExtendedSurvey => "Extended Survey",
            _ => id.ToString()
        };

        public void TickWeek(List<string> log)
        {
            if (Active == MainGameResearchId.None || WeeksRemaining <= 0) return;
            WeeksRemaining--;
            if (WeeksRemaining > 0) return;
            unlocked.Add(Active);
            log?.Add($"Research complete: {DisplayName(Active)}");
            Active = MainGameResearchId.None;
        }

        /// <summary>Colony days charged per Survey Team hex beyond the card budget (min 0.25).</summary>
        public float SurveyOverageDaysPerHex()
        {
            float days = Mathf.Max(0.25f, 1f - unlocked.Count * 0.18f);
            if (IsUnlocked(MainGameResearchId.ExtendedSurvey))
                days *= 0.75f;
            return days;
        }
    }
}
