namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// When true, TakeDamage applies. Colony Acts enables this for card-tied disasters.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>When false, all <see cref="IDamageable.TakeDamage"/> calls are no-ops.</summary>
        public static bool Enabled { get; set; } = false;
    }
}
