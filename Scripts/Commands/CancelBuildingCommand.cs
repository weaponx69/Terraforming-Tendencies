using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Cancel Building", menuName = "Units/Commands/Cancel Building")]
    public class CancelBuildingCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is IBuildingBuilder
                && context.Button == MouseButton.Left;
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder buildingBuilder = context.Commandable as IBuildingBuilder;
            buildingBuilder.CancelBuilding();
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
