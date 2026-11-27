namespace GameDevTV.RTS.UI
{
    /// <summary>Shared prologue HUD + hand strip metrics (keep in sync across MainGameHUD, briefing, hand).</summary>
    public static class MainGameUiLayout
    {
        public const float CardWidth = 168f;
        public const float CardHeight = 244f;
        public const float CardSpacing = 10f;
        public const float CardBottomMargin = 12f;
        public const float CardDockPadding = 8f;
        public const float HintGapAboveCards = 6f;

        public static float CardDockTopY => CardBottomMargin + CardHeight + CardDockPadding;
        public static float AboveCardDockY => CardDockTopY + HintGapAboveCards;

        public const float BannerHeight = 158f;
        public const float StatsPanelWidth = 304f;
        public const float StatsPanelHeight = 340f;
        public const float StatsPanelCollapsedHeight = 56f;

        /// <summary>Keep the stats stack above the yellow hint + card dock.</summary>
        public static float StatsPanelBottomInset => AboveCardDockY + 58f;
        public const float BannerRightReserveExtra = 12f;
    }
}