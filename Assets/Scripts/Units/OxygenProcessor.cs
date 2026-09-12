using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    public class OxygenProcessor : BaseBuilding
    {
        /// <summary>
        /// Soft rate cap per tick. Planet share is additionally capped at 100/N % per sector.
        /// </summary>
        private const float MaxOxygenPerTick = 0.08f;

        [Tooltip("Percentage of oxygen generated per tick")]
        [SerializeField] private float oxygenPerTick = 0.08f;

        [Tooltip("How often in seconds the oxygen tick occurs")]
        [SerializeField] private float tickRate = 1f;

        private float tickTimer = 0f;

        private void Update()
        {
            if (Owner == Owner.Invalid) return;
            if (Progress.State != BuildingProgress.BuildingState.Completed) return;

            float efficiency = ProductionEfficiency;
            if (efficiency <= 0f) return;

            bool shouldGenerateOxygen = BuildingSO != null && (
                BuildingSO.Name.Contains("Oxygen Processor") ||
                BuildingSO.Name.Contains("Algae Spreader") ||
                BuildingSO.Name.Contains("Greenery Dome")
            );
            if (!shouldGenerateOxygen) return;

            tickTimer += Time.deltaTime;
            if (tickTimer < tickRate) return;
            tickTimer -= tickRate;

            if (Supplies.Oxygen == null || !Supplies.Oxygen.ContainsKey(Owner)) return;

            float rate = Mathf.Min(Mathf.Max(0f, oxygenPerTick), MaxOxygenPerTick) * efficiency;
            if (rate <= 0f) return;

            var acts = ColonyActManager.Instance;
            if (acts != null)
            {
                if (!acts.TryApplySectorOxygenContribution(transform.position, ref rate))
                    return;
            }

            Supplies.UpdateOxygen(Owner, Supplies.Oxygen[Owner] + rate);
        }
    }
}
