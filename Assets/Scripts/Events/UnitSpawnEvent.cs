using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct UnitSpawnEvent : IEvent
    {
        public AbstractUnit Unit { get; private set; }

        public UnitSpawnEvent(AbstractUnit unit)
        {
            Unit = unit;
        }
    }
}