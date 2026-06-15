using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Gather Action", menuName = "Units/Commands/Gather", order = 105)]
    public class GatherCommand : BaseCommand
    {
        [SerializeField] private AbstractUnitSO commandPostSO;

        public override bool CanHandle(CommandContext context)
        {
            if (context.Commandable is Worker)
            {
                if (context.Hit.collider != null)
                {
                    if (IsGatherableSupplyOrCommandPost(context.Hit.collider))
                    {
                        return true;
                    }
                    
                    // Smart detection: check if there's a nearby gatherable where they clicked
                    var nearby = FindNearbyGatherable(context.Hit.point);
                    if (nearby != null) return true;
                }
            }
            return false;
        }

        public override void Handle(CommandContext context)
        {
            Worker worker = context.Commandable as Worker;
            if (context.Hit.collider == null) return;

            if (context.Hit.collider.TryGetComponent(out GatherableSupply supply))
            {
                worker.Gather(supply);
            }

            else if (IsCommandPost(context.Hit.collider) && worker.HasSupplies)
            {
                worker.ReturnSupplies(context.Hit.collider.gameObject);
            }
            else
            {
                var nearby = FindNearbyGatherable(context.Hit.point);
                if (nearby != null)
                {
                    worker.Gather(nearby);
                }
                else
                {
                    worker.MoveTo(context.Hit.point);
                }
            }
        }

        public override bool IsLocked(CommandContext context) => false;

        private bool IsGatherableSupplyOrCommandPost(Collider collider) => collider.TryGetComponent(out GatherableSupply _) || IsCommandPost(collider);
        private bool IsCommandPost(Collider collider) => collider.TryGetComponent(out BaseBuilding building) && building.UnitSO.Equals(commandPostSO);

        private GatherableSupply FindNearbyGatherable(Vector3 point)
        {
            GatherableSupply best = null;
            float minDist = 4.0f; // 4-meter radius
            foreach (var gs in GatherableSupply.ActiveSupplies)
            {
                if (gs == null) continue;
                float dist = Vector3.Distance(point, gs.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    best = gs;
                }
            }
            return best;
        }
    }
}