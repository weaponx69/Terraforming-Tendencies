using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Events
{
    public struct PlaceholderSpawnEvent : IEvent
    {
        public Placeholder Placeholder { get; private set; }

        public PlaceholderSpawnEvent(Placeholder placeholder)
        {
            Placeholder = placeholder;
        }
    }
}