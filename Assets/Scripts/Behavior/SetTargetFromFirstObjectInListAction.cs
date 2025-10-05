using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Set Target from First Object in List", story: "Set [Target] to the first item in [List] .", category: "Action/Blackboard", id: "2c9e730c13f95755dc04048c77676251")]
public partial class SetTargetFromFirstObjectInListAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<List<GameObject>> List;

    protected override Status OnStart()
    {
        if (List.Value == null || List.Value.Count == 0) return Status.Failure;

        Target.Value = List.Value[0];

        return Status.Success;
    }
}

