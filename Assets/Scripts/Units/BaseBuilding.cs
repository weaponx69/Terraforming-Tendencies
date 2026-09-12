using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    public class BaseBuilding : AbstractCommandable
    {
        public static readonly List<BaseBuilding> ActiveBuildings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitSceneEvents()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ActiveBuildings.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearStaticData()
        {
            ActiveBuildings.Clear();
        }

        public int CurrentQueueCount => buildingQueue.Count;
        public int MaxQueueSize
        {
            get
            {
                if (BuildingSO != null && BuildingSO.BuildingConfig != null && BuildingSO.BuildingConfig.QueueSize > 0)
                {
                    return BuildingSO.BuildingConfig.QueueSize;
                }
                return MAX_QUEUE_SIZE;
            }
        }
        public int QueueSize => buildingQueue.Count; // Keep for backward compatibility with external scripts, but it represents the current count!
        public UnlockableSO[] Queue => buildingQueue.ToArray();
        [field: SerializeField] public float CurrentQueueStartTime { get; private set; }
        [field: SerializeField] public UnlockableSO SOBeingBuilt { get; private set; }
        [field: SerializeField] public MeshRenderer MainRenderer { get; protected set; }
        [field: SerializeField]
        public BuildingProgress Progress { get; private set; } = new(
            BuildingProgress.BuildingState.Completed, 0, 1
        );
        [field: SerializeField] public BuildingSO BuildingSO { get; private set; }
        [SerializeField] private Material primaryMaterial;
        [SerializeField] private NavMeshObstacle navMeshObstacle;

        /// <summary>
        /// Prefabs often leave <see cref="BuildingSO"/> null and only set <see cref="UnitSO"/>.
        /// Climate / power must resolve the definition either way.
        /// </summary>
        public BuildingSO ResolvedBuildingSO =>
            BuildingSO != null ? BuildingSO : UnitSO as BuildingSO;

        public delegate void QueueUpdatedEvent(UnlockableSO[] unitsInQueue);
        public event QueueUpdatedEvent OnQueueUpdated;

        private bool isDegraded = false;

        /// <summary>Whether this building is in a degraded state due to Materials shortage (50% efficiency).</summary>
        public bool IsDegraded => isDegraded;

        /// <summary>Called by BuildingUpkeepManager to set the degraded state.</summary>
        public void SetDegraded(bool degraded)
        {
            if (isDegraded == degraded) return;
            isDegraded = degraded;

            // Visual feedback: yellow tint when degraded
            if (MainRenderer != null && primaryMaterial != null)
            {
                if (degraded)
                {
                    // Tint yellow
                    MainRenderer.material.color = new Color(1f, 0.85f, 0.3f);
                }
                else
                {
                    // Restore original color
                    MainRenderer.material.color = Color.white;
                }
            }
        }

        //what does isOperating mean?
        /// <summary>
        /// True when fully powered (or needs no power). Under Colony Acts, unpowered
        /// buildings still produce at <see cref="ProductionEfficiency"/> — this flag
        /// means "full rate / powered", not "offline".
        /// </summary>
        public bool IsOperating
        {
            get
            {
                if (Progress.State != BuildingProgress.BuildingState.Completed) return false;

                BuildingSO def = ResolvedBuildingSO;
                bool isCommandPost = def != null && def.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                if (isCommandPost) return true;

                bool needsPower = def != null && def.BuildingConfig != null && def.BuildingConfig.PowerUpkeep > 0;
                if (needsPower)
                {
                    var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                    // Missing node = not on the grid yet → not fully powered.
                    if (pNode == null || !pNode.IsPowered) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Colony Acts: unpowered consumers crawl at this fraction of config rates.
        /// Powered (or no-upkeep) buildings run at 1.
        /// </summary>
        public const float UnpoweredProductionEfficiency = 0.2f;

        /// <summary>
        /// Multiplier for climate / turn production. Power is an efficiency bonus,
        /// not an on/off gate under Colony Acts.
        /// </summary>
        public float ProductionEfficiency
        {
            get
            {
                if (Progress.State != BuildingProgress.BuildingState.Completed) return 0f;

                BuildingSO def = ResolvedBuildingSO;
                if (def == null) return 0f;

                // Generators and zero-upkeep tiles are always full rate.
                if (BuildingSiteRegistry.IsPowerGeneratorBuilding(def))
                    return 1f;
                if (def.BuildingConfig == null || def.BuildingConfig.PowerUpkeep <= 0f)
                    return 1f;
                if (def.Name != null
                    && def.Name.IndexOf("Command", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return 1f;

                var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                if (pNode != null && pNode.IsPowered)
                    return 1f;

                // Colony Acts: crawl. Legacy modes: treat unpowered as offline (0).
                if (ColonyActManager.Instance != null)
                    return UnpoweredProductionEfficiency;
                return 0f;
            }
        }

        private bool isHousingActive = false;

        // Must track if building is powered or not.
        private void HandlePowerStateChanged(bool isPowered)
        {
            UpdateHousingContribution();
        }

        private void UpdateHousingContribution()
        {
            if (BuildingSO == null || BuildingSO.BuildingConfig == null || BuildingSO.BuildingConfig.HousingCapacity <= 0) return;

            bool shouldBeActive = Progress.State == BuildingProgress.BuildingState.Completed;
            if (shouldBeActive && BuildingSO.BuildingConfig.PowerUpkeep > 0)
            {
                var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                if (pNode != null && !pNode.IsPowered)
                {
                    shouldBeActive = false;
                }
            }

            if (shouldBeActive && !isHousingActive)
            {
                isHousingActive = true;
                int currentPopLimit = Supplies.PopulationLimit != null && Supplies.PopulationLimit.TryGetValue(Owner, out int l) ? l : 0;
                Supplies.UpdatePopulationLimit(Owner, currentPopLimit + BuildingSO.BuildingConfig.HousingCapacity);
            }
            else if (!shouldBeActive && isHousingActive)
            {
                isHousingActive = false;
                int currentPopLimit = Supplies.PopulationLimit != null && Supplies.PopulationLimit.TryGetValue(Owner, out int l) ? l : 0;
                Supplies.UpdatePopulationLimit(Owner, Mathf.Max(0, currentPopLimit - BuildingSO.BuildingConfig.HousingCapacity));
            }
        }

        private Placeholder culledVisuals;
        private IBuildingBuilder unitBuildingThis;
        private Coroutine productionCoroutine;
        private List<UnlockableSO> buildingQueue = new(MAX_QUEUE_SIZE);
        public const int MAX_QUEUE_SIZE = 5;
        private int spawnCount = 0; // Tracks how many units have been spawned, for angle distribution
        private bool hasRaisedSpawnEvent = false;
        private bool isBuildingInitialized = false;

        public override void InitializeIfNeeded()
        {
            if (isBuildingInitialized) return;
            base.InitializeIfNeeded();

            if (selectionIndicator == null)
            {
                Transform child = transform.Find("Selection Indicator");
                if (child != null)
                {
                    selectionIndicator = child.gameObject;
                }
                else
                {
                    GameObject indicatorGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    indicatorGO.name = "Selection Indicator";
                    var col = indicatorGO.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    indicatorGO.transform.SetParent(transform, false);
                    indicatorGO.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                    indicatorGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    float scale = 8f;
                    BuildingSO = UnitSO as BuildingSO;
                    var checkSO = BuildingSO != null ? BuildingSO : (UnitSO as BuildingSO);
                    if (checkSO != null)
                    {
                        if (checkSO.Name.Contains("Solar", System.StringComparison.OrdinalIgnoreCase))
                        {
                            scale = 10f;
                        }
                        else if (checkSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                        {
                            scale = 15f;
                        }
                    }
                    indicatorGO.transform.localScale = new Vector3(scale, scale, 1f);
                    indicatorGO.SetActive(false);
                    selectionIndicator = indicatorGO;
                }
            }

            EnsureSelectionIndicatorRing();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO != null ? BuildingSO.Health : 1000;
            
            if (MainRenderer == null)
            {
                foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                {
                    string nameLower = mr.gameObject.name.ToLower();
                    if (!nameLower.Contains("vision") && !nameLower.Contains("indicator") && !nameLower.Contains("selection"))
                    {
                        MainRenderer = mr;
                        break;
                    }
                }

                if (MainRenderer == null)
                {
                    MainRenderer = GetComponentInChildren<MeshRenderer>();
                }
            }

            if (MainRenderer != null && primaryMaterial == null)
            {
                primaryMaterial = MainRenderer.material;
            }

            if (navMeshObstacle == null)
            {
                navMeshObstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            }

            if (gameObject.GetComponent<GameDevTV.RTS.Environment.PowerNode>() == null)
            {
                gameObject.AddComponent<GameDevTV.RTS.Environment.PowerNode>();
            }

            EnsureBuildingHealthBar();

            isBuildingInitialized = true;
        }


        protected override void OnEnable()
        {
            base.OnEnable();
            if (!ActiveBuildings.Contains(this))
                ActiveBuildings.Add(this);

            if (buildingQueue != null && buildingQueue.Count > 0 && productionCoroutine == null)
            {
                productionCoroutine = StartCoroutine(DoBuildUnits());
            }

            // Register with BuildingUpkeepManager for Materials upkeep tax
            if (GameDevTV.RTS.Player.BuildingUpkeepManager.Instance != null &&
                Progress.State == BuildingProgress.BuildingState.Completed)
            {
                GameDevTV.RTS.Player.BuildingUpkeepManager.Instance.RegisterBuilding(this);
            }

            if (GameDevTV.RTS.Player.GameFlowManager.Instance != null)
            {
                GameDevTV.RTS.Player.GameFlowManager.Instance.OnTurnUpkeep += HandleTurnUpkeep;
                GameDevTV.RTS.Player.GameFlowManager.Instance.OnTurnIncome += HandleTurnIncome;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ActiveBuildings.Remove(this);
            if (pendingColonyActScore)
            {
                pendingColonyActScore = false;
                PendingColonyActScoreBuildings.Remove(this);
            }

            // Unregister from BuildingUpkeepManager
            if (GameDevTV.RTS.Player.BuildingUpkeepManager.Instance != null)
            {
                GameDevTV.RTS.Player.BuildingUpkeepManager.Instance.UnregisterBuilding(this);
            }

            if (GameDevTV.RTS.Player.GameFlowManager.Instance != null)
            {
                GameDevTV.RTS.Player.GameFlowManager.Instance.OnTurnUpkeep -= HandleTurnUpkeep;
                GameDevTV.RTS.Player.GameFlowManager.Instance.OnTurnIncome -= HandleTurnIncome;
            }
            productionCoroutine = null;

            if (BuildingSO != null && BuildingSO.BuildingConfig != null)
            {
                var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                if (pNode != null)
                {
                    pNode.OnPowerStateChanged -= HandlePowerStateChanged;
                }

                if (isHousingActive)
                {
                    isHousingActive = false;
                    int currentPopLimit = Supplies.PopulationLimit != null && Supplies.PopulationLimit.TryGetValue(Owner, out int l) ? l : 0;
                    Supplies.UpdatePopulationLimit(Owner, Mathf.Max(0, currentPopLimit - BuildingSO.BuildingConfig.HousingCapacity));
                }
            }
        }

        private void RaiseSpawnEvent()
        {
            if (!hasRaisedSpawnEvent)
            {
                hasRaisedSpawnEvent = true;
                Debug.Log($"[BaseBuilding.DIAG] Raising BuildingSpawnEvent. Owner={Owner}, name={gameObject.name}, state={Progress.State}");
                Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));
            }
            else
            {
                Debug.Log($"[BaseBuilding.DIAG] RaiseSpawnEvent SKIPPED (already raised). Owner={Owner}, name={gameObject.name}");
            }
        }

        protected override void Start()
        {
            base.Start();

            // Ensure we are initialized before checking BuildingSO
            InitializeIfNeeded();

            // Automatically attach damaged status indicator programmatically
            if (gameObject.GetComponent<GameDevTV.RTS.UI.Components.DamagedIndicator>() == null)
            {
                gameObject.AddComponent<GameDevTV.RTS.UI.Components.DamagedIndicator>();
            }

            // Automatically attach unpowered status indicator programmatically
            if (gameObject.GetComponent<GameDevTV.RTS.UI.Components.UnpoweredIndicator>() == null)
            {
                gameObject.AddComponent<GameDevTV.RTS.UI.Components.UnpoweredIndicator>();
            }

            bool isCommandPost = BuildingSO != null && (BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
            
            // If this is a Command Post, automatically reveal the entire sector immediately (even as a ghost)
            if (isCommandPost && VisionTransform != null && GameDevTV.RTS.Environment.PlanetGenerator.Instance != null)
            {
                var config = GameDevTV.RTS.Environment.PlanetGenerator.Instance.Config;
                if (config != null)
                {
                    float secW = (config.MapWidth * GameDevTV.RTS.Environment.PlanetGenerator.Instance.CellSize) / config.SectorsX;
                    float secH = (config.MapHeight * GameDevTV.RTS.Environment.PlanetGenerator.Instance.CellSize) / config.SectorsY;
                    float diagonal = Mathf.Sqrt(secW * secW + secH * secH);
                    VisionTransform.localScale = new Vector3(diagonal, diagonal, diagonal);
                }
            }

            // Only apply material and auto-complete if we are NOT a ghost / uninitialized prefab.
            // Prefabs often ship Progress.State = Destroyed — that must NOT auto-complete, or
            // hasCompletedConstruction latches true and the real drone finish becomes a no-op
            // (powered-looking building that never ticks climate).
            if (Progress.State != BuildingProgress.BuildingState.Paused
                && Progress.State != BuildingProgress.BuildingState.Destroyed)
            {
                // Reserved-site builds call CompleteConstruction before Start. Do not re-apply
                // primaryMaterial afterward — it may still be the translucent ghost captured
                // during Awake (SmokestackVisuals), which would undo ActivateSmoke.
                if (!hasCompletedConstruction && MainRenderer != null && primaryMaterial != null)
                {
                    MainRenderer.material = primaryMaterial;
                }
                
                // If already done (e.g. AI spawn) finish setup. Do NOT auto-complete
                // while State==Building with no drone — that is self-construction.
                if (unitBuildingThis == null
                    && Progress.State != BuildingProgress.BuildingState.Building)
                {
                    CompleteConstruction();
                }
                else if (hasCompletedConstruction
                    || Progress.State == BuildingProgress.BuildingState.Completed)
                {
                    // Already completed earlier this frame (reserved-site) — still wire neighbors.
                    AutoConnectAdjacentPowerNodes();
                }
                
                RaiseSpawnEvent();
            }

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;

            if (BuildingSO != null && BuildingSO.Upgrades != null)
            {
                foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
                {
                    if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                    {
                        upgrade.Apply(BuildingSO);
                    }
                }
            }

            if (BuildingSO != null && BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
            {
                if (selectionIndicator != null)
                {
                    // Shrink it down to be closer to the outer perimeter
                    selectionIndicator.transform.localScale *= 0.6f; 
                }
            }
        }

        private bool hasCompletedConstruction = false;
        /// <summary>
        /// Instant card builds complete before <see cref="CardDeckController.ConsumeCardAfterBuild"/>
        /// spends the week — defer Act score so the week counts against the Act that was played.
        /// </summary>
        private bool deferColonyActScore;
        private bool pendingColonyActScore;
        private static readonly List<BaseBuilding> PendingColonyActScoreBuildings = new();

        /// <summary>Defer Colony Act score until <see cref="FlushDeferredColonyActScore"/> (instant card builds).</summary>
        public void DeferColonyActScoreOnce()
        {
            deferColonyActScore = true;
        }

        /// <summary>Grant deferred Act score after the card week is spent.</summary>
        public void FlushDeferredColonyActScore()
        {
            if (!pendingColonyActScore) return;
            pendingColonyActScore = false;
            PendingColonyActScoreBuildings.Remove(this);
            if (Owner == Owner.Player1)
                ColonyActManager.Instance?.GrantTileScore(this);
        }

        /// <summary>Flush all buildings that completed instantly before their card week was spent.</summary>
        public static void FlushAllDeferredColonyActScores()
        {
            for (int i = PendingColonyActScoreBuildings.Count - 1; i >= 0; i--)
            {
                var building = PendingColonyActScoreBuildings[i];
                if (building == null)
                {
                    PendingColonyActScoreBuildings.RemoveAt(i);
                    continue;
                }

                building.FlushDeferredColonyActScore();
            }
        }

        public static bool HasPendingDeferredColonyActScores()
        {
            for (int i = PendingColonyActScoreBuildings.Count - 1; i >= 0; i--)
            {
                if (PendingColonyActScoreBuildings[i] == null)
                    PendingColonyActScoreBuildings.RemoveAt(i);
            }

            return PendingColonyActScoreBuildings.Count > 0;
        }

        public void CompleteConstruction()
        {
            Debug.Log($"[BaseBuilding] CompleteConstruction called on {gameObject.name} (Owner={Owner})");

            // Guard against double-completion (can be called from both WorkerBrainController and Start)
            if (hasCompletedConstruction) return;
            hasCompletedConstruction = true;

            CurrentHealth = MaxHealth;
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1);
            unitBuildingThis = null;
            // Prefabs often ship with BaseBuilding disabled — climate / production need Update.
            enabled = true;
            EnsureBuildingHealthBar();
            Supplies.BeginColonyIntegrityIfNeeded(this);

            // Combolands: finished tile grants Colony Score (+ Habitability for climate tags).
            if (Owner == Owner.Player1)
            {
                if (deferColonyActScore)
                {
                    deferColonyActScore = false;
                    pendingColonyActScore = true;
                    if (!PendingColonyActScoreBuildings.Contains(this))
                        PendingColonyActScoreBuildings.Add(this);
                }
                else
                {
                    ColonyActManager.Instance?.GrantTileScore(this);
                }
            }

            // Drone builds finish after the reserved-site spawn frame; re-wire cluster solar now.
            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.EnsureClusterPowerForBuilding(this);

            // Turn on vision when completed
            if (VisionTransform != null)
            {
                VisionTransform.gameObject.SetActive(Owner == Owner.Player1);
            }

            // Crush any rocks/supplies underneath when construction is completed!
            bool isCommandPost = BuildingSO != null && (BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
            if (isCommandPost)
            {
                Collider hitbox = GetComponent<Collider>();
                if (hitbox != null)
                {
                    Collider[] crushed = Physics.OverlapBox(
                        hitbox.bounds.center,
                        hitbox.bounds.extents,
                        Quaternion.identity,
                        LayerMask.GetMask("Supplies")
                    );
                    foreach (var rock in crushed)
                    {
                        if (rock != null)
                        {
                            Destroy(rock.gameObject);
                        }
                    }
                }
            }



            if (MainRenderer != null && primaryMaterial != null)
            {
                MainRenderer.material = primaryMaterial;
            }

            SpawnWaterVisualEffect();

            // Attach a LifeSupportNode so GlobalDecayManager protects this building and those nearby.
            bool isLifeSupportBldg = BuildingSO != null && (BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
            bool isOxygenProcessor = BuildingSO != null && (BuildingSO.Name.Contains("Oxygen", System.StringComparison.OrdinalIgnoreCase));
            if ((BuildingSO != null && BuildingSO.IsLifeSupport) || isLifeSupportBldg || isOxygenProcessor)
            {
                if (!TryGetComponent<LifeSupportNode>(out _))
                {
                    var node = gameObject.AddComponent<LifeSupportNode>();
                    node.Radius = isLifeSupportBldg ? Mathf.Max(BuildingSO.LifeSupportRadius, 30f) :
                                  isOxygenProcessor ? Mathf.Max(BuildingSO.LifeSupportRadius, 25f) :
                                  BuildingSO.LifeSupportRadius;
                }

                AssignUniqueName();
            }

            // Activate any procedural visual effects (e.g. SmokestackVisuals).
            GetComponent<SmokestackVisuals>()?.ActivateSmoke();

            // Combolands: former RTS build menu options become hand cards next week.
            if (Owner == Owner.Player1)
                CardDeckController.Instance?.QueueProductionFromBuilding(this);

            // Orthogonal tile neighbors share power automatically (no manual Connect Power).
            AutoConnectAdjacentPowerNodes();

            // This is where available commandables are named.
            if (BuildingSO != null)
            {
                if (BuildingSO.Name.Contains("Deep-Core Mining Laser"))
                {
                    AddActiveAbilityCommand("Fire Mining Laser", "Extract deep-core thermal energy.", 2f, 0f, 0f, 200, 0);
                }
                else if (BuildingSO.Name.Contains("Carbon Dioxide Import Laser"))
                {
                    AddActiveAbilityCommand("Import CO2", "Direct orbital laser to vaporize comets.", 0f, 0.05f, 0f, 0, 0);
                }
                else if (BuildingSO.Name.Contains("Methanogenic Microbe Spreader"))
                {
                    AddActiveAbilityCommand("Spread Microbes", "Release greenhouse-gas producing microbes.", 1.5f, 0f, 0f, 0, 30);
                }
                else if (BuildingSO.Name.Contains("Genetically Modified Algae Spreader"))
                {
                    AddActiveAbilityCommand("Spread Algae", "Disperse oxygen-generating algae cultures.", 0f, 0f, 2.0f, 0, 60);
                }
                // GHG Factory now generates temperature and atmosphere passively — see UpkeepRoutine
                else if (BuildingSO.Name.Contains("Atmospheric Condenser"))
                {
                    // Passive generation is the primary effect; keep a small oxygen bonus.
                    AddActiveAbilityCommand("Condense Atmosphere", "Concentrate atmospheric gases.", 0f, 0.05f, 0.1f, 0, 0);
                }
                else if (BuildingSO.Name.Contains("Basalt Strip-Mine"))
                {
                    AddActiveAbilityCommand("Strip-Mine Basalt", "Mine surface basalt to gain construction materials.", 0f, 0f, 0f, 150, 0);
                }
                else if (BuildingSO.Name.Contains("Water Ice Aquifer"))
                {
                    AddActiveAbilityCommand("Extract Water Ice", "Melt and extract subterranean ice reservoirs.", 0f, 0f, 0f, 0, 0, 5.0f);
                }
                else if (BuildingSO.Name.Contains("Subglacial Water Extractor"))
                {
                    AddActiveAbilityCommand("Pump Subglacial Water", "Pump subglacial water to surface reservoirs.", 0f, 0f, 0f, 0, 0, 8.0f);
                }
                else if (BuildingSO.Name.Contains("Biosphere Center"))
                {
                    AddActiveAbilityCommand("Release Glacial Melt", "Release glacial water to global biosphere.", 0f, 0f, 0f, 0, 0, 15.0f);
                }
                else if (BuildingSO.Name.Contains("Lake"))
                {
                    AddActiveAbilityCommand("Refill Lake", "Pump active water lines to expand the lake surface.", 0f, 0f, 0f, 0, 0, 10.0f);
                }
            }

            if (BuildingSO != null && BuildingSO.BuildingConfig != null)
            {
                if (BuildingSO.BuildingConfig.PowerUpkeep > 0)
                {
                    var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                    if (pNode != null)
                    {
                        pNode.OnPowerStateChanged += HandlePowerStateChanged;
                    }
                }

                UpdateHousingContribution();
                
                // Note: The periodic UpkeepRoutine has been replaced with the new GameFlowManager's OnTurnUpkeep/OnTurnIncome events.


                // Trigger grid recalculation on construction completed
                GameDevTV.RTS.Environment.PowerGridManager.RecalculateGrids();
            }
            if (isCommandPost && Owner == Owner.Player1)
            {
                var workers = Object.FindObjectsByType<Worker>(FindObjectsInactive.Include);
                int count = 0;
                foreach (var w in workers)
                {
                    if (w != null && w.Owner == Owner.Player1) count++;
                }

                if (count == 0)
                {
                    // 1. Spawn Colony Commander (VIP) only on the first Command Post of the run.
#if UNITY_EDITOR
                    var commanderSO = UnityEditor.AssetDatabase.LoadAssetAtPath<AbstractUnitSO>("Assets/Units/Rifleman/Rifleman.asset");
#else
                    var commanderSO = Resources.Load<AbstractUnitSO>("Units/Rifleman");
#endif
                    if (commanderSO != null && commanderSO.Prefab != null)
                    {
                        Vector3 spawnPos = transform.position + new Vector3(-5f, 0f, -5f);
                        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                        }
                        GameObject instance = Instantiate(commanderSO.Prefab, spawnPos, Quaternion.identity);
                        if (instance.TryGetComponent(out AbstractCommandable commandable))
                        {
                            commandable.Owner = Owner.Player1;
                        }
                        var colonist = instance.AddComponent<MartianColonist>();
                        colonist.EnterBuilding(this);
                        Debug.Log("[BaseBuilding] Spawned Colony Commander (VIP) for Player 1 inside Command Post.");
                    }
                }

                // One free working Mining Drone per sector Act (in-world unit, not a hand card).
                GameDevTV.RTS.Utilities.SectorMiningDroneBootstrap.TryGrantForCommandPost(this);
            }

            if (isCommandPost && Owner == Owner.Player1)
            {
                if (GenerationManager.Instance != null && GenerationManager.Instance.IsExpansionPhase)
                {
                    GenerationManager.Instance.CompleteExpansion();
                }
            }

            if (Owner == Owner.Player1 && BuildingSO != null && BuildingSO.BuildingConfig != null &&
                BuildingSO.BuildingConfig.PowerGeneration > 0)
            {
                StartCoroutine(ConnectPowerGeneratorToCommandPost());
            }

            GameDevTV.RTS.Environment.PowerGridManager.RecalculateGrids();

            // Register with BuildingUpkeepManager for Materials upkeep tax
            if (GameDevTV.RTS.Player.BuildingUpkeepManager.Instance != null)
            {
                GameDevTV.RTS.Player.BuildingUpkeepManager.Instance.RegisterBuilding(this);
            }

            RaiseSpawnEvent();
        }

        /// <summary>
        /// Wire this building into the power graph of nearby completed buildings.
        /// Ortho neighbors first, then nearest generator within 2 cells so climate
        /// consumers can reach Solar without being stranded on the Command Post grid.
        /// </summary>
        private void AutoConnectAdjacentPowerNodes()
        {
            if (!TryGetComponent(out PowerNode myNode)) return;

            Vector2Int cell = ColonyTileGrid.WorldToCell(transform.position);
            var neighbors = new List<BaseBuilding>(4);
            ColonyTileGrid.CollectOrthogonalNeighborBuildings(cell, Owner, neighbors);

            int linked = 0;
            for (int i = 0; i < neighbors.Count; i++)
            {
                BaseBuilding other = neighbors[i];
                if (other == null || other == this) continue;
                if (other.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (!other.TryGetComponent(out PowerNode otherNode)) continue;
                if (myNode.ConnectedNodes.Contains(otherNode)) continue;

                myNode.ConnectTo(otherNode);
                linked++;
            }

            // Reach a generator even if not ortho-adjacent (Manhattan ≤ 2).
                linked += ConnectToNearestPowerGenerator(myNode, maxManhattan: 4);

            if (linked > 0)
                Debug.Log($"[Power] {name} auto-linked ({linked} connection(s)).");
        }

        /// <summary>
        /// Connect to the nearest completed building that generates power within
        /// <paramref name="maxManhattan"/> tile steps. Skips Command Posts so Solar
        /// clusters are not drained by CP upkeep.
        /// </summary>
        private int ConnectToNearestPowerGenerator(PowerNode myNode, int maxManhattan)
        {
            if (myNode == null) return 0;

            Vector2Int myCell = ColonyTileGrid.WorldToCell(transform.position);
            BaseBuilding best = null;
            int bestDist = int.MaxValue;

            foreach (var b in ActiveBuildings)
            {
                if (b == null || b == this || b.Owner != Owner) continue;
                if (b.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                var def = b.ResolvedBuildingSO;
                if (def?.BuildingConfig == null || def.BuildingConfig.PowerGeneration <= 0f) continue;
                if (def.Name != null && def.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!b.TryGetComponent(out PowerNode otherNode)) continue;
                if (myNode.ConnectedNodes.Contains(otherNode)) continue;

                Vector2Int otherCell = ColonyTileGrid.WorldToCell(b.transform.position);
                int dist = Mathf.Abs(myCell.x - otherCell.x) + Mathf.Abs(myCell.y - otherCell.y);
                if (dist <= 0 || dist > maxManhattan) continue;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = b;
                }
            }

            if (best != null && best.TryGetComponent(out PowerNode genNode))
            {
                myNode.ConnectTo(genNode);
                return 1;
            }
            return 0;
        }

        private IEnumerator ConnectPowerGeneratorToCommandPost()
        {
            yield return null;

            // Solar panels power their cluster consumer (Oxygen Processor, etc.).
            // Never merge them into the Command Post grid — CP upkeep would steal
            // the only watts those buildings need. CP uses its own backup cells.
            bool isSolar = BuildingSiteRegistry.IsSolarBuilding(BuildingSO)
                || BuildingSiteRegistry.IsClusterSolar(this)
                || (BuildingSO != null && BuildingSO.Name.Contains("Solar", System.StringComparison.OrdinalIgnoreCase));
            if (isSolar)
            {
                if (TryGetComponent(out PowerNode solarNode))
                {
                    DisconnectCommandPostLinks(solarNode);
                }
                Debug.Log($"[Power] Solar {name} stays off the Command Post grid.");
                yield break;
            }

            // Reserved-site cluster consumers (Geothermal, GHG, Aquifer, etc.) are
            // wired to their pad's solar — do not also auto-link them to the CP.
            if (BuildingSiteRegistry.TryGetSiteForBuilding(this, out BuildingSiteSlot site)
                && site.Cluster != null
                && (site.Kind == BuildingSiteKind.PairedBuilding
                    || site.Kind == BuildingSiteKind.Infrastructure
                    || site.Kind == BuildingSiteKind.Solar))
            {
                if (TryGetComponent(out PowerNode clusterNode))
                {
                    DisconnectCommandPostLinks(clusterNode);
                }
                Debug.Log($"[Power] Cluster building {name} stays off the Command Post grid.");
                yield break;
            }

            // Geothermal is a paired climate generator even if site lookup races.
            if (BuildingSO != null &&
                BuildingSO.Name.Contains("Geothermal", System.StringComparison.OrdinalIgnoreCase))
            {
                if (TryGetComponent(out PowerNode geoNode))
                {
                    DisconnectCommandPostLinks(geoNode);
                }
                Debug.Log($"[Power] Geothermal {name} stays off the Command Post grid.");
                yield break;
            }

            if (!TryGetComponent(out PowerNode generatorNode)) yield break;

            BaseBuilding nearestCommandPost = null;
            float nearestDistance = float.MaxValue;
            foreach (BaseBuilding building in ActiveBuildings)
            {
                if (building == null || building == this || building.Owner != Owner.Player1 ||
                    building.Progress.State != BuildingProgress.BuildingState.Completed || building.BuildingSO == null ||
                    !building.BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float distance = (building.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCommandPost = building;
                }
            }

            if (nearestCommandPost != null && nearestCommandPost.TryGetComponent(out PowerNode commandPostNode) &&
                !generatorNode.ConnectedNodes.Contains(commandPostNode))
            {
                generatorNode.ConnectTo(commandPostNode);
                Debug.Log($"[Power] Automatically connected {name} to {nearestCommandPost.name}.");
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

        private void AddActiveAbilityCommand(string name, string desc, float tempBonus, float atmosBonus, float oxyBonus, int matsBonus, int bioBonus, float waterBonus = 0f)
        {
            var cmd = ScriptableObject.CreateInstance<GameDevTV.RTS.Commands.ActiveAbilityCommand>();
            cmd.Initialize(name, desc, tempBonus, atmosBonus, oxyBonus, matsBonus, bioBonus, waterBonus);

            var list = new System.Collections.Generic.List<GameDevTV.RTS.Commands.BaseCommand>(AvailableCommands);
            cmd.Slot = FindFreeSlot(list);
            list.Add(cmd);
            AvailableCommands = list.ToArray();
        }

        private void SpawnWaterVisualEffect()
        {
            if (BuildingSO == null) return;

            bool isAquifer = BuildingSO.Name.Contains("Water Ice Aquifer", System.StringComparison.OrdinalIgnoreCase);
            bool isExtractor = BuildingSO.Name.Contains("Subglacial Water Extractor", System.StringComparison.OrdinalIgnoreCase);
            bool isBiosphere = BuildingSO.Name.Contains("Biosphere Center", System.StringComparison.OrdinalIgnoreCase);
            bool isLake = BuildingSO.Name.Contains("Lake", System.StringComparison.OrdinalIgnoreCase);

            if (!(isAquifer || isExtractor || isBiosphere || isLake)) return;

            // Abstract marker — a solid geometric shape, not a fake water surface.
            float size = isBiosphere ? 6f : (isLake ? 5f : (isExtractor ? 4f : 3f));
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Water Resource Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, size * 0.55f, 0f);
            marker.transform.localScale = Vector3.one * size;

            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                Color blue = new Color(0.2f, 0.55f, 1f, 1f);
                mat.color = blue;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", blue);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.75f);
                renderer.material = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (isLake)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r != renderer) r.enabled = false;
                }
            }
        }

        private void HandleTurnUpkeep()
        {
            if (!gameObject.activeInHierarchy || Progress.State != BuildingProgress.BuildingState.Completed) return;
            var config = BuildingSO.BuildingConfig;
            if (config == null) return;

            // Grid-powered buildings (solar clusters) are covered by PowerNode — do not
            // drain/damage them for the separate Energy stockpile currency.
            if (config.PowerUpkeep > 0)
            {
                var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                if (pNode != null && pNode.IsPowered)
                {
                    return;
                }

                float curEnergy = Supplies.Energy != null && Supplies.Energy.TryGetValue(Owner, out float e) ? e : 0f;
                if (curEnergy >= config.PowerUpkeep)
                {
                    Supplies.UpdateEnergy(Owner, curEnergy - config.PowerUpkeep);
                }
                else
                {
                    TakeDamage((int)Mathf.Min(20f, CurrentHealth - 1f));
                }
            }

            if (config.BiomassUpkeep > 0)
            {
                float curBiomass = Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner, out float b) ? b : 0f;
                Supplies.UpdateBiomass(Owner, Mathf.Max(0f, curBiomass - config.BiomassUpkeep));
            }

            if (config.OxygenUpkeep > 0)
            {
                float curOxygen = Supplies.Oxygen != null && Supplies.Oxygen.TryGetValue(Owner, out float o) ? o : 0;
                Supplies.UpdateOxygen(Owner, Mathf.Max(0, curOxygen - config.OxygenUpkeep));
            }
        }

        private void HandleTurnIncome()
        {
            if (!gameObject.activeInHierarchy || Progress.State != BuildingProgress.BuildingState.Completed) return;
            var config = BuildingSO.BuildingConfig;
            if (config == null) return;

            float efficiency = ProductionEfficiency;
            if (efficiency <= 0f) return;

            if (config.PowerGeneration > 0)
            {
                float curE = Supplies.Energy != null && Supplies.Energy.TryGetValue(Owner, out float eng) ? eng : 0f;
                Supplies.UpdateEnergy(Owner, curE + config.PowerGeneration * efficiency);
            }

            if (config.BiomassGeneration > 0)
            {
                float bioGen = config.BiomassGeneration * efficiency;
                float curBiomass = Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner, out float b) ? b : 0f;
                Supplies.UpdateBiomass(Owner, curBiomass + bioGen);

                bool isGreenhouse = BuildingSO != null && BuildingSO.Name.Contains("Greenhouse", System.StringComparison.OrdinalIgnoreCase);
                if (isGreenhouse)
                {
                    float foodGen = bioGen * 0.5f;
                    if (MartianColonist.Instance != null && MartianColonist.Instance.IsInside && MartianColonist.Instance.CurrentBuilding == this)
                    {
                        foodGen *= 1.5f;
                    }
                    float curFood = Supplies.Food != null && Supplies.Food.TryGetValue(Owner, out float f) ? f : 0f;
                    Supplies.UpdateFood(Owner, curFood + foodGen);
                }
            }
        }

        /// <summary>
        /// Config climate rates are per-second. Scales by <see cref="ProductionEfficiency"/>
        /// (unpowered crawl vs full when grid-powered).
        /// </summary>
        public void TickClimateGeneration(float dt)
        {
            if (dt <= 0f) return;
            if (!gameObject.activeInHierarchy) return;
            if (Progress.State != BuildingProgress.BuildingState.Completed) return;

            // Colony Acts: climate buildings anywhere on the planet push Act meters.
            // (Geography expands via Command Posts; Acts are independent of sectors.)
            if (SectorManager.Instance != null
                && !SectorManager.Instance.DoesBuildingCountForActiveClimate(this))
            {
                return;
            }

            BuildingSO def = ResolvedBuildingSO;
            if (def?.BuildingConfig == null) return;

            var config = def.BuildingConfig;
            float atmosRate = config.AtmosphereGeneration;
            float tempRate = config.TemperatureGeneration;
            float waterRate = config.WaterGeneration;

            // Name-based fallbacks for stale/zeroed configs (apply per-channel).
            if (def.Name != null)
            {
                string n = def.Name;
                if (atmosRate <= 0f
                    && (n.Contains("Atmospheric Condenser", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Carbon Dioxide Import", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("GHG", System.StringComparison.OrdinalIgnoreCase)))
                {
                    atmosRate = 0.012f;
                }
                if (tempRate <= 0f
                    && (n.Contains("GHG", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Geothermal", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Methanogenic", System.StringComparison.OrdinalIgnoreCase)))
                {
                    tempRate = 0.2f;
                }
                if (waterRate <= 0f
                    && (n.Contains("Aquifer", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Subglacial", System.StringComparison.OrdinalIgnoreCase)
                        || (n.Contains("Water", System.StringComparison.OrdinalIgnoreCase)
                            && !n.Contains("Processor", System.StringComparison.OrdinalIgnoreCase))))
                {
                    waterRate = 0.1f;
                }
            }

            // Soft caps so a single tile cannot clear an Act channel in seconds.
            tempRate = Mathf.Min(tempRate, 0.3f);
            atmosRate = Mathf.Min(atmosRate, 0.015f);
            waterRate = Mathf.Min(waterRate, 0.12f);

            if (tempRate <= 0f && atmosRate <= 0f && waterRate <= 0f)
            {
                return;
            }

            // Keep trying to link solar neighbors so efficiency can rise to full.
            BuildingSO defForPower = def;
            float powerNeed = defForPower.BuildingConfig != null ? defForPower.BuildingConfig.PowerUpkeep : 0f;
            if (powerNeed > 0f && !IsOperating)
                TryRepairClusterPowerLink();

            float efficiency = ProductionEfficiency;
            if (efficiency <= 0f) return;

            tempRate *= efficiency;
            atmosRate *= efficiency;
            waterRate *= efficiency;

            float tempAdd = tempRate > 0f ? tempRate * dt : 0f;
            float atmosAdd = atmosRate > 0f ? atmosRate * dt : 0f;
            float waterAdd = waterRate > 0f ? waterRate * dt : 0f;

            var acts = ColonyActManager.Instance;
            if (acts != null && acts.IsRunActive)
            {
                if (!acts.TryApplySectorClimateContribution(
                        transform.position, ref tempAdd, ref atmosAdd, ref waterAdd))
                    return;
            }

            Owner climateOwner = Owner != Owner.Invalid ? Owner : Owner.Player1;

            if (tempAdd > 0f)
            {
                float curTemp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(climateOwner, out float t) ? t : -60f;
                Supplies.UpdateTemperature(climateOwner, curTemp + tempAdd);
            }
            if (atmosAdd > 0f)
            {
                float curAtmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(climateOwner, out float a) ? a : 0.01f;
                Supplies.UpdateAtmosphere(climateOwner, curAtmos + atmosAdd);
            }
            if (waterAdd > 0f)
            {
                float curWater = Supplies.Water != null && Supplies.Water.TryGetValue(climateOwner, out float w) ? w : 0f;
                Supplies.UpdateWater(climateOwner, curWater + waterAdd);
            }
        }

        private float nextClusterPowerRetryTime;

        private void TryRepairClusterPowerLink()
        {
            if (Time.time < nextClusterPowerRetryTime) return;
            nextClusterPowerRetryTime = Time.time + 1f;
            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.EnsureClusterPowerForBuilding(this);
            // Free-placed climate tiles: keep retrying neighbor / nearby-solar links.
            AutoConnectAdjacentPowerNodes();
        }

        private void Update()
        {
            // Prefer the global ticker when present; keep a local fallback for editor/tests.
            if (ClimateGenerationTicker.Instance == null)
            {
                TickClimateGeneration(Time.deltaTime);
            }
        }

        public void TryRepair()
        {
            if (Progress.State != BuildingProgress.BuildingState.Completed || CurrentHealth >= MaxHealth) return;

            // Check if player has at least 2 materials
            int currentMats = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner, out int m) ? m : 0;
            if (currentMats >= 2)
            {
                // Deduct 2 materials
                Supplies.UpdateMaterials(Owner, currentMats - 2);

                // Heal structure fully
                Heal(MaxHealth - CurrentHealth);

                Debug.Log($"[BaseBuilding] {gameObject.name} repaired for 2 materials.");

                // Notify GameFlowManager
                if (GameDevTV.RTS.Player.GameFlowManager.Instance != null)
                {
                    GameDevTV.RTS.Player.GameFlowManager.Instance.PlayerActed();
                }
            }
            else
            {
                Debug.Log($"[BaseBuilding] Not enough materials to repair {gameObject.name}. Costs 2.");
            }
        }

        /// <summary>
        /// Scrap this building for a partial Materials refund (store / roguelike economy).
        /// </summary>
        public bool TryDemolish(bool refund = true)
        {
            if (Owner != Owner.Player1) return false;
            if (Progress.State == BuildingProgress.BuildingState.Destroyed) return false;

            BuildingSO def = ResolvedBuildingSO;
            int buildCost = GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.GetMaterialsCost(def);
            int refundAmt = 0;
            if (refund && buildCost > 0)
            {
                // Under construction: larger refund; completed: half back to the store.
                float rate = Progress.State == BuildingProgress.BuildingState.Building ? 0.75f : 0.5f;
                refundAmt = Mathf.Max(0, Mathf.RoundToInt(buildCost * rate));
            }

            string label = def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : gameObject.name;
            if (refundAmt > 0)
            {
                int have = Supplies.Materials != null && Supplies.Materials.TryGetValue(Owner, out int m) ? m : 0;
                Supplies.UpdateMaterials(Owner, have + refundAmt);
                ColonyActManager.Instance?.ShowStatusBanner(
                    $"<color=#FFE08A><b>DEMOLISHED</b></color> {label}  <color=#7CFF9A>+{refundAmt} Materials</color>",
                    4f);
            }
            else
            {
                ColonyActManager.Instance?.ShowStatusBanner(
                    $"<color=#FFE08A><b>DEMOLISHED</b></color> {label}",
                    3f);
            }

            Debug.Log($"[BaseBuilding] Demolished {label} (refund {refundAmt}).");
            GameFlowManager.Instance?.PlayerActed();
            Die();
            return true;
        }

        private void EnsureBuildingHealthBar()
        {
            if (GetComponent<GameDevTV.RTS.UI.Components.BuildingHealthBar>() != null) return;
            gameObject.AddComponent<GameDevTV.RTS.UI.Components.BuildingHealthBar>();
        }

        private void AssignUniqueName()
        {
            if (BuildingSO == null) return;

            // If already has a #, we don't need to rename it
            if (gameObject.name.Contains("#")) return;

            int maxNum = 0;
            string prefix = $"{BuildingSO.Name} #";

            foreach (var b in ActiveBuildings)
            {
                if (b == null || b == this) continue;
                if (b.Owner == Owner && b.name.StartsWith(prefix))
                {
                    string numPart = b.name.Substring(prefix.Length);
                    if (int.TryParse(numPart, out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }
            }

            string newName = $"{prefix}{maxNum + 1}";
            gameObject.name = newName;
            // // Debug.Log($"[BaseBuilding] Renamed {BuildingSO.Name} to {newName} for owner {Owner}");
        }

        /// <summary>
        /// Allows procedural visual components (e.g. SmokestackVisuals) to override
        /// which renderer receives placement/ghost materials during construction.
        /// </summary>
        public void SetMainRenderer(MeshRenderer renderer)
        {
            MainRenderer = renderer;
        }

        /// <summary>
        /// Updates the material Start/CompleteConstruction restore to (used when
        /// SmokestackVisuals swaps from ghost to final opaque metal).
        /// </summary>
        public void SetPrimaryMaterial(Material material)
        {
            if (material == null) return;
            primaryMaterial = material;
            if (MainRenderer != null && Progress.State == BuildingProgress.BuildingState.Completed)
            {
                MainRenderer.material = material;
            }
        }



        public bool IsFirstInQueueProbe()
        {
            return false;
        }

        public void ClearQueue()
        {
            if (productionCoroutine != null)
            {
                StopCoroutine(productionCoroutine);
                productionCoroutine = null;
            }
            buildingQueue.Clear();
            SOBeingBuilt = null;
            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
        }

        public void BuildPriorityUnlockable(UnlockableSO unlockable)
        {
            if (unlockable == null) return;
            
            // Deduct resources
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Minerals, unlockable.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Gas, unlockable.Cost.GasSO));

            // If we were building something, push it back.
            // But if we want it to be "first before anything else", we should probably clear or handle the swap.
            buildingQueue.Insert(0, unlockable);
            Debug.Log($"[BaseBuilding] Priority build queued at index 0: {unlockable.Name}");

            // Restart production coroutine to ensure the first item is processed immediately
            if (productionCoroutine != null)
            {
                StopCoroutine(productionCoroutine);
                productionCoroutine = null;
            }

            if (enabled)
            {
                productionCoroutine = StartCoroutine(DoBuildUnits());
            }

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
        }

        public void BuildUnlockable(UnlockableSO unlockable)
        {
            if (buildingQueue.Count >= MaxQueueSize)
            {
                Debug.LogWarning($"[BaseBuilding] Cannot build {unlockable.Name}: Queue is full ({MaxQueueSize}/{MaxQueueSize}).");
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Minerals, unlockable.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Gas, unlockable.Cost.GasSO));

            buildingQueue.Add(unlockable);

            if (productionCoroutine == null && enabled)
            {
                productionCoroutine = StartCoroutine(DoBuildUnits());
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
        }

        public void CancelBuildingUnit(int index)
        {
            if (index < 0 || index >= buildingQueue.Count)
            {
                Debug.LogError("Attempting to cancel building a unit outside the bounds of the queue!");
                return;
            }

            UnlockableSO unlockableSO = buildingQueue[index];
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Minerals, unlockableSO.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Gas, unlockableSO.Cost.GasSO));
            buildingQueue.RemoveAt(index);
            
            if (index == 0)
            {
                if (productionCoroutine != null)
                {
                    StopCoroutine(productionCoroutine);
                    productionCoroutine = null;
                }

                if (buildingQueue.Count > 0)
                {
                    productionCoroutine = StartCoroutine(DoBuildUnits());
                }
                else
                {
                    OnQueueUpdated?.Invoke(buildingQueue.ToArray());
                }
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
        }

        public void InitializeAsGhost(Material ghostMaterial, Owner owner)
        {
            Owner = owner;
            // Prefabs often serialize Progress as Destroyed; Start() used to treat that as
            // "ready to complete". Always reset completion guards when becoming a ghost so
            // the drone's later CompleteConstruction actually runs.
            hasCompletedConstruction = false;
            hasRaisedSpawnEvent = false;
            deferColonyActScore = false;
            pendingColonyActScore = false;
            PendingColonyActScoreBuildings.Remove(this);
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Paused, 0, 0);
            CurrentHealth = 0;
            Heal(300);
            // Prefer the caller's placement material (Ghost Placement shader). SmokestackVisuals'
            // runtime GhostMaterial is URP Lit and was rendering fully opaque for site pads.
            Material effectiveMat = ghostMaterial != null
                ? ghostMaterial
                : (TryGetComponent<SmokestackVisuals>(out var sv) ? sv.GhostMaterial : null);

            ApplyGhostMaterialToRenderers(effectiveMat);

            if (navMeshObstacle != null)
            {
                navMeshObstacle.enabled = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            if (VisionTransform != null)
            {
                VisionTransform.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Prefer the card/registry BuildingSO (authoritative config) over the prefab's
        /// cloned UnitSO, which can lose climate generation rates.
        /// </summary>
        public void BindBuildingDefinition(BuildingSO definition)
        {
            if (definition == null) return;
            BuildingSO = definition;
            MaxHealth = definition.Health > 0 ? definition.Health : MaxHealth;

            // Patch zeroed climate rates on runtime clones of stale configs.
            if (definition.BuildingConfig != null
                && definition.Name != null)
            {
                string n = definition.Name;
                var cfg = definition.BuildingConfig;
                if (cfg.AtmosphereGeneration <= 0f
                    && (n.Contains("Atmospheric Condenser", System.StringComparison.OrdinalIgnoreCase)
                        || n.Contains("Carbon Dioxide Import", System.StringComparison.OrdinalIgnoreCase)))
                {
                    cfg.AtmosphereGeneration = 0.05f;
                }
                if (n.Contains("GHG", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (cfg.AtmosphereGeneration <= 0f) cfg.AtmosphereGeneration = 0.04f;
                    if (cfg.TemperatureGeneration <= 0f) cfg.TemperatureGeneration = 0.5f;
                }
            }
        }

        private void ApplyGhostMaterialToRenderers(Material ghostMaterial)
        {
            if (ghostMaterial == null) return;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                string nameLower = renderer.gameObject.name.ToLowerInvariant();
                if (nameLower.Contains("vision") || nameLower.Contains("selection") || nameLower.Contains("indicator"))
                {
                    continue;
                }

                renderer.material = ghostMaterial;
            }
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            InitializeIfNeeded();
            unitBuildingThis = buildingBuilder;
            if (unitBuildingThis != null)
                Owner = unitBuildingThis.Owner;
            if (MainRenderer != null)
            {
                // Use the visual override material if present (e.g. dull grey for smokestack).
                Material buildMat = TryGetComponent<SmokestackVisuals>(out var sv2)
                    ? sv2.GhostMaterial
                    : (ResolvedBuildingSO != null ? ResolvedBuildingSO.PlacementMaterial : BuildingSO.PlacementMaterial);
                MainRenderer.material = buildMat;
            }

            float buildTime = Mathf.Max(0.35f, (ResolvedBuildingSO != null ? ResolvedBuildingSO.BuildTime : BuildingSO.BuildTime));
            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Time.time - buildTime * Progress.Completion,
                Progress.Completion
            );

            if (navMeshObstacle != null)
            {
                navMeshObstacle.enabled = true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = true;
            }

            if (Progress.Completion == 0)
            {
                Heal(300);
            }

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<UnitDeathEvent>.OnEvent[Owner] += HandleUnitDeath;
        }

        private Coroutine selfBuildCoroutine;

        /// <summary>
        /// Combolands card place: building finishes immediately (no rise timer / drone).
        /// Defers Colony Act score until after <see cref="CardDeckController.ConsumeCardAfterBuild"/>.
        /// </summary>
        public void CompleteInstantCardPlace(Owner owner, BuildingSO definition = null)
        {
            if (definition != null)
                BindBuildingDefinition(definition);

            Owner = owner;
            enabled = true;
            hasCompletedConstruction = false;
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Building, Time.time, 0);

            DeferColonyActScoreOnce();
            CompleteConstruction();
            GameDevTV.RTS.Utilities.ReservedSiteBuildUtility.GroundBuilding(this);
        }

        /// <summary>
        /// Free place / no-drone fallback: ghost → rise animation → complete.
        /// Card plays use <see cref="CompleteInstantCardPlace"/> instead.
        /// </summary>
        public void BeginSelfConstruction(Owner owner, BuildingSO definition = null, Material ghostMaterial = null)
        {
            if (definition != null)
                BindBuildingDefinition(definition);

            Owner = owner;
            enabled = true;
            hasCompletedConstruction = false;

            Material mat = ghostMaterial != null
                ? ghostMaterial
                : (ResolvedBuildingSO != null ? ResolvedBuildingSO.PlacementMaterial : null);
            InitializeAsGhost(mat, owner);
            StartBuilding(null);

            if (selfBuildCoroutine != null)
                StopCoroutine(selfBuildCoroutine);
            selfBuildCoroutine = StartCoroutine(SelfBuildLoop());
        }

        private IEnumerator SelfBuildLoop()
        {
            BuildingSO def = ResolvedBuildingSO;
            float buildTime = Mathf.Max(0.35f, def != null ? def.BuildTime : 3f);
            if (def?.BuildingConfig != null)
                buildTime *= Mathf.Max(0.1f, def.BuildingConfig.BuildTimeMultiplier);

            Renderer buildingRenderer = MainRenderer;
            Vector3 endPosition = transform.position;
            Vector3 startPosition = endPosition;
            if (buildingRenderer != null)
            {
                startPosition = endPosition - Vector3.up * buildingRenderer.bounds.size.y;
                buildingRenderer.transform.position = startPosition;
            }

            float elapsed = 0f;
            float targetHealth = 0f;
            float maxHealth = def != null ? def.Health : MaxHealth;

            while (elapsed < buildTime)
            {
                elapsed += Time.deltaTime;
                float normalized = elapsed / buildTime;

                targetHealth += Time.deltaTime * (maxHealth / buildTime);
                if (targetHealth >= 1f)
                {
                    int healAmount = Mathf.FloorToInt(targetHealth);
                    Heal(healAmount);
                    targetHealth -= healAmount;
                }

                if (buildingRenderer != null)
                    buildingRenderer.transform.position = Vector3.Lerp(startPosition, endPosition, normalized);

                // Keep Progress.Completion moving for any UI that reads it.
                Progress = new BuildingProgress(
                    BuildingProgress.BuildingState.Building,
                    Time.time - buildTime * normalized,
                    Mathf.Clamp01(normalized));

                yield return null;
            }

            if (buildingRenderer != null && buildingRenderer.transform != transform)
                buildingRenderer.transform.localPosition = Vector3.zero;

            enabled = true;
            CompleteConstruction();
            ReservedSiteBuildUtility.GroundBuilding(this);
            selfBuildCoroutine = null;
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit.TryGetComponent(out IBuildingBuilder buildingBuilder) && buildingBuilder == unitBuildingThis)
            {
                Progress = new BuildingProgress(
                    BuildingProgress.BuildingState.Paused,
                    Progress.StartTime,
                    (Time.time - Progress.StartTime) / BuildingSO.BuildTime
                );

                Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            }
        }

        private IEnumerator DoBuildUnits()
        {
            try
            {
                while (buildingQueue.Count > 0)
                {
                    SOBeingBuilt = buildingQueue[0];
                    CurrentQueueStartTime = Time.time;
                    OnQueueUpdated?.Invoke(buildingQueue.ToArray());

                    float buildTime = SOBeingBuilt.BuildTime;
                    if (BuildingSO != null && BuildingSO.BuildingConfig != null)
                    {
                        buildTime *= BuildingSO.BuildingConfig.BuildTimeMultiplier;
                    }

                    float elapsed = 0f;
                    bool isCommandPost = BuildingSO != null && BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    bool needsPower = !isCommandPost && BuildingSO != null && BuildingSO.BuildingConfig != null && BuildingSO.BuildingConfig.PowerUpkeep > 0;
                    GameDevTV.RTS.Environment.PowerNode pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();

                    while (elapsed < buildTime)
                    {
                        bool isPowered = true;
                        if (needsPower && pNode != null && !pNode.IsPowered)
                        {
                            isPowered = false;
                        }

                        if (isPowered)
                        {
                            elapsed += Time.deltaTime;
                        }

                        // Update UI timer (which is based on CurrentQueueStartTime). We shift the start time forward when stalled so the UI doesn't think it's done.
                        if (!isPowered)
                        {
                            CurrentQueueStartTime += Time.deltaTime;
                        }

                        yield return null;
                    }

                    if (SOBeingBuilt is AbstractUnitSO unitSO)
                    {
                        // Determine AgentTypeID from the prefab
                        int agentTypeID = 0; // Default to Humanoid
                        if (unitSO.Prefab.TryGetComponent(out NavMeshAgent prefabAgent))
                        {
                            agentTypeID = prefabAgent.agentTypeID;
                        }

                        bool isAirUnit = agentTypeID != 0;

                        // Calculate building footprint radius to ensure ground units spawn outside the NavMeshObstacle
                        float buildingRadius = 4f; // Safe fallback
                        if (navMeshObstacle != null)
                        {
                            if (navMeshObstacle.shape == NavMeshObstacleShape.Capsule)
                            {
                                buildingRadius = navMeshObstacle.radius + 1f;
                            }
                            else if (navMeshObstacle.shape == NavMeshObstacleShape.Box)
                            {
                                buildingRadius = Mathf.Max(navMeshObstacle.size.x, navMeshObstacle.size.z) * 0.5f + 1f;
                            }
                        }

                        // Distribute drones evenly around the building to prevent overlap.
                        // Each unit gets a base angle of (360 / goldenRatio) * spawnCount to spread them
                        // naturally, plus a small random jitter to avoid perfect symmetry.
                        float goldenAngle = 137.508f * Mathf.Deg2Rad; // Golden angle for natural spread
                        float baseAngle = goldenAngle * spawnCount;
                        float jitter = Random.Range(-0.2f, 0.2f);
                        float angle = baseAngle + jitter;
                        spawnCount++;

                        float distance = isAirUnit ? Random.Range(4f, 8f) : Random.Range(buildingRadius + 1f, buildingRadius + 3f);
                        
                        float heightOffset = 0f;
                        if (isAirUnit && PlanetGenerator.Instance != null)
                        {
                            heightOffset = PlanetGenerator.Instance.AirUnitFlightHeight;
                        }

                        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, heightOffset, Mathf.Sin(angle) * distance);
                        Vector3 spawnPosition = transform.position + offset;

                        // Snap to NavMesh for the specific agent type
                        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas };
                        bool onNavMesh = NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 15f, filter);
                        
                        if (onNavMesh)
                        {
                            spawnPosition = hit.position;
                        }
                        else
                        {
                            // CRITICAL: Ensure the fallback spawn position is at the flight height for Air units
                            // so they are close enough to the Air NavMesh even if SamplePosition fails.
                            // Air Units have agentTypeID -1372625422 and standard flight height is 4.0
                            if (agentTypeID == -1372625422)
                            {
                                spawnPosition = transform.position + offset + Vector3.up * 4f;
                            }
                            else
                            {
                                spawnPosition = transform.position + offset;
                            }
                        }

                        GameObject instance = Instantiate(unitSO.Prefab, spawnPosition, Quaternion.identity);
                        if (instance.TryGetComponent(out AbstractCommandable commandable))
                        {
                            commandable.Owner = Owner;
                        }
                        
                        if (instance.TryGetComponent(out NavMeshAgent agent))
                        {
                            agent.enabled = false; 
                            instance.transform.position = spawnPosition;
                            
                            if (onNavMesh)
                            {
                                agent.enabled = true;
                                agent.Warp(spawnPosition);
                            }
                            else
                            {
                                // // Debug.LogWarning($"[BaseBuilding] Disabled NavMeshAgent on {instance.name} because no NavMesh was found for type {agentTypeID} at spawn location.");
                            }
                        }


                    }
                    else if (SOBeingBuilt is UpgradeSO upgrade)
                    {
                        Bus<UpgradeResearchedEvent>.Raise(Owner, new UpgradeResearchedEvent(Owner, upgrade));
                    }

                    buildingQueue.RemoveAt(0);
                }
            }
            finally
            {
                SOBeingBuilt = null;
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
                productionCoroutine = null;
            }
        }

        public void RemoveBuildUnitCommand(AbstractUnitSO unitSO)
        {
            var list = new System.Collections.Generic.List<GameDevTV.RTS.Commands.BaseCommand>(overrideCommands ?? _availableCommands);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] is GameDevTV.RTS.Commands.BuildUnitCommand buc && buc.Unit == unitSO)
                {
                    list.RemoveAt(i);
                    break;
                }
            }
            overrideCommands = list.ToArray();
            
            // Draw a card since a consumable unit card was played/constructed
            if (GameDevTV.RTS.Player.CardDeckController.Instance != null)
            {
                GameDevTV.RTS.Player.CardDeckController.Instance.DrawCard();
            }
            // Trigger refresh in UI globally and safely
            GameDevTV.RTS.EventBus.Bus<GameDevTV.RTS.Events.UpgradeResearchedEvent>.Raise(Owner, new GameDevTV.RTS.Events.UpgradeResearchedEvent(Owner, null));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            if (hasRaisedSpawnEvent)
            {
                Bus<BuildingDeathEvent>.Raise(Owner, new BuildingDeathEvent(Owner, this));
            }
        }

        protected override void OnGainVisibility()
        {
            base.OnGainVisibility();
            if (culledVisuals != null)
            {
                culledVisuals.gameObject.SetActive(false);
            }
        }

        protected override void OnLoseVisibility()
        {
            base.OnLoseVisibility();

            if (culledVisuals == null && MainRenderer != null)
            {
                Transform originalRendererTransform = MainRenderer.transform;
                GameObject culledGO = new ($"Culled {BuildingSO.Name} Visuals")
                {
                    layer = LayerMask.NameToLayer("TransparentFX"),
                };
                culledGO.transform.SetParent(transform);
                culledGO.transform.position = originalRendererTransform.position;
                culledGO.transform.rotation = originalRendererTransform.rotation;
                culledGO.transform.localScale = originalRendererTransform.localScale;

                culledVisuals = culledGO.AddComponent<Placeholder>();
                culledVisuals.Owner = Owner;
                culledVisuals.ParentObject = gameObject;
                MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
                meshFilter.mesh = MainRenderer.GetComponent<MeshFilter>().mesh;
                MeshRenderer renderer = culledGO.AddComponent<MeshRenderer>();
                renderer.materials = MainRenderer.materials;
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }

        public override BaseCommand[] AvailableCommands
        {
            get
            {
                // Combolands: buildings do not offer RTS build/train menus — everything is cards.
                if (Owner == Owner.Player1)
                    return System.Array.Empty<BaseCommand>();

                BaseCommand[] baseCmds = base.AvailableCommands;

                // Add ExitBuildingCommand if MartianColonist is inside this building
                if (MartianColonist.Instance != null && MartianColonist.Instance.IsInside && MartianColonist.Instance.CurrentBuilding == this)
                {
                    var exitCmd = ScriptableObject.CreateInstance<GameDevTV.RTS.Commands.ExitBuildingCommand>();
                    exitCmd.Slot = 7;
                    var list = new System.Collections.Generic.List<BaseCommand>(baseCmds);
                    list.Add(exitCmd);
                    baseCmds = list.ToArray();
                }

                bool isCommandPost = BuildingSO != null && BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                if (isCommandPost && Owner == Owner.Player1)
                {
                    return GetAugmentedCommands(baseCmds);
                }

                return baseCmds;
            }
        }

        /// <summary>Native prefab commands used to seed hand cards when this building completes.</summary>
        public BaseCommand[] GetNativeCommandsForCardOffers()
        {
            return _availableCommands;
        }

        private BaseCommand[] GetAugmentedCommands(BaseCommand[] cmds)
        {
            if (cmds == null) return null;

            var unlockedBuildingNames = BlueprintDraftManager.GetUnlockedBuildingNames();
            if (unlockedBuildingNames.Count == 0) return cmds;

            var list = new System.Collections.Generic.List<BaseCommand>();
            foreach (var cmd in cmds)
            {
                if (cmd == null) continue;

                if (cmd is OverrideCommandsCommand overrideCmd && overrideCmd.name != null && overrideCmd.name.Contains("Show Buildings"))
                {
                    var augmentedSub = GetAugmentedCommands(overrideCmd.Commands);
                    var newOverrideCmd = ScriptableObject.CreateInstance<OverrideCommandsCommand>();
                    newOverrideCmd.Name = overrideCmd.Name;
                    newOverrideCmd.Icon = overrideCmd.Icon;
                    newOverrideCmd.Slot = overrideCmd.Slot;
                    
                    var field = typeof(OverrideCommandsCommand).GetField("<Commands>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (field != null)
                    {
                        field.SetValue(newOverrideCmd, augmentedSub);
                    }
                    list.Add(newOverrideCmd);
                }
                else
                {
                    list.Add(cmd);
                }
            }

            foreach (var bldName in unlockedBuildingNames)
            {
                var bldSO = BlueprintDraftManager.GetBuildingSOByName(bldName);
                if (bldSO != null)
                {
                    bool alreadyExists = false;
                    foreach (var c in list)
                    {
                        if (c is BuildBuildingCommand bbc && bbc.Building != null && bbc.Building.Name == bldSO.Name)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        var newCmd = ScriptableObject.CreateInstance<BuildBuildingCommand>();
                        newCmd.Name = "Build " + bldSO.Name;
                        newCmd.Building = bldSO;
                        newCmd.Icon = bldSO.Icon;
                        newCmd.Slot = FindFreeSlot(list);

                        // Set restrictions using reflection
                        bool targetIsCommand = bldSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                        var copiedRestrictions = GetTemplateRestrictions(cmds, targetIsCommand) ?? FindAnyRestrictions(cmds);
                        if (copiedRestrictions != null)
                        {
                            var restrictionsField = typeof(BaseCommand).GetField("<Restrictions>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (restrictionsField != null)
                            {
                                restrictionsField.SetValue(newCmd, copiedRestrictions);
                            }
                        }

                        list.Add(newCmd);
                    }
                }
            }

            return list.ToArray();
        }

        private BuildingRestrictionSO[] GetTemplateRestrictions(BaseCommand[] sourceCmds, bool targetIsCommand)
        {
            if (sourceCmds == null) return null;

            foreach (var cmd in sourceCmds)
            {
                if (cmd is BuildBuildingCommand bbc && bbc.Restrictions != null && bbc.Restrictions.Length > 0)
                {
                    bool templateIsCommand = bbc.Building != null && bbc.Building.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                    if (templateIsCommand == targetIsCommand)
                    {
                        return bbc.Restrictions;
                    }
                }
                else if (cmd is OverrideCommandsCommand overrideCmd && overrideCmd.Commands != null)
                {
                    var res = GetTemplateRestrictions(overrideCmd.Commands, targetIsCommand);
                    if (res != null) return res;
                }
            }
            return null;
        }

        private BuildingRestrictionSO[] FindAnyRestrictions(BaseCommand[] sourceCmds)
        {
            if (sourceCmds == null) return null;

            foreach (var cmd in sourceCmds)
            {
                if (cmd is BuildBuildingCommand bbc && bbc.Restrictions != null && bbc.Restrictions.Length > 0)
                {
                    return bbc.Restrictions;
                }
                else if (cmd is OverrideCommandsCommand overrideCmd && overrideCmd.Commands != null)
                {
                    var res = FindAnyRestrictions(overrideCmd.Commands);
                    if (res != null) return res;
                }
            }
            return null;
        }

        private int FindFreeSlot(System.Collections.Generic.List<BaseCommand> list)
        {
            var usedSlots = new System.Collections.Generic.HashSet<int>();
            foreach (var c in list)
            {
                if (c != null) usedSlots.Add(c.Slot);
            }
            for (int i = 0; i < 8; i++)
            {
                if (!usedSlots.Contains(i)) return i;
            }
            return -1;
        }
    }
}
