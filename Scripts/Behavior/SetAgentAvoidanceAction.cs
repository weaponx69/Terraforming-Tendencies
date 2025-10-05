using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Set Agent Avoidance", story: "Set [Agent] avoidance quality to [AvoidanceQuality] .", category: "Action/Navigation", id: "3a4f7ab7967bf186b0c645339d0ace1e")]
    public partial class SetAgentAvoidanceAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<int> AvoidanceQuality;

        protected override Status OnStart()
        {
            if (!Agent.Value.TryGetComponent(out NavMeshAgent agent) || AvoidanceQuality > 4 || AvoidanceQuality < 0)
            {
                return Status.Failure;
            }

            agent.obstacleAvoidanceType = (ObstacleAvoidanceType)AvoidanceQuality.Value;

            return Status.Success;
        }
    }
}