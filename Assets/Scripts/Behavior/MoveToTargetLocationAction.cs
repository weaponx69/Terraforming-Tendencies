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
    [NodeDescription(name: "Move to Target Location", story: "[Agent] moves to [TargetLocation] .", category: "Action/Navigation", id: "c96373f56a4b683d189e362795d042fa")]
    public partial class MoveToTargetLocationAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;

        private NavMeshAgent agent;
        private Animator animator;
        private bool destinationSet;

        protected override Status OnStart()
        {
            if (!Agent.Value.TryGetComponent(out agent))
            {
                return Status.Failure;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            Agent.Value.TryGetComponent(out animator);

            Vector3 targetPosition = TargetLocation.Value;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 15.0f, filter))
            {
                targetPosition = hit.position;
            }

            Debug.Log($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) OnStart targetPosition={targetPosition}, stoppingDistance={agent.stoppingDistance}");

            if (Vector3.Distance(agent.transform.position, targetPosition) <= agent.stoppingDistance)
            {
                Debug.Log($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) OnStart ALREADY AT DESTINATION (distance={Vector3.Distance(agent.transform.position, targetPosition)} <= stoppingDistance={agent.stoppingDistance})");
                return Status.Success;
            }

            if (agent.isOnNavMesh)
            {
                destinationSet = agent.SetDestination(targetPosition);
                Debug.Log($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) SetDestination returned {destinationSet} for {targetPosition}");
            }
            else
            {
                destinationSet = false;
                Debug.LogWarning($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) cannot SetDestination because agent is not on NavMesh!");
            }

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, agent.velocity.magnitude);
            }

            if (!agent.isOnNavMesh)
            {
                return Status.Running;
            }

            if (!destinationSet)
            {
                Vector3 targetPosition = TargetLocation.Value;
                NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 15.0f, filter))
                {
                    targetPosition = hit.position;
                }
                destinationSet = agent.SetDestination(targetPosition);
                Debug.Log($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) OnUpdate retry SetDestination returned {destinationSet} for {targetPosition}");
                if (!destinationSet) return Status.Running;
            }

            if (agent.pathPending)
            {
                return Status.Running;
            }

            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                Debug.Log($"[Navigation] {agent.name} (ID: {agent.gameObject.GetInstanceID()}) OnUpdate REACHED DESTINATION (remainingDistance={agent.remainingDistance} <= stoppingDistance={agent.stoppingDistance})");
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, 0);
            }
        }
    }
}