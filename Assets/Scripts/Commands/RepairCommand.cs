using GameDevTV.RTS.Units;
using GameDevTV.RTS.Player;
using UnityEngine;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Repair", menuName = "Units/Commands/Repair")]
    public class RepairCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            if (context.Hit.collider == null) return false;
            
            if (context.Hit.collider.TryGetComponent(out AbstractCommandable target))
            {
                // Only repair friendly units that are damaged
                return target.Owner == context.Owner && target.CurrentHealth < target.MaxHealth;
            }
            
            return false;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Hit.collider.TryGetComponent(out AbstractCommandable target))
            {
                if (context.Commandable is IRepairer repairer)
                {
                    repairer.Repair(target);
        
                    // Notify GameFlowManager that an action was taken
                    if (GameFlowManager.Instance != null)
                    {
                        GameFlowManager.Instance.PlayerActed();
                    }
                }
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
