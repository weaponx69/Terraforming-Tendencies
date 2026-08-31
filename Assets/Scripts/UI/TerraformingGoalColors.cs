using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Colors for sector completion terraforming only — the primary milestone plus
    /// temperature, atmosphere, and water. Support cards (materials, scans, units)
    /// stay neutral.
    /// </summary>
    public static class TerraformingGoalColors
    {
        public static readonly Color Temperature = new Color(1.00f, 0.45f, 0.18f, 1f);
        public static readonly Color Atmosphere  = new Color(0.35f, 0.70f, 1.00f, 1f);
        public static readonly Color Water       = new Color(0.15f, 0.85f, 0.90f, 1f);
        public static readonly Color Oxygen      = new Color(0.35f, 0.95f, 0.75f, 1f);
        public static readonly Color Biomass     = new Color(0.40f, 0.90f, 0.30f, 1f);
        public static readonly Color Power       = new Color(1.00f, 0.85f, 0.20f, 1f);
        public static readonly Color Population  = new Color(0.85f, 0.45f, 1.00f, 1f);
        public static readonly Color CommandPost = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color Neutral     = new Color(0.75f, 0.78f, 0.82f, 1f);

        public static readonly Color MetValue   = new Color(0.40f, 0.95f, 0.45f, 1f);
        public static readonly Color UnmetValue = new Color(1.00f, 0.40f, 0.40f, 1f);

        /// <summary>
        /// True for goals that gate sector completion: milestone + climate trio.
        /// </summary>
        public static bool IsSectorCompletionGoal(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return false;

            return goal.ToUpperInvariant() switch
            {
                "TEMPERATURE" or "ATMOSPHERE" or "WATER" or
                "BIOMASS" or "OXYGEN" or "POWER" or "POPULATION" or "COMMAND POST" => true,
                _ => false
            };
        }

        /// <summary>
        /// Returns the card's sector-completion goal, or null if it is support/other.
        /// </summary>
        public static string GetSectorGoalForCard(BlueprintCardSO card)
        {
            if (card == null) return null;
            string goal = card.GetCardGoal();
            return IsSectorCompletionGoal(goal) ? goal : null;
        }

        public static Color ForGoal(string goal)
        {
            if (!IsSectorCompletionGoal(goal)) return Neutral;

            return goal.ToUpperInvariant() switch
            {
                "TEMPERATURE" => Temperature,
                "ATMOSPHERE" => Atmosphere,
                "WATER" => Water,
                "OXYGEN" => Oxygen,
                "BIOMASS" => Biomass,
                "POWER" => Power,
                "POPULATION" => Population,
                "COMMAND POST" => CommandPost,
                _ => Neutral
            };
        }

        public static Color ForMilestone(MilestoneType type) => ForGoal(GoalKeyForMilestone(type));

        public static string GoalKeyForMilestone(MilestoneType type)
        {
            return type switch
            {
                MilestoneType.Biomass => "BIOMASS",
                MilestoneType.Oxygen => "OXYGEN",
                MilestoneType.Power => "POWER",
                MilestoneType.Population => "POPULATION",
                MilestoneType.CommandPosts => "COMMAND POST",
                _ => string.Empty
            };
        }

        public static string ToHex(Color color) => $"#{ColorUtility.ToHtmlStringRGB(color)}";

        public static string Colorize(string text, string goal)
        {
            return $"<color={ToHex(ForGoal(goal))}>{text}</color>";
        }

        public static string Colorize(string text, Color color)
        {
            return $"<color={ToHex(color)}>{text}</color>";
        }

        public static string ShortLabel(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return string.Empty;

            return goal.ToUpperInvariant() switch
            {
                "COMMAND POST" => "COMMAND",
                _ => goal.ToUpperInvariant()
            };
        }
    }
}
