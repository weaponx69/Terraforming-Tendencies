using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Stop Action", menuName = "Units/Commands/Stop", order = 101)]
    public class StopCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is AbstractUnit;
        }

        public override void Handle(CommandContext context)
        {
            AbstractUnit unit = (AbstractUnit)context.Commandable;
            unit.Stop();
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}