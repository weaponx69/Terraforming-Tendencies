using Unity.VisualScripting;

namespace GameDevTV.RTS.Units
{
    [IncludeInSettings(true)]
    public class BaseMilitaryUnit : AbstractUnit, ITransportable
    {
        public int TransportCapacityUsage => unitSO.TransportConfig.GetTransportCapacityUsage();

        protected override void Start()
        {
            base.Start();

            SetCurrentCommand(UnitCommands.Attack);
        }

        public void LoadInto(ITransporter transporter)
        {
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }
    }
}