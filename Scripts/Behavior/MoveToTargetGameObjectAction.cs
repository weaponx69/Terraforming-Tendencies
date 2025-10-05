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
            if (!Agent.Value.TryGetComponent(out agent) || TargetGameObject.Value == null)
            {
                return Status.Failure;
            }

            Agent.Value.TryGetComponent(out animator);

            Vector3 targetPosition = GetTargetPosition();

            if (Vector3.Distance(agent.transform.position, targetPosition) <= agent.stoppingDistance)
            {
                return Status.Success;
            }

            agent.SetDestination(targetPosition);
            lastPosition = targetPosition;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (animator != null)
            {
                animator.SetFloat(AnimationConstants.SPEED, agent.velocity.magnitude);
            }

            Vector3 targetPosition = GetTargetPosition();
            if (Vector3.Distance(targetPosition, lastPosition) >= MoveThreshold)
            {
                agent.SetDestination(targetPosition);
                lastPosition = agent.destination;
                return Status.Running;
            }

            if (agent.remainingDistance <= agent.stoppingDistance)
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
            Vector3 targetPosition;
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