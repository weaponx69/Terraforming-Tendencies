using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Builds at pre-placed reserved sites when the player plays a building card.
    /// </summary>
    public static class ReservedSiteBuildUtility
    {
        private static bool subscribed;
        private static bool isBuildingReservedSite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandleBuildingDeath;
            SectorManager.OnSectorUnlocked += BuildingSiteRegistry.RefreshAllMarkers;
            PlanetGenerator.OnPlanetGenerated += BuildingSiteRegistry.RefreshAllMarkers;
            HexGridManager.OnStartingAreaRevealed += BuildingSiteRegistry.RefreshAllMarkers;
        }

        private static void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building != null)
            {
                BuildingSiteRegistry.RegisterOccupancy(evt.Building);
            }
        }

        private static void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building != null)
            {
                BuildingSiteRegistry.ClearOccupancy(evt.Building);
            }
        }

        public static bool CanAffordBuilding(BuildingSO building, Owner owner)
        {
            return building != null && building.Prefab != null && HasEnoughMaterials(building, owner);
        }

        public static bool CanBuildAtReservedSite(BuildingSO building, Owner owner, out string reason, bool requireUnlocked = true)
        {
            reason = null;
            if (building == null)
            {
                reason = "No building specified.";
                return false;
            }

            if (building.Prefab == null)
            {
                reason = $"{building.Name} has no Prefab assigned.";
                return false;
            }

            if (!BuildingSiteRegistry.HasAvailableSite(building, owner))
            {
                if (BuildingSiteRegistry.IsMineBuilding(building))
                {
                    reason = $"No available mine site for {building.Name}. Explore resource deposits first.";
                }
                else if (BuildingSiteRegistry.IsCommandBuilding(building))
                {
                    reason = "No available Command Post site in an unlocked sector.";
                }
                else if (BuildingSiteRegistry.IsSolarBuilding(building))
                {
                    reason = "No open solar array sites in unlocked sectors.";
                }
                else
                {
                    reason = $"No powered building sites for {building.Name}. Build a Solar Panel first, then pick its cluster.";
                }
                return false;
            }

            if (!HasEnoughMaterials(building, owner))
            {
                int cost = GetMaterialsCost(building);
                int have = Supplies.Materials != null && Supplies.Materials.TryGetValue(owner, out int m) ? m : 0;
                reason = $"Not enough materials for {building.Name} (need {cost}, have {have}).";
                return false;
            }

            // Card plays unlock on apply — never gate reserved-site eligibility on blueprint unlock.
            // Free-placement command locks / restriction spheres are also skipped here; pads are pre-placed.
            _ = requireUnlocked;
            return true;
        }

        public static bool TryBuildAtReservedSite(BuildingSO building, Owner owner, out string reason)
        {
            var site = BuildingSiteRegistry.GetAvailableSite(building, owner);
            if (site == null)
            {
                reason = $"No available site for {building?.Name}.";
                return false;
            }

            return TryBuildAtSite(building, owner, site, out reason);
        }

        public static bool TryBuildAtSite(BuildingSO building, Owner owner, BuildingSiteSlot site, out string reason, bool waiveCost = false)
        {
            reason = null;
            if (isBuildingReservedSite)
            {
                reason = "A reserved-site build is already in progress.";
                return false;
            }

            if (building == null || site == null)
            {
                reason = "No building or site specified.";
                return false;
            }

            isBuildingReservedSite = true;
            try
            {
                return TryBuildAtSiteInternal(building, owner, site, out reason, waiveCost);
            }
            finally
            {
                isBuildingReservedSite = false;
            }
        }

        private static bool TryBuildAtSiteInternal(BuildingSO building, Owner owner, BuildingSiteSlot site, out string reason, bool waiveCost = false)
        {
            reason = null;
            if (building == null || site == null)
            {
                reason = "No building or site specified.";
                return false;
            }

            if (site.IsOccupied)
            {
                reason = "That build site is already occupied.";
                return false;
            }

            // Auto-claim colonization may target a pad that is not yet fog-visible;
            // skip the normal eligibility list when waiving cost for that path.
            if (!waiveCost && !BuildingSiteRegistry.GetEligibleSites(building, owner).Contains(site))
            {
                reason = BuildingSiteRegistry.IsSolarBuilding(building)
                    ? "That solar site is not available."
                    : "That building site needs its own solar array first.";
                return false;
            }

            if (building.Prefab == null)
            {
                reason = $"{building.Name} has no Prefab assigned.";
                return false;
            }

            if (!waiveCost && !HasEnoughMaterials(building, owner))
            {
                reason = $"Not enough materials to build {building.Name}.";
                return false;
            }

            // Reserved pads are pre-authored — do not re-run free-placement IsLocked /
            // AllRestrictionsPass (nearby solar, rocks, and card-not-yet-unlocked all
            // falsely rejected pad clicks).

            Vector3 targetPos = SnapToNavMesh(site.Position);

            if (!waiveCost && !ConsumeMaterials(building, owner))
            {
                reason = $"Not enough materials to build {building.Name}.";
                return false;
            }

            GameObject instance = Object.Instantiate(building.Prefab, targetPos, Quaternion.identity);
            if (!instance.TryGetComponent(out BaseBuilding built))
            {
                Object.Destroy(instance);
                reason = $"Failed to spawn {building.Name}.";
                return false;
            }

            built.enabled = true;
            built.Owner = owner;

            // Occupy before CompleteConstruction so IsClusterSolar / solar-skip logic
            // sees the pad on the same frame the CP auto-wire coroutine starts.
            site.SetOccupied(built);
            site.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();

            if (site.Cluster?.BuildingSlot?.MarkerGO != null)
            {
                site.Cluster.BuildingSlot.MarkerGO.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
            }

            built.CompleteConstruction();
            EnsurePowerNodeReady(built);

            // Ensure power nodes are grid-registered before cluster wiring (Start may not
            // have run yet in the same frame as Instantiate).
            if (site.Cluster?.SolarBuilding != null)
            {
                EnsurePowerNodeReady(site.Cluster.SolarBuilding);
            }

            if (site.Kind == BuildingSiteKind.Solar &&
                built.TryGetComponent(out PowerNode solarNode))
            {
                DisconnectCommandPostLinks(solarNode);
            }

            ConnectToClusterSolar(built, site);

            // Force a grid pass after occupancy + wiring so UnpoweredIndicator clears this frame.
            PowerGridManager.RecalculateGrids();

            if (!BuildingSiteRegistry.IsCommandBuilding(building) && !BuildingSiteRegistry.IsSolarBuilding(building))
            {
                BlueprintDraftManager.LockBuilding(building.Name);
            }

            Debug.Log($"[ReservedSiteBuild] Built {building.Name} at reserved site {targetPos}");
            return true;
        }

        private static void ConnectToClusterSolar(BaseBuilding built, BuildingSiteSlot site)
        {
            if (built == null || site?.Cluster == null) return;
            if (site.Kind != BuildingSiteKind.PairedBuilding && site.Kind != BuildingSiteKind.Infrastructure) return;

            BaseBuilding solar = site.Cluster.SolarBuilding;
            if (solar == null)
            {
                Debug.LogWarning($"[ReservedSiteBuild] No solar on cluster for {built.name}; power will need a manual Connect Power.");
                return;
            }

            if (!built.TryGetComponent(out PowerNode consumerNode))
            {
                consumerNode = built.gameObject.AddComponent<PowerNode>();
            }
            if (!solar.TryGetComponent(out PowerNode solarNode))
            {
                solarNode = solar.gameObject.AddComponent<PowerNode>();
            }

            EnsurePowerNodeReady(built);
            EnsurePowerNodeReady(solar);

            DisconnectCommandPostLinks(solarNode);

            if (!consumerNode.ConnectedNodes.Contains(solarNode))
            {
                consumerNode.ConnectTo(solarNode);
                Debug.Log($"[ReservedSiteBuild] Connected {built.name} to cluster solar {solar.name}.");
            }
        }

        private static void DisconnectCommandPostLinks(PowerNode node)
        {
            if (node == null) return;

            var commandLinks = new List<PowerNode>();
            foreach (var other in node.ConnectedNodes)
            {
                if (other?.Building?.BuildingSO?.Name != null &&
                    other.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                {
                    commandLinks.Add(other);
                }
            }

            foreach (var commandNode in commandLinks)
            {
                node.DisconnectFrom(commandNode);
            }
        }

        private static void EnsurePowerNodeReady(BaseBuilding building)
        {
            if (building == null) return;
            if (!building.TryGetComponent(out PowerNode node))
            {
                node = building.gameObject.AddComponent<PowerNode>();
            }

            // Ensure a clickable collider exists for Connect Power targeting.
            if (building.GetComponentInChildren<Collider>() == null)
            {
                var box = building.gameObject.AddComponent<BoxCollider>();
                box.center = Vector3.up * 1.5f;
                box.size = new Vector3(6f, 4f, 6f);
            }

            PowerGridManager.RegisterNode(node);
        }

        private static BuildBuildingCommand CreateCommand(BuildingSO building)
        {
            var cmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
            cmd.Building = building;
            cmd.Name = "Build " + building.Name;
            cmd.Icon = building.Icon;
            cmd.GhostPrefab = building.Prefab;

            var templates = Resources.FindObjectsOfTypeAll<BuildBuildingCommand>();
            foreach (var template in templates)
            {
                if (template != null && template.Building != null &&
                    template.Building.Name == building.Name)
                {
                    if (template.GhostPrefab != null)
                    {
                        cmd.GhostPrefab = template.GhostPrefab;
                    }
                    break;
                }
            }

            return cmd;
        }

        private static CommandContext CreateContext(Owner owner, Vector3 position)
        {
            var hit = new RaycastHit { point = position };
            AbstractCommandable commandable = Object.FindAnyObjectByType<GlobalCommander>();
            if (commandable == null)
            {
                Worker[] workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
                foreach (var worker in workers)
                {
                    if (worker != null && worker.Owner == owner)
                    {
                        commandable = worker;
                        break;
                    }
                }
            }

            return new CommandContext(owner, commandable, hit, 0);
        }

        private static Vector3 SnapToNavMesh(Vector3 approximate)
        {
            Vector3 grounded = SnapToGround(approximate);

            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = 0,
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };
            if (UnityEngine.AI.NavMesh.SamplePosition(grounded, out UnityEngine.AI.NavMeshHit navHit, 8f, filter))
            {
                // Prefer NavMesh XZ, but never adopt an elevated/air sample — that leaves
                // buildings (esp. Oxygen Processor) hovering above the terrain.
                Vector3 navPos = navHit.position;
                if (Mathf.Abs(navPos.y - grounded.y) <= 1.25f)
                    return new Vector3(navPos.x, grounded.y, navPos.z);
            }

            return grounded;
        }

        private static Vector3 SnapToGround(Vector3 approximate)
        {
            Vector3 origin = approximate + Vector3.up * 80f;
            int mask = LayerMask.GetMask("Default", "Terrain");
            if (mask == 0) mask = Physics.DefaultRaycastLayers;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, mask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            // Fallback: unrestricted raycast, but reject hits that are clearly elevated meshes.
            if (Physics.Raycast(origin, Vector3.down, out hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y <= approximate.y + 2f)
                    return hit.point;
            }

            return new Vector3(approximate.x, approximate.y, approximate.z);
        }

        private static bool HasEnoughMaterials(BuildingSO building, Owner owner)
        {
            if (building == null || building.Cost == null) return true;
            if (Supplies.Materials == null || !Supplies.Materials.TryGetValue(owner, out int materials))
            {
                return false;
            }

            int materialsCost = GetMaterialsCost(building);
            return materialsCost <= materials;
        }

        private static bool ConsumeMaterials(BuildingSO building, Owner owner)
        {
            if (building == null || building.Cost == null)
            {
                return true;
            }

            _ = Supplies.Materials;

            if (!Supplies.Materials.TryGetValue(owner, out int materials))
            {
                return false;
            }

            int materialsCost = GetMaterialsCost(building);
            if (materialsCost <= 0)
            {
                Debug.LogWarning($"[ReservedSiteBuild] {building.Name} has zero materials cost configured.");
                return true;
            }

            if (materialsCost > materials)
            {
                return false;
            }

            int remaining = materials - materialsCost;
            Supplies.Materials[owner] = remaining;
            Supplies.RaiseMaterialsChanged(owner, remaining);
            Debug.Log($"[ReservedSiteBuild] Spent {materialsCost} materials on {building.Name}. Remaining: {remaining}");
            return true;
        }

        private static int GetMaterialsCost(BuildingSO building)
        {
            return Mathf.FloorToInt(
                building.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic +
                building.Cost.Gas * Supplies.GasToMaterialsRateStatic);
        }
    }
}
