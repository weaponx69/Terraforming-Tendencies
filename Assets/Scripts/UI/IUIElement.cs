namespace GameDevTV.RTS.UI
{
    public interface IUIElement<T>
    {
        void EnableFor(T item);
        void Disable();
    }

    public interface IUIElement<T1,T2>
    {
        void EnableFor(T1 item, T2 callback);
        void Disable();
    }

    public interface IUIElement<T1, T2, T3>
    {
        void EnableFor(T1 item, T2 context, T3 callback);
        void Disable();
    }
}