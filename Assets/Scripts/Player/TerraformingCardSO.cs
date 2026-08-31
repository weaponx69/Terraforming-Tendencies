using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class TerraformingCardSO : UnlockBuildingCardSO
        {
            [Header("Climate Gates")]
            public float minTemperature = float.MinValue;
            public float maxTemperature = float.MaxValue;
            public float minOxygen = float.MinValue;
            public float maxOxygen = float.MaxValue;
            public float minAtmosphere = float.MinValue;
            public float maxAtmosphere = float.MaxValue;
            public float minWater = float.MinValue;
            public float maxWater = float.MaxValue;
            public GameDevTV.RTS.Environment.SectorManager.SectorFeature requiredSectorFeature = GameDevTV.RTS.Environment.SectorManager.SectorFeature.None;
    
            public override bool IsGateMet()
            {
                if (!base.IsGateMet()) return false;
                return PassesClimateRequirements();
            }

            /// <summary>
            /// Climate / sector-feature gates. When this card's sector goal is still
            /// unmet, minimum climate requirements and sector features are waived so
            /// the card can appear in hand (e.g. Water Ice Aquifer at -60°C).
            /// </summary>
            public bool PassesClimateRequirements()
            {
                string goal = GetCardGoal();
                bool relaxMinGates = GenerationManager.IsUnmetSectorGoal(goal);

                float currentTemp = Supplies.Temperature.TryGetValue(Owner.Player1, out float t) ? t : -60f;
                if (!PassesClimateBound(currentTemp, minTemperature, maxTemperature, relaxMinGates))
                    return false;

                float currentOxygen = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                if (!PassesClimateBound(currentOxygen, minOxygen, maxOxygen, relaxMinGates))
                    return false;

                float currentAtmosphere = Supplies.Atmosphere.TryGetValue(Owner.Player1, out float a) ? a : 0.01f;
                if (!PassesClimateBound(currentAtmosphere, minAtmosphere, maxAtmosphere, relaxMinGates))
                    return false;

                float currentWater = Supplies.Water.TryGetValue(Owner.Player1, out float w) ? w : 0f;
                if (!PassesClimateBound(currentWater, minWater, maxWater, relaxMinGates))
                    return false;

                if (requiredSectorFeature != GameDevTV.RTS.Environment.SectorManager.SectorFeature.None
                    && !relaxMinGates)
                {
                    if (GameDevTV.RTS.Environment.SectorManager.Instance == null) return false;
                    bool hasFeature = false;
                    foreach (var sector in GameDevTV.RTS.Environment.SectorManager.Instance.Sectors)
                    {
                        if (!sector.IsLocked && sector.Feature == requiredSectorFeature)
                        {
                            hasFeature = true;
                            break;
                        }
                    }
                    if (!hasFeature) return false;
                }

                return true;
            }

            private static bool PassesClimateBound(float current, float min, float max, bool skipMin)
            {
                if (!skipMin && current < min) return false;
                if (current > max) return false;
                return true;
            }
    
            public override void Apply()
            {
                base.Apply();
                if (buildingToUnlock != null)
                {
                    BlueprintDraftManager.RegisterBuildingSO(buildingToUnlock);
                }
            }
        }
}
