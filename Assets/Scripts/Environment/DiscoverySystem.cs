using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Tracks which resource types have been discovered by the player.
    /// Resources of undiscovered types remain hidden on the map.
    /// Default discovered types: Minerals, Gas (always visible from start).
    /// Iron and Regolith must be discovered via Discovery cards.
    /// </summary>
    public static class DiscoverySystem
    {
        /// <summary>
        /// Default discovered types are empty — even Minerals and Gas must be discovered via cards
        /// or via Sector 0 force-discovery. This creates real scarcity: players must find deposits
        /// before they can mine them.
        /// </summary>
        private static HashSet<string> discoveredTypes = new();

        /// <summary>Check if a resource type has been discovered.</summary>
        public static bool IsTypeDiscovered(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            return discoveredTypes.Contains(typeName);
        }

        /// <summary>Reveal a resource type, making all nodes of that type visible in explored sectors.</summary>
        public static void RevealResourceType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return;

            if (discoveredTypes.Add(typeName))
            {
                Debug.Log($"[DiscoverySystem] Resource type '{typeName}' discovered!");
                DiscoverAllNodesOfType(typeName);
            }
        }

        /// <summary>Get all currently discovered resource type names.</summary>
        public static HashSet<string> GetDiscoveredTypes()
        {
            return new HashSet<string>(discoveredTypes);
        }

        /// <summary>
        /// Get all resource types that exist in at least one explored sector.
        /// Used for card curation — only show discovery cards for types that exist somewhere.
        /// </summary>
        public static HashSet<string> GetResourceTypesInExploredSectors()
        {
            var types = new HashSet<string>();
            var hiddenResources = Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            foreach (var hr in hiddenResources)
            {
                if (hr == null) continue;
                var sector = SectorManager.Instance?.GetNearestSector(hr.transform.position);
                if (sector != null && sector.IsExplored && !string.IsNullOrEmpty(hr.ResourceTypeName))
                {
                    types.Add(hr.ResourceTypeName);
                }
            }
            // Only return types that actually exist in explored sectors — no hardcoded defaults
            return types;
        }

        /// <summary>Discover all HiddenResource nodes of a given type in explored sectors.</summary>
        private static void DiscoverAllNodesOfType(string typeName)
        {
            var hiddenResources = Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            int count = 0;
            foreach (var hr in hiddenResources)
            {
                if (hr == null || hr.IsDiscovered) continue;
                if (hr.ResourceTypeName != typeName) continue;

                var sector = SectorManager.Instance?.GetNearestSector(hr.transform.position);
                if (sector != null && sector.IsExplored)
                {
                    hr.ForceDiscover();
                    count++;
                }
            }
            if (count > 0)
            {
                Debug.Log($"[DiscoverySystem] Revealed {count} '{typeName}' nodes in explored sectors.");
            }
        }

        /// <summary>Reset all discoveries (called on game restart).</summary>
        public static void Reset()
        {
            discoveredTypes.Clear();
            // No default types — Sector 0 force-discovery handles the starting resources
        }

        /// <summary>
        /// Resource type a mine building needs (Basalt→Regolith, Deep-Core→Minerals, etc.).
        /// Returns false for non-mine buildings.
        /// </summary>
        public static bool TryGetMineResourceType(BuildingSO building, out string resourceType)
        {
            resourceType = null;
            if (!BuildingSiteRegistry.IsMineBuilding(building)) return false;

            string name = building.Name.ToLowerInvariant();
            if (name.Contains("gas")) resourceType = "Gas";
            else if (name.Contains("iron")) resourceType = "Iron";
            else if (name.Contains("regolith") || name.Contains("basalt") || name.Contains("strip"))
                resourceType = "Regolith";
            else if (name.Contains("laser") || name.Contains("deep"))
                resourceType = "Minerals";
            else
                resourceType = "Minerals";
            return true;
        }

        /// <summary>True if at least one deposit of the mine's resource type has been discovered.</summary>
        public static bool HasDiscoveredMineDeposit(BuildingSO building)
        {
            if (!TryGetMineResourceType(building, out string type)) return true;
            return HasDiscoveredDepositOfType(type);
        }

        public static bool HasDiscoveredDepositOfType(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType)) return false;
            if (!IsTypeDiscovered(resourceType)) return false;

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!string.Equals(hr.ResourceTypeName, resourceType, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// True when <paramref name="worldPos"/> sits on the same colony tile as a matching
        /// discovered deposit (mines may only be built on the deposit spot).
        /// </summary>
        public static bool IsOnDiscoveredMineDeposit(BuildingSO building, Vector3 worldPos)
        {
            if (!TryGetMineResourceType(building, out string type)) return true;
            var cell = ColonyTileGrid.WorldToCell(worldPos);
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!string.Equals(hr.ResourceTypeName, type, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ColonyTileGrid.WorldToCell(hr.transform.position) == cell)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Legacy name — same as <see cref="IsOnDiscoveredMineDeposit"/> (on-spot, not nearby).
        /// </summary>
        public static bool HasDiscoveredMineDepositNear(BuildingSO building, Vector3 worldPos, float radius = 28f)
        {
            return IsOnDiscoveredMineDeposit(building, worldPos);
        }

        /// <summary>
        /// If the cursor is near a matching discovered deposit, snap to that deposit's tile.
        /// Returns false when no deposit is in range (ghost stays on free grid snap).
        /// </summary>
        public static bool TrySnapToMineDeposit(
            BuildingSO building,
            Vector3 cursorWorld,
            out Vector3 snapped,
            out Vector2Int cell,
            float maxDist = -1f)
        {
            snapped = cursorWorld;
            cell = ColonyTileGrid.WorldToCell(cursorWorld);
            if (!TryGetMineResourceType(building, out string type)) return false;

            float max = maxDist > 0f ? maxDist : ColonyTileGrid.TileSize * 1.25f;
            float bestDist = max;
            HiddenResource best = null;
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!string.Equals(hr.ResourceTypeName, type, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                float d = Mathf.Sqrt(ColonyTileGrid.HorizontalDistSq(cursorWorld, hr.transform.position));
                if (d > bestDist) continue;
                bestDist = d;
                best = hr;
            }

            if (best == null) return false;
            cell = ColonyTileGrid.WorldToCell(best.transform.position);
            snapped = ColonyTileGrid.CellToWorld(cell, cursorWorld.y);
            return true;
        }
    }
}