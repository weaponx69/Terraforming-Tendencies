using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.TechTree
{
    public interface IModifier
    {
        public string PropertyPath { get; }
        public void Apply(AbstractUnitSO unit);
    }
}