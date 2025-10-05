using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct UnitDeathEvent : IEvent
    {
        public AbstractUnit Unit { get; private set; }

        public UnitDeathEvent(AbstractUnit unit)
        {
            Unit = unit;
        }
    }
}