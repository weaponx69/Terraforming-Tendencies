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
            if (Self.Value == null)
            {
                Debug.LogWarning($"[SetNavMeshAgentEnabledAction] Self.Value is null!");
                return Status.Failure;
            }

            if (!Self.Value.TryGetComponent(out NavMeshAgent agent))
            {
                Debug.LogWarning($"[SetNavMeshAgentEnabledAction] {Self.Value.name} has no NavMeshAgent!");
                return Status.Failure;
            }

            agent.enabled = Active;
            Debug.Log($"[SetNavMeshAgentEnabledAction] {agent.name} set agent enabled to {Active.Value}");

            return Status.Success;
        }
    }
}