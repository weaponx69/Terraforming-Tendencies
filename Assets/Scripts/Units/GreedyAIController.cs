using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Player;
using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameDevTV.RTS.Units
{
    public enum PackageItemType { Worker, Probe, OxygenProcessor, CommandCenter }

    /// <summary>One toggleable line in a package. The player can veto it before confirming.</summary>
    public class PackageItem
    {
        public string Name;
        public PackageItemType Type;
        public int Cost;
        public bool Enabled = true;
    }

    /// <summary>
    /// A player-approved growth package. Bundles a list of individually vetoable items
    /// (units, oxygen processor, optional command center toward a chosen direction) so the
    /// player makes ONE decision but keeps fine-grained control over what gets paid for.
    /// </summary>
    public class ExpansionProposal
    {
        public Vector3 Position;
        public int ResourceCount;
        public string SiteName;
        public bool IsExpansion;              // true = this package can include a Command Center
        public System.Collections.Generic.List<PackageItem> Items = new System.Collections.Generic.List<PackageItem>();

        /// <summary>Total biomass cost of only the items the player has left enabled.</summary>
        public int EnabledCost
        {
            get
            {
                int sum = 0;
                if (Items != null)
                    foreach (var i in Items) if (i.Enabled) sum += i.Cost;
                return sum;
            }
        }
    }

    public class GreedyAIController : MonoBehaviour
    {
        public static GreedyAIController Instance { get; private set; }

        [Header("Owner")]
        [SerializeField] private Owner aiOwner = Owner.Player1;

        [Header("Assets")]
        [SerializeField] private BuildingSO commandPostSO;
        [SerializeField] private BuildingSO oxygenProcessorSO;
        [SerializeField] private AbstractUnitSO probeSO;
        [SerializeField] private AbstractUnitSO workerSO;
        
        [Header("Settings")]
        [SerializeField] private int probesPerBase = 2;
        [SerializeField] private int workersPerBase = 4;
        [SerializeField] private float expansionRadius = 35f;
        [SerializeField] private float minExpansionDistance = 45f;
        [SerializeField] private int minResourcesForExpansion = 3;
        [SerializeField] private int oxygenProcessorsPerBase = 1;
        [SerializeField] private float tickRate = 2f;
        [SerializeField] private float proposalDuration = 10f;

        [Header("Growth Package (player-approved batch)")]
        [Tooltip("How many Workers are included in each approved package.")]
        [SerializeField] private int packageWorkers = 1;
        [Tooltip("How many Probes are included in each approved package.")]
        [SerializeField] private int packageProbes = 1;
[Tooltip("How many Oxygen Processors are included in each approved package.")]
        [SerializeField] private int packageOxygen = 1;
        [Tooltip("Seconds to wait before offering a new package after the player decides.")]
        [SerializeField] private float offerCooldown = 20f;
        [Tooltip("How long to keep trying to secure a builder for an approved command center before giving up (and re-offering).")]
        [SerializeField] private float expansionBuilderTimeout = 20f;
        private float nextOfferTime = 0f;
        
        private List<BaseBuilding> activeCommandPosts = new List<BaseBuilding>();
        private HashSet<HiddenResource> discoveredResources = new HashSet<HiddenResource>();
        private readonly Dictionary<Worker, GatherableSupply> assignedTargets = new Dictionary<Worker, GatherableSupply>();
        private readonly List<Vector3> ignoredExpansionSites = new List<Vector3>();
        private bool isSpawning = false;
        private bool isProposalActive = false;
        private bool isBuildingStructure = false;
        private bool proposalDeclined = false;
        private bool isExecutingPackage = false;

        // ── One-decision-per-command-center gating ──────────────────────────────
        // We offer at most one package per "command-center era". A new era opens when a
        // new command center is completed. Bootstrap exception: the first time an
        // expansion actually becomes possible within an era (after probes scout a site),
        // we allow that single expansion decision so the colony can keep growing.
        private int lastOfferedCommandPostCount = -1;
        private bool offeredExpansionThisEra = false;

        public event System.Action<List<ExpansionProposal>, float> OnExpansionProposed;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            
            Bus<ResourceDiscoveredEvent>.OnEvent[Owner.Unowned] += HandleResourceDiscovered;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] += HandleBuildingDeath;
            
            LoadAssets();
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            if (commandPostSO == null) commandPostSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Units/Buildings/Command Post/Command Post.asset");
            if (oxygenProcessorSO == null) oxygenProcessorSO = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Units/Buildings/Oxygen Processor/Oxygen Processor.asset");
            if (probeSO == null) probeSO = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>("Assets/Resources/Units/Probe.asset");
            if (workerSO == null) workerSO = AssetDatabase.LoadAssetAtPath<AbstractUnitSO>("Assets/Resources/Units/MiningDrone.asset");
#endif
            // Fallbacks for build
            if (commandPostSO == null) commandPostSO = Resources.Load<BuildingSO>("Buildings/CommandPost");
            if (oxygenProcessorSO == null) oxygenProcessorSO = Resources.Load<BuildingSO>("Buildings/OxygenProcessor");
            if (probeSO == null) probeSO = Resources.Load<AbstractUnitSO>("Units/Probe");
            if (workerSO == null) workerSO = Resources.Load<AbstractUnitSO>("Units/MiningDrone");
        }

        private void Start()
        {
            GrantStartingBiomass();
            InvokeRepeating(nameof(Tick), 2f, tickRate);
            
            // Re-scan already discovered resources in scene
            foreach (var hr in FindObjectsByType<HiddenResource>(FindObjectsInactive.Exclude))
            {
                if (hr.IsDiscovered) discoveredResources.Add(hr);
            }

            // Initial building if none exist
            if (BaseBuilding.ActiveBuildings.Count(b => b.Owner == aiOwner) == 0)
            {
                SpawnInitialBase();
            }
        }

        private void GrantStartingBiomass()
        {
            if (Supplies.Biomass == null) return;
            int current = Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            if (current < 1000)
            {
                Supplies.Biomass[aiOwner] = 1000;
                Supplies.RaiseBiomassChanged(aiOwner, 1000);
            }
        }

        private void SpawnInitialBase()
        {
            Vector3 center = Vector3.zero;
            if (PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null)
            {
                float w = PlanetGenerator.Instance.Config.MapWidth * PlanetGenerator.Instance.CellSize;
                float h = PlanetGenerator.Instance.Config.MapHeight * PlanetGenerator.Instance.CellSize;
                center = new Vector3(w / 2f, 0, h / 2f);
            }
            else
            {
                // Fallback center if planet not generated yet
                center = new Vector3(25, 0, 25);
            }
            Debug.Log("[GreedyAI] Spawning initial base at " + center);
            StartCoroutine(BuildCommandPostSequence(center, true));
        }

        private void OnDestroy()
        {
            Bus<ResourceDiscoveredEvent>.OnEvent[Owner.Unowned] -= HandleResourceDiscovered;
            Bus<BuildingSpawnEvent>.OnEvent[aiOwner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[aiOwner] -= HandleBuildingDeath;
        }

        private void Tick()
        {
            UpdateCommandPosts();
            
            // If no completed bases exist, check if any base at all exists (ghost, building, etc.)
            if (activeCommandPosts.Count == 0 && !isSpawning)
            {
                // ... (existing base-check logic)
                return;
            }

            // Brain is active - log status occasionally
            if (Time.frameCount % 300 == 0)
            {
                Debug.Log($"[GreedyAI] Tick: {activeCommandPosts.Count} bases, {Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude).Count(u => u.Owner == aiOwner)} units.");
            }

            // Mining of existing drones stays automatic — it is what funds the packages.
            AssignIdleWorkers();

            // Nothing is purchased autonomously. Wait while a decision is pending,
            // a base is spawning, or a previously-approved package is still executing.
            if (isProposalActive || isSpawning || isExecutingPackage) return;
            if (Time.time < nextOfferTime) return;

            // Once we've banked enough to afford a meaningful batch, consider one decision.
            List<ExpansionProposal> packages = BuildPackages();
            if (packages.Count == 0) return;

            int ccCount = activeCommandPosts.Count;
            bool newEra = ccCount > lastOfferedCommandPostCount;           // a new command center since last offer
            if (newEra) offeredExpansionThisEra = false;                   // fresh era → expansion decision available again
            bool expansionAvailable = packages.Any(p => p.IsExpansion);

            // One decision per command center. A new command center opens a fresh decision.
            // Bootstrap exception: the first time expansion becomes possible within an era
            // (probes have scouted a site), allow that single expansion decision.
            // ADDITION: Always allow "Grow Colony" offers if enough time has passed.
            bool allow = newEra || (expansionAvailable && !offeredExpansionThisEra) || !expansionAvailable;
            if (!allow) return;

            Debug.Log("[GreedyAI] Offering " + packages.Count + " growth package(s) for player approval. (era CC=" + ccCount + ")");
StartCoroutine(ProposalSequence(packages));
        }

        // ── Build the set of player-approvable packages ─────────────────────────
        // Returns one or more affordable packages. If expansion sites have been
        // scouted, each package is an expansion toward a direction (Command Center +
        // bundle). Otherwise a single "Grow Colony" package (bundle at home) is offered
        // so the economy can bootstrap. Returns empty when nothing is affordable yet
        // ("when resources are ready").
        private List<ExpansionProposal> BuildPackages()
        {
            var packages = new List<ExpansionProposal>();

            if (activeCommandPosts.Count == 0) return packages;

            int biomass = CurrentBiomass();

            // Candidate expansion directions
            var sites = new List<ExpansionProposal>();

            // 1. Sector centers (priority for winning condition)
if (SectorManager.Instance != null)
            {
                int sectorIndex = 1;
                float occupationRadius = SectorManager.Instance != null && PlanetGenerator.Instance != null && PlanetGenerator.Instance.Config != null 
                    ? PlanetGenerator.Instance.Config.SectorOccupationRadius + 5f 
                    : minExpansionDistance;

                foreach (var sector in SectorManager.Instance.Sectors)
                {
                    if (!sector.IsOccupied)
                    {
                        // Check if we are already proposing this site or if a base is already nearby (using occupation radius + buffer)
                        if (!CommandPostExistsNear(sector.Center, occupationRadius))
                        {
                            sites.Add(new ExpansionProposal
                            {
                                Position = sector.Center,
                                ResourceCount = 100, // High weight to ensure they are offered
                                SiteName = "Sector " + sectorIndex,
                                IsExpansion = true,
                                Items = MakeItems(true)
                            });
                        }
                    }
                    sectorIndex++;
                }
}

            // 2. Candidate expansion directions (discovered resource clusters away from bases).
            foreach (var res in discoveredResources.ToList())
            {
                if (res == null) { discoveredResources.Remove(res); continue; }
                Vector3 pos = res.transform.position;
                if (CommandPostExistsNear(pos, minExpansionDistance)) continue;
                if (sites.Any(p => Vector3.Distance(p.Position, pos) < minExpansionDistance)) continue;
                if (ignoredExpansionSites.Any(ignoredPos => Vector3.Distance(ignoredPos, pos) < minExpansionDistance)) continue;

                int count = discoveredResources.Count(r => r != null && Vector3.Distance(r.transform.position, pos) <= expansionRadius);
                if (count >= minResourcesForExpansion)
                {
                    sites.Add(new ExpansionProposal { Position = pos, ResourceCount = count, SiteName = "Resource Site" });
                }
            }
            
            // Limit to a few best options to avoid UI clutter
            var expansionSites = sites.OrderByDescending(s => s.ResourceCount).Take(3).ToList();

            foreach (var s in expansionSites)
            {
                // Items already assigned for sector sites; ensure they are set for resource sites.
                if (s.Items == null)
                {
                    s.IsExpansion = true;
                    s.Items = MakeItems(true);
                }
                
                // Only add if affordable
                if (s.Items.Sum(i => i.Cost) <= biomass)
                {
                    packages.Add(s);
                }
            }

            // If no affordable expansion is available, offer a "Grow Colony" package at an existing base
            if (packages.Count == 0 && activeCommandPosts.Count > 0)
            {
                var grow = new ExpansionProposal
                {
                    Position = activeCommandPosts[0].transform.position,
                    ResourceCount = 0,
                    SiteName = "Grow Colony",
                    IsExpansion = false,
                    Items = MakeItems(false)
                };

                if (grow.Items.Sum(i => i.Cost) <= biomass)
                {
                    packages.Add(grow);
                }
            }

            // Filter to only include packages that contain a Command Center as requested.
            return packages.Where(p => p.Items != null && p.Items.Any(i => i.Type == PackageItemType.CommandCenter)).ToList();
        }

        // Builds the per-unit item list for a package. Each unit is its own vetoable line.
        private List<PackageItem> MakeItems(bool expansion)
        {
            var items = new List<PackageItem>();
            if (expansion)
                items.Add(new PackageItem { Name = "Command Center", Type = PackageItemType.CommandCenter, Cost = CostOf(commandPostSO) });
            for (int i = 0; i < packageWorkers; i++)
                items.Add(new PackageItem { Name = "Worker", Type = PackageItemType.Worker, Cost = CostOf(workerSO) });
            for (int i = 0; i < packageProbes; i++)
                items.Add(new PackageItem { Name = "Probe", Type = PackageItemType.Probe, Cost = CostOf(probeSO) });
            for (int i = 0; i < packageOxygen; i++)
                items.Add(new PackageItem { Name = "Oxygen Processor", Type = PackageItemType.OxygenProcessor, Cost = CostOf(oxygenProcessorSO) });
            return items;
        }

        // Persistent (AFK-friendly): the offer waits for the player to decide.
        // Nothing is purchased unless a package is explicitly selected.
        private IEnumerator ProposalSequence(List<ExpansionProposal> packages)
        {
            isProposalActive = true;
            proposalDeclined = false;
            ExpansionProposal selectedChoice = null;

            System.Action<ExpansionProposal> onChoice = (choice) => selectedChoice = choice;
            OnProposalAccepted += onChoice;

            OnExpansionProposed?.Invoke(packages, proposalDuration);

            while (selectedChoice == null && !proposalDeclined)
            {
                yield return null;
            }

            OnProposalAccepted -= onChoice;
            isProposalActive = false;

            if (proposalDeclined || selectedChoice == null)
            {
                Debug.Log("[GreedyAI] Package declined — keeping resources, will re-offer later.");
                
                // If the player explicitly canceled, ignore any expansion sites that were offered
                // so they don't keep popping up if the player isn't interested in those locations.
                foreach (var p in packages)
                {
                    if (p.IsExpansion && !ignoredExpansionSites.Any(pos => Vector3.Distance(pos, p.Position) < minExpansionDistance))
                    {
                        ignoredExpansionSites.Add(p.Position);
                    }
                }

                nextOfferTime = Time.time + offerCooldown;
                yield break;
            }

            // Player accepted: advance the per-command-center gate so we don't offer again
            // until a new command center is built (the bootstrap expansion exception aside).
            lastOfferedCommandPostCount = activeCommandPosts.Count;
            if (selectedChoice.IsExpansion) offeredExpansionThisEra = true;

            yield return StartCoroutine(ExecutePackage(selectedChoice));
        }

        // Executes an approved package: queues units, builds oxygen processor(s),
        // and (if an expansion) builds a new command center at the chosen site.
        private IEnumerator ExecutePackage(ExpansionProposal pkg)
        {
            isExecutingPackage = true;

            var enabled = pkg.Items.Where(i => i.Enabled).ToList();
            Debug.Log("[GreedyAI] Executing package '" + pkg.SiteName + "': " +
                      string.Join(", ", enabled.Select(i => i.Name)));

            // Hub = command post nearest the target, to queue units from.
            BaseBuilding hub = activeCommandPosts
                .Where(cp => cp != null)
                .OrderBy(cp => (cp.transform.position - pkg.Position).sqrMagnitude)
                .FirstOrDefault();

            // 1. Queue units first — these are instant to order and BuildUnlockable
            //    deducts their cost itself; they need no free worker.
            foreach (var item in enabled)
            {
                if (item.Type == PackageItemType.Worker && workerSO != null && CanAfford(workerSO) && hub != null && hub.QueueSize < 5)
                    hub.BuildUnlockable(workerSO);
                else if (item.Type == PackageItemType.Probe && probeSO != null && CanAfford(probeSO) && hub != null && hub.QueueSize < 5)
                    hub.BuildUnlockable(probeSO);
            }

            // 2. Command center (priority) — the sequence secures a builder (retrying /
            //    pulling a miner off its job). If it can't, the decision is re-opened (no biomass lost).
            if (pkg.IsExpansion && enabled.Any(i => i.Type == PackageItemType.CommandCenter))
            {
                yield return StartCoroutine(BuildCommandPostSequence(pkg.Position));
            }

            // 3. Oxygen processor(s) — best effort with whatever worker is now free.
            //    Now waits for a builder to ensure they actually get built if approved.
            foreach (var item in enabled.Where(i => i.Type == PackageItemType.OxygenProcessor))
            {
                if (oxygenProcessorSO == null || !CanAfford(oxygenProcessorSO)) continue;
                
                Worker builder = null;
                float deadline = Time.time + expansionBuilderTimeout;
                while (Time.time < deadline)
                {
                    builder = FindWorkerForExpansion();
                    if (builder != null) break;
                    yield return new WaitForSeconds(1f);
                }

                if (builder != null)
                {
                    // Prefer the package position (e.g. the new expansion site) over the old hub
                    // for support structures included in that package.
                    Vector3 anchor = pkg.IsExpansion ? pkg.Position : (hub != null ? hub.transform.position : pkg.Position);
                    
                    int agentTypeId = 0;
                    if (builder.TryGetComponent(out NavMeshAgent builderAgent)) agentTypeId = builderAgent.agentTypeID;
                    
                    Vector3 spot = FindBuildSpotNear(anchor, 12f, agentTypeId);
                    Debug.Log("[GreedyAI] Dispatching worker " + builder.name + " to build Oxygen Processor at " + spot);
                    builder.Build(oxygenProcessorSO, spot);
                    
                    // Brief yield to ensure biomass updates before the next iteration's CanAfford check
                    yield return new WaitForSeconds(0.1f);
                }
                else
                {
                    Debug.LogWarning("[GreedyAI] Could not secure a worker for Oxygen Processor — skipping this item.");
                }
            }

            // Cooldown so a fresh package doesn't pop again instantly.
            nextOfferTime = Time.time + offerCooldown;
            isExecutingPackage = false;
        }

        private int CurrentBiomass()
        {
            return Supplies.Biomass != null && Supplies.Biomass.TryGetValue(aiOwner, out int b) ? b : 0;
        }

        /// <summary>Current spendable biomass for the colony — used by the UI for live affordability.</summary>
        public int AvailableBiomass => CurrentBiomass();

        private int CostOf(UnlockableSO so)
        {
            if (so == null || so.Cost == null) return 0;
            return Mathf.FloorToInt(so.Cost.Minerals * Supplies.MineralsToBiomassRateStatic
                                  + so.Cost.Gas * Supplies.GasToBiomassRateStatic);
        }

        private event System.Action<ExpansionProposal> OnProposalAccepted;

        public void AcceptProposal(ExpansionProposal proposal)
        {
            OnProposalAccepted?.Invoke(proposal);
        }

        /// <summary>Called by the UI Cancel button to decline the current package.</summary>
        public void DeclineProposal()
        {
            proposalDeclined = true;
        }

        private void UpdateCommandPosts()
        {
            // Scan all buildings in the scene directly to find completed command centers.
            // Bypassing static list reliance to prevent stalls caused by registration delays or culling.
            var allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            
            activeCommandPosts = allBuildings
                .Where(b => b != null && b.Owner == aiOwner &&
                       (b.name.Contains("Command") || (b.BuildingSO != null && b.BuildingSO.Name.Contains("Command"))) && 
                       b.Progress.State == BuildingProgress.BuildingState.Completed)
                .ToList();

            // If we found bases, ensure they are enabled (fixing the registration bug)
            foreach (var b in activeCommandPosts)
            {
                if (!b.enabled) b.enabled = true;
            }
        }

        private void ManageProduction()
        {
            if (activeCommandPosts.Count == 0) return;

            foreach (var cp in activeCommandPosts)
            {
                if (cp == null || cp.QueueSize >= 3) continue;

                var allUnits = Object.FindObjectsByType<AbstractUnit>(FindObjectsInactive.Exclude)
                    .Where(u => u.Owner == aiOwner).ToList();

                // Count workers (those without ProbeMovement)
                int workerCount = allUnits.Count(u => u is Worker && u.GetComponent<ProbeMovement>() == null);
                
                if (workerCount < workersPerBase)
                {
                    if (workerSO != null && CanAfford(workerSO)) 
                    {
                        Debug.Log("[GreedyAI] Building Worker at " + cp.name);
                        cp.BuildUnlockable(workerSO);
                    }
                }
                
                // Count Probes (those with ProbeMovement)
                int probeCount = allUnits.Count(u => u.GetComponent<ProbeMovement>() != null);
                
                if (probeCount < activeCommandPosts.Count * probesPerBase)
                {
                    if (probeSO != null && CanAfford(probeSO)) 
                    {
                        Debug.Log("[GreedyAI] Building Probe at " + cp.name);
                        cp.BuildUnlockable(probeSO);
                    }
                }
            }
        }

        private void HandleResourceDiscovered(ResourceDiscoveredEvent evt)
        {
            if (evt.Resource == null) return;
            discoveredResources.Add(evt.Resource);
        }

        // ── Base development: build support structures (Oxygen Processors) ───────
        // Each completed command post gets one or more Oxygen Processors built nearby.
        // Oxygen Processors are Life Support nodes, so they also drive vegetation growth.
        private void ManageBaseBuildings()
        {
            if (isBuildingStructure || oxygenProcessorSO == null) return;

            foreach (var cp in activeCommandPosts)
            {
                if (cp == null) continue;

                int oxygenNearby = CountBuildingsNear(oxygenProcessorSO, cp.transform.position, expansionRadius);
                if (oxygenNearby >= oxygenProcessorsPerBase) continue;

                if (!CanAfford(oxygenProcessorSO)) continue;

                Worker builder = FindAvailableWorker();
                if (builder == null) return; // no free worker right now; try again next tick

                Vector3 spot = FindBuildSpotNear(cp.transform.position, 12f);
                Debug.Log("[GreedyAI] Dispatching " + builder.name + " to build Oxygen Processor near " + cp.name);
                builder.Build(oxygenProcessorSO, spot);
                StartCoroutine(StructureBuildCooldown());
                return; // build one structure at a time
            }
        }

        private IEnumerator StructureBuildCooldown()
        {
            isBuildingStructure = true;
            yield return new WaitForSeconds(15f);
            isBuildingStructure = false;
        }

        private int CountBuildingsNear(BuildingSO so, Vector3 position, float radius)
        {
            var all = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            return all.Count(b => b != null && b.Owner == aiOwner && b.BuildingSO != null &&
                (b.BuildingSO == so || b.BuildingSO.Name == so.Name) &&
                Vector3.Distance(b.transform.position, position) < radius);
        }

        private Vector3 FindBuildSpotNear(Vector3 origin, float distance, int agentTypeId = 0)
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            for (int i = 0; i < 10; i++)
            {
                Vector2 circle = Random.insideUnitCircle.normalized * distance;
                Vector3 candidate = origin + new Vector3(circle.x, 0f, circle.y);
                
                // Established ground height via Raycast to prevent inheriting a high Y from the origin.
                Ray ray = new Ray(candidate + Vector3.up * 50f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
                {
                    candidate = groundHit.point;
                }

                // Ensure we sample the Ground (Agent 0)
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 15f, filter))
                    return hit.position;
            }
            return origin + new Vector3(distance, 0f, 0f);
        }

        // ── Drone mining (ported from the old AIController) ─────────────────────
        // Assigns idle Mining Drones to the nearest unclaimed resource. Probes are
        // explicitly excluded — they are driven by ProbeMovement for exploration.
        private void AssignIdleWorkers()
        {
            // Drop stale assignments: depleted/destroyed target, missing drone, OR a drone
            // that has gone idle (finished its haul). Dropping idle drones' claims here is
            // what guarantees they get re-tasked below instead of being skipped forever.
            foreach (var pair in assignedTargets.ToList())
            {
                if (pair.Key == null || pair.Value == null || pair.Value.Amount <= 0 || pair.Key.IsIdle)
                    assignedTargets.Remove(pair.Key);
            }

            var idleDrones = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude)
                .Where(w => w != null
                            && w.Owner == aiOwner
                            && w.GetComponent<ProbeMovement>() == null            // not a Probe
                            && w.IsIdle)
                .ToList();

            foreach (Worker drone in idleDrones)
            {
                if (drone.HasSupplies)
                {
                    // Drone is carrying supplies but sitting idle (likely finished mining
                    // when no base existed). Send it home.
                    BaseBuilding home = activeCommandPosts
                        .Where(cp => cp != null)
                        .OrderBy(cp => (cp.transform.position - drone.transform.position).sqrMagnitude)
                        .FirstOrDefault();

                    if (home != null)
                    {
                        drone.GetComponent<WorkerBrainController>()?.SetHomeBase(home.transform);
                        drone.ReturnSupplies(home.gameObject);
                    }
                    continue;
                }

                if (assignedTargets.ContainsKey(drone)) continue;

                // Supplies actively claimed by other (still-working) drones this cycle.
                HashSet<GatherableSupply> claimed = new HashSet<GatherableSupply>(assignedTargets.Values);

                // Prefer an unclaimed, non-busy supply so drones spread out.
                GatherableSupply supply = GatherableSupply.ActiveSupplies
                    .Where(s => s != null && s.Amount > 0 && !s.IsBusy
                                && (s.transform.parent != null && s.transform.parent.GetComponent<PlanetGenerator>() != null)
&& !claimed.Contains(s))
                    .OrderBy(s => (s.transform.position - drone.transform.position).sqrMagnitude)
                    .FirstOrDefault();

                // Fallback: if everything nearby is already claimed, share the nearest
                // non-depleted supply rather than letting this drone sit idle.
                if (supply == null)
                {
                    supply = GatherableSupply.ActiveSupplies
                        .Where(s => s != null && s.Amount > 0 && (s.transform.parent != null && s.transform.parent.GetComponent<PlanetGenerator>() != null))
.OrderBy(s => (s.transform.position - drone.transform.position).sqrMagnitude)
                        .FirstOrDefault();
                }

                if (supply == null)
                {
                    // Debug.Log("[GreedyAI] Idle drone " + drone.name + " could not find any supply to mine.");
                    continue;
                }

                // Send the drone home to the nearest command post after gathering.
                BaseBuilding homeForMining = activeCommandPosts
                    .Where(cp => cp != null)
                    .OrderBy(cp => (cp.transform.position - drone.transform.position).sqrMagnitude)
                    .FirstOrDefault();

                assignedTargets[drone] = supply;
                drone.GetComponent<WorkerBrainController>()?.SetHomeBase(homeForMining != null ? homeForMining.transform : null);
                drone.Gather(supply);
            }
        }

        // Counts EVERY command post belonging to this owner currently in the scene,
        // regardless of build state (ghost, under-construction, or completed).
        // Used as the authoritative duplicate guard at the moment of placement.
        private bool CommandPostExistsNear(Vector3 position, float radius)
        {
            // First check the cached list for speed
            bool existsInActive = BaseBuilding.ActiveBuildings.Any(b =>
                b != null && b.Owner == aiOwner && b.BuildingSO != null &&
                (b.BuildingSO == commandPostSO || b.BuildingSO.Name == commandPostSO.Name) &&
                Vector3.Distance(b.transform.position, position) < radius);
            
            if (existsInActive) return true;

            // Fallback: search scene directly to be 100% sure
            var allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            foreach (var b in allBuildings)
            {
                if (b.Owner == aiOwner && b.BuildingSO != null && 
                    (b.BuildingSO == commandPostSO || b.BuildingSO.Name == commandPostSO.Name))
                {
                    if (Vector3.Distance(b.transform.position, position) < radius) return true;
                }
            }

            return false;
        }

        private bool AnyCommandPostExists()
        {
            if (BaseBuilding.ActiveBuildings.Any(b => b != null && b.Owner == aiOwner)) return true;
            
            var allBuildings = Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Include);
            return allBuildings.Any(b => b.Owner == aiOwner);
        }

        private IEnumerator BuildCommandPostSequence(Vector3 position, bool immediate = false)
        {
            isSpawning = true;
            Debug.Log("[GreedyAI] Spawning sequence started for " + position + ". Immediate: " + immediate);
            
            // 1. Establish the actual ground height via Physics Raycast.
            // This prevents picking the Air NavMesh (Y=4) if the Ground NavMesh isn't baked at that specific spot.
            Ray ray = new Ray(position + Vector3.up * 50f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, LayerMask.GetMask("Default", "Terrain")))
            {
                position = groundHit.point;
            }

            // 2. Align to the Humanoid (Ground) NavMesh for valid placement.
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 20f, filter))
            {
                position = navHit.position;
            }

            if (immediate)
            {
                // Hard guard: never spawn an initial base if ANY command post already exists.
                if (AnyCommandPostExists())
                {
                    Debug.LogWarning("[GreedyAI] Aborted initial base spawn — a building already exists for " + aiOwner);
                }
                else if (commandPostSO == null || commandPostSO.Prefab == null)
                {
                    Debug.LogError("[GreedyAI] Cannot spawn initial base: commandPostSO or Prefab is null!");
                }
                else
                {
                    GameObject prefab = commandPostSO.Prefab;
                    GameObject inst = Instantiate(prefab, position, Quaternion.identity);
                    if (inst.TryGetComponent(out BaseBuilding building))
                    {
                        building.enabled = true; // Ensure it registers in ActiveBuildings
                        building.Owner = aiOwner;
                        building.CompleteConstruction();
                        Debug.Log("[GreedyAI] Initial base spawned at " + position + " and completed.");
                    }
                }
            }
            else
            {
                // Hard guard: never build an expansion on top of an existing command post.
                if (CommandPostExistsNear(position, minExpansionDistance))
                {
                    Debug.LogWarning("[GreedyAI] Aborted expansion — a command post already exists near " + position);
                }
                else
                {
                    // Keep trying to secure a builder. Workers may all be mining or building
                    // the package's oxygen processor; wait for one to free up (and, if needed,
                    // pull one off mining) rather than wasting the player's biomass.
                    Worker builder = null;
                    float deadline = Time.time + expansionBuilderTimeout;
                    while (Time.time < deadline)
                    {
                        builder = FindWorkerForExpansion();
                        if (builder != null) break;
                        yield return new WaitForSeconds(1f);
                    }

                    if (builder != null && CanAfford(commandPostSO))
                    {
                        // Match the sampling to the worker's NavMesh agent type (e.g. Airborne)
                        int agentTypeId = 0;
                        if (builder.TryGetComponent(out NavMeshAgent builderAgent)) agentTypeId = builderAgent.agentTypeID;
                        
                        Vector3 buildPos = position;
                        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 15f, new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas }))
                        {
                            buildPos = hit.position;
                        }

                        Debug.Log("[GreedyAI] Dispatching worker " + builder.name + " to build expansion at " + buildPos);
                        builder.Build(commandPostSO, buildPos);
                    }
