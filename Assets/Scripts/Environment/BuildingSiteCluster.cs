using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// A solar array pad paired with an adjacent building pad. Each consumer building
    /// hooks to the solar in its cluster.
    /// </summary>
    public class BuildingSiteCluster
    {
        public int Id;
        [System.NonSerialized] public SectorManager.Sector Sector;
        [System.NonSerialized] public BuildingSiteSlot SolarSlot;
        [System.NonSerialized] public BuildingSiteSlot BuildingSlot;

        public BaseBuilding SolarBuilding =>
            SolarSlot != null && SolarSlot.IsOccupied ? SolarSlot.OccupyingBuilding : null;

        public bool CanPlaceSolar => SolarSlot != null && !SolarSlot.IsOccupied;
        public bool CanPlaceBuilding => BuildingSlot != null && !BuildingSlot.IsOccupied && SolarBuilding != null;
    }
}
