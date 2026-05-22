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
            // Self is set by Worker.Start() which may run after BehaviorGraphAgent.Start().
            // Return Running so the BT retries next tick rather than failing the whole sequence.
            if (Self.Value == null)
                return Status.Running;

            if (!Self.Value.TryGetComponent(out NavMeshAgent agent))
            {
                Debug.LogWarning($"[SetNavMeshAgentEnabledAction] {Self.Value.name} has no NavMeshAgent!");
                return Status.Failure;
            }

            agent.enabled = Active.Value;
            return Status.Success;
        }

        protected override Status OnUpdate()
        {
            // Keep waiting until Self is populated.
            if (Self.Value == null)
                return Status.Running;

            if (!Self.Value.TryGetComponent(out NavMeshAgent agent))
            {
                Debug.LogWarning($"[SetNavMeshAgentEnabledAction] {Self.Value.name} has no NavMeshAgent!");
                return Status.Failure;
            }

            agent.enabled = Active.Value;
            return Status.Success;
        }
    }
}