using System;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, ITransportable
    {
        public bool IsBuilding => GetCurrentCommand() == UnitCommands.BuildBuilding;
        public bool IsIdle => GetCurrentCommand() == UnitCommands.Stop;
        public bool IsGathering => brain != null && brain.CurrentState == WorkerBrainController.State.Gathering;
        public bool IsActivelyWorking => brain != null && brain.CurrentState != WorkerBrainController.State.Idle;
        public WorkerBrainController.State BrainState => brain != null ? brain.CurrentState : WorkerBrainController.State.Idle;
        public bool HasSupplies
        {
            get
            {
                if (graphAgent != null && graphAgent.GetVariable("SupplyAmountHeld", out BlackboardVariable<int> heldVariable))
                {
                    return heldVariable.Value > 0;
                }

                return false;
            }
        }
        public int TransportCapacityUsage => unitSO.TransportConfig.GetTransportCapacityUsage();
        [SerializeField] private BaseCommand CancelBuildingCommand;

        private GatherSuppliesEventChannel gatherEventChannel;
        private WorkerBrainController brain;

        protected override void Start()
        {
            base.Start();

            brain = GetComponent<WorkerBrainController>();
            if (brain == null)
                brain = gameObject.AddComponent<WorkerBrainController>();

            // Fix: Set every possible name the BT might use for the local unit
            if (graphAgent != null)
            {
                graphAgent.SetVariableValue("Self", gameObject);
                graphAgent.SetVariableValue("Unit", gameObject);
                graphAgent.SetVariableValue("Agent", gameObject);

                // Ensure event channels are loaded into the blackboard
                LoadEventChannels();
            }
        }

        private void LoadEventChannels()
        {
            if (graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> gatherEvt))
            {
                if (gatherEvt.Value == null) gatherEvt.Value = Resources.Load<GatherSuppliesEventChannel>("Events/GatherSuppliesEventChannel");
                if (gatherEvt.Value != null)
                {
                    gatherEventChannel = gatherEvt.Value;
                    gatherEvt.Value.Event += HandleGatherSupplies;
                    brain?.SetEventChannel(gatherEventChannel);
                }
            }
            if (graphAgent.GetVariable("BuildingEventChannel", out BlackboardVariable<BuildingEventChannel> buildEvt))
            {
                if (buildEvt.Value == null) buildEvt.Value = Resources.Load<BuildingEventChannel>("Events/BuildingEventChannel");
                if (buildEvt.Value != null) buildEvt.Value.Event += HandleBuildingEvent;
            }
        }

        public void LoadInto(ITransporter transporter)
        {
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }

        public void Gather(GatherableSupply supply)
        {
            if (supply == null) return;

            if (Agent != null)
            {
                float verticalGap = Mathf.Abs(transform.position.y - supply.transform.position.y);
                Agent.stoppingDistance = (Agent.agentTypeID != 0) ? verticalGap + 1.5f : 1.5f;
            }

            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply.gameObject);
            SetCurrentCommand(UnitCommands.Gather);

            brain.StartGather(supply);
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            if (Agent != null)
            {
                float verticalGap = Mathf.Abs(transform.position.y - commandPost.transform.position.y);
                Agent.stoppingDistance = (Agent.agentTypeID != 0) ? verticalGap + 2.5f : 2.5f;
            }
            graphAgent.SetVariableValue("CommandPost", commandPost);
            SetCurrentCommand(UnitCommands.ReturnSupplies);

            if (Agent != null && Agent.isOnNavMesh)
                Agent.SetDestination(commandPost.transform.position);
        }

        public override void Stop()
        {
            brain?.Halt();
            base.Stop();
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            brain?.Halt();
            Debug.Log($"[Worker] Build called for {building.name} at {targetLocation}");
            GameObject instance = Instantiate(building.Prefab, targetLocation, Quaternion.identity);
            if (!instance.TryGetComponent(out BaseBuilding baseBuilding))
            {
                Debug.LogError($"Missing BaseBuilding on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                return null;
            }

            // Ensure the building starts in a Paused state so it doesn't immediately function!
            baseBuilding.InitializeAsGhost(building.PlacementMaterial);

            // Project the target location onto the Drone's specific NavMesh layer (e.g. Airborne)
            // This prevents airborne drones from rejecting ground-level pathfinding destinations!
            Vector3 navDestination = targetLocation;
            if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
            {
                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                if (UnityEngine.AI.NavMesh.SamplePosition(targetLocation, out UnityEngine.AI.NavMeshHit hit, 15f, filter))
                {
                    navDestination = hit.position;
                }
            }

            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", navDestination);
            graphAgent.SetVariableValue("Ghost", instance);
            graphAgent.SetVariableValue("BuildingUnderConstruction", baseBuilding);
            SetCurrentCommand(UnitCommands.BuildBuilding);

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Minerals, building.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Gas, building.Cost.GasSO));

            return instance;
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            brain?.Halt();

            // Project the target location onto the Drone's specific NavMesh layer (e.g. Airborne)
            Vector3 navDestination = building.transform.position;
            if (TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
            {
                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                if (UnityEngine.AI.NavMesh.SamplePosition(building.transform.position, out UnityEngine.AI.NavMeshHit hit, 15f, filter))
                {
                    navDestination = hit.position;
                }
            }

            graphAgent.SetVariableValue("TargetLocation", navDestination);
            graphAgent.SetVariableValue("BuildingUnderConstruction", building);
            graphAgent.SetVariableValue("BuildingSO", building.BuildingSO);
            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            SetCurrentCommand(UnitCommands.BuildBuilding);
        }

        public void CancelBuilding()
        {
            brain?.Halt();
            if (graphAgent.GetVariable("Ghost", out BlackboardVariable<GameObject> ghostVariable)
                && ghostVariable.Value != null)
            {
                Debug.Log($"[Worker] CancelBuilding called! Destroying ghost instance {ghostVariable.Value.name}");
                Destroy(ghostVariable.Value);
            }
            else
            {
                Debug.Log($"[Worker] CancelBuilding called, but no ghost was found in the blackboard to destroy.");
            }
            if (graphAgent.GetVariable("BuildingUnderConstruction", out BlackboardVariable<BaseBuilding> buildingVariable)
                && buildingVariable.Value != null)
            {
                Destroy(buildingVariable.Value.gameObject);

                BuildingSO buildingSO = buildingVariable.Value.BuildingSO;
                Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                    Owner,
                    Mathf.FloorToInt(0.75f * buildingSO.Cost.Minerals),
                    buildingSO.Cost.MineralsSO
                ));
                Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(
                    Owner,
                    Mathf.FloorToInt(0.75f * buildingSO.Cost.Gas),
                    buildingSO.Cost.GasSO
                ));
            }

            SetCommandOverrides(Array.Empty<BaseCommand>());
            Stop();
        }

        public void ClearSupplies()
        {
            graphAgent.SetVariableValue("SupplyAmountHeld", 0);
            graphAgent.SetVariableValue<GameObject>("Supply", null);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
        }

        public override void Deselect()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(false);
            }

            IsSelected = false;
            if (!IsBuilding)
            {
                SetCommandOverrides(null);
            }

            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        private void HandleGatherSupplies(GameObject self, int amount, SupplySO supply)
        {
            if (self != gameObject) 
            {
                // // // Debug.Log($"[Worker] {name} ignoring event for {self?.name ?? "null"}");
                return; 
            }
            
            if (supply == null)
            {
                // // Debug.LogWarning($"HandleGatherSupplies called with null supply. Owner={Owner}, Self={(self != null ? self.name : "null")}, Amount={amount}");
                return;
            }

            // // // Debug.Log($"[Worker] {name} received gather event: amount={amount}, biomassDictExists={GameDevTV.RTS.Player.Supplies.Biomass != null}");
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, amount, supply));
        }

        private void HandleBuildingEvent(GameObject self, BuildingEventType eventType, BaseBuilding building)
        {
            switch(eventType)
            {
                case BuildingEventType.ArrivedAt:
                    if (building != null && building.Progress.State == BuildingProgress.BuildingState.Building)
                    {
                        Stop();
                        break;
                    }
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Begin:
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Cancel:
                case BuildingEventType.Abort:
                case BuildingEventType.Completed:
                    SetCommandOverrides(null);
                    break;
                default:
                    break;
            }
        }
    }
}
