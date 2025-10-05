using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Stop Agent", story: "[Agent] stops moving.", category: "Action/Navigation", id: "b4af498b0656fc524515ec0b094c06a9")]
    public partial class StopAgentAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;

        protected override Status OnStart()
        {
            if (Agent.Value.TryGetComponent(out NavMeshAgent agent))
            {
                if (agent.TryGetComponent(out Animator animator))
                {
                    animator.SetFloat(AnimationConstants.SPEED, 0);
                }

                agent.ResetPath();
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}