using GameDevTV.RTS.Units;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Cancel Building", menuName = "Units/Commands/Cancel Building")]
    public class CancelBuildingCommand : BaseCommand
    {
        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is IBuildingBuilder
                && context.Button == UnityEngine.InputSystem.LowLevel.MouseButton.Left;
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder buildingBuilder = context.Commandable as IBuildingBuilder;
            buildingBuilder.CancelBuilding();
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
