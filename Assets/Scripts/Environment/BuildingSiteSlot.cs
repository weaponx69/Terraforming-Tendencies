using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public enum BuildingSiteKind
    {
        CommandPost,
        Infrastructure,
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
        [System.NonSerialized] public BaseBuilding OccupyingBuilding;
        [System.NonSerialized] public GameObject MarkerGO;

        public bool IsOccupied => OccupyingBuilding != null;

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
