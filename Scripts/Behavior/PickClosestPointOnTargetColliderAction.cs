using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Pick Closest Point on Target Collider", story: "[Self] picks the closest point on [Collider] .", category: "Action", id: "7e63cff713b7d62dd91e41e08ceab5db")]
    public partial class PickClosestPointOnTargetColliderAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<GameObject> Collider;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;

        protected override Status OnStart()
        {
            if (Self.Value == null || Collider.Value == null || !Collider.Value.TryGetComponent(out Collider collider))
            {
                return Status.Failure;
            }

            TargetLocation.Value = collider.ClosestPoint(Self.Value.transform.position);

            return Status.Success;
        }
    }
}