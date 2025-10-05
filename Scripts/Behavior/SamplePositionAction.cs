using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Sample Position", story: "Set [TargetLocation] to the closest point ont he NavMesh to [Target] .", category: "Action/Navigation", id: "3d14bf6d90be26b701a721ff74e1449d")]
    public partial class SamplePositionAction : Action
    {
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;
        [SerializeReference] public BlackboardVariable<NavMeshAgent> Target;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new (5);

        protected override Status OnStart()
        {
            if (Target.Value == null) return Status.Failure;

            NavMeshQueryFilter navMeshQueryFilter = new()
            {
                agentTypeID = Target.Value.agentTypeID,
                areaMask = Target.Value.areaMask
            };

            if (NavMesh.SamplePosition(Target.Value.transform.position, out NavMeshHit hit, SearchRadius, navMeshQueryFilter))
            {
                TargetLocation.Value = hit.position;
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}