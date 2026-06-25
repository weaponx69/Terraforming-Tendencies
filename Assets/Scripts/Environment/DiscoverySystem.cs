using System.Collections.Generic;
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
    }
}