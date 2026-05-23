using UnityEngine;

namespace GameDevTV.RTS.Units
{
    public class GlobalCommander : AbstractCommandable
    {
        protected override void Start()
        {
            base.Start();
            // The global commander is always owned by Player 1
            Owner = Owner.Player1;
        }
    }
}
