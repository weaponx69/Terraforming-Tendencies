using Unity.Behavior;

namespace GameDevTV.RTS.Units
{
    [BlackboardEnum]
    public enum BuildingEventType
    {
        ArrivedAt,
        Begin,
        Cancel,
        Abort,
        Completed
    }
}
