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
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(100);
        [SerializeReference] public BlackboardVariable<BuildingSO> CommandPostBuilding;

        protected override Status OnStart()
        {
            Owner unitOwner = Owner.Player1;
            if (Unit.Value != null && Unit.Value.TryGetComponent(out AbstractCommandable commandableUnit))
            {
                unitOwner = commandableUnit.Owner;
            }
            Debug.Log($"[FindClosestCommandPostAction] {Unit.Value.name} starting search. UnitOwner={unitOwner}, SearchRadius={SearchRadius.Value}");

            int layerMask = LayerMask.GetMask("Buildings", "Units");
            Collider[] colliders = Physics.OverlapSphere(
                Unit.Value.transform.position, 
                SearchRadius.Value, 
                layerMask);

            List<BaseBuilding> nearbyCommandPosts = new();

            foreach(Collider collider in colliders)
            {
                if (collider.GetComponentInParent<BaseBuilding>() is BaseBuilding building) 
                {
                    bool soMatch = CommandPostBuilding.Value == null || (building.UnitSO != null && building.UnitSO.Name == CommandPostBuilding.Value.Name);
                    bool ownerMatch = building.Owner == unitOwner;
                    bool stateMatch = building.Progress.State == BuildingProgress.BuildingState.Completed;
                    
                    if (soMatch && ownerMatch && stateMatch)
                    {
                        nearbyCommandPosts.Add(building);
                    }
                }
            }

            // Fallback: If nothing found in radius, try finding any Command Post in the scene
            if (nearbyCommandPosts.Count == 0)
            {
                var allBuildings = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
                foreach (var building in allBuildings)
                {
                    bool soMatch = CommandPostBuilding.Value == null || (building.UnitSO != null && building.UnitSO.Name == CommandPostBuilding.Value.Name);
                    bool ownerMatch = building.Owner == unitOwner;
                    bool stateMatch = building.Progress.State == BuildingProgress.BuildingState.Completed;

                    if (soMatch && ownerMatch && stateMatch)
                    {
                        nearbyCommandPosts.Add(building);
                    }
                    else
                    {
                        // Log why it didn't match
                        Debug.Log($"[FindClosestCommandPostAction] Scene Candidate {building.name}: SOMatch={soMatch} (Building: {building.UnitSO?.Name} vs Filter: {CommandPostBuilding.Value?.Name}), OwnerMatch={ownerMatch} (Building: {building.Owner} vs Unit: {unitOwner}), StateMatch={stateMatch} (State: {building.Progress.State})");
                    }
                }
            }

            if (nearbyCommandPosts.Count == 0)
            {
                Debug.LogWarning($"[FindClosestCommandPostAction] {Unit.Value.name} failed to find any completed Command Post. " +
                                 $"UnitOwner={unitOwner}, FilterSO={(CommandPostBuilding.Value != null ? CommandPostBuilding.Value.Name : "None")}");
                return Status.Failure;
            }

            nearbyCommandPosts.Sort(new ClosestCommandPostComparer(Unit.Value.transform.position));
            CommandPost.Value = nearbyCommandPosts[0].gameObject;
            Debug.Log($"[FindClosestCommandPostAction] {Unit.Value.name} SUCCESS! Found {CommandPost.Value.name} at {CommandPost.Value.transform.position}.");

            return Status.Success;
            }
            }
            }
