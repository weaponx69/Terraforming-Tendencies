using System;

namespace GameDevTV.RTS.MainGame
{
    public enum MainGameFeature
    {
        None = 0,
        Ice,
        Glacier,
        Minerals // unused in MainGame slice but reserved
    }

    public readonly struct MainGameBuildingInfo
    {
        public readonly MainGameBuildingId Id;
        public readonly string DisplayName;
        public readonly int BuildWeeks;
        public readonly int Beds;
        public readonly int PowerOutput; // solar=1 pre-storm; wind=2 through dust storm
        public readonly bool NeedsPower;
        public readonly int IceHarvestPerWeek; // aquifer
        public readonly int IceConsumedPerWeek; // oxygen
        public readonly int OxygenProducedPerWeek; // oxygen when powered + has ice

        public MainGameBuildingInfo(
            MainGameBuildingId id, string name, int buildWeeks, int beds,
            int powerOutput, bool needsPower, int iceHarvest, int iceIn, int o2Out)
        {
            Id = id; DisplayName = name; BuildWeeks = buildWeeks; Beds = beds;
            PowerOutput = powerOutput; NeedsPower = needsPower;
            IceHarvestPerWeek = iceHarvest; IceConsumedPerWeek = iceIn; OxygenProducedPerWeek = o2Out;
        }
    }

    public static class MainGameData
    {
        // From Unreal TTBuildings.h / TTColony.h / TTStory
        public const int LandingOxygen = 50;
        public const double O2PerColonist = 0.5;
        public const int StartColonists = 12;
        public const int HarvestRange = 3; // tiles
        public const int MainGameSteps = 6;
        public const int MainGameShuttleWeeks = 3;
        public const int MainGameShuttleSize = 6;
        public const int MainGameStormWeeks = 2;
        public const int OxygenStoreTargetMult = 3; // step 5: store 3 * O2Need
        /// <summary>Typed mine harvest per week (matches aquifer ice scale).</summary>
        public const int MineHarvestPerWeek = 8;

        /// <summary>Landing stockpile: two Aquifers, Oxygen, two Habitat domes before mines come online.</summary>
        // Two Aquifers, Oxygen, two Habitats, Geothermal (18) — prologue has no mineral mine income yet.
        public const int StartingMinerals = 92;
        /// <summary>Camp grid fill per End Week while storm has not darkened camp.</summary>
        public const int CampEnergyGeneration = 8;
        /// <summary>Orbital Scan spend from the energy pool (not minerals).</summary>
        public const int OrbitalScanEnergyCost = 5;

        /// <summary>Minerals paid to place a mine on a surveyed deposit.</summary>
        public const int MineMineralCost = 12;

        /// <summary>Colonists work construction and terraforming at or above this.</summary>
        public const int WorkThreshold = 40;
        public const int SatisfactionStart = 80;
        public const int FaithStart = 80;
        public const int AirCriticalSatisfactionLoss = 20;
        public const int UnhousedSatisfactionLoss = 15;
        public const int NeedsMetSatisfactionGain = 8;
        public const int StepAdvancedFaithGain = 15;
        public const int QuietWeekFaithLoss = 8;
        /// <summary>Faith bump when the colony meets life-support without advancing a story step.</summary>
        public const int SteadyWeekFaithGain = 6;
        public const int DeadlineFaithLoss = 15;
        public const int DeadlinePressureWeeks = 2;
        /// <summary>If Faith hits 0 but AIR ≥ this multiple of weekly need (and crew still works), contract extends here instead of cancelling.</summary>
        public const int FaithRecoveryFloor = 12;
        public static int AirReserveFaithExtensionThreshold(int weeklyNeed) =>
            Math.Max(weeklyNeed, 1) * OxygenStoreTargetMult;

        public static MainGameBuildingInfo Get(MainGameBuildingId id) => id switch
        {
            MainGameBuildingId.Aquifer => new(id, "Ice Aquifer", 2, 0, 0, false, 8, 0, 0),
            MainGameBuildingId.Oxygen => new(id, "Atmosphere Processor", 1, 0, 0, true, 0, 4, 12),
            MainGameBuildingId.Solar => new(id, "Solar Array", 1, 0, 1, false, 0, 0, 0),
            MainGameBuildingId.Habitat => new(id, "Habitat Domes", 1, 10, 0, false, 0, 0, 0),
            MainGameBuildingId.WindTurbine => new(id, "Windmill", 1, 0, 2, false, 0, 0, 0),
            MainGameBuildingId.CommandPost => new(id, "Command Post", 0, 0, 0, false, 0, 0, 0),
            _ => new(MainGameBuildingId.None, "", 0, 0, 0, false, 0, 0, 0)
        };

        /// <summary>
        /// Extra weeks to haul crew and materials from the nearest finished building.
        /// The first two hexes are free. After that, one week per two hexes.
        /// </summary>
        public static int HaulWeeks(int hexDistance)
        {
            if (hexDistance <= 2) return 0;
            return (hexDistance - 2 + 1) / 2;
        }

        /// <summary>Minerals spent when starting construction (0 = free / not buildable via hand).</summary>
        public static int MineralCost(MainGameBuildingId id) => id switch
        {
            MainGameBuildingId.Aquifer => 16,
            MainGameBuildingId.Oxygen => 14,
            MainGameBuildingId.Solar => 10,
            MainGameBuildingId.Habitat => 12,
            MainGameBuildingId.WindTurbine => 18,
            _ => 0
        };

        /// <summary>Colony weeks to complete a MainGame research project.</summary>
        public static int ResearchWeeks(MainGameResearchId id) => id switch
        {
            MainGameResearchId.FasterBuild => 2,
            MainGameResearchId.LongerPowerReach => 3,
            MainGameResearchId.ExtraScanType => 2,
            MainGameResearchId.ExtendedSurvey => 2,
            _ => 1
        };
    }
}
