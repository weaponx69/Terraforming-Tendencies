using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// Shared color coding for sector terraforming goals and the cards that advance them.
    /// Keep objectives UI and hand buttons on the same palette so matches are obvious.
    /// </summary>
    public static class TerraformingGoalColors
    {
        // Distinct, high-contrast colors for dark HUD backgrounds.
        public static readonly Color Temperature = new Color(1.00f, 0.45f, 0.18f, 1f); // orange
        public static readonly Color Atmosphere  = new Color(0.35f, 0.70f, 1.00f, 1f); // sky blue
        public static readonly Color Water       = new Color(0.15f, 0.85f, 0.90f, 1f); // teal
        public static readonly Color Oxygen      = new Color(0.35f, 0.95f, 0.75f, 1f); // mint
        public static readonly Color Biomass     = new Color(0.40f, 0.90f, 0.30f, 1f); // green
        public static readonly Color Power       = new Color(1.00f, 0.85f, 0.20f, 1f); // gold
        public static readonly Color Population  = new Color(0.85f, 0.45f, 1.00f, 1f); // violet
        public static readonly Color CommandPost = new Color(0.95f, 0.95f, 0.95f, 1f); // white
        public static readonly Color Materials   = new Color(0.90f, 0.70f, 0.35f, 1f); // amber
        public static readonly Color Exploration = new Color(0.65f, 0.55f, 1.00f, 1f); // indigo
        public static readonly Color Mining      = new Color(0.75f, 0.65f, 0.40f, 1f); // bronze
        public static readonly Color Neutral     = new Color(0.75f, 0.78f, 0.82f, 1f); // grey

        public static readonly Color MetValue    = new Color(0.40f, 0.95f, 0.45f, 1f);
        public static readonly Color UnmetValue  = new Color(1.00f, 0.40f, 0.40f, 1f);

        public static Color ForGoal(string goal)
        {
            if (string.IsNullOrEmpty(goal)) return Neutral;

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
                "MATERIALS" => Materials,
                "EXPLORATION" or "SCOUTING" => Exploration,
                "MINING" or "SALVAGE" or "GAS" => Mining,
                "MAINTENANCE" or "UNIT SUPPORT" or "SOLID TUBES" or "PASSIVE BUFF"
                    or "BLUEPRINT" or "CONSTRUCTION" or "RESOURCES" => Neutral,
                _ => Neutral
            };
        }

        public static Color ForMilestone(MilestoneType type)
        {
            return type switch
            {
                MilestoneType.Biomass => Biomass,
                MilestoneType.Oxygen => Oxygen,
                MilestoneType.Power => Power,
                MilestoneType.Population => Population,
                MilestoneType.CommandPosts => CommandPost,
                _ => Neutral
            };
        }

        public static string GoalKeyForMilestone(MilestoneType type)
        {
            return type switch
            {
                MilestoneType.Biomass => "BIOMASS",
                MilestoneType.Oxygen => "OXYGEN",
                MilestoneType.Power => "POWER",
                MilestoneType.Population => "POPULATION",
                MilestoneType.CommandPosts => "COMMAND POST",
                _ => "BLUEPRINT"
            };
        }

        public static string ToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

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
            if (string.IsNullOrEmpty(goal)) return "OTHER";
            return goal.ToUpperInvariant() switch
            {
                "COMMAND POST" => "COMMAND",
                "UNIT SUPPORT" => "UNIT",
                "PASSIVE BUFF" => "BUFF",
                "SOLID TUBES" => "TUBES",
                _ => goal.ToUpperInvariant()
            };
        }
    }
}
