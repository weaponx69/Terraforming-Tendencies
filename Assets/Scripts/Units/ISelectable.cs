namespace GameDevTV.RTS.Units
{
    public interface ISelectable
    {
        bool IsSelected { get; }
        void Select();
        void Deselect();
    }
}
