using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Events
{
    public struct BuildingSpawnEvent : IEvent
    {
        public BaseBuilding Building { get; private set; }
        public Owner Owner { get; private set; }

        public BuildingSpawnEvent(Owner owner, BaseBuilding building)
        {
            Building = building;
            Owner = owner;
        }
    }
}