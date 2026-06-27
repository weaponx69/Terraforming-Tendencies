using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Override Commands", menuName = "Units/Commands/Override Commands", order = 110)]
    public class OverrideCommandsCommand : BaseCommand
    {
        public override bool RequiresClickToActivate => false;

        [field: SerializeField] public BaseCommand[] Commands { get; private set; }

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable != null;
        }

        public override void Handle(CommandContext context)
        {
            context.Commandable.SetCommandOverrides(Commands);
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}