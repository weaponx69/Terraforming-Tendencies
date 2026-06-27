using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;
using Unity.VisualScripting;

namespace GameDevTV.RTS.Commands
{
    [IncludeInSettings(true)]
    [CreateAssetMenu(fileName = "Connect Power Command", menuName = "RTS/Commands/Connect Power")]
    public class ConnectPowerCommand : BaseCommand
    {
        public override bool RequiresClickToActivate => true;

        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is BaseBuilding;
        }

        public override void Handle(CommandContext context)
        {
            if (context.Commandable is BaseBuilding sourceBuilding && context.Hit.collider != null)
            {
                BaseBuilding targetBuilding = context.Hit.collider.GetComponentInParent<BaseBuilding>();
                
                if (targetBuilding != null && targetBuilding != sourceBuilding)
                {
                    if (sourceBuilding.TryGetComponent(out PowerNode sourceNode) && targetBuilding.TryGetComponent(out PowerNode targetNode))
                    {
                        sourceNode.ConnectTo(targetNode);
                        Debug.Log($"[Power] Connected {sourceBuilding.name} to {targetBuilding.name}");
                    }
                }
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}
