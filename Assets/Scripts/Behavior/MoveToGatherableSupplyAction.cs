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
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(100f);

        private NavMeshAgent agent;
        private Animator animator;
        private LayerMask suppliesMask;
        private SupplySO supplySO;
        private Vector3 targetPosition;
        private Vector3 randomOffset;

        protected override Status OnStart()
        {
            suppliesMask = LayerMask.GetMask("Supplies");

            if (!HasValidInputs())
            {
                string agentName = Agent.Value != null ? Agent.Value.name : "NullAgent";
                string supplyName = Supply.Value != null ? Supply.Value.name : "NullSupply";
                Debug.LogWarning($"[MoveToGatherableSupplyAction] {agentName} HasValidInputs failed! Supply={supplyName}, supplySO={supplySO}");
                return Status.Failure;
            }

            agent.TryGetComponent(out animator);

            // Calculate random offset once to prevent jitter. Keep it small to avoid circling.
            float offsetAmount = 0.2f;
            randomOffset = new Vector3(UnityEngine.Random.Range(-offsetAmount, offsetAmount), 0, UnityEngine.Random.Range(-offsetAmount, offsetAmount));

            targetPosition = GetTargetPosition();
            
            // Use horizontal distance for arrival checks to accommodate Air Units flying at high altitude
            Vector2 agentPos2D = new Vector2(agent.transform.position.x, agent.transform.position.z);
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.z);
            float distance = Vector2.Distance(agentPos2D, targetPos2D);

            if (distance <= agent.stoppingDistance + 0.1f)
            {
                return Status.Success;
            }

            if (agent.isOnNavMesh)
            {
                bool setDestResult = agent.SetDestination(targetPosition);
                Debug.Log($"[MoveToGatherableSupplyAction] {agent.name} OnStart: targetPosition={targetPosition}, setDestResult={setDestResult}, agentDest={agent.destination}, supply={Supply.Value.name}, pathPending={agent.pathPending}");
            }
            else
            {
                Debug.LogWarning($"[MoveToGatherableSupplyAction] {agent.name} is not on NavMesh. Cannot set destination.");
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, agent.velocity.magnitude);
            }

            if (Supply.Value == null) return Status.Failure;

            if (!agent.isOnNavMesh) return Status.Running;

            if (agent.pathPending)
            {
                return Status.Running;
            }

            Vector2 agentPos2D = new Vector2(agent.transform.position.x, agent.transform.position.z);
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.z);
            float directDistance = Vector2.Distance(agentPos2D, targetPos2D);

            // Treat as arrived if either agent reports remainingDistance is close
            // OR if the direct Euclidean distance is within stopping distance + 0.1f buffer.
            bool hasArrived = (agent.isOnNavMesh && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.1f) || (directDistance <= agent.stoppingDistance + 0.1f);

            if (!hasArrived)
            {
                return Status.Running;
            }

            if (!Supply.Value.IsBusy && Supply.Value.Amount > 0)
            {
                Debug.Log($"[MoveToGatherableSupplyAction] {agent.name} Arrived at {Supply.Value.name} successfully. directDistance={directDistance}");
                return Status.Success;
            }
            Collider[] colliders = FindNearbyNotBusyColliders();

            if (colliders.Length > 0)
            {
                Array.Sort(colliders, new ClosestColliderComparer(agent.transform.position));

                Supply.Value = colliders[0].GetComponent<GatherableSupply>();
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(GetTargetPosition());
                }
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
            if (Agent.Value == null || !Agent.Value.TryGetComponent(out agent))
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
                    if (Supply.Value != null)
                    {
                        supplySO = Supply.Value.Supply;
                    }
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
            if (Supply.Value == null || Supply.Value.Supply == null)
            {
                return Array.Empty<Collider>();
            }

            return Physics.OverlapSphere(
                agent.transform.position,
                SearchRadius,
                suppliesMask
            ).Where(collider =>
                    collider.TryGetComponent(out GatherableSupply supply)
                    && !supply.IsBusy
                    && supply.Supply != null
                    && supply.Supply.Equals(Supply.Value.Supply)
            ).ToArray();
        }

        private Vector3 GetTargetPosition()
        {
            Vector3 targetPosition = Vector3.zero;
            if (Supply.Value == null)
            {
                return targetPosition;
            }

            if (Supply.Value.TryGetComponent(out Collider collider))
            {
                targetPosition = collider.bounds.ClosestPoint(agent.transform.position);
            }
            else
            {
                targetPosition = Supply.Value.transform.position;
            }

            // Apply pre-calculated random offset to prevent jitter
            targetPosition += randomOffset;

            // Ensure the final position is valid on the NavMesh. 
            // We use a large vertical radius (15.0f) because Air units fly at y=10 on a separate NavMesh surface.
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 15.0f, filter))
            {
                targetPosition = hit.position;
            }

            return targetPosition;
            }
}
}