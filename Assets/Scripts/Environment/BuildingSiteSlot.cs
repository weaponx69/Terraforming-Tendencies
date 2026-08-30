using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public enum BuildingSiteKind
    {
        CommandPost,
        /// <summary>Deprecated: replaced by Solar + PairedBuilding clusters.</summary>
        Infrastructure,
        Solar,
        PairedBuilding,
        Mine
    }

    /// <summary>
    /// Pre-placed build pad generated at planet setup. Stored on Sector.BuildingSites (runtime only).
    /// Do not serialize back-references to Sector or SectorNode — that creates Unity serialization cycles.
    /// </summary>
    [System.Serializable]
    public class BuildingSiteSlot
    {
        public BuildingSiteKind Kind;
        public Vector3 Position;
        /// <summary>For mine pads: the resource type at this geographic location.</summary>
        public SectorNode.NodeType LinkedResourceType;
        public bool HasLinkedResource;

        [System.NonSerialized] public SectorManager.Sector Sector;
        [System.NonSerialized] public BuildingSiteCluster Cluster;
        [System.NonSerialized] public BaseBuilding OccupyingBuilding;
        [System.NonSerialized] public GameObject MarkerGO;

        public bool IsOccupied => IsValidOccupant(OccupyingBuilding);

        public static bool IsValidOccupant(BaseBuilding building)
        {
            if (building == null) return false;
            if (!building.gameObject.activeInHierarchy) return false;
            if (building.Progress.State == BuildingProgress.BuildingState.Paused) return false;
            if (building.name.Contains("Ghost", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (building.GetComponentInParent<BuildingSiteMarker>() != null) return false;
            return true;
        }

        public BuildingSiteSlot(
            BuildingSiteKind kind,
            Vector3 position,
            SectorManager.Sector sector,
            SectorNode linkedResourceNode = null)
        {
            Kind = kind;
            Position = position;
            Sector = sector;
            if (linkedResourceNode != null)
            {
                HasLinkedResource = true;
                LinkedResourceType = linkedResourceNode.type;
            }
        }

        public void SetOccupied(BaseBuilding building)
        {
            OccupyingBuilding = building;
        }

        public void ClearOccupancy()
        {
            OccupyingBuilding = null;
        }
    }
}
