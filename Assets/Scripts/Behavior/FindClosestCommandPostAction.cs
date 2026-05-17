using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Find Closest Command Post", story: "[Unit] finds nearest [CommandPost] .", category: "Action/Units", id: "df019f9861776b3b31754a035175faf5")]
    public partial class FindClosestCommandPostAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Unit;
        [SerializeReference] public BlackboardVariable<GameObject> CommandPost;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(500);
        [SerializeReference] public BlackboardVariable<BuildingSO> CommandPostBuilding;

        protected override Status OnStart()
        {
            int layerMask = LayerMask.GetMask("Buildings", "Units");
            Collider[] colliders = Physics.OverlapSphere(
                Unit.Value.transform.position, 
                SearchRadius.Value, 
                layerMask);

            List<BaseBuilding> nearbyCommandPosts = new();

            foreach(Collider collider in colliders)
            {
                if (collider.TryGetComponent(out BaseBuilding building) 
                        && (CommandPostBuilding.Value == null || building.UnitSO == CommandPostBuilding.Value)
                        && building.Progress.State == BuildingProgress.BuildingState.Completed)
                {
                    nearbyCommandPosts.Add(building);
                }
            }

            // Fallback: If nothing found in radius, try finding any Command Post in the scene
            if (nearbyCommandPosts.Count == 0)
            {
                var allBuildings = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
                foreach (var building in allBuildings)
                {
                    if ((CommandPostBuilding.Value == null || building.UnitSO == CommandPostBuilding.Value)
                        && building.Progress.State == BuildingProgress.BuildingState.Completed)
                    {
                        nearbyCommandPosts.Add(building);
                    }
                }
            }

            if (nearbyCommandPosts.Count == 0)
            {
                Debug.LogWarning($"[FindClosestCommandPostAction] {Unit.Value.name} failed to find any completed Command Post. SearchRadius={SearchRadius.Value}");
                return Status.Failure;
            }

            nearbyCommandPosts.Sort(new ClosestCommandPostComparer(Unit.Value.transform.position));
            CommandPost.Value = nearbyCommandPosts[0].gameObject;

            return Status.Success;
        }
    }
}
