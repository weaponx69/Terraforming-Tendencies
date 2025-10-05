using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Events
{
    public struct PlaceholderDestroyEvent : IEvent
    {
        public Placeholder Placeholder { get; private set; }

        public PlaceholderDestroyEvent(Placeholder placeholder)
        {
            Placeholder = placeholder;
        }
    }
}