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
            if (evt.Building == null) return;

            BuildingSiteRegistry.RegisterOccupancy(evt.Building);

            // Drone-built reserved pads finish later — wire cluster power on completion.
            if (BuildingSiteRegistry.TryGetSiteForBuilding(evt.Building, out _))
            {
                EnsureClusterPowerForBuilding(evt.Building);
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
                    reason = "No available Command Post site on the planet.";
                }
                else if (BuildingSiteRegistry.IsSolarBuilding(building))
                {
                    reason = "No open solar array sites on the planet.";
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

            Vector3 targetPos = SnapToNavMesh(site.Position);
            site.Position = targetPos;
            if (site.MarkerGO != null)
            {
                site.MarkerGO.transform.position = targetPos;
            }

            bool instant = waiveCost || IsFirstPlayerCommandPost(building, owner);

            Worker worker = null;
            if (!instant)
            {
                worker = FindAvailableWorker(owner, targetPos);
                if (worker == null)
                {
                    reason = "A drone is needed.";
                    return false;
                }
            }

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

            // Re-ground after spawn — prefab pivots / child meshes can look airborne.
            GroundBuilding(built);

            built.enabled = true;
            built.Owner = owner;
            built.BindBuildingDefinition(building);

            // Occupy before construction so the pad cannot be double-booked.
            site.SetOccupied(built);
            site.MarkerGO?.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();

            if (site.Cluster?.BuildingSlot?.MarkerGO != null)
            {
                site.Cluster.BuildingSlot.MarkerGO.GetComponent<BuildingSiteMarker>()?.RefreshVisibility();
            }

            if (instant)
            {
                built.CompleteConstruction();
                EnsurePowerNodeReady(built);

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
                PowerGridManager.RecalculateGrids();
                GroundBuilding(built);
            }
            else
            {
                Material ghostMat = building.PlacementMaterial;
                built.InitializeAsGhost(ghostMat, owner);
                GroundBuilding(built);
                worker.ResumeBuilding(built);
                Debug.Log($"[ReservedSiteBuild] {worker.name} assigned to build {building.Name} at {built.transform.position}");
            }

            if (!BuildingSiteRegistry.IsCommandBuilding(building) && !BuildingSiteRegistry.IsSolarBuilding(building))
            {
                BlueprintDraftManager.LockBuilding(building.Name);
            }

            return true;
        }

        private static bool IsFirstPlayerCommandPost(BuildingSO building, Owner owner)
        {
            if (!BuildingSiteRegistry.IsCommandBuilding(building)) return false;

            foreach (var b in BaseBuilding.ActiveBuildings)
            {
                if (b == null || b.Owner != owner || b.BuildingSO == null) continue;
                if (!b.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (b.name.Contains("Clone", System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>Idle construction drone nearest to the build site, or null.</summary>
        private static Worker FindAvailableWorker(Owner owner, Vector3 near)
        {
            Worker best = null;
            float bestDist = float.MaxValue;
            Worker[] workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
            foreach (var worker in workers)
            {
                if (worker == null || worker.Owner != owner) continue;
                if (worker.IsBuilding) continue;

                float dist = (worker.transform.position - near).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = worker;
                }
            }

            return best;
        }

        /// <summary>
        /// Ensure a reserved-site consumer is wired to its cluster solar and the grid
        /// has been recalculated. Safe to call after drone construction completes.
        /// </summary>
        public static void EnsureClusterPowerForBuilding(BaseBuilding built)
        {
            if (built == null) return;
            if (!BuildingSiteRegistry.TryGetSiteForBuilding(built, out BuildingSiteSlot site)) return;

            EnsurePowerNodeReady(built);
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
            PowerGridManager.RecalculateGrids();
        }

        private static void ConnectToClusterSolar(BaseBuilding built, BuildingSiteSlot site)
        {
            if (built == null || site?.Cluster == null) return;
            // Any consumer pad on a solar cluster should auto-wire (paired, infra, or misc).
            if (site.Kind == BuildingSiteKind.Solar || site.Kind == BuildingSiteKind.CommandPost) return;

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

        /// <summary>Pin a building's root to terrain under its XZ (fixes air-spawned themed pads).</summary>
        public static void GroundBuilding(BaseBuilding building)
        {
            if (building == null) return;
            Vector3 grounded = SnapToNavMesh(building.transform.position);
            building.transform.position = grounded;
        }

        private static Vector3 SnapToNavMesh(Vector3 approximate)
        {
            Vector3 grounded = SnapToGround(approximate);

            UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = 0,
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };
            if (UnityEngine.AI.NavMesh.SamplePosition(grounded, out UnityEngine.AI.NavMeshHit navHit, 12f, filter))
            {
                // Prefer NavMesh XZ, but never adopt an elevated/air sample — that leaves
                // buildings (esp. GHG / Oxygen Processor) hovering above the terrain.
                Vector3 navPos = navHit.position;
                if (navPos.y <= grounded.y + 1.25f)
                    return new Vector3(navPos.x, grounded.y, navPos.z);
            }

            return grounded;
        }

        private static Vector3 SnapToGround(Vector3 approximate)
        {
            int terrainMask = LayerMask.GetMask("Terrain");
            int groundMask = LayerMask.GetMask("Default", "Terrain");
            if (groundMask == 0) groundMask = Physics.DefaultRaycastLayers;

            Vector3[] origins =
            {
                new Vector3(approximate.x, approximate.y + 120f, approximate.z),
                new Vector3(approximate.x, 200f, approximate.z),
            };

            foreach (Vector3 origin in origins)
            {
                if (terrainMask != 0
                    && Physics.Raycast(origin, Vector3.down, out RaycastHit terrainHit, 400f, terrainMask,
                        QueryTriggerInteraction.Ignore))
                {
                    return terrainHit.point;
                }

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f, groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    if (IsIgnorableGroundHit(hit)) continue;
                    return hit.point;
                }
            }

            // Last resort: any non-trigger collider that is not a building/pad ghost.
            if (Physics.Raycast(new Vector3(approximate.x, 200f, approximate.z), Vector3.down,
                    out RaycastHit anyHit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !IsIgnorableGroundHit(anyHit))
            {
                return anyHit.point;
            }

            return new Vector3(approximate.x, approximate.y, approximate.z);
        }

        private static bool IsIgnorableGroundHit(RaycastHit hit)
        {
            if (hit.collider == null) return true;
            if (hit.collider.isTrigger) return true;
            if (hit.collider.GetComponentInParent<BaseBuilding>() != null) return true;
            if (hit.collider.GetComponentInParent<BuildingSiteMarker>() != null) return true;
            if (hit.collider.GetComponentInParent<AbstractUnit>() != null) return true;
            return false;
        }

        private static bool HasEnoughMaterials(BuildingSO building, Owner owner)
        {
            if (building == null) return false;
            if (Supplies.Materials == null || !Supplies.Materials.TryGetValue(owner, out int materials))
            {
                return false;
            }

            int materialsCost = GetMaterialsCost(building);
            return materialsCost <= materials;
        }

        private static bool ConsumeMaterials(BuildingSO building, Owner owner)
        {
            if (building == null) return false;

            _ = Supplies.Materials;

            if (!Supplies.Materials.TryGetValue(owner, out int materials))
            {
                return false;
            }

            int materialsCost = GetMaterialsCost(building);
            if (materialsCost <= 0)
            {
                // Never treat a real building as free to place.
                materialsCost = Mathf.Max(150, building.Cost != null ? building.Cost.Minerals : 150);
                Debug.LogWarning($"[ReservedSiteBuild] {building.Name} resolved to 0 cost — charging {materialsCost} Materials.");
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

        public static int GetMaterialsCost(BuildingSO building)
        {
            if (building == null) return 0;

            float mineralRate = Supplies.MineralsToMaterialsRateStatic;
            float gasRate = Supplies.GasToMaterialsRateStatic;
            // Uninitialized / zero rates would make every card show Free.
            if (mineralRate <= 0.0001f) mineralRate = 1f;
            if (gasRate <= 0.0001f) gasRate = 1f;

            if (building.Cost != null)
            {
                int configured = Mathf.FloorToInt(
                    building.Cost.Minerals * mineralRate +
                    building.Cost.Gas * gasRate);
                // Prefer raw minerals when conversion somehow collapses to 0.
                if (configured <= 0 && building.Cost.Minerals > 0)
                    configured = building.Cost.Minerals;
                if (configured > 0) return configured;
            }

            // Themed BuildingSOs often shipped with Cost=null — keep play priced.
            string goal = UnlockBuildingCardSO.ClassifyBuildingGoal(building);
            return goal switch
            {
                "COMMAND POST" => 400,
                "POWER" => 100,
                "ATMOSPHERE" or "TEMPERATURE" or "WATER" or "OXYGEN" => 150,
                "POPULATION" => 150,
                "MATERIALS" => 200,
                _ => 150
            };
        }
    }
}
