using System;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    public class AirTransport : AbstractUnit, ITransporter
    {
        public int Capacity => unitSO.TransportConfig.Capacity;
        [field: SerializeField] public int UsedCapacity { get; private set; }

        private List<ITransportable> loadedUnits = new(8);

        public List<ITransportable> GetLoadedUnits() => loadedUnits.ToList();

        public void Load(ITransportable unit)
        {
            if (UsedCapacity + unit.TransportCapacityUsage > Capacity) return;

            if (graphAgent.GetVariable("LoadUnitTargets", out BlackboardVariable<List<GameObject>> loadUnitVariable))
            {
                loadUnitVariable.Value.Add(unit.Transform.gameObject);
                graphAgent.SetVariableValue("LoadUnitTargets", loadUnitVariable.Value);
            }
            
            graphAgent.SetVariableValue("Command", UnitCommands.LoadUnits);
        }

        public void Load(ITransportable[] units)
        {
            throw new NotImplementedException();
        }

        public bool Unload(ITransportable unit)
        {
            NavMeshQueryFilter queryFilter = new()
            {
                areaMask = unit.Agent.areaMask,
                agentTypeID = unit.Agent.agentTypeID
            };

            if (Physics.Raycast(
                    transform.position, 
                    Vector3.down, 
                    out RaycastHit raycastHit, 
                    float.MaxValue, 
                    unitSO.TransportConfig.SafeDropLayers)
                && NavMesh.SamplePosition(raycastHit.point, out NavMeshHit hit, 1, queryFilter))
            {
                UsedCapacity -= unit.TransportCapacityUsage;
                unit.Transform.SetParent(null);
                unit.Transform.position = hit.position;
                unit.Transform.gameObject.SetActive(true);
                unit.Agent.Warp(hit.position);

                if (unit is IMoveable moveable)
                {
                    moveable.MoveTo(hit.position);
                }

                loadedUnits.Remove(unit);
                Bus<UnitUnloadEvent>.Raise(Owner, new UnitUnloadEvent(unit, this));
                return true;
            }

            return false;
        }

        public bool UnloadAll()
        {
            for(int i = loadedUnits.Count - 1; i >= 0; i--)
            {
                Unload(loadedUnits[i]);
            }

            return true;
        }

        protected override void Start()
        {
            base.Start();

            if(graphAgent.GetVariable("LoadUnitEventChannel", out BlackboardVariable<LoadUnitEventChannel> eventChannelVariable))
            {
                eventChannelVariable.Value.Event += HandleLoadUnit;
            }
        }

        private void HandleLoadUnit(GameObject self, GameObject targetGameObject)
        {
            targetGameObject.SetActive(false);
            targetGameObject.transform.SetParent(self.transform);
            ITransportable transportable = targetGameObject.GetComponent<ITransportable>();
            UsedCapacity += transportable.TransportCapacityUsage;

            loadedUnits.Add(transportable);
            Bus<UnitLoadEvent>.Raise(Owner, new UnitLoadEvent(transportable, this));

            if(graphAgent.GetVariable("LoadUnitTargets", out BlackboardVariable<List<GameObject>> loadUnitsVariable))
            {
                loadUnitsVariable.Value.Remove(targetGameObject);
                graphAgent.SetVariableValue("LoadUnitTargets", loadUnitsVariable.Value);
            }

            if (UsedCapacity >= Capacity)
            {
                graphAgent.SetVariableValue("Command", UnitCommands.Stop);
                graphAgent.SetVariableValue("LoadUnitTargets", new List<GameObject>(unitSO.TransportConfig.Capacity));
            }
        }
    }
}
