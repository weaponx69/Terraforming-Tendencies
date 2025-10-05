using GameDevTV.RTS.Units;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Build Building", story: "[Self] builds [BuildingSO] at [TargetLocation] .", category: "Action/Units", id: "32f744f391217b3bc542115e266d16c0")]
    public partial class BuildBuildingAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<BuildingSO> BuildingSO;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;
        [SerializeReference] public BlackboardVariable<BaseBuilding> BuildingUnderConstruction;

        private float startBuildTime;
        private BaseBuilding completedBuilding;
        private Renderer buildingRenderer;
        private Vector3 startPosition;
        private Vector3 endPosition;
        private float targetHealth;

        protected override Status OnStart()
        {
            if (!HasValidInputs()) return Status.Failure;

            if (BuildingUnderConstruction.Value == null)
            {
                GameObject building = GameObject.Instantiate(BuildingSO.Value.Prefab, TargetLocation, Quaternion.identity);
                if (!building.TryGetComponent(out completedBuilding)
                    || completedBuilding.MainRenderer == null) return Status.Failure;
            }
            else
            {
                completedBuilding = BuildingUnderConstruction.Value;
            }

            completedBuilding.StartBuilding(Self.Value.GetComponent<IBuildingBuilder>());
            startBuildTime = completedBuilding.Progress.StartTime;

            buildingRenderer = completedBuilding.MainRenderer;

            BuildingUnderConstruction.Value = completedBuilding;

            startPosition = TargetLocation.Value - Vector3.up * buildingRenderer.bounds.size.y;
            endPosition = TargetLocation.Value;
            buildingRenderer.transform.position = startPosition;

            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            float normalizedTime = (Time.time - startBuildTime) / BuildingSO.Value.BuildTime;

            targetHealth += Time.deltaTime * (BuildingSO.Value.Health / BuildingSO.Value.BuildTime);
            if (targetHealth >= 1)
            {
                int healAmount = Mathf.FloorToInt(targetHealth);
                completedBuilding.Heal(healAmount);
                targetHealth -= healAmount;
            }

            buildingRenderer.transform.position = Vector3.Lerp(startPosition, endPosition, normalizedTime);

            return normalizedTime >= 1 ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            if (CurrentStatus == Status.Success)
            {
                completedBuilding.enabled = true;
            }
        }

        private bool HasValidInputs()
        {
            return Self.Value != null
                && BuildingSO.Value != null
                && BuildingSO.Value.Prefab != null;
        }
    }
}
