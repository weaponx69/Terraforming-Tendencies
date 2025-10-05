using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct UnitSelectedEvent : IEvent
    {
        public ISelectable Unit { get; private set; }

        public UnitSelectedEvent(ISelectable unit)
        {
            Unit = unit;
        }
    }
}