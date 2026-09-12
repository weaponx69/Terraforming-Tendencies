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
            // Exact Solar Panel only — "Solar Greenhouse" is habitat, not a generator.
            return building.Name.IndexOf("Solar Panel", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>True when the building produces grid power (Solar, Geothermal, Magnetic Shield, …).</summary>
        public static bool IsPowerGeneratorBuilding(BuildingSO building)
        {
            if (building == null) return false;
            if (IsSolarBuilding(building)) return true;
            var cfg = building.BuildingConfig;
            return cfg != null && cfg.PowerGeneration > 0f;
        }

        public static bool IsMineBuilding(BuildingSO building)
        {
            if (building == null || string.IsNullOrEmpty(building.Name)) return false;
            string name = building.Name.ToLowerInvariant();
            // Atmosphere "Import Laser" must NOT match — that is a climate pad building.
            if (name.Contains("import") || name.Contains("carbon dioxide")) return false;
            return name.Contains("mine")
                || name.Contains("strip")
                || name.Contains("deep-core mining laser")
                || name.Contains("mining laser");
        }

        public static bool IsCommandBuilding(BuildingSO building)
        {
            return building != null &&
                   building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Expansion Command Post only — not Sector Command Center / other "Command" names.
        /// </summary>
        public static bool IsCommandPostBuilding(BuildingSO building)
        {
            return building != null
                && building.Name.IndexOf("Command Post", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static BuildingSiteKind GetRequiredKind(BuildingSO building)
        {
            if (IsCommandPostBuilding(building)) return BuildingSiteKind.CommandPost;
            if (IsMineBuilding(building)) return BuildingSiteKind.Mine;
            if (IsSolarBuilding(building)) return BuildingSiteKind.Solar;
            return BuildingSiteKind.PairedBuilding;
        }

        /// <summary>True once PlanetGenerator has placed at least one reserved pad.</summary>
        public static bool HasRegisteredSites()
        {
            if (SectorManager.Instance?.Sectors == null) return false;
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.BuildingSites != null && sector.BuildingSites.Count > 0)
                    return true;
            }
            return false;
        }

        public static bool HasAvailableSite(BuildingSO building, Owner owner)
        {
            return GetEligibleSites(building, owner, visibleToPlayerOnly: false).Count > 0;
        }

        public static BuildingSiteSlot GetAvailableSite(BuildingSO building, Owner owner)
        {
            var eligible = GetEligibleSites(building, owner, visibleToPlayerOnly: false);
            if (eligible.Count == 0) return null;

            Vector3 reference = GetReferencePosition(owner);
            eligible.Sort((a, b) =>
                Vector3.Distance(reference, a.Position).CompareTo(Vector3.Distance(reference, b.Position)));
            return eligible[0];
        }

        public static List<BuildingSiteSlot> GetEligibleSites(
            BuildingSO building,
            Owner owner,
            bool visibleToPlayerOnly = true)
        {
            var candidates = new List<BuildingSiteSlot>();
            if (building == null || SectorManager.Instance == null) return candidates;

            BuildingSiteKind kind = GetRequiredKind(building);

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                // Whole-planet pads: sector lock no longer gates eligibility.
                if (sector == null || sector.BuildingSites == null) continue;

                foreach (var site in sector.BuildingSites)
                {
                    if (site == null) continue;
                    // Card selection passes visibleToPlayerOnly:false so every open pad on the map is offered.
                    if (visibleToPlayerOnly && !IsSiteVisibleToPlayer(site)) continue;
                    if (site.OccupyingBuilding != null && !BuildingSiteSlot.IsValidOccupant(site.OccupyingBuilding))
                    {
                        site.ClearOccupancy();
                    }
                    if (site.IsOccupied) continue;
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

        public static bool IsSiteVisibleToPlayer(BuildingSiteSlot site)
        {
            // No FoW — all pads visible (Combolands-style full map).
            return site != null;
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
            if (!BuildingSiteSlot.IsValidOccupant(building) || SectorManager.Instance == null) return;

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

        /// <summary>Find the reserved pad this building occupies, if any.</summary>
        public static bool TryGetSiteForBuilding(BaseBuilding building, out BuildingSiteSlot site)
        {
            site = null;
            if (building == null || SectorManager.Instance == null) return false;

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.BuildingSites == null) continue;
                foreach (var candidate in sector.BuildingSites)
                {
                    if (candidate?.OccupyingBuilding == building)
                    {
                        site = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Solar on a cluster pad should power the adjacent building slot only,
        /// not auto-merge into the Command Post grid.
        /// </summary>
        public static bool IsClusterSolar(BaseBuilding building)
        {
            return TryGetSiteForBuilding(building, out BuildingSiteSlot site)
                && site.Kind == BuildingSiteKind.Solar
                && site.Cluster != null;
        }

        public static void RefreshAllMarkers()
        {
            if (SectorManager.Instance == null) return;

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector?.BuildingSites == null) continue;
                foreach (var site in sector.BuildingSites)
                {
                    if (site == null) continue;
                    if (site.OccupyingBuilding != null && !BuildingSiteSlot.IsValidOccupant(site.OccupyingBuilding))
                    {
                        site.ClearOccupancy();
                    }
                    site.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
                }
            }
        }
    }
}
