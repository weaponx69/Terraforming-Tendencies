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
            public SectorFeature Feature = SectorFeature.None;
        }

        public List<Sector> Sectors = new List<Sector>();
        public Sector ActiveSector { get; set; }

        /// <summary>Fired whenever a previously locked sector becomes unlocked.</summary>
        public static event Action OnSectorUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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
                    Sectors.Add(new Sector { Center = center, IsOccupied = false, IsLocked = !isFirst, Feature = feature });
                }
            }
            
            if (Sectors.Count > 0)
            {
                ActiveSector = Sectors[0];
                OnSectorUnlocked?.Invoke();
            }
            Debug.Log($"[SectorManager] Initialized {Sectors.Count} sectors for {worldWidth}x{worldHeight} map. Sector 0 is unlocked.");
        }

        private void Update()
        {
            if (Sectors.Count == 0 && Time.timeSinceLevelLoad > 1f)
            {
                InitializeSectors();
            }
            UpdateOccupancy();
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
                    Sectors[i].IsLocked = false;
                    ActiveSector = Sectors[i];
                    Debug.Log($"[SectorManager] Sector {i} unlocked! It is now the active sector.");
                    OnSectorUnlocked?.Invoke();
                    return; // Only unlock one at a time
                }
            }
            Debug.Log("[SectorManager] All sectors are already unlocked!");
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
