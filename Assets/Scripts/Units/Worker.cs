using System;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Utilities;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, ITransportable, IRepairer
    {
        public bool IsBuilding => GetCurrentCommand() == UnitCommands.BuildBuilding;
        public bool IsRepairing => GetCurrentCommand() == UnitCommands.Repair;
        public new bool IsIdle => GetCurrentCommand() == UnitCommands.Stop;
        public bool IsGathering => Brain.CurrentState == WorkerBrainController.State.Gathering;
        public bool IsActivelyWorking => Brain.CurrentState != WorkerBrainController.State.Idle;
        public WorkerBrainController.State BrainState => Brain.CurrentState;

        public void Repair(AbstractCommandable target)
        {
            if (target == null) return;
            
            // If the target is an unfinished building, resume construction instead of just healing it.
            if (target is BaseBuilding building && building.Progress.State == BuildingProgress.BuildingState.Paused)
            {
                ResumeBuilding(building);
                return;
            }

            Brain.Halt();

            if (Agent != null)
            {
                float verticalGap = Mathf.Abs(transform.position.y - target.transform.position.y);
                Agent.stoppingDistance = verticalGap + 2.5f;
            }

            graphAgent.SetVariableValue("TargetGameObject", target.gameObject);
            SetCurrentCommand(UnitCommands.Repair);

            Brain.StartRepair(target);
        }
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
        private BuildingEventChannel buildingEventChannel;
        private WorkerBrainController brain;
        private WorkerBrainController Brain
        {
            get
            {
                if (brain == null)
                {
                    brain = GetComponent<WorkerBrainController>();
                    if (brain == null)
                        brain = gameObject.AddComponent<WorkerBrainController>();
                }
                return brain;
            }
        }

        protected override void Start()
        {
            base.Start();

            // Ensure event channels are loaded (even if graphAgent is null for purely script-driven drones)
            LoadEventChannels();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (gatherEventChannel != null)
            {
                gatherEventChannel.Event -= HandleGatherSupplies;
            }

            if (buildingEventChannel != null)
            {
                buildingEventChannel.Event -= HandleBuildingEvent;
            }
        }

        private void LoadEventChannels()
        {
            // Gather Supplies Event Channel
            bool gatherLoadedFromGraph = false;
            if (graphAgent != null && graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> gatherEvt))
            {
                if (gatherEvt.Value == null) gatherEvt.Value = Resources.Load<GatherSuppliesEventChannel>("Events/GatherSuppliesEventChannel");
                if (gatherEvt.Value != null)
                {
                    gatherEventChannel = gatherEvt.Value;
                    gatherEvt.Value.Event += HandleGatherSupplies;
                    Brain.SetEventChannel(gatherEventChannel);
                    gatherLoadedFromGraph = true;
                }
            }

            if (!gatherLoadedFromGraph)
            {
                gatherEventChannel = Resources.Load<GatherSuppliesEventChannel>("Events/GatherSuppliesEventChannel");
                if (gatherEventChannel != null)
                {
                    gatherEventChannel.Event += HandleGatherSupplies;
                    Brain.SetEventChannel(gatherEventChannel);
                }
            }

            // Building Event Channel
            bool buildingLoadedFromGraph = false;
            if (graphAgent != null && graphAgent.GetVariable("BuildingEventChannel", out BlackboardVariable<BuildingEventChannel> buildEvt))
            {
                if (buildEvt.Value == null) buildEvt.Value = Resources.Load<BuildingEventChannel>("Events/BuildingEventChannel");
                if (buildEvt.Value != null)
                {
                    buildingEventChannel = buildEvt.Value;
                    buildEvt.Value.Event += HandleBuildingEvent;
                    buildingLoadedFromGraph = true;
                }
            }

            if (!buildingLoadedFromGraph)
            {
                buildingEventChannel = Resources.Load<BuildingEventChannel>("Events/BuildingEventChannel");
                if (buildingEventChannel != null)
                {
                    buildingEventChannel.Event += HandleBuildingEvent;
                }
            }
        }

        protected override void UpdateAnimation()
        {
            base.UpdateAnimation();
            SetAnimBool("IsGathering", IsGathering);
            SetAnimBool("IsBuilding", IsBuilding);
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
                Agent.stoppingDistance = verticalGap + 1.5f;
            }

            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply.gameObject);
            SetCurrentCommand(UnitCommands.Gather);

            Brain.StartGather(supply);
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            if (Agent != null)
            {
                float verticalGap = Mathf.Abs(transform.position.y - commandPost.transform.position.y);
                Agent.stoppingDistance = verticalGap + 2.5f;
            }
            graphAgent.SetVariableValue("CommandPost", commandPost);
            SetCurrentCommand(UnitCommands.ReturnSupplies);

            if (Agent != null && Agent.isOnNavMesh)
                Agent.SetDestination(commandPost.transform.position);
        }

        public override void Stop()
        {
            Brain.Halt();
            base.Stop();
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            Brain.Halt();
            GameObject instance = Instantiate(building.Prefab, targetLocation, Quaternion.identity);
            if (!instance.TryGetComponent(out BaseBuilding baseBuilding))
            {
                Debug.LogError($"Missing BaseBuilding on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                return null;
            }

            // Ensure the building starts in a Paused state so it doesn't immediately function!
            baseBuilding.InitializeAsGhost(building.PlacementMaterial, Owner);

            // Project the target location onto the Drone's specific NavMesh layer (e.g. Airborne)
            // This prevents airborne drones from rejecting ground-level pathfinding destinations!
            Vector3 navDestination = targetLocation;
            if (Agent != null)
            {
                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                if (UnityEngine.AI.NavMesh.SamplePosition(targetLocation, out UnityEngine.AI.NavMeshHit hit, 15f, filter))
                {
                    navDestination = hit.position;
                }

                float verticalGap = Mathf.Abs(transform.position.y - navDestination.y);
                Agent.stoppingDistance = GetStoppingDistance(instance, navDestination);
            }

            // Keep blackboard variables updated for diagnostic logging
            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", navDestination);
            graphAgent.SetVariableValue("Ghost", instance);
            graphAgent.SetVariableValue("BuildingUnderConstruction", baseBuilding);
            SetCurrentCommand(UnitCommands.BuildBuilding);

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Minerals, building.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Gas, building.Cost.GasSO));

            // Drive navigation and construction via the C# brain coroutine for better control over the procedural rise-from-ground animation.
            // Note: The behavior tree is still running but remains in a waiting state.
            Brain.StartBuild(baseBuilding, building, navDestination);

            return instance;
        }

        public void BuildPipeline(EnergyPipelineManager pipelineManager)
        {
            if (pipelineManager == null) return;
            SetCurrentCommand(UnitCommands.Build);
            Brain.StartPipelineBuild(pipelineManager);
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            if (building == null) return;
            Brain.Halt();

            // Project the target location onto the Drone's specific NavMesh layer (e.g. Airborne)
            Vector3 navDestination = building.transform.position;
            if (Agent != null)
            {
                UnityEngine.AI.NavMeshQueryFilter filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
                if (UnityEngine.AI.NavMesh.SamplePosition(building.transform.position, out UnityEngine.AI.NavMeshHit hit, 15f, filter))
                {
                    navDestination = hit.position;
                }

                float verticalGap = Mathf.Abs(transform.position.y - navDestination.y);
                Agent.stoppingDistance = GetStoppingDistance(building.gameObject, navDestination);
            }

            graphAgent.SetVariableValue("TargetLocation", navDestination);
            graphAgent.SetVariableValue("BuildingUnderConstruction", building);
            graphAgent.SetVariableValue("BuildingSO", building.BuildingSO);
            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            SetCurrentCommand(UnitCommands.BuildBuilding);

            // Trigger the actual construction loop in the C# brain.
            Brain.StartBuild(building, building.BuildingSO, navDestination);
        }

        public void CancelBuilding()
        {
            Brain.Halt();
            if (graphAgent.GetVariable("Ghost", out BlackboardVariable<GameObject> ghostVariable)
                && ghostVariable.Value != null)
            {
                Destroy(ghostVariable.Value);
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

        private float GetStoppingDistance(GameObject target, Vector3 targetLocation)
        {
            float verticalGap = Mathf.Abs(transform.position.y - targetLocation.y);
            if (target == null) return verticalGap + 1.5f;

            float radius = 1.0f;
            if (target.TryGetComponent(out UnityEngine.AI.NavMeshObstacle obstacle))
            {
                if (obstacle.shape == UnityEngine.AI.NavMeshObstacleShape.Box)
                {
                    radius = Mathf.Max(obstacle.size.x, obstacle.size.z) * 0.5f;
                }
                else
                {
                    radius = obstacle.radius;
                }
            }
            else if (target.TryGetComponent(out Collider collider))
            {
                radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
            }

            return verticalGap + radius + 1.0f;
        }

        public override void Deselect()
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(false);
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
            if (this == null || gameObject == null || self != gameObject) 
            {
                return; 
            }

            if (supply == null)
            {
                Debug.LogWarning($"HandleGatherSupplies called with null supply. Owner={Owner}, Self={(self != null ? self.name : "null")}, Amount={amount}");
                return;
            }

            Debug.Log($"[Worker] {name} received gather event: amount={amount}, biomassDictExists={GameDevTV.RTS.Player.Supplies.Biomass != null}");
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

        public override BaseCommand[] AvailableCommands
        {
            get
            {
                if (overrideCommands != null)
                {
                    return overrideCommands;
                }

                return GetAugmentedCommands(base.AvailableCommands);
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

                if (cmd is OverrideCommandsCommand overrideCmd && overrideCmd.name.Contains("Show Buildings"))
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
                        list.Add(newCmd);
                    }
                }
            }

            return list.ToArray();
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
