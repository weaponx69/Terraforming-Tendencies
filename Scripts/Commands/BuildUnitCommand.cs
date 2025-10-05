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

        public override bool IsLocked(CommandContext context) =>
            !HasEnoughSupplies(context) || !Unit.TechTree.IsUnlocked(context.Owner, Unit);

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            return Unit.TechTree.GetUnmetDependencies(owner, Unit);
        }

        private bool HasEnoughSupplies(CommandContext context)
        {
            int biomassCost = Mathf.FloorToInt(Unit.Cost.Minerals * Supplies.MineralsToBiomassRateStatic
                + Unit.Cost.Gas * Supplies.GasToBiomassRateStatic);

            return biomassCost <= Supplies.Biomass[context.Owner];
        }
    }
}
