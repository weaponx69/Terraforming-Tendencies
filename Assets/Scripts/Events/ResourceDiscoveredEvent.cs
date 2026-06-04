using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;

namespace GameDevTV.RTS.Events
{
    public struct ResourceDiscoveredEvent : IEvent
    {
        public HiddenResource Resource;
        public ResourceDiscoveredEvent(HiddenResource resource)
        {
            Resource = resource;
        }
    }
}
