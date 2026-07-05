using System;
using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class SectorManager : MonoBehaviour
    {
        public static SectorManager Instance { get; private set; }

        public enum SectorFeature { None, Volcano, FaultLine, LavaTube, WaterDeposit }

        [System.Serializable]
        public class Sector
        {
            public Vector3 Center;
            public bool IsOccupied;
            public BaseBuilding OccupyingBuilding;
            public bool IsLocked = true;
            public bool IsExplored = false;
            public bool IsDiscovered = false;   // "???" state — partial visibility showing node markers
            public SectorFeature Feature = SectorFeature.None;
            public List<SectorNode> Nodes = new List<SectorNode>();  // Pre-placed nodes in this sector
        }

        public List<Sector> Sectors = new List<Sector>();
        public Sector ActiveSector { get; set; }

        /// <summary>Fired whenever a previously locked sector becomes unlocked.</summary>
        public static event Action OnSectorUnlocked;

        /// <summary>Fired when a sector is explored (scouted) but not necessarily unlocked yet.</summary>
        public static event Action<int> OnSectorExplored;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            PlanetGenerator.OnPlanetGenerated += InitializeSectors;
        }

        private void OnDisable()
        {
            PlanetGenerator.OnPlanetGenerated -= InitializeSectors;
        }

        private void Start()
        {
            InitializeSectors();
        }

        public void InitializeSectors()
        {
            var pg = PlanetGenerator.Instance;
            if (pg == null) pg = UnityEngine.Object.FindAnyObjectByType<PlanetGenerator>();
            
            if (pg == null || pg.Config == null) 
            {
                Debug.LogWarning("[SectorManager] Cannot initialize sectors: PlanetGenerator or Config is null.");
                return;
            }

            var config = pg.Config;
            float cellSize = pg.CellSize;
            float worldWidth = config.MapWidth * cellSize;
            float worldHeight = config.MapHeight * cellSize;

            Sectors.Clear();

            float secW = worldWidth / config.SectorsX;
            float secH = worldHeight / config.SectorsY;

            for (int y = 0; y < config.SectorsY; y++)
            {
                for (int x = 0; x < config.SectorsX; x++)
                {
                    Vector3 center = new Vector3(
                        (x + 0.5f) * secW,
                        0,
                        (y + 0.5f) * secH
                    );

                    // Snap to ground height
                    if (Physics.Raycast(center + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f, LayerMask.GetMask("Default", "Terrain")))
                    {
                        center.y = hit.point.y;
                    }

                    bool isFirst = (x == 0 && y == 0);
                    SectorFeature feature = SectorFeature.None;
                    if (!isFirst)
                    {
                        int featureIndex = 1 + ((Sectors.Count - 1) % 4);
                        feature = (SectorFeature)featureIndex;
                    }
                    Sectors.Add(new Sector { Center = center, IsOccupied = false, IsLocked = !isFirst, IsExplored = isFirst, Feature = feature });
                }
            }
            
            if (Sectors.Count > 0)
            {
                ActiveSector = Sectors[0];

                // Force-discover Minerals and Gas in Sector 0 so the player can bootstrap.
                // All other sectors require discovery cards to reveal resource types.
                DiscoverySystem.RevealResourceType("Minerals");
                DiscoverySystem.RevealResourceType("Gas");

                DiscoverResourcesInUnlockedSectors();
                OnSectorUnlocked?.Invoke();
            }
            Debug.Log($"[SectorManager] Initialized {Sectors.Count} sectors for {worldWidth}x{worldHeight} map. Sector 0 is unlocked.");
        }

        public void DiscoverResourcesInUnlockedSectors()
        {
            var hiddenResources = UnityEngine.Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            foreach (var hr in hiddenResources)
            {
                if (hr == null || hr.IsDiscovered) continue;

                var nearestSector = GetNearestSector(hr.transform.position);
                if (nearestSector != null && !nearestSector.IsLocked)
                {
                    hr.Discover();
                }
            }
        }

        private bool hasDoneStartingDiscovery = false;

        private void Update()
        {
            if (Sectors.Count == 0 && Time.timeSinceLevelLoad > 1f)
            {
                InitializeSectors();
            }
            UpdateOccupancy();

            if (!hasDoneStartingDiscovery && Sectors.Count > 0 && Time.timeSinceLevelLoad > 0.1f)
            {
                hasDoneStartingDiscovery = true;
                DiscoverResourcesInUnlockedSectors();
                Debug.Log("[SectorManager] Completed deferred starting resource discovery for unlocked sectors!");
            }
        }

        private void UpdateOccupancy()
        {
            foreach (var sector in Sectors)
            {
                bool found = false;
                foreach (var building in BaseBuilding.ActiveBuildings)
                {
                    if (building.Owner != GameOverManager.MonitoredOwner) continue;
                    if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                    
                    // Check if it's a Command Post (using name contains check like in CompleteConstruction)
                    bool isCommandPost = building.BuildingSO != null && building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    if (!isCommandPost) continue;

                    if (GetNearestSector(building.transform.position) == sector)
                    {
                        found = true;
                        sector.OccupyingBuilding = building;
                        if (ActiveSector == null) ActiveSector = sector;
                        break;
                    }
                }
                sector.IsOccupied = found;
                if (!found) sector.OccupyingBuilding = null;
            }
        }

        public Sector GetNearestSector(Vector3 position)
        {
            if (Sectors == null || Sectors.Count == 0) return null;

            Sector nearest = null;
            float minDistance = float.MaxValue;

            foreach (var sector in Sectors)
            {
                float dist = Vector3.Distance(position, sector.Center);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest = sector;
                }
            }

            return nearest;
        }

        public bool AreAllSectorsOccupied()
        {
            if (Sectors.Count == 0) 
            {
                InitializeSectors();
                if (Sectors.Count == 0) return false;
            }

            foreach (var s in Sectors)
            {
                if (!s.IsOccupied) return false;
            }
            return true;
        }

        public void UnlockNextSector()
        {
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].IsLocked)
                {
                    // Only unlock if the sector has been explored first
                    if (!Sectors[i].IsExplored)
                    {
                        Debug.LogWarning($"[SectorManager] Cannot unlock Sector {i} — it has not been explored yet. Use scouting cards to explore it first.");
                        return;
                    }

                    Sectors[i].IsLocked = false;
                    ActiveSector = Sectors[i];
                    Debug.Log($"[SectorManager] Sector {i} unlocked! It is now the active sector.");
                    DiscoverResourcesInUnlockedSectors();
                    OnSectorUnlocked?.Invoke();
                    return; // Only unlock one at a time
                }
            }
            Debug.Log("[SectorManager] All sectors are already unlocked!");
        }

        /// <summary>Mark a sector as discovered (partial visibility — shows "???" markers).</summary>
        public void DiscoverSector(int index)
        {
            if (index < 0 || index >= Sectors.Count) return;
            if (Sectors[index].IsDiscovered) return;
            if (!Sectors[index].IsLocked) return;  // Already unlocked, no need for discovery state

            Sectors[index].IsDiscovered = true;
            Debug.Log($"[SectorManager] Sector {index} discovered — showing markers!");
        }

        /// <summary>Mark a specific sector as explored (fully revealed) and unlock it.</summary>
        public void FullyExploreSector(int index)
        {
            if (index < 0 || index >= Sectors.Count) return;
            if (Sectors[index].IsExplored) return;

            Sectors[index].IsDiscovered = true;
            Sectors[index].IsExplored = true;
            Sectors[index].IsLocked = false;
            ActiveSector = Sectors[index];
            OnSectorExplored?.Invoke(index);
            Debug.Log($"[SectorManager] Sector {index} fully explored and unlocked!");
        }

        /// <summary>Mark a specific sector as explored. Fires OnSectorExplored event.</summary>
        public void ExploreSector(int index)
        {
            if (index < 0 || index >= Sectors.Count) return;
            if (Sectors[index].IsExplored) return;

            Sectors[index].IsExplored = true;
            OnSectorExplored?.Invoke(index);
            Debug.Log($"[SectorManager] Sector {index} explored (scouted).");
        }

        /// <summary>Reveal all hidden nodes in a sector (make them visible in scene).</summary>
        public void RevealNodesInSector(int index)
        {
            if (index < 0 || index >= Sectors.Count) return;
            var sector = Sectors[index];
            foreach (var node in sector.Nodes)
            {
                node.isRevealed = true;
                node.SetVisualVisible(true);
            }

            // Also force-discover any HiddenResource components in this sector
            var hiddenResources = UnityEngine.Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            Vector3 secMin = sector.Center - new Vector3(50f, 0, 50f);
            Vector3 secMax = sector.Center + new Vector3(50f, 0, 50f);
            foreach (var hr in hiddenResources)
            {
                if (hr == null || hr.IsDiscovered) continue;
                Vector3 pos = hr.transform.position;
                if (pos.x >= secMin.x && pos.x <= secMax.x &&
                    pos.z >= secMin.z && pos.z <= secMax.z)
                {
                    hr.ForceDiscover();
                }
            }
        }

        /// <summary>
        /// Check if a sector has a specific feature (e.g., LavaTube) that has been revealed.
        /// </summary>
        public bool SectorHasFeature(int sectorIndex, SectorFeature feature)
        {
            if (sectorIndex < 0 || sectorIndex >= Sectors.Count) return false;
            var sector = Sectors[sectorIndex];
            if (!sector.IsExplored) return false;
            return sector.Feature == feature;
        }

        /// <summary>Explore the next locked sector. Returns the index, or -1 if none remain.</summary>
        public int ExploreNextSector()
        {
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].IsLocked && !Sectors[i].IsExplored)
                {
                    Sectors[i].IsExplored = true;
                    OnSectorExplored?.Invoke(i);
                    Debug.Log($"[SectorManager] Sector {i} explored (scouted).");
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Get the index of the next locked sector, or -1 if none remain.</summary>
        public int GetNextLockedSectorIndex()
        {
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].IsLocked) return i;
            }
            return -1;
        }

        /// <summary>Get the index of the next locked AND unexplored sector, or -1 if none.</summary>
        public int GetNextUnexploredSectorIndex()
        {
            for (int i = 0; i < Sectors.Count; i++)
            {
                if (Sectors[i].IsLocked && !Sectors[i].IsExplored) return i;
            }
            return -1;
        }

        private void OnDrawGizmos()
        {
            if (Sectors == null) return;
            foreach (var sector in Sectors)
            {
                Gizmos.color = sector.IsOccupied ? Color.green : Color.red;
                Gizmos.DrawWireSphere(sector.Center, 2f);
                
                if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
                {
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f);
                    Gizmos.DrawWireSphere(sector.Center, PlanetGenerator.Instance.Config.SectorOccupationRadius);
                }
            }
        }
    }
}
