using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
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
                Time.time - BuildingSO.BuildTime * Progress.Progress,
                Progress.Progress
            );

            if (Progress.Progress == 0)
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
                    // Spawn at a random offset between 4 and 7 units to ensure they are outside the building
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float distance = Random.Range(4f, 7f);
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                    Vector3 spawnPosition = transform.position + offset;
                    
                    // Snap to NavMesh if possible
                    bool onNavMesh = NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 20f, NavMesh.AllAreas);
                    if (onNavMesh)
                    {
                        spawnPosition = hit.position;
                    }
                    else
                    {
                        Debug.LogWarning($"[BaseBuilding] Could not find NavMesh near spawn position {spawnPosition} for unit {unitSO.Name} within 20 units. Spawning at building center.");
                        spawnPosition = transform.position; // Fallback to building center
                        onNavMesh = NavMesh.SamplePosition(spawnPosition, out hit, 10f, NavMesh.AllAreas);
                        if (onNavMesh) spawnPosition = hit.position;
                    }

                    // We instantiate the prefab disabled, or we just disable the agent immediately
                    GameObject instance = Instantiate(unitSO.Prefab, spawnPosition, Quaternion.identity);
                    if (instance.TryGetComponent(out AbstractCommandable commandable))
                    {
                        commandable.Owner = Owner;
                    }
                    
                    if (instance.TryGetComponent(out NavMeshAgent agent))
                    {
                        agent.enabled = false; // Disable it so we can safely warp/move it
                        instance.transform.position = spawnPosition;
                        
                        if (onNavMesh)
                        {
                            agent.enabled = true;
                            agent.Warp(spawnPosition);
                        }
                        else
                        {
                            Debug.LogWarning($"[BaseBuilding] Disabled NavMeshAgent on {instance.name} because no NavMesh was found at spawn location.");
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
