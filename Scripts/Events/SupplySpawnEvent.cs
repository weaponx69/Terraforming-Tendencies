using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Events
{
    public struct SupplySpawnEvent : IEvent
    {
        public GatherableSupply Supply { get; private set; }

        public SupplySpawnEvent(GatherableSupply supply)
        {
            Supply = supply;
        }
    }
}