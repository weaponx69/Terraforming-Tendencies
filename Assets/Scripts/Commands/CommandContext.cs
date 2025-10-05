using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Commands
{
    public struct CommandContext
    {
        public AbstractCommandable Commandable { get; private set; }
        public RaycastHit Hit { get; private set; }
        public int UnitIndex { get; private set; }
        public MouseButton Button { get; private set; }
        public Owner Owner { get; private set; }

        public CommandContext(AbstractCommandable commandable, RaycastHit hit, int unitIndex = 0, MouseButton mouseButton = MouseButton.Left)
        {
            Commandable = commandable;
            Hit = hit;
            UnitIndex = unitIndex;
            Button = mouseButton;
            Owner = Owner.Player1;
        }

        public CommandContext(Owner owner, AbstractCommandable commandable, RaycastHit hit, int unitIndex = 0, MouseButton mouseButton = MouseButton.Left)
        {
            Commandable = commandable;
            Hit = hit;
            UnitIndex = unitIndex;
            Button = mouseButton;
            Owner = owner;
        }
    }
}
