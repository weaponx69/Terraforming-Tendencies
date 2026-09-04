using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Colors for sector completion terraforming only — climate basics
    /// (temperature, atmosphere, water) plus primary milestones (oxygen/power/etc).
    /// Biomass is deprecated as a terraforming goal. Support cards stay neutral.
    /// </summary>
    public static class TerraformingGoalColors
    {
        // Palette tuned for dark HUD contrast and pairwise distinguishability
        // (especially Oxygen cyan vs Biomass green, Temp amber vs Atmos fuchsia).
        public static readonly Color Temperature = new Color(1.00f, 0.58f, 0.12f, 1f); // amber orange
        public static readonly Color Atmosphere  = new Color(1.00f, 0.35f, 0.72f, 1f); // hot fuchsia
        public static readonly Color Water       = new Color(0.25f, 0.55f, 1.00f, 1f); // bright blue
        public static readonly Color Oxygen      = new Color(0.15f, 0.85f, 1.00f, 1f); // cyan
        public static readonly Color Biomass     = new Color(0.45f, 0.92f, 0.20f, 1f); // lime green
        public static readonly Color Power       = new Color(1.00f, 0.88f, 0.20f, 1f); // gold
        public static readonly Color Population  = new Color(0.65f, 0.45f, 1.00f, 1f); // indigo violet
        public static readonly Color CommandPost = new Color(0.95f, 0.95f, 0.95f, 1f); // white
        public static readonly Color Neutral     = new Color(0.82f, 0.86f, 0.90f, 1f); // light grey

        /// <summary>Objectives panel only: progress number when the climate target is unmet.</summary>
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
                // Climate basics + remaining primary sector milestones.
                // Biomass is deprecated and is not a sector-completion goal.
                "TEMPERATURE" or "ATMOSPHERE" or "WATER" or
                "OXYGEN" or "POWER" or "POPULATION" or "COMMAND POST" => true,
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
                MilestoneType.Temperature => "TEMPERATURE",
                MilestoneType.Oxygen => "OXYGEN",
                MilestoneType.Power => "POWER",
                MilestoneType.Population => "POPULATION",
                MilestoneType.CommandPosts => "COMMAND POST",
                MilestoneType.Biomass => string.Empty, // deprecated
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
                "TEMPERATURE" => "TEMP",
                "ATMOSPHERE" => "ATMOS",
                _ => goal.ToUpperInvariant()
            };
        }

        public static string DisplayName(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return string.Empty;

            return goal.ToUpperInvariant() switch
            {
                "TEMPERATURE" => "Temperature",
                "ATMOSPHERE" => "Atmosphere",
                "WATER" => "Water",
                "OXYGEN" => "Oxygen",
                "BIOMASS" => "Biomass",
                "POWER" => "Power",
                "POPULATION" => "Population",
                "COMMAND POST" => "Command Post",
                _ => goal
            };
        }

        /// <summary>Compact rich-text legend for Active Objectives / HUD help.</summary>
        public static string BuildLegendLine(params string[] goals)
        {
            if (goals == null || goals.Length == 0) return string.Empty;

            var parts = new System.Collections.Generic.List<string>(goals.Length);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string goal in goals)
            {
                if (!IsSectorCompletionGoal(goal)) continue;
                string key = goal.ToUpperInvariant();
                if (!seen.Add(key)) continue;
                parts.Add(Colorize(DisplayName(goal), goal));
            }

            return parts.Count == 0 ? string.Empty : string.Join("  ·  ", parts);
        }
    }
}
