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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            Bus<BuildingSpawnEvent>.OnEvent[Owner.Player1] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandleBuildingDeath;
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
                else
                {
                    reason = $"No available build site for {building.Name}.";
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
            if (!CanBuildAtReservedSite(building, owner, out reason))
            {
                return false;
            }

            var site = BuildingSiteRegistry.GetAvailableSite(building, owner);
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

            site.SetOccupied(built);
            if (site.MarkerGO != null)
            {
                site.MarkerGO.SetActive(false);
            }

            if (!BuildingSiteRegistry.IsCommandBuilding(building))
            {
                BlueprintDraftManager.LockBuilding(building.Name);
            }

            Debug.Log($"[ReservedSiteBuild] Built {building.Name} at reserved site {targetPos}");
            return true;
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
            if (building == null || building.Cost == null) return true;
            if (Supplies.Materials == null || !Supplies.Materials.TryGetValue(owner, out int materials))
            {
                return false;
            }

            int materialsCost = GetMaterialsCost(building);
            if (materialsCost > materials) return false;

            Supplies.UpdateMaterials(owner, materials - materialsCost);
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
