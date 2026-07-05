using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Represents a discoverable node within a sector.
    /// Nodes are pre-placed at planet generation and hidden until the sector is explored.
    /// Types include: resource deposits, sector features, nexus connections.
    /// Each node has a 3D visual marker spawned at its position.
    /// </summary>
    [System.Serializable]
    public class SectorNode
    {
        public enum NodeType
        {
            Minerals,       // Mineable mineral deposit
            Gas,            // Mineable gas deposit
            Iron,           // Mineable iron deposit
            Regolith,       // Mineable regolith deposit
            Feature,        // Special sector feature (LavaTube, FaultLine, WaterDeposit)
            Nexus           // Connection point leading to the next sector
        }

        public NodeType type;
        public Vector3 position;
        public bool isRevealed;
        public string labelOverride;       // Custom label shown when revealed (e.g., "Lava Tube Detected")
        public int connectedSectorIndex;   // For Nexus nodes: which sector this connects to
        public string flavorText;          // Description shown in discovery UI
        public GameObject visualGO;        // 3D marker object in the scene (spawned by PlanetGenerator)

        public SectorNode(NodeType type, Vector3 position, string flavorText = "", string labelOverride = "")
        {
            this.type = type;
            this.position = position;
            this.flavorText = flavorText;
            this.labelOverride = labelOverride;
            this.isRevealed = false;
            this.connectedSectorIndex = -1;
            this.visualGO = null;
        }

        /// <summary>Show or hide the 3D visual marker.</summary>
        public void SetVisualVisible(bool visible)
        {
            if (visualGO != null) visualGO.SetActive(visible);
        }
    }
}