using System;
using Unity.Behavior.GraphFramework;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

namespace GameDevTV.RTS.Behavior
{
#if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Behavior/Event Channels/Load Unit Event Channel")]
#endif
    [Serializable, GeneratePropertyBag]
    [EventChannelDescription(name: "Load Unit Event Channel", message: "[Self] loads [TargetGameObject] into itself.", category: "Events", id: "8f5a9ec420fed80211054b539e7c79f7")]
    public partial class LoadUnitEventChannel : EventChannelBase
    {
        public delegate void LoadUnitEventChannelEventHandler(GameObject Self, GameObject TargetGameObject);
        public event LoadUnitEventChannelEventHandler Event;

        public void SendEventMessage(GameObject Self, GameObject TargetGameObject)
        {
            Event?.Invoke(Self, TargetGameObject);
        }

        public override void SendEventMessage(BlackboardVariable[] messageData)
        {
            BlackboardVariable<GameObject> SelfBlackboardVariable = messageData[0] as BlackboardVariable<GameObject>;
            var Self = SelfBlackboardVariable != null ? SelfBlackboardVariable.Value : default(GameObject);

            BlackboardVariable<GameObject> TargetGameObjectBlackboardVariable = messageData[1] as BlackboardVariable<GameObject>;
            var TargetGameObject = TargetGameObjectBlackboardVariable != null ? TargetGameObjectBlackboardVariable.Value : default(GameObject);

            Event?.Invoke(Self, TargetGameObject);
        }

        public override Delegate CreateEventHandler(BlackboardVariable[] vars, System.Action callback)
        {
            LoadUnitEventChannelEventHandler del = (Self, TargetGameObject) =>
            {
                BlackboardVariable<GameObject> var0 = vars[0] as BlackboardVariable<GameObject>;
                if (var0 != null)
                    var0.Value = Self;

                BlackboardVariable<GameObject> var1 = vars[1] as BlackboardVariable<GameObject>;
                if (var1 != null)
                    var1.Value = TargetGameObject;

                callback();
            };
            return del;
        }

        public override void RegisterListener(Delegate del)
        {
            Event += del as LoadUnitEventChannelEventHandler;
        }

        public override void UnregisterListener(Delegate del)
        {
            Event -= del as LoadUnitEventChannelEventHandler;
        }
    }
}