using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class ColonyExpansionManager : MonoBehaviour
    {
        public static ColonyExpansionManager Instance { get; private set; }

        private GameObject ghostPrefab;
        private GameObject realPrefab;

        private Dictionary<SectorManager.Sector, EnergyPipelineManager> activeExpansions = new Dictionary<SectorManager.Sector, EnergyPipelineManager>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            LoadPrefabs();
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] += HandlePlayerBuildingDeath;
        }

        private void OnDestroy()
        {
            Bus<BuildingDeathEvent>.OnEvent[Owner.Player1] -= HandlePlayerBuildingDeath;
        }

        private void HandlePlayerBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building != null && evt.Building.BuildingSO != null 
                && evt.Building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                if (evt.Building.Progress.State != BuildingProgress.BuildingState.Completed)
                {
                    if (SectorManager.Instance != null)
                    {
                        var sector = SectorManager.Instance.GetNearestSector(evt.Building.transform.position);
                        if (sector != null)
                        {
                            VetoSector(sector);
                        }
                    }
                }
            }
        }

        private void LoadPrefabs()
        {
#if UNITY_EDITOR
            ghostPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post Ghost Variant.prefab");
            realPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Units/Buildings/Command Post/Command Post.prefab");
#endif
            if (ghostPrefab == null)
            {
                ghostPrefab = Resources.Load<GameObject>("Buildings/Command Post Ghost Variant");
            }
            if (realPrefab == null)
            {
                realPrefab = Resources.Load<GameObject>("Buildings/Command Post");
            }
        }

        public bool IsExpandingToSector(SectorManager.Sector sector)
        {
            if (sector == null) return false;
            return activeExpansions.ContainsKey(sector) && activeExpansions[sector] != null;
        }

        public void StartExpansion(Vector3 position, SectorManager.Sector sector)
        {
            if (sector == null || IsExpandingToSector(sector)) return;

            // Spawn the ghost blueprint
            GameObject ghostObj = Instantiate(ghostPrefab, position, Quaternion.identity);

            // Ensure colliders are enabled as triggers for click detection
            foreach (var col in ghostObj.GetComponentsInChildren<Collider>())
            {
                col.enabled = true;
                col.isTrigger = true;
            }

            // Attach pipeline manager
            EnergyPipelineManager pipelineManager = ghostObj.AddComponent<EnergyPipelineManager>();
            pipelineManager.Initialize(position, sector, realPrefab);

            activeExpansions[sector] = pipelineManager;
        }

        public void ClearExpansion(SectorManager.Sector sector)
        {
            if (sector != null && activeExpansions.ContainsKey(sector))
            {
                activeExpansions.Remove(sector);
            }
        }

        private HashSet<SectorManager.Sector> vetoedSectors = new HashSet<SectorManager.Sector>();

        public void VetoSector(SectorManager.Sector sector)
        {
            if (sector != null)
            {
                vetoedSectors.Add(sector);
                ClearExpansion(sector);
            }
        }

        public bool IsSectorVetoed(SectorManager.Sector sector)
        {
            return sector != null && vetoedSectors.Contains(sector);
        }
    }
}
