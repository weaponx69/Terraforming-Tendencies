using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Represents a discoverable node within a sector that connects to other nodes.
    /// Nodes form a graph: exploring one node reveals its connections.
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
        public bool isExplored;          // Player has explored this node
        public bool isDiscovered;        // "?" state — connected to an explored node but not yet explored
        public string labelOverride;     // Custom label (e.g., "Lava Tube Detected")
        public int connectedSectorIndex; // For Nexus: which sector this leads to
        public string flavorText;        // Description shown in discovery UI
        [System.NonSerialized] public GameObject visualGO;      // 3D marker in the scene (runtime only)
        [System.NonSerialized] public GameObject questionMarkGO; // "?" floating text (runtime only)
        [System.NonSerialized] public List<SectorNode> connections = new List<SectorNode>(); // Built at runtime

        public SectorNode(NodeType type, Vector3 position, string flavorText = "", string labelOverride = "")
        {
            this.type = type;
            this.position = position;
            this.flavorText = flavorText;
            this.labelOverride = labelOverride;
            this.isExplored = false;
            this.isDiscovered = false;
            this.connectedSectorIndex = -1;
            this.visualGO = null;
            this.questionMarkGO = null;
        }

        public void SetVisualVisible(bool visible)
        {
            if (visualGO != null) visualGO.SetActive(visible);
        }

        public void SetQuestionMarkVisible(bool visible)
        {
            if (questionMarkGO != null) questionMarkGO.SetActive(visible);
        }

        /// <summary>
        /// When this node is explored, discover all connected nodes (show "?" on them).
        /// </summary>
        public void OnExplored()
        {
            isExplored = true;
            isDiscovered = false;
            SetVisualVisible(true);
            SetQuestionMarkVisible(false);

            foreach (var conn in connections)
            {
                if (conn != null && !conn.isExplored)
                {
                    conn.isDiscovered = true;
                    conn.SetQuestionMarkVisible(true);
                }
            }
        }
    }
}