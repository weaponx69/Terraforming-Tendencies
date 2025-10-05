using Unity.Behavior;

namespace GameDevTV.RTS.Units
{
    [BlackboardEnum]
    public enum UnitCommands
    {
        Stop,
        Move,
        Gather,
        ReturnSupplies,
        BuildBuilding,
        Attack,
        LoadUnits
    }
}
