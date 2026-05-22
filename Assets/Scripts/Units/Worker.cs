using System;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, ITransportable
    {
        public bool IsBuilding => GetCurrentCommand() == UnitCommands.BuildBuilding;
        public bool IsIdle => GetCurrentCommand() == UnitCommands.Stop;
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

        protected override void Start()
        {
            base.Start();
            
            // Fix: Ensure the Behavior Tree knows which unit it is on (Variable name is 'Self' in the BT)
            if (graphAgent != null)
            {
                graphAgent.SetVariableValue("Self", gameObject);
            }

            if (graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> eventChannelVariable))
            {
                if (eventChannelVariable.Value == null)
                {
                    eventChannelVariable.Value = Resources.Load<GatherSuppliesEventChannel>("Events/GatherSuppliesEventChannel");
                    if (eventChannelVariable.Value == null)
                    {
                        // Fallback search if not in Resources
                        string[] guids = UnityEngine.AI.NavMesh.GetSettingsCount() > 0 ? new string[0] : null; // Dummy to use namespace
                        Debug.LogWarning($"[Worker] GatherSuppliesEventChannel is null on {name}. Ensure it is assigned in the Behavior Tree blackboard or exists in Resources/Events/");
                    }
                }

                if (eventChannelVariable.Value != null)
                {
                    eventChannelVariable.Value.Event += HandleGatherSupplies;
                }
            }
            if (graphAgent.GetVariable("BuildingEventChannel", out BlackboardVariable<BuildingEventChannel> buildingEventChannelVariable))
            {
                buildingEventChannelVariable.Value.Event += HandleBuildingEvent;
            }
        }

        public void LoadInto(ITransporter transporter)
        {
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }

        public void Gather(GatherableSupply supply)
        {
            if (Agent != null)
            {
                // For air units, account for the vertical gap (height ~4.0)
                float verticalGap = Mathf.Abs(transform.position.y - supply.transform.position.y);
                Agent.stoppingDistance = (Agent.agentTypeID != 0) ? verticalGap + 1.5f : 1.5f;
            }
            Debug.Log($"[Worker] {name} (ID: {UnitID}) Gather called for {supply?.name ?? "null"}");
            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply?.gameObject);
            SetCurrentCommand(UnitCommands.Gather);
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            if (Agent != null)
            {
                // For air units, account for the vertical gap (height ~4.0)
                float verticalGap = Mathf.Abs(transform.position.y - commandPost.transform.position.y);
                Agent.stoppingDistance = (Agent.agentTypeID != 0) ? verticalGap + 2.5f : 2.5f;
            }
            Debug.Log($"[Worker] {name} (ID: {UnitID}) ReturnSupplies called for {commandPost?.name ?? "null"}");
            graphAgent.SetVariableValue("CommandPost", commandPost);
            SetCurrentCommand(UnitCommands.ReturnSupplies);
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            GameObject instance = Instantiate(building.Prefab, targetLocation, Quaternion.identity);
            if (!instance.TryGetComponent(out BaseBuilding baseBuilding))
            {
                Debug.LogError($"Missing BaseBuilding on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                return null;
            }

            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", targetLocation);
            graphAgent.SetVariableValue("Ghost", instance);
            SetCurrentCommand(UnitCommands.BuildBuilding);

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Minerals, building.Cost.MineralsSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Gas, building.Cost.GasSO));

            return instance;
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            graphAgent.SetVariableValue("TargetLocation", building.transform.position);
            graphAgent.SetVariableValue("BuildingUnderConstruction", building);
            graphAgent.SetVariableValue("BuildingSO", building.BuildingSO);
            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            SetCurrentCommand(UnitCommands.BuildBuilding);
        }

        public void CancelBuilding()
        {
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
                // Debug.Log($"[Worker] {name} ignoring event for {self?.name ?? "null"}");
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
    }
}
