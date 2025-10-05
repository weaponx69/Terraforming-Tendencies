using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Set NavMeshAgent Enabled", story: "[Self] sets NavMeshAgent component active status to [active] .", category: "Action/Navigation", id: "5709b9d34125bf0009d5e586dd840a33")]
    public partial class SetNavMeshAgentEnabledAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<bool> Active;

        protected override Status OnStart()
        {
            if (Self.Value == null || !Self.Value.TryGetComponent(out NavMeshAgent agent)) return Status.Failure;

            agent.enabled = Active;

            return Status.Success;
        }
    }
}