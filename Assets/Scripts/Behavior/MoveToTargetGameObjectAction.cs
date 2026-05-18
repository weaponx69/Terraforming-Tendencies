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

        protected override Status OnStart()
        {
            if (Agent.Value == null || !Agent.Value.TryGetComponent(out agent) || TargetGameObject.Value == null)
            {
                Debug.LogWarning($"[MoveToTargetGameObjectAction] {Agent.Value?.name} failed to start. TargetGameObject={TargetGameObject.Value?.name}");
                return Status.Failure;
            }

            Agent.Value.TryGetComponent(out animator);

            Vector3 targetPosition = GetTargetPosition();
            float distance = Vector3.Distance(agent.transform.position, targetPosition);

            if (distance <= agent.stoppingDistance + 0.5f)
            {
                Debug.Log($"[MoveToTargetGameObjectAction] {agent.name} already at destination {TargetGameObject.Value.name}. distance={distance}, stoppingDistance={agent.stoppingDistance}");
                return Status.Success;
            }

            bool setDestResult = agent.SetDestination(targetPosition);
            Debug.Log($"[MoveToTargetGameObjectAction] {agent.name} started moving to {TargetGameObject.Value.name} at {targetPosition}. setDestinationResult={setDestResult}, distance={distance}");
            
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

            if (agent.pathPending)
            {
                return Status.Running;
            }

            Vector3 targetPosition = GetTargetPosition();
            Vector3 currentTargetObjectPos = TargetGameObject.Value.transform.position;
            
            // Only update destination if the target object itself moves in world space
            if (Vector3.Distance(currentTargetObjectPos, lastPosition) >= MoveThreshold)
            {
                agent.SetDestination(targetPosition);
                lastPosition = currentTargetObjectPos;
                return Status.Running;
            }

            float directDistance = Vector3.Distance(agent.transform.position, targetPosition);
            if (agent.remainingDistance <= agent.stoppingDistance || directDistance <= agent.stoppingDistance + 0.5f)
            {
                Debug.Log($"[MoveToTargetGameObjectAction] {agent.name} arrived at {TargetGameObject.Value.name} successfully. directDistance={directDistance}, remainingDistance={agent.remainingDistance}");
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
            Vector3 targetPosition = Vector3.zero;
            if (TargetGameObject.Value == null)
            {
                return targetPosition;
            }

            if (TargetGameObject.Value.TryGetComponent(out Collider collider))
            {
                targetPosition = collider.ClosestPoint(agent.transform.position);
            }
            else
            {
                targetPosition = TargetGameObject.Value.transform.position;
            }

            return targetPosition;
        }
    }
}