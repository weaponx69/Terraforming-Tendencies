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
            return context.Commandable is Worker 
                && context.Hit.collider != null 
                && IsGatherableSupplyOrCommandPost(context.Hit.collider);
        }

        public override void Handle(CommandContext context)
        {
            Worker worker = context.Commandable as Worker;
            if (!IsHitColliderVisible(context))
            {
                worker.MoveTo(context.Hit.collider.gameObject.transform.position);
            }
            else if (context.Hit.collider.TryGetComponent(out GatherableSupply supply))
            {
                worker.Gather(supply);
            }
            else if (IsCommandPost(context.Hit.collider) && worker.HasSupplies)
            {
                worker.ReturnSupplies(context.Hit.collider.gameObject);
            }
            else
            {
                worker.MoveTo(context.Hit.collider.gameObject.transform.position);
            }
        }

        public override bool IsLocked(CommandContext context) => false;

        private bool IsGatherableSupplyOrCommandPost(Collider collider) => collider.TryGetComponent(out GatherableSupply _) || IsCommandPost(collider);
        private bool IsCommandPost(Collider collider) => collider.TryGetComponent(out BaseBuilding building) && building.UnitSO.Equals(commandPostSO);
    }
}