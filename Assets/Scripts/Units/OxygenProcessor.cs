using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Units
{
    public class OxygenProcessor : BaseBuilding
    {
        [Tooltip("Percentage of oxygen generated per tick (e.g. 0.001)")]
        [SerializeField] private float oxygenPerTick = 0.001f;
        
        [Tooltip("How often in seconds the oxygen tick occurs")]
        [SerializeField] private float tickRate = 1f;

        private float tickTimer = 0f;

        private void Update()
        {
            
            // Only generate oxygen if the building is fully operating and powered
            if (Owner != Owner.Invalid && IsOperating)
            {
                bool shouldGenerateOxygen = BuildingSO != null && (
                    BuildingSO.Name.Contains("Oxygen Processor") || 
                    BuildingSO.Name.Contains("Algae Spreader") || 
                    BuildingSO.Name.Contains("Atmospheric Condenser") ||
                    BuildingSO.Name.Contains("Greenery Dome")
                );

                if (!shouldGenerateOxygen) return;

                tickTimer += Time.deltaTime;
                if (tickTimer >= tickRate)
                {
                    tickTimer -= tickRate;
                    if (Supplies.Oxygen.ContainsKey(Owner))
                    {
                        Supplies.UpdateOxygen(Owner, Supplies.Oxygen[Owner] + oxygenPerTick);
                    }
                }
            }
        }
    }
}
