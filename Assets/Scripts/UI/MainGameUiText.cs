namespace GameDevTV.RTS.UI
{
    /// <summary>LiberationSans SDF lacks many symbols — keep HUD copy ASCII-only to avoid canvas rebuild storms.</summary>
    public static class MainGameUiText
    {
        public static string SanitizeForTmp(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("\u25C0", "<")
                .Replace("\u25B6", ">")
                .Replace("\u2713", "[OK]")
                .Replace("\u2192", ">")
                .Replace("\u2014", "-")
                .Replace("\u2013", "-")
                .Replace("\u00B7", " - ")
                .Replace("\u2026", "...")
                .Replace("∞", "inf");
        }

        /// <summary>Keep the top banner to step + one short objective (detail lives in hint + stats GUIDE).</summary>
        public static string TrimBannerObjective(string objective)
        {
            if (string.IsNullOrEmpty(objective)) return "";
            objective = objective.Trim();
            const int maxLen = 120;
            if (objective.Length <= maxLen) return objective;
            int cut = objective.LastIndexOf(' ', maxLen);
            if (cut < 40) cut = maxLen;
            return objective.Substring(0, cut).TrimEnd() + "...";
        }
    }
}