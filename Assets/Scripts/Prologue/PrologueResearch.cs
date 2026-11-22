using System.Collections.Generic;

namespace GameDevTV.RTS.Prologue
{
    public enum PrologueResearchTier { Tier1 = 1, Tier2 = 2, Tier3 = 3 }

    public enum PrologueResearchId
    {
        None = 0,
        FasterBuild = 1,
        LongerPowerReach = 2,
        ExtraScanType = 3,
    }

    public class PrologueResearch
    {
        readonly HashSet<PrologueResearchId> unlocked = new();
        public PrologueResearchId Active { get; private set; }
        public int WeeksRemaining { get; private set; }

        public bool IsTierUnlocked(PrologueResearchTier tier) =>
            tier == PrologueResearchTier.Tier1 ||
            (tier == PrologueResearchTier.Tier2 && unlocked.Count > 0);

        public bool IsUnlocked(PrologueResearchId id) =>
            id == PrologueResearchId.None || unlocked.Contains(id);

        public bool TryBegin(PrologueResearchId id, out string reason)
        {
            reason = null;
            if (id == PrologueResearchId.None) { reason = "No project"; return false; }
            if (unlocked.Contains(id)) { reason = "Already unlocked"; return false; }
            if (Active != PrologueResearchId.None) { reason = "Research already underway"; return false; }
            Active = id;
            WeeksRemaining = PrologueData.ResearchWeeks(id);
            return true;
        }

        public void TickWeek(List<string> log)
        {
            if (Active == PrologueResearchId.None || WeeksRemaining <= 0) return;
            WeeksRemaining--;
            if (WeeksRemaining > 0) return;
            unlocked.Add(Active);
            log?.Add($"Research complete: {Active}");
            Active = PrologueResearchId.None;
        }
    }
}
