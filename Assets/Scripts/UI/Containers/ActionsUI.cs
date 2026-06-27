using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Components;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Original selection-driven action panel. Now inherits shared logic from ActionPanelBase.
    /// This panel is shown/hidden based on unit selection (managed by RuntimeUI).
    /// </summary>
    public class ActionsUI : ActionPanelBase, IUIElement<HashSet<AbstractCommandable>>
    {
        // actionButtons is inherited from ActionPanelBase — wire in Inspector on this component

        public new void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            base.EnableFor(selectedUnits);
        }

        public new void Disable()
        {
            base.Disable();
            gameObject.SetActive(false);
        }
    }
}
