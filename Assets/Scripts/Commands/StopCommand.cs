using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Stop Action", menuName = "Units/Commands/Stop", order = 101)]
    public class StopCommand : BaseCommand
    {
        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context)
        {
            // Right-click on the ground should move/gather, not stop.
            if (context.Button == MouseButton.Right) return false;
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