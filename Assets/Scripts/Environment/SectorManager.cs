using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    public class SectorManager : MonoBehaviour
    {
        public static SectorManager Instance { get; private set; }

        [System.Serializable]
        public class Sector
        {
            public Vector3 Center;
            public bool IsOccupied;
            public BaseBuilding OccupyingBuilding;
        }

        public List<Sector> Sectors = new List<Sector>();

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
            if (pg == null) pg = Object.FindAnyObjectByType<PlanetGenerator>();
            
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

                    Sectors.Add(new Sector { Center = center, IsOccupied = false });
                }
            }
            Debug.Log($"[SectorManager] Initialized {Sectors.Count} sectors for {worldWidth}x{worldHeight} map.");
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
            var pg = PlanetGenerator.Instance;
            if (pg == null) pg = Object.FindAnyObjectByType<PlanetGenerator>();
            if (pg == null || pg.Config == null) return;

            float radius = pg.Config.SectorOccupationRadius;

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

                    if (Vector3.Distance(building.transform.position, sector.Center) <= radius)
                    {
                        found = true;
                        sector.OccupyingBuilding = building;
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
