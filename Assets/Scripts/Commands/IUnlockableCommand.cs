using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Commands
{
    public interface IUnlockableCommand
    {
        public UnlockableSO[] GetUnmetDependencies(Owner owner);
    }
}