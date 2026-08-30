using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Connect Power Command", menuName = "RTS/Commands/Connect Power")]
    public class ConnectPowerCommand : BaseCommand
    {
        public override bool RequiresClickToActivate => true;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding sourceBuilding)
            {
                return;
            }

            if (!sourceBuilding.TryGetComponent(out PowerNode sourceNode))
            {
                sourceNode = sourceBuilding.gameObject.AddComponent<PowerNode>();
            }

            BaseBuilding targetBuilding = null;
            if (context.Hit.collider != null)
            {
                targetBuilding = context.Hit.collider.GetComponentInParent<BaseBuilding>();
            }

            // Fallback: nearest completed building to the click (some solar prefabs lack colliders).
            if ((targetBuilding == null || targetBuilding == sourceBuilding) && context.Hit.point != Vector3.zero)
            {
                targetBuilding = FindNearestConnectableBuilding(sourceBuilding, context.Hit.point, 8f);
            }

            if (targetBuilding == null || targetBuilding == sourceBuilding)
            {
                Debug.LogWarning("[Power] Connect Power needs another building near the click point.");
                return;
            }

            if (!targetBuilding.TryGetComponent(out PowerNode targetNode))
            {
                targetNode = targetBuilding.gameObject.AddComponent<PowerNode>();
            }

            PowerGridManager.RegisterNode(sourceNode);
            PowerGridManager.RegisterNode(targetNode);
            sourceNode.ConnectTo(targetNode);
            Debug.Log($"[Power] Connected {sourceBuilding.name} to {targetBuilding.name}");
        }

        private static BaseBuilding FindNearestConnectableBuilding(BaseBuilding source, Vector3 point, float radius)
        {
            BaseBuilding nearest = null;
            float best = radius;
            foreach (var building in BaseBuilding.ActiveBuildings)
            {
                if (building == null || building == source) continue;
                if (building.Progress.State != BuildingProgress.BuildingState.Completed) continue;
                if (building.GetComponentInParent<BuildingSiteMarker>() != null) continue;
                float dist = Vector3.Distance(point, building.transform.position);
                if (dist < best)
                {
                    best = dist;
                    nearest = building;
                }
            }

            return nearest;
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
