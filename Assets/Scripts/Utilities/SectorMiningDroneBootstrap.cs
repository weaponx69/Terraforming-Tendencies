using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// Colony Acts: one working Mining Drone unit per sector Act, spawned in-world
    /// (not as a hand card) near that sector's Command Post.
    /// </summary>
    public static class SectorMiningDroneBootstrap
    {
        private static readonly HashSet<int> grantedSectors = new();

        public static void ResetForNewRun() => grantedSectors.Clear();

        /// <summary>Call when a player Command Post finishes — grants the free drone for that sector once.</summary>
        public static bool TryGrantForCommandPost(BaseBuilding commandPost)
        {
            if (commandPost == null || commandPost.Owner != Owner.Player1) return false;
            if (commandPost.BuildingSO == null
                || commandPost.BuildingSO.Name == null
                || commandPost.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            int sectorIndex = ResolveSectorIndex(commandPost.transform.position);
            if (grantedSectors.Contains(sectorIndex)) return false;

            if (!SpawnWorkingMiningDrone(commandPost, out string fail))
            {
                Debug.LogWarning($"[SectorMiningDroneBootstrap] Failed to spawn free drone for sector {sectorIndex}: {fail}");
                return false;
            }

            grantedSectors.Add(sectorIndex);
            Debug.Log($"[SectorMiningDroneBootstrap] Spawned free working Mining Drone for sector {sectorIndex}.");
            return true;
        }

        /// <summary>After between-Act Continue — spawn if a Command Post already exists in the focus sector.</summary>
        public static bool TryGrantForFocusSector()
        {
            var sm = SectorManager.Instance;
            var acts = ColonyActManager.Instance;
            if (sm == null || acts == null || !acts.IsRunActive) return false;

            int sectorIndex = acts.FocusSectorIndex;
            if (grantedSectors.Contains(sectorIndex)) return false;

            BaseBuilding cp = FindCommandPostInSector(sectorIndex);
            if (cp == null) return false;

            return TryGrantForCommandPost(cp);
        }

        private static int ResolveSectorIndex(Vector3 worldPos)
        {
            var sm = SectorManager.Instance;
            if (sm == null || sm.Sectors == null || sm.Sectors.Count == 0) return 0;
            var nearest = sm.GetNearestSector(worldPos);
            if (nearest == null) return 0;
            int idx = sm.Sectors.IndexOf(nearest);
            return idx >= 0 ? idx : 0;
        }

        private static BaseBuilding FindCommandPostInSector(int sectorIndex)
        {
            var sm = SectorManager.Instance;
            if (sm == null || sm.Sectors == null || sectorIndex < 0 || sectorIndex >= sm.Sectors.Count)
                return null;

            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building.Owner != Owner.Player1) continue;
                if (building.BuildingSO == null || building.BuildingSO.Name == null) continue;
                if (building.BuildingSO.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (ResolveSectorIndex(building.transform.position) == sectorIndex)
                    return building;
            }

            return null;
        }

        private static bool SpawnWorkingMiningDrone(BaseBuilding homeBase, out string failReason)
        {
            failReason = null;
            var miningDroneSO = Resources.Load<AbstractUnitSO>("Units/MiningDrone");
            if (miningDroneSO == null || miningDroneSO.Prefab == null)
            {
                failReason = "MiningDrone UnitSO / Prefab missing from Resources/Units.";
                return false;
            }

            Vector3 approx = homeBase.transform.position + new Vector3(5f, 0f, 5f);
            int agentType = 0;
            var prefabAgent = miningDroneSO.Prefab.GetComponent<NavMeshAgent>();
            if (prefabAgent != null) agentType = prefabAgent.agentTypeID;

            if (!NavMeshSpawnUtility.TryGetSpawnPosition(approx, agentType, out Vector3 spawnPos))
            {
                if (NavMesh.SamplePosition(approx, out NavMeshHit hit, 20f, NavMesh.AllAreas))
                    spawnPos = hit.position;
                else
                    spawnPos = approx;
            }

            GameObject instance = Object.Instantiate(miningDroneSO.Prefab, spawnPos, Quaternion.identity);
            if (instance.TryGetComponent(out AbstractUnit unit))
                unit.Owner = Owner.Player1;
            else if (instance.TryGetComponent(out AbstractCommandable commandable))
                commandable.Owner = Owner.Player1;

            if (instance.TryGetComponent(out NavMeshAgent agent))
                NavMeshSpawnUtility.EnsureAgentOnNavMesh(agent);

            if (instance.TryGetComponent(out Worker worker))
                worker.BeginAutoGather(homeBase);

            return true;
        }
    }
}
