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

        public override bool IsLocked(CommandContext context)
        {
            float oxygen = 0f;
            if (GameDevTV.RTS.Player.Supplies.Oxygen != null && 
                GameDevTV.RTS.Player.Supplies.Oxygen.TryGetValue(context.Owner, out float ox))
            {
                oxygen = ox;
            }
            
            bool breathable = oxygen >= 30f;
            bool hasSpacesuits = GameDevTV.RTS.Player.BlueprintDraftManager.HasSpacesuits;

            return !breathable && !hasSpacesuits;
        }
    }
}
