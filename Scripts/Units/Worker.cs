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
        public bool IsBuilding => graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> command) && command.Value == UnitCommands.BuildBuilding;
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
            if (graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> eventChannelVariable))
            {
                eventChannelVariable.Value.Event += HandleGatherSupplies;
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
            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply.gameObject);
            graphAgent.SetVariableValue("Command", UnitCommands.Gather);
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            graphAgent.SetVariableValue("CommandPost", commandPost);
            graphAgent.SetVariableValue("Command", UnitCommands.ReturnSupplies);
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
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);

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
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);
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
