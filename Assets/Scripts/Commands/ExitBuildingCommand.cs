using GameDevTV.RTS.Units;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "ExitBuilding", menuName = "Units/Commands/ExitBuilding")]
    public class ExitBuildingCommand : BaseCommand
    {
        public ExitBuildingCommand()
        {
            Name = "Send Commander Outside";
            Slot = 7; 
            RequiresClickToActivate = false; 
        }

        public override bool CanHandle(CommandContext context)
        {
            return MartianColonist.Instance != null && 
                   MartianColonist.Instance.IsInside && 
                   MartianColonist.Instance.CurrentBuilding == context.Commandable;
        }

        public override void Handle(CommandContext context)
        {
            if (MartianColonist.Instance != null && MartianColonist.Instance.IsInside)
            {
                MartianColonist.Instance.ExitBuilding();
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
