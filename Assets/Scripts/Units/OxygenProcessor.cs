using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    public class OxygenProcessor : BaseBuilding
    {
        /// <summary>
        /// Soft cap so a single Life tile cannot slam Oxygen to 100% in seconds.
        /// Prefab historically shipped at 5%/tick — Colony Acts treats Oxygen as flavor, not a win meter.
        /// </summary>
        private const float MaxOxygenPerTick = 0.08f;

        [Tooltip("Percentage of oxygen generated per tick (e.g. 0.08 ≈ ~20 min to 100% with one tile)")]
        [SerializeField] private float oxygenPerTick = 0.08f;

        [Tooltip("How often in seconds the oxygen tick occurs")]
        [SerializeField] private float tickRate = 1f;

        private float tickTimer = 0f;

        private void Update()
        {
            // Climate is driven by ClimateGenerationTicker (and BaseBuilding fallback).
            // Derived Update replaces BaseBuilding.Update — keep oxygen ticks only.

            // Only generate oxygen if the building is fully operating and powered
            if (Owner != Owner.Invalid && IsOperating)
            {
                bool shouldGenerateOxygen = BuildingSO != null && (
                    BuildingSO.Name.Contains("Oxygen Processor") ||
                    BuildingSO.Name.Contains("Algae Spreader") ||
                    BuildingSO.Name.Contains("Greenery Dome")
                );

                if (!shouldGenerateOxygen) return;

                // Sector mini-game: only the active sector's processors count toward oxygen goals.
                if (Environment.SectorManager.Instance != null
                    && !Environment.SectorManager.Instance.IsBuildingInActiveSector(this))
                {
                    return;
                }

                tickTimer += Time.deltaTime;
                if (tickTimer >= tickRate)
                {
                    tickTimer -= tickRate;
                    if (Supplies.Oxygen.ContainsKey(Owner))
                    {
                        float rate = Mathf.Min(Mathf.Max(0f, oxygenPerTick), MaxOxygenPerTick);
                        Supplies.UpdateOxygen(Owner, Supplies.Oxygen[Owner] + rate);
                    }
                }
            }
        }
    }
}
