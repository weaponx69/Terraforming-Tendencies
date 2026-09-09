using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.VisualScriptingStubs;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Scrap a selected player building for a partial Materials refund (roguelike store loop).
    /// </summary>
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Demolish", menuName = "Units/Commands/Demolish")]
    public class DemolishCommand : BaseCommand
    {
        public override bool RequiresClickToActivate
        {
            get => false;
            protected set { }
        }

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding building
                && building.Owner == context.Owner
                && building.Progress.State != BuildingProgress.BuildingState.Destroyed;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is not BaseBuilding building) return;
            building.TryDemolish(refund: true);
        }

        public override bool IsLocked(CommandContext context) => !CanHandle(context);
    }
}
