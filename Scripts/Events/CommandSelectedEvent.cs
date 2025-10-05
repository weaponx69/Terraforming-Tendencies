using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Events
{
    public struct CommandSelectedEvent : IEvent
    {
        public BaseCommand Command { get; }

        public CommandSelectedEvent(BaseCommand command)
        {
            Command = command;
        }
    }
}