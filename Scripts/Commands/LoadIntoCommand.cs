using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Load Unit into Transport", menuName = "Units/Commands/Load Unit Into", order = 107)]
    public class LoadIntoCommand : BaseCommand
    {
        public override bool CanHandle(CommandContext context)
        {
            return context.Commandable is ITransportable transportable
                && context.Hit.collider != null
                && context.Hit.collider.TryGetComponent(out ITransporter transporter)
                && transportable.Owner == transporter.Owner;
        }

        public override void Handle(CommandContext context)
        {
            ITransportable transportable = (ITransportable)context.Commandable;
            ITransporter transporter = context.Hit.collider.GetComponent<ITransporter>();

            transportable.LoadInto(transporter);
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}