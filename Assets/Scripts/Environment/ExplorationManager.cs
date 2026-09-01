using UnityEngine;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine.Internal;

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

        /// <summary>
        /// Auto-spawn the ExplorationManager when the game starts.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<ExplorationManager>() != null) return;
            var go = new GameObject("ExplorationManager (auto)");
            go.AddComponent<ExplorationManager>();
            DontDestroyOnLoad(go);
        }

        [Header("Exploration Settings")]
        [Tooltip("Base time (seconds) for passive exploration to complete.")]
        [SerializeField] private float baseExplorationTime = 30f;

        [Tooltip("Current exploration speed multiplier (modified by Pipeline Boost cards).")]
        [SerializeField] private float explorationSpeedMultiplier = 1f;

        [Tooltip("Energy spent to commit exploration of a frontier node.")]
        [SerializeField] private float exploreEnergyCost = 1f;

        public float ExploreEnergyCost => exploreEnergyCost;

        /// <summary>Fired when exploration is blocked (insufficient energy, no target, etc.).</summary>
        public static event System.Action<string> OnExplorationFailed;

        /// <summary>Whether exploration is currently in progress.</summary>
        public bool IsExploring { get; private set; }

        /// <summary>Exploration progress from 0 to 1.</summary>
        public float ExplorationProgress { get; private set; }

        /// <summary>Fired when exploration progress changes (0-1).</summary>
        public static event System.Action<float> OnExplorationProgressChanged;

        /// <summary>Fired when a sector has been explored.</summary>
#pragma warning disable CS0067
        public static event System.Action<int> OnSectorExplored;
