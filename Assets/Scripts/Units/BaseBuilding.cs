using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Commands;
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
        public bool IsOperating
        {
            get
            {
                if (Progress.State != BuildingProgress.BuildingState.Completed) return false;
                
                bool isCommandPost = BuildingSO != null && BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                if (isCommandPost) return true;

                bool needsPower = BuildingSO != null && BuildingSO.BuildingConfig != null && BuildingSO.BuildingConfig.PowerUpkeep > 0;
                if (needsPower)
                {
                    var pNode = GetComponent<GameDevTV.RTS.Environment.PowerNode>();
                    if (pNode != null && !pNode.IsPowered) return false;
                }
                return true;
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
                    Material selectionMat = null;
                    foreach (var cmd in ActiveCommandables)
                    {
                        if (cmd != null)
                        {
                            var cmdIndicator = cmd.transform.Find("Selection Indicator");
                            if (cmdIndicator != null)
                            {
                                var renderer = cmdIndicator.GetComponent<MeshRenderer>();
                                if (renderer != null && renderer.sharedMaterial != null)
                                {
                                    selectionMat = renderer.sharedMaterial;
                                    break;
                                }
                            }
                        }
                    }

                    if (selectionMat == null && GameConfiguration.Instance != null && GameConfiguration.Instance.CommandPostPrefab != null)
                    {
                        var cpIndicator = GameConfiguration.Instance.CommandPostPrefab.transform.Find("Selection Indicator");
                        if (cpIndicator != null)
                        {
                            var renderer = cpIndicator.GetComponent<MeshRenderer>();
                            if (renderer != null)
                            {
                                selectionMat = renderer.sharedMaterial;
                            }
                        }
                    }

                    if (selectionMat == null)
                    {
                        selectionMat = Resources.Load<Material>("Materials/SelectionIndicator");
                    }
                    if (selectionMat == null)
                    {
                        selectionMat = Resources.Load<Material>("SelectionIndicator");
                    }
#if UNITY_EDITOR
                    if (selectionMat == null)
                    {
                        selectionMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SelectionIndicator.mat");
                    }
#endif

                    GameObject indicatorGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    indicatorGO.name = "Selection Indicator";
                    var col = indicatorGO.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                    indicatorGO.transform.SetParent(transform, false);
                    indicatorGO.transform.localPosition = new Vector3(0f, 0.05f, 0f);
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

                    var mr = indicatorGO.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        if (selectionMat != null)
                        {
                            mr.sharedMaterial = selectionMat;
                        }
                    }

                    indicatorGO.SetActive(false);
                    selectionIndicator = indicatorGO;
                }
            }

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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ActiveBuildings.Remove(this);

            // Unregister from BuildingUpkeepManager
            if (GameDevTV.RTS.Player.BuildingUpkeepManager.Instance != null)
            {
                GameDevTV.RTS.Player.BuildingUpkeepManager.Instance.UnregisterBuilding(this);
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
                Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));
            }
        }

        protected override void Start()
        {
            base.Start();

            // Ensure we are initialized before checking BuildingSO
            InitializeIfNeeded();

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

            // Only apply material and auto-complete if we are NOT a ghost waiting for a drone
            if (Progress.State != BuildingProgress.BuildingState.Paused)
            {
                if (MainRenderer != null && primaryMaterial != null)
                {
                    MainRenderer.material = primaryMaterial;
                }
                
                // If the building is already completed (e.g. spawned by AI), ensure it has health and completed progress
                if (unitBuildingThis == null)
                {
                    CompleteConstruction();
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

        public void CompleteConstruction()
        {
            Debug.Log($"[BaseBuilding] CompleteConstruction called on {gameObject.name} (Owner={Owner})");

            // Guard against double-completion (can be called from both WorkerBrainController and Start)
            if (hasCompletedConstruction) return;
            hasCompletedConstruction = true;

            CurrentHealth = MaxHealth;
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1);
            unitBuildingThis = null;

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

            // Add the dynamic Connect Power command if it doesn't already have one
            bool hasConnectCommand = false;
            foreach (var cmd in AvailableCommands)
            {
                if (cmd is GameDevTV.RTS.Commands.ConnectPowerCommand)
                {
                    hasConnectCommand = true;
                    break;
                }
            }

            if (!hasConnectCommand)
            {
                var connectCommand = ScriptableObject.CreateInstance<GameDevTV.RTS.Commands.ConnectPowerCommand>();
                var nameField = typeof(GameDevTV.RTS.Commands.BaseCommand).GetField("<Name>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (nameField != null) nameField.SetValue(connectCommand, "Connect Power");

                var iconField = typeof(GameDevTV.RTS.Commands.BaseCommand).GetField("<Icon>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (iconField != null) iconField.SetValue(connectCommand, UnityEngine.Resources.Load<UnityEngine.Sprite>("PlugIcon"));

                var commandList = new System.Collections.Generic.List<GameDevTV.RTS.Commands.BaseCommand>(AvailableCommands);
                connectCommand.Slot = FindFreeSlot(commandList);
                commandList.Add(connectCommand);
                AvailableCommands = commandList.ToArray();
            }
            
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
                    AddActiveAbilityCommand("Condense Atmosphere", "Extract and concentrate atmospheric gases to enrich oxygen.", 0f, 0f, 0.5f, 0, 0);
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
                StartCoroutine(UpkeepRoutine());

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
                    var miningDroneSO = Resources.Load<AbstractUnitSO>("Units/MiningDrone");
                    if (miningDroneSO == null)
                    {
#if UNITY_EDITOR
                        miningDroneSO = UnityEditor.AssetDatabase.LoadAssetAtPath<AbstractUnitSO>("Assets/Resources/Units/MiningDrone.asset");
#endif
                    }

                    if (miningDroneSO != null && miningDroneSO.Prefab != null)
                    {
                        Vector3 spawnPos = transform.position + new Vector3(5f, 0f, 5f);
                        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 15f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            spawnPos = hit.position;
                        }
                        GameObject instance = Instantiate(miningDroneSO.Prefab, spawnPos, Quaternion.identity);
                        if (instance.TryGetComponent(out AbstractCommandable commandable))
                        {
                            commandable.Owner = Owner.Player1;
                        }
                        Debug.Log("[BaseBuilding] Spawned 1 free starting Mining Drone for Player 1 to prevent softlock.");
                    }
                }
            }

            if (isCommandPost && Owner == Owner.Player1)
            {
                if (GenerationManager.Instance != null && GenerationManager.Instance.IsExpansionPhase)
                {
                    GenerationManager.Instance.CompleteExpansion();
                }
            }

            GameDevTV.RTS.Environment.PowerGridManager.RecalculateGrids();

            // Register with BuildingUpkeepManager for Materials upkeep tax
            if (GameDevTV.RTS.Player.BuildingUpkeepManager.Instance != null)
            {
                GameDevTV.RTS.Player.BuildingUpkeepManager.Instance.RegisterBuilding(this);
            }

            RaiseSpawnEvent();
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

            if (isAquifer || isExtractor || isBiosphere || isLake)
            {
                GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                waterPlane.name = "Terraformed Water Body";

                // Position on the ground, slightly offset vertically to avoid z-fighting
                waterPlane.transform.position = transform.position + new Vector3(0f, 0.1f, 0f);

                float radius = isBiosphere ? 25f : (isLake ? 20f : (isExtractor ? 15f : 8f));
                float scale = (radius * 2f) / 10f;
                waterPlane.transform.localScale = new Vector3(scale, 1f, scale);

                var col = waterPlane.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var renderer = waterPlane.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material waterMat = Resources.Load<Material>("Materials/Water");
                    if (waterMat == null)
                    {
#if UNITY_EDITOR
                        waterMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Water.mat");
#endif
                    }
                    if (waterMat != null)
                    {
                        renderer.material = waterMat;
                    }
                    else
                    {
                        Material fallbackMat = new Material(Shader.Find("Standard"));
                        fallbackMat.color = new Color(0f, 0.4f, 0.8f, 0.6f);
                        fallbackMat.SetFloat("_Mode", 3f); // Transparent
                        fallbackMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        fallbackMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        fallbackMat.SetInt("_ZWrite", 0);
                        fallbackMat.DisableKeyword("_ALPHATEST_ON");
                        fallbackMat.EnableKeyword("_ALPHABLEND_ON");
                        fallbackMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        fallbackMat.renderQueue = 3000;
                        renderer.material = fallbackMat;
                    }
                }

                waterPlane.transform.SetParent(transform, true);

                if (isLake)
                {
                    // Hide default metal meshes/structures so that only the water plane is visible
                    foreach (var r in GetComponentsInChildren<Renderer>(true))
                    {
                        if (r != renderer)
                        {
                            r.enabled = false;
                        }
                    }
                }
            }
        }

        private IEnumerator UpkeepRoutine()
        {
            var config = BuildingSO.BuildingConfig;
            if (config == null) yield break;

            Debug.Log($"[BaseBuilding] UpkeepRoutine started on {gameObject.name} — Config='{config?.name}', BuildingSO='{BuildingSO?.name}', PowerGeneration={config.PowerGeneration}");

            while (gameObject.activeInHierarchy && Progress.State == BuildingProgress.BuildingState.Completed)
            {
                yield return new WaitForSeconds(1f);

                bool isOperating = true;

                if (TryGetComponent(out GameDevTV.RTS.Environment.PowerNode powerNode))
                {
                    if (config.PowerUpkeep > 0)
                    {
                        if (!powerNode.IsGridPowered)
                        {
                            if (TryGetComponent(out GameDevTV.RTS.Environment.BatteryNode battery) && battery.HasCharge)
                            {
                                battery.Drain(1f);
                            }
                        }

                        if (!powerNode.IsPowered)
                        {
                            isOperating = false;
                        }
                    }
                }

                // Generation is managed globally via PowerGridManager.RecalculateGrids()

                if (isOperating)
                {
                    if (config.BiomassGeneration > 0)
                    {
                        float curBiomass = Supplies.Biomass != null && Supplies.Biomass.TryGetValue(Owner, out float b) ? b : 0f;
                        Supplies.UpdateBiomass(Owner, curBiomass + config.BiomassGeneration);
                    }

                    // Passive climate generation (config-driven — replaces old hardcoded GHG Factory check)
                    if (config.TemperatureGeneration > 0f)
                    {
                        float curTemp = Supplies.Temperature != null && Supplies.Temperature.TryGetValue(Owner, out float t) ? t : -60f;
                        Supplies.UpdateTemperature(Owner, curTemp + config.TemperatureGeneration);
                    }
                    if (config.AtmosphereGeneration > 0f)
                    {
                        float curAtmos = Supplies.Atmosphere != null && Supplies.Atmosphere.TryGetValue(Owner, out float a) ? a : 0.01f;
                        Supplies.UpdateAtmosphere(Owner, curAtmos + config.AtmosphereGeneration);
                    }
                    if (config.WaterGeneration > 0f)
                    {
                        float curWater = Supplies.Water != null && Supplies.Water.TryGetValue(Owner, out float w) ? w : 0f;
                        Supplies.UpdateWater(Owner, curWater + config.WaterGeneration);
                    }

                    // Upkeep is managed globally via PowerGridManager.RecalculateGrids()
                    
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
            }
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
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Paused, 0, 0);
            CurrentHealth = 0;
            Heal(300);
            // Let SmokestackVisuals (or any future visual override) supply its own ghost material.
Material effectiveMat = TryGetComponent<SmokestackVisuals>(out var sv)
                ? sv.GhostMaterial
                : ghostMaterial;

            if (MainRenderer != null && effectiveMat != null)
            {
                MainRenderer.material = effectiveMat;
            }

            if (navMeshObstacle != null)
            {
                navMeshObstacle.enabled = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                c.enabled = false;
            }

            // Turn off vision range while under construction/ghost
            if (VisionTransform != null)
            {
                VisionTransform.gameObject.SetActive(false);
            }
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            InitializeIfNeeded();
            unitBuildingThis = buildingBuilder;
            Owner = unitBuildingThis.Owner;
            if (MainRenderer != null)
            {
                // Use the visual override material if present (e.g. dull grey for smokestack).
                Material buildMat = TryGetComponent<SmokestackVisuals>(out var sv2)
                    ? sv2.GhostMaterial
                    : BuildingSO.PlacementMaterial;
                MainRenderer.material = buildMat;
            }

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Time.time - BuildingSO.BuildTime * Progress.Completion,
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
                BaseCommand[] baseCmds = base.AvailableCommands;

                bool isCommandPost = BuildingSO != null && BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase);
                if (isCommandPost && Owner == Owner.Player1)
                {
                    return GetAugmentedCommands(baseCmds);
                }

                return baseCmds;
            }
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
