namespace GameDevTV.RTS.Commands
{
    public interface ICommand
    {
        public bool IsSingleUnitCommand { get; }
        bool CanHandle(CommandContext context);
        void Handle(CommandContext context);
    }
}