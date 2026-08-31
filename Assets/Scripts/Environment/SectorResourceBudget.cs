using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Ensures each map sector contains enough gatherable materials to finish
    /// its terraforming round without being short-circuited by map depletion.
    /// </summary>
    public static class SectorResourceBudget
    {
        /// <summary>
        /// Target total materials yield from gatherable nodes in one sector.
        /// Covers several solar clusters, climate buildings, and exploration overhead.
        /// </summary>
        public const int MinGatherableMaterialsPerSector = 4000;

        public static bool IsGatherableNodeType(SectorNode.NodeType type)
        {
            return type == SectorNode.NodeType.Minerals
                || type == SectorNode.NodeType.Gas
                || type == SectorNode.NodeType.Iron
                || type == SectorNode.NodeType.Regolith;
        }

        public static int GetYieldPerNode(SectorNode.NodeType type, SupplySO minerals, SupplySO gas, SupplySO iron, SupplySO regolith)
        {
            SupplySO so = type switch
            {
                SectorNode.NodeType.Minerals => minerals,
                SectorNode.NodeType.Gas => gas,
                SectorNode.NodeType.Iron => iron,
                SectorNode.NodeType.Regolith => regolith,
                _ => null
            };

            if (so == null) return 250;
            return so.MaxAmount > 0 ? so.MaxAmount : 250;
        }

        public static int CalculateGatherableYield(
            SectorManager.Sector sector,
            SupplySO minerals,
            SupplySO gas,
            SupplySO iron,
            SupplySO regolith)
        {
            if (sector?.Nodes == null) return 0;

            int total = 0;
            foreach (var node in sector.Nodes)
            {
                if (node == null || !IsGatherableNodeType(node.type)) continue;
                total += GetYieldPerNode(node.type, minerals, gas, iron, regolith);
            }

            return total;
        }

        /// <summary>
        /// Materials the player should have access to in a sector: starting stock plus local deposits.
        /// </summary>
        public static int TotalSectorMaterialsBudget =>
            MinGatherableMaterialsPerSector + Supplies.StartingMaterials;
    }
}