#pragma warning restore CS0067

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

        public bool CanAffordExploration(Owner owner = Owner.Player1)
        {
            float current = Supplies.Energy != null && Supplies.Energy.TryGetValue(owner, out float energy)
                ? energy
                : 0f;
            return current >= exploreEnergyCost;
        }

        /// <summary>
        /// Orbital Scan: playable when a locked sector remains and energy is available.
        /// Unlike frontier scouting, it does not require an existing "?" node.
        /// </summary>
        public bool CanOrbitalScan(Owner owner = Owner.Player1)
        {
            if (SectorManager.Instance == null) return false;
            if (SectorManager.Instance.GetNextLockedSectorIndex() < 0) return false;
            if (!GenerationManager.CanUnlockNextMapSector()) return false;
            return CanAffordExploration(owner);
        }

        public bool HasFrontierNode(out SectorNode node, out int sectorIndex)
        {
            node = null;
            sectorIndex = -1;
            if (SectorManager.Instance == null) return false;

            for (int i = 0; i < SectorManager.Instance.Sectors.Count; i++)
            {
                var sector = SectorManager.Instance.Sectors[i];
                foreach (var candidate in sector.Nodes)
                {
                    if (candidate.isDiscovered && !candidate.isExplored)
                    {
                        node = candidate;
                        sectorIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsValidExploreTarget(SectorNode node)
        {
            return node != null && node.isDiscovered && !node.isExplored;
        }

        private bool TrySpendExplorationEnergy(Owner owner = Owner.Player1)
        {
            if (!CanAffordExploration(owner))
            {
                ReportExplorationFailed($"Need {exploreEnergyCost:0.#} Energy to explore.");
                return false;
            }

            float current = Supplies.Energy[owner];
            Supplies.UpdateEnergy(owner, current - exploreEnergyCost);
            return true;
        }

        /// <summary>
        /// Instantly explore the nearest discovered-but-unexplored node (Orbital Scan card).
        /// Node-by-node exploration: reveals one node, shows "?" on its connections.
        /// </summary>
        public bool TryExploreFrontier(Owner owner = Owner.Player1)
        {
            if (!HasFrontierNode(out SectorNode targetNode, out int targetSectorIndex))
            {
                ReportExplorationFailed("No frontier nodes to explore. Scout from an explored node first.");
                return false;
            }

            return TryExploreNode(targetNode, targetSectorIndex, owner);
        }

        /// <summary>
        /// Orbital Scan: unlocks / explores into the next locked sector when one remains.
        /// Falls back to a frontier "?" node only when every sector is already unlocked.
        /// </summary>
        public bool TryOrbitalScan(Owner owner = Owner.Player1)
        {
            if (SectorManager.Instance == null)
            {
                ReportExplorationFailed("No SectorManager — cannot orbital scan.");
                return false;
            }

            if (!GenerationManager.CanUnlockNextMapSector())
            {
                ReportExplorationFailed("Finish this sector's terraforming goals before opening the next sector.");
                return false;
            }

            int next = SectorManager.Instance.GetNextLockedSectorIndex();
            if (next >= 0)
            {
                var sector = SectorManager.Instance.Sectors[next];
                if (sector?.Nodes == null || sector.Nodes.Count == 0)
                {
                    ReportExplorationFailed($"Sector {next} has no nodes to scan.");
                    return false;
                }

                SectorNode target = FindCrossSectorEntryNode(next) ?? sector.Nodes[0];
                target.isDiscovered = true;
                return TryExploreNode(target, next, owner);
            }

            // All sectors unlocked — spend the card on a remaining frontier "?" if any.
            if (HasFrontierNode(out SectorNode frontier, out int frontierSector))
            {
                return TryExploreNode(frontier, frontierSector, owner);
            }

            ReportExplorationFailed("All sectors are unlocked and no frontier nodes remain.");
            return false;
        }

        private static SectorNode FindCrossSectorEntryNode(int sectorIndex)
        {
            if (SectorManager.Instance == null || sectorIndex <= 0) return null;
            var prev = SectorManager.Instance.Sectors[sectorIndex - 1];
            var next = SectorManager.Instance.Sectors[sectorIndex];
            if (prev?.Nodes == null || next?.Nodes == null) return null;

            foreach (var node in prev.Nodes)
            {
                if (node == null || !node.isExplored) continue;
                foreach (var conn in node.connections)
                {
                    if (conn != null && next.Nodes.Contains(conn) && !conn.isExplored)
                    {
                        return conn;
                    }
                }
            }

            return null;
        }

        /// <summary>Backward-compatible alias for scouting cards.</summary>
        public void InstantExplore()
        {
            TryExploreFrontier();
        }

        /// <summary>
        /// Spend energy and explore a specific frontier node.
        /// </summary>
        public bool TryExploreNode(SectorNode node, int sectorIndex, Owner owner = Owner.Player1)
        {
            if (!IsValidExploreTarget(node))
            {
                ReportExplorationFailed("That node cannot be explored right now.");
                return false;
            }

            if (SectorManager.Instance != null &&
                sectorIndex >= 0 &&
                sectorIndex < SectorManager.Instance.Sectors.Count &&
                SectorManager.Instance.Sectors[sectorIndex].IsLocked &&
                !GenerationManager.CanUnlockNextMapSector())
            {
                ReportExplorationFailed("Finish this sector's terraforming goals before exploring a new sector.");
                return false;
            }

            if (!TrySpendExplorationEnergy(owner)) return false;

            ExploreNodeInternal(node, sectorIndex, owner);
            return true;
        }

        /// <summary>
        /// Explore a specific node: reveal it, show "?" on connections, unlock sector if first node.
        /// Does not spend energy — prefer <see cref="TryExploreNode"/>.
        /// </summary>
        public void ExploreNode(SectorNode node, int sectorIndex)
        {
            if (!IsValidExploreTarget(node)) return;
            ExploreNodeInternal(node, sectorIndex, Owner.Player1);
        }

        private void ExploreNodeInternal(SectorNode node, int sectorIndex, Owner owner)
        {
            if (node == null || node.isExplored) return;

            var sector = SectorManager.Instance.Sectors[sectorIndex];

            // 1. Mark node as explored (this also discovers its connections)
            node.OnExplored();

            // 2. If this is the first explored node in a locked sector, unlock the sector
            if (sector.IsLocked)
            {
                SectorManager.Instance.OnFirstNodeExploredInSector(sectorIndex, owner);
            }

            // 3. Reveal hidden gatherable resources at this node's position
            RevealGatherableAtNode(node);

            // 4. Grant climate bonus rewards
            GrantExplorationBonuses(sectorIndex, node);

            // 5. Show discovery UI
            var resourceNodes = new System.Collections.Generic.List<SectorNode> { node };
            var bonuses = ExplorationNodeDatabase.Instance != null
                ? ExplorationNodeDatabase.Instance.GetRandomNodes(1)
                : new ExplorationNodeSO[0];
            UI.Containers.ExplorationDiscoveryUI.Instance.Show(sectorIndex, resourceNodes, bonuses);

            // 6. Update all node visibility
            UpdateNodeVisibility();

            // Notify GameFlowManager that an action was taken
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.PlayerActed();
            }

            Debug.Log($"[ExplorationManager] Explored node '{node.labelOverride ?? node.type.ToString()}' in Sector {sectorIndex}");
        }

        /// <summary>
        /// Reveal any HiddenResource gatherable at this node's position.
        /// </summary>
        private void RevealGatherableAtNode(SectorNode node)
        {
            var hiddenResources = UnityEngine.Object.FindObjectsByType<HiddenResource>(FindObjectsInactive.Include);
            foreach (var hr in hiddenResources)
            {
                if (hr == null || hr.IsDiscovered) continue;
                float dist = Vector3.Distance(hr.transform.position, node.position);
                if (dist < 2f)
                {
                    hr.ForceDiscover();
                }
            }
        }

        /// <summary>
        /// Update all node visibility across all sectors.
        /// </summary>
        private void UpdateNodeVisibility()
        {
            foreach (var sector in SectorManager.Instance.Sectors)
            {
                foreach (var n in sector.Nodes)
                {
                    n.SetVisualVisible(n.isExplored || n.isDiscovered);
                    n.SetQuestionMarkVisible(n.isDiscovered && !n.isExplored);
                }
            }
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
            if (TryExploreFrontier())
            {
                Debug.Log("[ExplorationManager] Survey Drone deployed!");
            }
        }

        private static void ReportExplorationFailed(string message)
        {
            Debug.Log($"[ExplorationManager] {message}");
            OnExplorationFailed?.Invoke(message);
        }

        public static void NotifyExplorationFailed(string message) => ReportExplorationFailed(message);

        private void CompleteExploration()
        {
            IsExploring = false;
            ExplorationProgress = 1f;

            if (SectorManager.Instance == null) return;

            // Same per-node flow as InstantExplore
            InstantExplore();
        }

        /// <summary>
        /// Grant random climate bonus rewards when a node is explored.
        /// </summary>
        private void GrantExplorationBonuses(int sectorIndex, SectorNode exploredNode)
        {
            if (ExplorationNodeDatabase.Instance == null) return;

            int bonusCount = Random.Range(1, 2); // 1 bonus per node explored
            var bonuses = ExplorationNodeDatabase.Instance.GetRandomNodes(bonusCount);

            foreach (var bonus in bonuses)
            {
                if (bonus != null) bonus.ApplyReward();
            }
        }
    }
}