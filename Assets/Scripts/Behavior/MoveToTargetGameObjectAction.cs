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
    [NodeDescription(name: "Move to Target GameObject", story: "[Agent] moves to [TargetGameObject] .", category: "Action/Navigation", id: "f07a8fab1fc459315f3380eef35b2aa0")]
    public partial class MoveToTargetGameObjectAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> TargetGameObject;
        [SerializeReference] public BlackboardVariable<float> MoveThreshold = new(0.25f);

        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 lastPosition;
        private Vector3 randomOffset;
        private Vector3 targetPosition;
        private bool destinationSet;

        protected override Status OnStart()
        {
            if (Agent.Value == null || !Agent.Value.TryGetComponent(out agent) || TargetGameObject.Value == null)
            {
                return Status.Failure;
            }

            Agent.Value.TryGetComponent(out animator);

            // Calculate random offset once to prevent jitter. Keep it small to avoid circling.
            float offsetAmount = 0.2f;
            randomOffset = new Vector3(UnityEngine.Random.Range(-offsetAmount, offsetAmount), 0, UnityEngine.Random.Range(-offsetAmount, offsetAmount));

            targetPosition = GetTargetPosition();
            float distance = Vector3.Distance(agent.transform.position, targetPosition);

            // Use a small buffer for arrival.
            if (distance <= agent.stoppingDistance + 0.1f)
            {
                return Status.Success;
            }

            if (agent.isOnNavMesh)
            {
                destinationSet = agent.SetDestination(targetPosition);
            }
            else
            {
                destinationSet = false;
            }
            
            lastPosition = TargetGameObject.Value.transform.position;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, agent.velocity.magnitude);
            }

            if (TargetGameObject.Value == null)
            {
                return Status.Failure;
            }

            if (!agent.isOnNavMesh)
            {
                return Status.Running;
            }

            if (!destinationSet)
            {
                targetPosition = GetTargetPosition();
                destinationSet = agent.SetDestination(targetPosition);
                if (!destinationSet) return Status.Running;
            }

            if (agent.pathPending)
            {
                return Status.Running;
            }

            Vector3 currentTargetObjectPos = TargetGameObject.Value.transform.position;
            
            // Only update destination if the target object itself moves in world space
            if (Vector3.Distance(currentTargetObjectPos, lastPosition) >= MoveThreshold)
            {
                targetPosition = GetTargetPosition();
                destinationSet = agent.SetDestination(targetPosition);
                lastPosition = currentTargetObjectPos;
                return Status.Running;
            }

            Vector2 agentPos2D = new Vector2(agent.transform.position.x, agent.transform.position.z);
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.z);
            float directDistance = Vector2.Distance(agentPos2D, targetPos2D);
            bool arrived = false;
        
            if (agent.isOnNavMesh && agent.hasPath)
            {
                arrived = agent.remainingDistance <= agent.stoppingDistance || directDistance <= agent.stoppingDistance + 0.1f;
            }
            else
            {
                arrived = directDistance <= agent.stoppingDistance + 0.1f;
            }

            if (arrived)
            {
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

        private Vector3 GetTargetPosition()
        {
            Vector3 targetPos = Vector3.zero;
            if (TargetGameObject.Value == null)
            {
                return targetPos;
            }

            if (TargetGameObject.Value.TryGetComponent(out Collider collider))
            {
                targetPos = collider.bounds.ClosestPoint(agent.transform.position);
            }
            else
            {
                targetPos = TargetGameObject.Value.transform.position;
            }

            // Apply pre-calculated random offset to prevent jitter
            targetPos += randomOffset;

            // Ensure the final position is valid on the NavMesh
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 25.0f, filter))
            {
                targetPos = hit.position;
            }

            return targetPos;
        }
    }
}