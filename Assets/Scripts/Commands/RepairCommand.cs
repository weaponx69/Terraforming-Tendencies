using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
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
                }
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
