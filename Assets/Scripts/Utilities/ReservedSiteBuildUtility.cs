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

        public static bool CanBuildAtReservedSite(BuildingSO building, Owner owner, out string reason, bool requireUnlocked = true)
        {
            reason = null;
            if (building == null)
            {
                reason = "No building specified.";
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
                reason = $"Not enough materials to build {building.Name}.";
                return false;
            }

            if (!requireUnlocked)
            {
                return true;
            }

            var site = BuildingSiteRegistry.GetAvailableSite(building, owner);
            var cmd = CreateCommand(building);
            var context = CreateContext(owner, site.Position);
            if (cmd.IsLocked(context))
            {
                reason = $"Cannot build {building.Name} yet (locked or insufficient materials).";
                Object.Destroy(cmd);
                return false;
            }

            if (!cmd.AllRestrictionsPass(SnapToNavMesh(site.Position), owner, requireWorker: false))
            {
                reason = $"Cannot build {building.Name} at the reserved site right now.";
                Object.Destroy(cmd);
                return false;
            }

            Object.Destroy(cmd);
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

        public static bool TryBuildAtSite(BuildingSO building, Owner owner, BuildingSiteSlot site, out string reason)
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
                return TryBuildAtSiteInternal(building, owner, site, out reason);
            }
            finally
            {
                isBuildingReservedSite = false;
            }
        }

        private static bool TryBuildAtSiteInternal(BuildingSO building, Owner owner, BuildingSiteSlot site, out string reason)
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

            if (!BuildingSiteRegistry.GetEligibleSites(building, owner).Contains(site))
            {
                reason = BuildingSiteRegistry.IsSolarBuilding(building)
                    ? "That solar site is not available."
                    : "That building site needs its own solar array first.";
                return false;
            }

            if (!HasEnoughMaterials(building, owner))
            {
                reason = $"Not enough materials to build {building.Name}.";
                return false;
            }

            var cmd = CreateCommand(building);
            var context = CreateContext(owner, site.Position);
            if (cmd.IsLocked(context))
            {
                reason = $"Cannot build {building.Name} yet (locked or insufficient materials).";
                Object.Destroy(cmd);
                return false;
            }

            if (!cmd.AllRestrictionsPass(SnapToNavMesh(site.Position), owner, requireWorker: false))
            {
                reason = $"Cannot build {building.Name} at the reserved site right now.";
                Object.Destroy(cmd);
                return false;
            }

            Object.Destroy(cmd);

            Vector3 targetPos = SnapToNavMesh(site.Position);

            if (!ConsumeMaterials(building, owner))
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
            built.CompleteConstruction();
            EnsurePowerNodeReady(built);

            // Ensure power nodes are grid-registered before cluster wiring (Start may not
            // have run yet in the same frame as Instantiate).
            if (site.Cluster?.SolarBuilding != null)
            {
                EnsurePowerNodeReady(site.Cluster.SolarBuilding);
            }

            site.SetOccupied(built);
            site.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();

            if (site.Cluster?.BuildingSlot?.MarkerGO != null)
            {
                site.Cluster.BuildingSlot.MarkerGO.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
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

            if (!consumerNode.ConnectedNodes.Contains(solarNode))
            {
                consumerNode.ConnectTo(solarNode);
                Debug.Log($"[ReservedSiteBuild] Connected {built.name} to cluster solar {solar.name}.");
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
                Worker[] workers = Object.FindObjectsByType<Worker>(FindObjectsSortMode.None);
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
            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = 0,
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };
            if (UnityEngine.AI.NavMesh.SamplePosition(approximate, out UnityEngine.AI.NavMeshHit navHit, 20f, filter))
            {
                return navHit.position;
            }

            return approximate;
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
