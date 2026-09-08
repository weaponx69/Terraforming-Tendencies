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
        public GameDevTV.RTS.Environment.SectorManager.SectorFeature requiredSectorFeature =
            GameDevTV.RTS.Environment.SectorManager.SectorFeature.None;

        public override bool IsGateMet()
        {
            // Combolands: climate soft-gates do not block drawing or selecting the card.
            // Materials / pads are checked when the player commits the play.
            if (buildingToUnlock == null || buildingToUnlock.Prefab == null) return false;
            return true;
        }

        /// <summary>
        /// Climate / sector-feature gates — ignored for free hand play (always true).
        /// </summary>
        public bool PassesClimateRequirements() => true;

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
