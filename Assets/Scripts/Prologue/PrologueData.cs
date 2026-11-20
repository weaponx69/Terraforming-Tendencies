namespace GameDevTV.RTS.Prologue
{
    public enum PrologueFeature
    {
        None = 0,
        Ice,
        Glacier,
        Minerals // unused in Prologue slice but reserved
    }

    public readonly struct PrologueBuildingInfo
    {
        public readonly PrologueBuildingId Id;
        public readonly string DisplayName;
        public readonly int BuildWeeks;
        public readonly int Beds;
        public readonly int PowerOutput; // solar=1, wind=2 (Unreal wind stronger in hills later; use 1 for both if needed)
        public readonly bool NeedsPower;
        public readonly int IceHarvestPerWeek; // aquifer
        public readonly int IceConsumedPerWeek; // oxygen
        public readonly int OxygenProducedPerWeek; // oxygen when powered + has ice

        public PrologueBuildingInfo(
            PrologueBuildingId id, string name, int buildWeeks, int beds,
            int powerOutput, bool needsPower, int iceHarvest, int iceIn, int o2Out)
        {
            Id = id; DisplayName = name; BuildWeeks = buildWeeks; Beds = beds;
            PowerOutput = powerOutput; NeedsPower = needsPower;
            IceHarvestPerWeek = iceHarvest; IceConsumedPerWeek = iceIn; OxygenProducedPerWeek = o2Out;
        }
    }

    public static class PrologueData
    {
        // From Unreal TTBuildings.h / TTColony.h / TTStory
        public const int LandingOxygen = 50;
        public const double O2PerColonist = 0.5;
        public const int StartColonists = 12;
        public const int HarvestRange = 3; // tiles
        public const int PrologueSteps = 6;
        public const int PrologueShuttleWeeks = 3;
        public const int PrologueShuttleSize = 6;
        public const int PrologueStormWeeks = 2;
        public const int OxygenStoreTargetMult = 3; // step 5: store 3 * O2Need

        public static PrologueBuildingInfo Get(PrologueBuildingId id) => id switch
        {
            PrologueBuildingId.Aquifer => new(id, "Ice Aquifer", 2, 0, 0, false, 8, 0, 0),
            PrologueBuildingId.Oxygen => new(id, "Oxygen Processor", 1, 0, 0, true, 0, 4, 8),
            PrologueBuildingId.Solar => new(id, "Solar Array", 1, 0, 1, false, 0, 0, 0),
            PrologueBuildingId.Habitat => new(id, "Habitat Domes", 1, 10, 0, false, 0, 0, 0),
            PrologueBuildingId.WindTurbine => new(id, "Wind Turbine", 1, 0, 2, false, 0, 0, 0),
            PrologueBuildingId.CommandPost => new(id, "Command Post", 0, 0, 0, false, 0, 0, 0),
            _ => new(PrologueBuildingId.None, "", 0, 0, 0, false, 0, 0, 0)
        };
    }
}
