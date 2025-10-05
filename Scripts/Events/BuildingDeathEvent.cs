using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct BuildingDeathEvent : IEvent
    {
        public BaseBuilding Building { get; private set; }
        public Owner Owner { get; private set; }

        public BuildingDeathEvent(Owner owner, BaseBuilding building)
        {
            Building = building;
            Owner = owner;
        }
    }
}