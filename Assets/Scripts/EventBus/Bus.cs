using System;
using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.EventBus
{
    public static class Bus<T> where T : IEvent
    {
        public delegate void Event(T args);
        public static Dictionary<Owner, Event> OnEvent = new()
        {
            { Owner.Player1, null },
            { Owner.AI1, null },
            { Owner.AI2, null },
            { Owner.AI3, null },
            { Owner.AI4, null },
            { Owner.AI5, null },
            { Owner.AI6, null },
            { Owner.AI7, null },
            { Owner.Invalid, null },
            { Owner.Unowned, null }
        };

        public static void Raise(Owner owner, T evt) => OnEvent[owner]?.Invoke(evt);

        public static void RegisterForAll(Event handler)
        {
            foreach(Owner owner in Enum.GetValues(typeof(Owner)))
            {
                OnEvent[owner] += handler;
            }
        }

        public static void UnregisterForAll(Event handler)
        {
            foreach(Owner owner in Enum.GetValues(typeof(Owner)))
            {
                OnEvent[owner] -= handler;
            }
        }
    }
}
