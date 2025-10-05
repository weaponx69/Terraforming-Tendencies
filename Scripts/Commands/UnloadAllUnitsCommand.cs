using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Unload Units", menuName = "Units/Commands/Unload All Units", order = 107)]
    public class UnloadAllUnitsCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is ITransporter transporter && transporter.UsedCapacity > 0;
        }

        public override void Handle(CommandContext context)
        {
            ITransporter transporter = context.Commandable as ITransporter;

            transporter.UnloadAll();
        }

        public override bool IsLocked(CommandContext context) => context.Commandable is not ITransporter transporter || transporter.UsedCapacity == 0;
    }
}