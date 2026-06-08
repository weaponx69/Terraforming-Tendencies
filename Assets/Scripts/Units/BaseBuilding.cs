using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    public class BaseBuilding : AbstractCommandable
    {
        public static readonly List<BaseBuilding> ActiveBuildings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearStaticData()
        {
            ActiveBuildings.Clear();
        }

        public int QueueSize => buildingQueue.Count;
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

        private Placeholder culledVisuals;
        private IBuildingBuilder unitBuildingThis;
        private Coroutine productionCoroutine;
        private List<UnlockableSO> buildingQueue = new(MAX_QUEUE_SIZE);
        private const int MAX_QUEUE_SIZE = 5;
        private int spawnCount = 0; // Tracks how many units have been spawned, for angle distribution
        private bool hasRaisedSpawnEvent = false;

        protected override void Awake()
        {
            base.Awake();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO.Health;
            // Current health is set as the building is being built via Heal()
            
            if (MainRenderer == null)
            {
                MainRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (MainRenderer != null && primaryMaterial == null)
            {
                // Auto-save the mesh's original material so it doesn't disappear if primaryMaterial wasn't set in the Inspector
                primaryMaterial = MainRenderer.material;
            }

            if (navMeshObstacle == null)
            {
                navMeshObstacle = GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            }
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ActiveBuildings.Remove(this);
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

            foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
            {
                if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(BuildingSO);
                }
            }
        }

        public void CompleteConstruction()
        {
            if (CurrentHealth == 0)
            {
                CurrentHealth = MaxHealth;
            }
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1);
            unitBuildingThis = null;

            if (MainRenderer != null && primaryMaterial != null)
            {
                MainRenderer.material = primaryMaterial;
            }

            // Attach a LifeSupportNode so GlobalDecayManager protects this building and those nearby.
            bool isCommandPost = BuildingSO != null && (BuildingSO.Name.Contains("Command", System.StringComparison.OrdinalIgnoreCase));
            if ((BuildingSO != null && BuildingSO.IsLifeSupport) || isCommandPost)
            {
                if (!TryGetComponent<LifeSupportNode>(out _))
                {
                    var node = gameObject.AddComponent<LifeSupportNode>();
                    node.Radius = isCommandPost ? Mathf.Max(BuildingSO.LifeSupportRadius, 30f) : BuildingSO.LifeSupportRadius;
                }

                AssignUniqueName();
            }

            // Activate any procedural visual effects (e.g. SmokestackVisuals).
            GetComponent<SmokestackVisuals>()?.ActivateSmoke();

            RaiseSpawnEvent();
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



        public void BuildUnlockable(UnlockableSO unlockable)
        {
            if (buildingQueue.Count == MAX_QUEUE_SIZE)
            {
                Debug.LogError("BuildUnit called when the queue was already full! This is not supported!");
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Minerals, unlockable.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Gas, unlockable.Cost.GasSO));

            buildingQueue.Add(unlockable);
            OnQueueUpdated?.Invoke(buildingQueue.ToArray());

            if (productionCoroutine == null)
            {
                productionCoroutine = StartCoroutine(DoBuildUnits());
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
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            Awake();
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
            while (buildingQueue.Count > 0)
            {
                SOBeingBuilt = buildingQueue[0];
                CurrentQueueStartTime = Time.time;
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());

                yield return new WaitForSeconds(SOBeingBuilt.BuildTime);

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

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            productionCoroutine = null;
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
    }
}
