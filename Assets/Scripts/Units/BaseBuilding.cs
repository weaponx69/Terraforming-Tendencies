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
        public int QueueSize => buildingQueue.Count;
        public UnlockableSO[] Queue => buildingQueue.ToArray();
        [field: SerializeField] public float CurrentQueueStartTime { get; private set; }
        [field: SerializeField] public UnlockableSO SOBeingBuilt { get; private set; }
        [field: SerializeField] public MeshRenderer MainRenderer { get; private set; }
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
        private List<UnlockableSO> buildingQueue = new(MAX_QUEUE_SIZE);
        private const int MAX_QUEUE_SIZE = 5;
        private int spawnCount = 0; // Tracks how many units have been spawned, for angle distribution

        protected override void Awake()
        {
            base.Awake();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO.Health;
            // Current health is set as the building is being built via Heal()
        }

        private void OnEnable()
        {
            if (!ActiveBuildings.Contains(this))
                ActiveBuildings.Add(this);
        }

        private void OnDisable()
        {
            ActiveBuildings.Remove(this);
        }

        protected override void Start()
        {
            base.Start();
            if (MainRenderer != null)
            {
                MainRenderer.material = primaryMaterial;
            }
            
            // If the building is already completed (e.g. spawned by AI), ensure it has health and completed progress
            if (unitBuildingThis == null)
            {
                CompleteConstruction();
            }

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));

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
            if (buildingQueue.Count == 1)
            {
                StartCoroutine(DoBuildUnits());
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
                StopAllCoroutines();

                if (buildingQueue.Count > 0)
                {
                    StartCoroutine(DoBuildUnits());
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

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            Awake();
            unitBuildingThis = buildingBuilder;
            Owner = unitBuildingThis.Owner;
            MainRenderer.material = BuildingSO.PlacementMaterial;

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Time.time - BuildingSO.BuildTime * Progress.Completion,
                Progress.Completion
            );

            if (Progress.Completion == 0)
            {
                Heal(1);
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
                        Debug.LogWarning($"[BaseBuilding] Could not find NavMesh for type {agentTypeID} near spawn position {spawnPosition} for unit {unitSO.Name}. Using fallback spread at flight height.");
                        // CRITICAL: Ensure the fallback spawn position is at the flight height for Air units
                        // so they are close enough to the Air NavMesh even if SamplePosition fails.
                        spawnPosition = transform.position + offset;
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
                            Debug.LogWarning($"[BaseBuilding] Disabled NavMeshAgent on {instance.name} because no NavMesh was found for type {agentTypeID} at spawn location.");
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
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingDeathEvent>.Raise(Owner, new BuildingDeathEvent(Owner, this));
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

            if (culledVisuals == null)
            {
                Transform originalRendererTransform = MainRenderer.transform;
                GameObject culledGO = new ($"Culled {BuildingSO.Name} Visuals")
                {
                    layer = LayerMask.GetMask("TransparentFX"),
                    transform =
                    {
                        position = originalRendererTransform.position,
                        rotation = originalRendererTransform.rotation,
                        localScale = originalRendererTransform.localScale
                    }
                };
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
