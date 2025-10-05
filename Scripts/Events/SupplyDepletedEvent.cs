using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Events
{
    public struct SupplyDepletedEvent : IEvent
    {
        public GatherableSupply Supply { get; private set; }

        public SupplyDepletedEvent(GatherableSupply supply)
        {
            Supply = supply;
        }
    }
}