else
                    {
                        // Could not build it — no cost was deducted. Re-open the expansion
                        // decision so the player is offered it again instead of being stuck.
                        Debug.LogWarning("[GreedyAI] Could not secure a worker for the new command center — re-opening the decision (no biomass spent).");
                        offeredExpansionThisEra = false;
                    }
                }
            }
            
            // Wait for registration
            yield return new WaitForSeconds(immediate ? 1.5f : 10f);
            isSpawning = false;
            Debug.Log("[GreedyAI] Spawning lock released.");
        }

        private Worker FindAvailableWorker()
        {
            return Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude)
                .OrderBy(w => Vector3.Distance(w.transform.position, transform.position))
                .FirstOrDefault(w => w.Owner == aiOwner && w.IsIdle && !w.HasSupplies);
        }

        // Like FindAvailableWorker but, if no worker is idle, falls back to pulling a
        // mining worker off its job (one that isn't carrying supplies or already building).
        // Used for player-approved command centers, which must get built.
        private Worker FindWorkerForExpansion()
        {
            var workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude)
                .Where(w => w != null && w.Owner == aiOwner
                            && w.GetComponent<ProbeMovement>() == null   // never a probe
                            && !w.HasSupplies)
                .ToList();

            // Prefer a genuinely idle worker.
            Worker idle = workers
                .Where(w => w.IsIdle)
                .OrderBy(w => Vector3.Distance(w.transform.position, transform.position))
                .FirstOrDefault();
            if (idle != null) return idle;

            // Otherwise interrupt a worker that is mining (but not mid-building something).
            return workers
                .Where(w => !w.IsBuilding)
                .OrderBy(w => Vector3.Distance(w.transform.position, transform.position))
                .FirstOrDefault();
        }

        /// <summary>
        /// True if at least one drone could be dispatched to build a structure right now —
        /// either a genuinely idle drone, or a mining drone that can be pulled off its job
        /// (not a probe, not carrying supplies, not already building). Mirrors the selection
        /// in <see cref="FindWorkerForExpansion"/> so UI purchase-gating matches execution.
        /// </summary>
        public bool HasAvailableBuilder()
        {
            var workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Exclude);
            foreach (var w in workers)
            {
                if (w == null) continue;
                if (w.Owner != aiOwner) continue;
                if (w.GetComponent<ProbeMovement>() != null) continue; // never a probe
                if (w.HasSupplies) continue;                           // busy hauling
                if (w.IsBuilding) continue;                            // already building
                return true;                                           // idle OR mining (reassignable)
            }
            return false;
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building.BuildingSO == commandPostSO) UpdateCommandPosts();
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building.BuildingSO == commandPostSO) UpdateCommandPosts();
        }

        private bool CanAfford(UnlockableSO unlockable)
        {
            if (unlockable?.Cost == null) return true;
            int cost = Mathf.FloorToInt(unlockable.Cost.Minerals * Supplies.MineralsToBiomassRateStatic + unlockable.Cost.Gas * Supplies.GasToBiomassRateStatic);
            int available = Supplies.Biomass.TryGetValue(aiOwner, out int biomass) ? biomass : 0;
            return cost <= available;
        }
    }
}
