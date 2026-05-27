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
            NavMeshAgent agent = GetAgent();

            // If no agent found at all, skip gracefully — this is a best-effort action.
            if (agent == null)
                return Status.Success;

            agent.enabled = Active.Value;
            return Status.Success;
        }

        protected override Status OnUpdate()
        {
            // OnUpdate should not be reached since OnStart always returns Success or Success.
            // Safety fallback: succeed immediately.
            return Status.Success;
        }

        private NavMeshAgent GetAgent()
        {
            // Primary: use the linked Self blackboard variable.
            if (Self?.Value != null && Self.Value.TryGetComponent(out NavMeshAgent linkedAgent))
                return linkedAgent;

            // Fallback: search the behavior owner's GameObject hierarchy.
            // This handles cases where the Self field is not linked in the BT asset.
            if (GameObject != null && GameObject.TryGetComponent(out NavMeshAgent ownerAgent))
                return ownerAgent;

            return null;
        }
    }
}