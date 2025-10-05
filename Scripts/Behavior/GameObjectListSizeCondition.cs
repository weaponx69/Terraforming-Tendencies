using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

namespace GameDevTV.RTS.Behavior
{

    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "GameObject List Size", story: "[List] size is [Operator] [Size] .", category: "Conditions", id: "789ad67d277dee41dd3baa3effe8a1b3")]
    public partial class GameObjectListSizeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<List<GameObject>> List;
        [Comparison(comparisonType: ComparisonType.All)]
        [SerializeReference] public BlackboardVariable<ConditionOperator> Operator;
        [SerializeReference] public BlackboardVariable<int> Size;

        public override bool IsTrue()
        {
            if (List.Value == null) return false;

            return ConditionUtils.Evaluate(List.Value.Count, Operator, Size);
        }
    }
}