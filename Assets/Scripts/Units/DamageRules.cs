namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// Colony Acts is placement-first — combat/hazard damage is retired.
    /// Flip <see cref="Enabled"/> only if a mode needs hit points again.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>When false, all <see cref="IDamageable.TakeDamage"/> calls are no-ops.</summary>
        public static bool Enabled { get; set; } = false;
    }
}
