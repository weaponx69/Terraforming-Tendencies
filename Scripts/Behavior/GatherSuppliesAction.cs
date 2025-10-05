using GameDevTV.RTS.Environment;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Gather Supplies", story: "[Unit] gathers [Amount] supplies from [GatherableSupplies] .", category: "Action/Units", id: "3b941d7ae99d1e36b7d806875379c977")]
    public partial class GatherSuppliesAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Unit;
        [SerializeReference] public BlackboardVariable<int> Amount;
        [SerializeReference] public BlackboardVariable<GatherableSupply> GatherableSupplies;
        [SerializeReference] public BlackboardVariable<SupplySO> SupplySO;

        private Animator animator;
        private float enterTime;

        protected override Status OnStart()
        {
            if (GatherableSupplies.Value == null) 
            {
                return Status.Failure;
            }
            enterTime = Time.time;

            if (Unit.Value.TryGetComponent(out animator))
            {
                animator.SetBool(AnimationConstants.IS_GATHERING, true);
            }
            GatherableSupplies.Value.BeginGather();
            SupplySO.Value = GatherableSupplies.Value.Supply;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (GatherableSupplies.Value.Supply.BaseGatherTime + enterTime <= Time.time)
            {
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (animator != null)
            {
                animator.SetBool(AnimationConstants.IS_GATHERING, false);
            }

            if (GatherableSupplies.Value == null) return;

            if (CurrentStatus == Status.Success)
            {
                Amount.Value = GatherableSupplies.Value.EndGather();
            }
            else
            {
                GatherableSupplies.Value.AbortGather();
            }
        }
    }
}
