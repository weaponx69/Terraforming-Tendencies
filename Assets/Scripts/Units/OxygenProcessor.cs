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
            
            // Only generate oxygen if the building is fully constructed and alive
            if (Owner != Owner.Invalid && Progress.State == BuildingProgress.BuildingState.Completed)
            {
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
