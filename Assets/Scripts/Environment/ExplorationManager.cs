using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Environment
{
    /// <summary>
    /// Central controller for sector exploration mechanics.
    /// Exploration happens through scouting cards (Orbital Scan, Survey Drone)
    /// rather than a physical FoundryCrawler unit.
    ///
    /// When a sector is explored, it becomes eligible for unlocking.
    /// Exploration also triggers resource replenishment in the newly explored sector.
    /// </summary>
    public class ExplorationManager : MonoBehaviour
    {
        public static ExplorationManager Instance { get; private set; }

        [Header("Exploration Settings")]
        [Tooltip("Base time (seconds) for passive exploration to complete.")]
        [SerializeField] private float baseExplorationTime = 30f;

        [Tooltip("Current exploration speed multiplier (modified by Pipeline Boost cards).")]
        [SerializeField] private float explorationSpeedMultiplier = 1f;

        /// <summary>Whether exploration is currently in progress.</summary>
        public bool IsExploring { get; private set; }

        /// <summary>Exploration progress from 0 to 1.</summary>
        public float ExplorationProgress { get; private set; }

        /// <summary>Fired when exploration progress changes (0-1).</summary>
        public static event System.Action<float> OnExplorationProgressChanged;

        /// <summary>Fired when a sector has been explored.</summary>
        public static event System.Action<int> OnSectorExplored;

        private float explorationTimer;
        private float boostedMultiplier = 1f;
        private float boostEndTime;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Handle temporary boost expiration
            if (boostedMultiplier > 1f && Time.time >= boostEndTime)
            {
                boostedMultiplier = 1f;
                Debug.Log("[ExplorationManager] Exploration boost expired.");
            }

            if (IsExploring)
            {
                float effectiveMultiplier = explorationSpeedMultiplier * boostedMultiplier;
                explorationTimer += Time.deltaTime * effectiveMultiplier;
                ExplorationProgress = Mathf.Clamp01(explorationTimer / baseExplorationTime);
                OnExplorationProgressChanged?.Invoke(ExplorationProgress);

                if (ExplorationProgress >= 1f)
                {
                    CompleteExploration();
                }
            }
        }

        /// <summary>
        /// Instantly explore the next locked sector (Orbital Scan card).
        /// Skips the timer — immediate exploration + unlock.
        /// </summary>
        public void InstantExplore()
        {
            if (SectorManager.Instance == null) return;

            int nextIndex = SectorManager.Instance.GetNextLockedSectorIndex();
            if (nextIndex < 0)
            {
                Debug.Log("[ExplorationManager] No locked sectors remain to explore.");
                return;
            }

            SectorManager.Instance.ExploreSector(nextIndex);
            ReplenishNewSector(nextIndex);     // Add resources FIRST so the draft sees them
            SectorManager.Instance.UnlockNextSector(); // Triggers draft via OnSectorUnlocked

            Debug.Log($"[ExplorationManager] Orbital Scan: Sector {nextIndex} instantly explored and unlocked!");
        }

        /// <summary>
        /// Begin passive exploration of the next sector.
        /// Progresses over baseExplorationTime seconds.
        /// </summary>
        public void BeginExploration()
        {
            if (SectorManager.Instance == null) return;

            int nextIndex = SectorManager.Instance.GetNextLockedSectorIndex();
            if (nextIndex < 0)
            {
                Debug.Log("[ExplorationManager] No locked sectors remain to explore.");
                return;
            }

            IsExploring = true;
            explorationTimer = 0f;
            ExplorationProgress = 0f;
            OnExplorationProgressChanged?.Invoke(0f);
            Debug.Log($"[ExplorationManager] Beginning exploration of sector {nextIndex}...");
        }

        /// <summary>
        /// Boost exploration speed temporarily (Pipeline Boost card).
        /// </summary>
        public void BoostExplorationSpeed(float multiplier, float duration)
        {
            boostedMultiplier = multiplier;
            boostEndTime = Time.time + duration;
            Debug.Log($"[ExplorationManager] Exploration speed boosted to {multiplier}x for {duration}s.");
        }

        /// <summary>
        /// Deploy a disposable survey drone (Survey Drone card).
        /// The drone scouts ahead and instantly explores the next sector.
        /// </summary>
        public void DeploySurveyDrone()
        {
            // Same effect as InstantExplore for now — the drone is a flavor wrapper.
            // In future, could spawn a physical probe unit that flies to the sector.
            InstantExplore();
            Debug.Log("[ExplorationManager] Survey Drone deployed — sector explored!");
        }

        private void CompleteExploration()
        {
            IsExploring = false;
            ExplorationProgress = 1f;

            if (SectorManager.Instance == null) return;

            int nextIndex = SectorManager.Instance.GetNextLockedSectorIndex();
            if (nextIndex < 0) return;

            SectorManager.Instance.ExploreSector(nextIndex);
            ReplenishNewSector(nextIndex);     // Add resources FIRST so the draft sees them
            SectorManager.Instance.UnlockNextSector(); // Triggers draft via OnSectorUnlocked

            OnSectorExplored?.Invoke(nextIndex);
            Debug.Log($"[ExplorationManager] Sector {nextIndex} exploration complete!");
        }

        private void ReplenishNewSector(int sectorIndex)
        {
            if (PlanetGenerator.Instance != null && SectorManager.Instance != null)
            {
                var sector = SectorManager.Instance.Sectors[sectorIndex];
                PlanetGenerator.Instance.ReplenishResourcesInSector(sector);
            }
        }
    }
}