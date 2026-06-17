using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Build Unit", menuName = "Buildings/Commands/Build Unit", order = 120)]
    public class BuildUnitCommand : BaseCommand, IUnlockableCommand
    {
        [field: SerializeField] public AbstractUnitSO Unit { get; private set; }
        
        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding && HasEnoughSupplies(context);
        }

        public override void Handle(CommandContext context)
        {
            if (!HasEnoughSupplies(context)) return;

            BaseBuilding building = (BaseBuilding)context.Commandable;
            building.BuildUnlockable(Unit);
        }

        public override bool IsLocked(CommandContext context)
        {
            if (!HasEnoughSupplies(context)) return true;
            if (!Unit.TechTree.IsUnlocked(context.Owner, Unit)) return true;
            
            if (context.Commandable is BaseBuilding building)
            {
                if (building.QueueSize >= building.MaxQueueSize) return true;
            }
            
            return false;
        }

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            return Unit.TechTree.GetUnmetDependencies(owner, Unit);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            if (Unit == null || Unit.Cost == null) return true;

            int materialsCost = Mathf.FloorToInt(Unit.Cost.Minerals * Supplies.MineralsToMaterialsRateStatic
                + Unit.Cost.Gas * Supplies.GasToMaterialsRateStatic);
            return materialsCost <= Supplies.Materials[context.Owner];
        }
    }
}
