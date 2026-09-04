using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class ResourceShipmentCardSO : BlueprintCardSO
        {
            public int materialsAmount = 0;
            public int biomassAmount = 0;
            public int oxygenAmount = 0;
            public float temperatureAmount = 0f;
            public float atmosphereAmount = 0f;
            public float waterAmount = 0f;
    
            public override string GetCardGoal()
            {
                if (temperatureAmount > 0f) return "TEMPERATURE";
                if (atmosphereAmount > 0f) return "ATMOSPHERE";
                if (waterAmount > 0f) return "WATER";
                if (biomassAmount > 0) return "RESOURCES"; // Biomass terraforming deprecated
                if (oxygenAmount > 0) return "OXYGEN";
                if (materialsAmount > 0) return "MATERIALS";
                return "RESOURCES";
            }
    
            public override void Apply()
            {
                if (temperatureAmount > 0f)
                {
                    float cur = Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
                    float target = cur + temperatureAmount;
                    Supplies.UpdateTemperature(Owner.Player1, target);
                    if (ClimateManager.Instance != null)
                        ClimateManager.Instance.SetTemperatureTarget(target);
                    Debug.Log($"[Blueprint] Temperature surge: +{temperatureAmount}°C (target {target}°C, ticking up)");
                }
                if (atmosphereAmount > 0f)
                {
                    float cur = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a) ? a : 0.01f;
                    float target = cur + atmosphereAmount;
                    Supplies.UpdateAtmosphere(Owner.Player1, target);
                    if (ClimateManager.Instance != null)
                        ClimateManager.Instance.SetAtmosphereTarget(target);
                    Debug.Log($"[Blueprint] Atmosphere injection: +{atmosphereAmount} atm (target {target} atm, ticking up)");
                }
                if (waterAmount > 0f)
                {
                    float cur = Supplies.Water.TryGetValue(Owner.Player1, out float w) ? w : 0f;
                    float target = cur + waterAmount;
                    Supplies.UpdateWater(Owner.Player1, target);
                    if (ClimateManager.Instance != null)
                        ClimateManager.Instance.SetWaterTarget(target);
                    Debug.Log($"[Blueprint] Water deposit: +{waterAmount}% (target {target}%, ticking up)");
                }
                if (materialsAmount > 0)
                {
                    int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                    Supplies.Materials[Owner.Player1] = cur + materialsAmount;
                    Supplies.RaiseMaterialsChanged(Owner.Player1, cur + materialsAmount);
                    Debug.Log($"[Blueprint] Materials shipment delivered: +{materialsAmount}");
                }
                if (biomassAmount > 0)
                {
                    float cur = Supplies.Biomass.TryGetValue(Owner.Player1, out float b) ? b : 0f;
                    Supplies.UpdateBiomass(Owner.Player1, cur + biomassAmount);
                    Debug.Log($"[Blueprint] Biomass shipment delivered: +{biomassAmount}");
                }
                if (oxygenAmount > 0)
                {
                    float cur = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                    Supplies.UpdateOxygen(Owner.Player1, cur + oxygenAmount);
                    Debug.Log($"[Blueprint] Oxygen shipment delivered: +{oxygenAmount}");
                }
            }
        }
}
