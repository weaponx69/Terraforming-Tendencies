namespace GameDevTV.RTS.UI
{
    /// <summary>LiberationSans SDF lacks many symbols — keep HUD copy ASCII-only to avoid canvas rebuild storms.</summary>
    public static class PrologueUiText
    {
        public static string SanitizeForTmp(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text
                .Replace("\u25C0", "<")
                .Replace("\u25B6", ">")
                .Replace("\u2713", "[OK]")
                .Replace("\u2192", ">")
                .Replace("∞", "inf");
        }
    }
}