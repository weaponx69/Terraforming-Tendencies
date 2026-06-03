namespace GameDevTV.RTS.Units
{
    public interface IRepairer
    {
        Owner Owner { get; }
        void Repair(AbstractCommandable target);
    }
}
