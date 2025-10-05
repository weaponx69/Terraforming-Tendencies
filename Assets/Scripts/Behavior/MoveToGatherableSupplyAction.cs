using GameDevTV.RTS.Environment;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Move to GatherableSupply", story: "[Agent] moves to [Supply] or nearby not busy supply.", category: "Action/Navigation", id: "b9248f874f11b1a358e671809522dbfc")]
    public partial class MoveToGatherableSupplyAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GatherableSupply> Supply;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(7f);

        private NavMeshAgent agent;
        private Animator animator;
        private LayerMask suppliesMask;
        private SupplySO supplySO;

        protected override Status OnStart()
        {
            suppliesMask = LayerMask.GetMask("Supplies");

            if (!HasValidInputs())
            {
                return Status.Failure;
            }

            agent.TryGetComponent(out animator);

            Vector3 targetPosition = GetTargetPosition();

            agent.SetDestination(targetPosition);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, agent.velocity.magnitude);
            }

            if (agent.remainingDistance >= agent.stoppingDistance)
            {
                return Status.Running;
            }

            if (!Supply.Value.IsBusy && Supply.Value.Amount > 0)
            {
                return Status.Success;
            }
            Collider[] colliders = FindNearbyNotBusyColliders();

            if (colliders.Length > 0)
            {
                Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));

                Supply.Value = colliders[0].GetComponent<GatherableSupply>();
                agent.SetDestination(GetTargetPosition());
                return Status.Running;
            }

            return Status.Failure;
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, 0);
            }
        }

        private bool HasValidInputs()
        {
            if (!Agent.Value.TryGetComponent(out agent) || (Supply.Value == null && supplySO == null))
            {
                return false;
            }

            if (Supply.Value != null)
            {
                supplySO = Supply.Value.Supply;
            }
            else
            {
                Collider[] colliders = FindNearbyNotBusyColliders();
                if (colliders.Length > 0)
                {
                    Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));
                    Supply.Value = colliders[0].GetComponent<GatherableSupply>();
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private Collider[] FindNearbyNotBusyColliders()
        {
            return Physics.OverlapSphere(
                agent.transform.position,
                SearchRadius,
                suppliesMask
            ).Where(collider =>
                    collider.TryGetComponent(out GatherableSupply supply)
                    && !supply.IsBusy
                    && supply.Supply.Equals(Supply.Value.Supply)
            ).ToArray();
        }

        private Vector3 GetTargetPosition()
        {
            Vector3 targetPosition;
            if (Supply.Value.TryGetComponent(out Collider collider))
            {
                targetPosition = collider.ClosestPoint(agent.transform.position);
            }
            else
            {
                targetPosition = Supply.Value.transform.position;
            }

            return targetPosition;
        }
    }
}