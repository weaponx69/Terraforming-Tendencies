using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct SupplyEvent : IEvent
    {
        public int Amount { get; private set; }
        public SupplySO Supply { get; private set; }
        public Owner Owner { get; private set; }

        public SupplyEvent(Owner owner, int amount, SupplySO supply)
        {
            Amount = amount;
            Supply = supply;
            Owner = owner;
        }
    }
}