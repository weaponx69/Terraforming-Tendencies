using GameDevTV.RTS.Units;
using System;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "Building Is In Progress", story: "[BaseBuilding] is being built.", category: "Conditions", id: "78f52539a1129c1794d082b6c8c3a40f")]
    public partial class BuildingIsInProgressCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BaseBuilding> BaseBuilding;

        public override bool IsTrue()
        {
            return BaseBuilding.Value != null 
                && BaseBuilding.Value.Progress.State == BuildingProgress.BuildingState.Building;
        }
    }
}
