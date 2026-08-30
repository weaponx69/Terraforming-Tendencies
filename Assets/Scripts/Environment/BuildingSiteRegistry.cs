using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Resolves pre-placed building sites for instant card builds.
    /// </summary>
    public static class BuildingSiteRegistry
    {
        public static bool IsMineBuilding(BuildingSO building)
        {
            if (building == null || string.IsNullOrEmpty(building.Name)) return false;
            string name = building.Name.ToLowerInvariant();
            return name.Contains("mine") || name.Contains("laser") || name.Contains("strip");
        }

        public static bool IsCommandBuilding(BuildingSO building)
        {
            return building != null &&
                   building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
        }

        public static BuildingSiteKind GetRequiredKind(BuildingSO building)
        {
            if (IsCommandBuilding(building)) return BuildingSiteKind.CommandPost;
            if (IsMineBuilding(building)) return BuildingSiteKind.Mine;
            return BuildingSiteKind.Infrastructure;
        }

        public static bool HasAvailableSite(BuildingSO building, Owner owner)
        {
            return GetAvailableSite(building, owner) != null;
        }

        public static BuildingSiteSlot GetAvailableSite(BuildingSO building, Owner owner)
        {
            if (building == null || SectorManager.Instance == null) return null;

            BuildingSiteKind kind = GetRequiredKind(building);
            var candidates = new List<BuildingSiteSlot>();

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.IsLocked) continue;
                if (sector.BuildingSites == null) continue;

                foreach (var site in sector.BuildingSites)
                {
                    if (site == null || site.IsOccupied || site.Kind != kind) continue;
                    if (!IsSiteValidForBuilding(building, site)) continue;
                    candidates.Add(site);
                }
            }

            if (candidates.Count == 0) return null;

            Vector3 reference = GetReferencePosition(owner);
            candidates.Sort((a, b) =>
                Vector3.Distance(reference, a.Position).CompareTo(Vector3.Distance(reference, b.Position)));
            return candidates[0];
        }

        private static bool IsSiteValidForBuilding(BuildingSO building, BuildingSiteSlot site)
        {
            if (!IsMineBuilding(building) || !site.HasLinkedResource) return true;

            // Mine buildings must sit on their geographic resource node.
            SectorNode.NodeType nodeType = site.LinkedResourceType;
            string name = building.Name.ToLowerInvariant();

            if (name.Contains("gas")) return nodeType == SectorNode.NodeType.Gas;
            if (name.Contains("iron")) return nodeType == SectorNode.NodeType.Iron;
            if (name.Contains("regolith") || name.Contains("basalt") || name.Contains("strip"))
                return nodeType == SectorNode.NodeType.Regolith;
            if (name.Contains("laser") || name.Contains("deep"))
                return nodeType == SectorNode.NodeType.Minerals;

            return nodeType == SectorNode.NodeType.Minerals
                || nodeType == SectorNode.NodeType.Gas
                || nodeType == SectorNode.NodeType.Iron
                || nodeType == SectorNode.NodeType.Regolith;
        }

        private static Vector3 GetReferencePosition(Owner owner)
        {
            if (SectorManager.Instance?.ActiveSector != null)
            {
                return SectorManager.Instance.ActiveSector.Center;
            }

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building != null && building.Owner == owner)
                {
                    return building.transform.position;
                }
            }

            return Vector3.zero;
        }

        public static void RegisterOccupancy(BaseBuilding building)
        {
            if (building == null || SectorManager.Instance == null) return;

            BuildingSiteSlot nearest = null;
            float nearestDist = 4f;
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.BuildingSites == null) continue;
                foreach (var site in sector.BuildingSites)
                {
                    if (site == null || site.IsOccupied) continue;
                    float dist = Vector3.Distance(building.transform.position, site.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = site;
                    }
                }
            }

            nearest?.SetOccupied(building);
        }

        public static void ClearOccupancy(BaseBuilding building)
        {
            if (building == null || SectorManager.Instance == null) return;

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.BuildingSites == null) continue;
                foreach (var site in sector.BuildingSites)
                {
                    if (site != null && site.OccupyingBuilding == building)
                    {
                        site.ClearOccupancy();
                    }
                }
            }
        }
    }
}
