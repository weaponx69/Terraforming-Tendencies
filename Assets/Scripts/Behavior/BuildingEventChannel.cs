using System;
using Unity.Behavior.GraphFramework;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Behavior
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/Building Event Channel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "Building Event Channel", message: "[Self] [BuildingEventType] on [BaseBuilding] .", category: "Events", id: "8097d6a8e4ca1335cad2cabbd43dc6e7")]
    public partial class BuildingEventChannel : EventChannelBase
    {
        public delegate void BuildingEventChannelEventHandler(GameObject Self, BuildingEventType BuildingEventType, BaseBuilding BaseBuilding);
        public event BuildingEventChannelEventHandler Event;

        public void SendEventMessage(GameObject Self, BuildingEventType BuildingEventType, BaseBuilding BaseBuilding)
        {
            Event?.Invoke(Self, BuildingEventType, BaseBuilding);
        }

        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<GameObject> SelfBlackboardVariable = messageData[0] as BlackboardVariable<GameObject>;
            var Self = SelfBlackboardVariable != null ? SelfBlackboardVariable.Value : default(GameObject);

            BlackboardVariable<BuildingEventType> BuildingEventTypeBlackboardVariable = messageData[1] as BlackboardVariable<BuildingEventType>;
            var BuildingEventType = BuildingEventTypeBlackboardVariable != null ? BuildingEventTypeBlackboardVariable.Value : default(BuildingEventType);

            BlackboardVariable<BaseBuilding> BaseBuildingBlackboardVariable = messageData[2] as BlackboardVariable<BaseBuilding>;
            var BaseBuilding = BaseBuildingBlackboardVariable != null ? BaseBuildingBlackboardVariable.Value : default(BaseBuilding);

            Event?.Invoke(Self, BuildingEventType, BaseBuilding);
        }

        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            BuildingEventChannelEventHandler del = (Self, BuildingEventType, BaseBuilding) =>
            {
                BlackboardVariable<GameObject> var0 = vars[0] as BlackboardVariable<GameObject>;
                if (var0 != null)
                    var0.Value = Self;

                BlackboardVariable<BuildingEventType> var1 = vars[1] as BlackboardVariable<BuildingEventType>;
                if (var1 != null)
                    var1.Value = BuildingEventType;

                BlackboardVariable<BaseBuilding> var2 = vars[2] as BlackboardVariable<BaseBuilding>;
                if (var2 != null)
                    var2.Value = BaseBuilding;

                callback();
            };
            return del;
        }

        public override void RegisterListener(Delegate del)
        {
            Event += del as BuildingEventChannelEventHandler;
        }

        public override void UnregisterListener(Delegate del)
        {
            Event -= del as BuildingEventChannelEventHandler;
        }
    }
}
