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
        public static bool IsSolarBuilding(BuildingSO building)
        {
            if (building == null || string.IsNullOrEmpty(building.Name)) return false;
            string name = building.Name.ToLowerInvariant();
            return name.Contains("solar");
        }

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
            if (IsSolarBuilding(building)) return BuildingSiteKind.Solar;
            return BuildingSiteKind.PairedBuilding;
        }

        public static bool HasAvailableSite(BuildingSO building, Owner owner)
        {
            return GetEligibleSites(building, owner).Count > 0;
        }

        public static BuildingSiteSlot GetAvailableSite(BuildingSO building, Owner owner)
        {
            var eligible = GetEligibleSites(building, owner);
            if (eligible.Count == 0) return null;

            Vector3 reference = GetReferencePosition(owner);
            eligible.Sort((a, b) =>
                Vector3.Distance(reference, a.Position).CompareTo(Vector3.Distance(reference, b.Position)));
            return eligible[0];
        }

        public static List<BuildingSiteSlot> GetEligibleSites(BuildingSO building, Owner owner)
        {
            var candidates = new List<BuildingSiteSlot>();
            if (building == null || SectorManager.Instance == null) return candidates;

            BuildingSiteKind kind = GetRequiredKind(building);

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.IsLocked) continue;
                if (sector.BuildingSites == null) continue;

                foreach (var site in sector.BuildingSites)
                {
                    if (site == null || site.IsOccupied) continue;
                    if (site.Kind != kind && !(kind == BuildingSiteKind.PairedBuilding && site.Kind == BuildingSiteKind.Infrastructure))
                    {
                        continue;
                    }

                    if (!IsSiteValidForBuilding(building, site)) continue;
                    if (!IsClusterValidForBuilding(building, site)) continue;
                    candidates.Add(site);
                }
            }

            return candidates;
        }

        private static bool IsClusterValidForBuilding(BuildingSO building, BuildingSiteSlot site)
        {
            if (site.Cluster == null) return true;

            if (IsSolarBuilding(building))
            {
                return site.Cluster.CanPlaceSolar;
            }

            if (GetRequiredKind(building) == BuildingSiteKind.PairedBuilding)
            {
                return site.Cluster.CanPlaceBuilding;
            }

            return true;
        }

        private static bool IsSiteValidForBuilding(BuildingSO building, BuildingSiteSlot site)
        {
            if (!IsMineBuilding(building) || !site.HasLinkedResource) return true;

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

            if (nearest != null)
            {
                nearest.SetOccupied(building);
                nearest.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
            }
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
                        site.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
                    }
                }
            }
        }
    }
}
