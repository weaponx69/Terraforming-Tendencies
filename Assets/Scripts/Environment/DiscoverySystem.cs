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
        private static readonly HashSet<SectorManager.SectorFeature> discoveredFeatures = new();

        /// <summary>Fired when a mine resource type or sector geological feature is newly revealed.</summary>
        public static event System.Action OnDiscoveryChanged;

        /// <summary>Check if a resource type has been discovered.</summary>
        public static bool IsTypeDiscovered(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            return discoveredTypes.Contains(typeName);
        }

        /// <summary>True if this sector geological feature has been revealed to the player.</summary>
        public static bool IsSectorFeatureDiscovered(SectorManager.SectorFeature feature)
        {
            if (feature == SectorManager.SectorFeature.None) return true;
            return discoveredFeatures.Contains(feature);
        }

        /// <summary>Player-facing name for a sector geology feature.</summary>
        public static string DescribeSectorFeature(SectorManager.SectorFeature feature)
        {
            return feature switch
            {
                SectorManager.SectorFeature.WaterDeposit => "Water Deposit",
                SectorManager.SectorFeature.Glacier => "Glacier",
                SectorManager.SectorFeature.Volcano => "Volcano",
                SectorManager.SectorFeature.FaultLine => "Fault Line",
                SectorManager.SectorFeature.LavaTube => "Lava Tube",
                _ => feature.ToString()
            };
        }

        /// <summary>
        /// Sector feature a building requires (Aquifer→WaterDeposit, Subglacial→Glacier, …).
        /// Returns false when the building is free-place climate/infra (no geology gate).
        /// </summary>
        public static bool TryGetRequiredSectorFeature(BuildingSO building, out SectorManager.SectorFeature feature)
        {
            feature = SectorManager.SectorFeature.None;
            if (building == null || string.IsNullOrEmpty(building.Name)) return false;

            string name = building.Name;
            // Subglacial extractors need Glacier sectors (polar ice sheets).
            if (name.IndexOf("Subglacial", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                feature = SectorManager.SectorFeature.Glacier;
                return true;
            }
            // Aquifers / biosphere / water-ice sit on WaterDeposit sectors.
            if (name.IndexOf("Aquifer", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Biosphere", System.StringComparison.OrdinalIgnoreCase) >= 0
                || (name.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && name.IndexOf("Ice", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                feature = SectorManager.SectorFeature.WaterDeposit;
                return true;
            }
            if (name.IndexOf("Lava Tube", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Subterranean", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                feature = SectorManager.SectorFeature.LavaTube;
                return true;
            }
            if (name.IndexOf("Sector Command", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Magnetic Shield", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                feature = SectorManager.SectorFeature.FaultLine;
                return true;
            }
            // Geothermal power plants sit on Volcano sectors.
            if (name.IndexOf("Geothermal", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                feature = SectorManager.SectorFeature.Volcano;
                return true;
            }
            return false;
        }

        /// <summary>Short placement hint for a required geology feature.</summary>
        public static string DescribeFeaturePlacementHint(SectorManager.SectorFeature feature)
        {
            return feature switch
            {
                SectorManager.SectorFeature.Glacier =>
                    "look for white Glacier markers at the poles",
                SectorManager.SectorFeature.WaterDeposit =>
                    "look for cyan Water Deposit markers",
                SectorManager.SectorFeature.Volcano =>
                    "look for orange Volcanic Vent markers",
                SectorManager.SectorFeature.FaultLine =>
                    "look for Fault Line markers (Q/E)",
                SectorManager.SectorFeature.LavaTube =>
                    "look for Lava Tube markers (Q/E)",
                _ => "Q/E to find the matching sector"
            };
        }

        /// <summary>
        /// True when <paramref name="worldPos"/> is inside a sector that has the building's
        /// required geology feature (Aquifer → WaterDeposit, etc.). Free-place buildings always pass.
        /// </summary>
        public static bool IsOnRequiredSectorFeature(BuildingSO building, Vector3 worldPos)
        {
            if (!TryGetRequiredSectorFeature(building, out var feature)) return true;
            var nearest = SectorManager.Instance?.GetNearestSector(worldPos);
            return nearest != null && nearest.Feature == feature;
        }

        /// <summary>First free tile in a matching feature sector (for aquifer auto-place / snap).</summary>
        public static bool TryGetAutoFeaturePlacement(BuildingSO building, out Vector3 worldPos, out string failReason)
        {
            worldPos = Vector3.zero;
            failReason = null;
            if (!TryGetRequiredSectorFeature(building, out var feature))
            {
                failReason = "No geology feature required.";
                return false;
            }
            if (!IsSectorFeatureDiscovered(feature))
            {
                failReason = $"Discover a {DescribeSectorFeature(feature)} sector first.";
                return false;
            }
            if (SectorManager.Instance == null || SectorManager.Instance.Sectors == null)
            {
                failReason = "No sectors.";
                return false;
            }

            var occupied = ColonyTileGrid.GetOccupiedCells(Owner.Player1);
            SectorManager.Sector focus = SectorManager.Instance.GetClimateFocusSector();
            Vector3? focusPick = null;
            Vector3? anyPick = null;

            foreach (var sector in SectorManager.Instance.Sectors)
            {
                if (sector == null || sector.Feature != feature) continue;
                Vector3 candidate = sector.Center;
                // Prefer the feature node marker when present.
                if (sector.Nodes != null)
                {
                    foreach (var node in sector.Nodes)
                    {
                        if (node != null && node.type == SectorNode.NodeType.Feature)
                        {
                            candidate = node.position;
                            break;
                        }
                    }
                }

                var cell = ColonyTileGrid.WorldToCell(candidate);
                if (occupied.Contains(cell))
                {
                    // Nudge to a free orthogonal neighbor.
                    bool foundFree = false;
                    Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(2, 0), new(0, 2) };
                    foreach (var d in dirs)
                    {
                        var n = new Vector2Int(cell.x + d.x, cell.y + d.y);
                        if (occupied.Contains(n)) continue;
                        candidate = ColonyTileGrid.CellToWorld(n, candidate.y);
                        cell = n;
                        foundFree = true;
                        break;
                    }
                    if (!foundFree) continue;
                }

                anyPick ??= candidate;
                if (focus != null && sector == focus)
                {
                    focusPick = candidate;
                    break;
                }
            }

            Vector3? pick = focusPick ?? anyPick;
            if (!pick.HasValue)
            {
                failReason = $"No free tile in a {DescribeSectorFeature(feature)} sector.";
                return false;
            }

            worldPos = ColonyTileGrid.CellToWorld(ColonyTileGrid.WorldToCell(pick.Value), pick.Value.y);
            return true;
        }

        /// <summary>
        /// Re-apply type discovery to nodes spawned after the initial RevealResourceType call
        /// (planet gen creates HiddenResources after SectorManager reveals Minerals/Gas).
        /// </summary>
        public static void RefreshDiscoveredResourceNodes()
        {
            foreach (string type in discoveredTypes)
                DiscoverAllNodesOfType(type);
        }

        /// <summary>
        /// False when the card must stay out of hand: undiscovered mine deposit
        /// or undiscovered sector geological feature.
        /// </summary>
        public static bool IsBuildingGeologicallyAvailable(BuildingSO building)
        {
            if (building == null) return false;
            if (BuildingSiteRegistry.IsMineBuilding(building)
                && !HasDiscoveredMineDeposit(building))
                return false;
            if (TryGetRequiredSectorFeature(building, out var feature)
                && !IsSectorFeatureDiscovered(feature))
                return false;
            return true;
        }

        /// <summary>Reveal a resource type, making all nodes of that type visible in explored sectors.</summary>
        public static void RevealResourceType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return;

            if (discoveredTypes.Add(typeName))
            {
                Debug.Log($"[DiscoverySystem] Resource type '{typeName}' discovered!");
                DiscoverAllNodesOfType(typeName);
                OnDiscoveryChanged?.Invoke();
            }
        }

        /// <summary>Reveal a sector geological feature (WaterDeposit, LavaTube, FaultLine, …).</summary>
        public static void RevealSectorFeature(SectorManager.SectorFeature feature)
        {
            if (feature == SectorManager.SectorFeature.None) return;
            if (!discoveredFeatures.Add(feature)) return;
            Debug.Log($"[DiscoverySystem] Sector feature '{feature}' discovered!");
            OnDiscoveryChanged?.Invoke();
        }

        /// <summary>Reveal whatever geological feature the given sector has (if any).</summary>
        public static void RevealFeaturesForSector(SectorManager.Sector sector)
        {
            if (sector == null) return;
            RevealSectorFeature(sector.Feature);
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
            discoveredFeatures.Clear();
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
        /// True when the tile hosts any mineable deposit (Minerals/Gas/Iron/Regolith),
        /// discovered or not. Non-mine buildings must not place here.
        /// </summary>
        public static bool IsOnAnyMineableDeposit(Vector3 worldPos)
        {
            var cell = ColonyTileGrid.WorldToCell(worldPos);
            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include))
            {
                if (hr == null || !IsMineableResourceType(hr.ResourceTypeName)) continue;
                if (ColonyTileGrid.WorldToCell(hr.transform.position) == cell)
                    return true;
            }
            return false;
        }

        public static bool IsMineableResourceType(string resourceType)
        {
            if (string.IsNullOrEmpty(resourceType)) return false;
            return resourceType.Equals("Minerals", System.StringComparison.OrdinalIgnoreCase)
                || resourceType.Equals("Gas", System.StringComparison.OrdinalIgnoreCase)
                || resourceType.Equals("Iron", System.StringComparison.OrdinalIgnoreCase)
                || resourceType.Equals("Regolith", System.StringComparison.OrdinalIgnoreCase);
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

        /// <summary>
        /// First free matching discovered deposit tile for auto-build (focus sector preferred).
        /// </summary>
        public static bool TryGetAutoMinePlacement(BuildingSO building, out Vector3 worldPos, out string failReason)
        {
            worldPos = Vector3.zero;
            failReason = null;
            if (!TryGetMineResourceType(building, out string type))
            {
                failReason = "Not a mine.";
                return false;
            }
            if (!HasDiscoveredMineDeposit(building))
            {
                failReason = $"No discovered {type} deposit yet.";
                return false;
            }

            var occupied = ColonyTileGrid.GetOccupiedCells(Owner.Player1);
            HiddenResource focusBest = null;
            HiddenResource anyBest = null;
            var focus = SectorManager.Instance != null ? SectorManager.Instance.GetClimateFocusSector() : null;

            foreach (var hr in Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr == null || !hr.IsDiscovered) continue;
                if (!string.Equals(hr.ResourceTypeName, type, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var cell = ColonyTileGrid.WorldToCell(hr.transform.position);
                if (occupied.Contains(cell)) continue;

                anyBest ??= hr;
                if (focus != null && SectorManager.Instance != null
                    && SectorManager.Instance.GetNearestSector(hr.transform.position) == focus)
                {
                    focusBest = hr;
                    break;
                }
            }

            var chosen = focusBest != null ? focusBest : anyBest;
            if (chosen == null)
            {
                failReason = $"All discovered {type} deposits already have a mine.";
                return false;
            }

            var placeCell = ColonyTileGrid.WorldToCell(chosen.transform.position);
            worldPos = ColonyTileGrid.CellToWorld(placeCell, chosen.transform.position.y);
            return true;
        }
    }
}