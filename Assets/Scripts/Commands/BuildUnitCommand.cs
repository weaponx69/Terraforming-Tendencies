using UnityEngine;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Build Unit", menuName = "Buildings/Commands/Build Unit", order = 120)]
    public class BuildUnitCommand : BaseCommand, IUnlockableCommand
    {
        [Inspectable]
        [field: SerializeField] public AbstractUnitSO Unit { get; private set; }
        
        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context)
        {
            return (context.Commandable is BaseBuilding || context.Commandable is GlobalCommander) && HasEnoughSupplies(context);
        }

        public override void Handle(CommandContext context)
        {
            if (!HasEnoughSupplies(context)) return;

            if (context.Commandable is BaseBuilding building)
            {
                building.BuildUnlockable(Unit);
            }
            else if (context.Commandable is GlobalCommander)
            {
                // For GlobalCommander, find a BaseBuilding to handle the build
                var buildings = FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
                foreach (var b in buildings)
                {
                    if (b.Owner == context.Owner)
                    {
                        b.BuildUnlockable(Unit);
                        break;
                    }
                }
            }
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
