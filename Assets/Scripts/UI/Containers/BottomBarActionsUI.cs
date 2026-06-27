using System.Collections.Generic;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Components;
using UnityEngine;

namespace GameDevTV.RTS.UI.Containers
{
    /// <summary>
    /// Persistent bottom-center action bar that mirrors the same commands as the
    /// original ActionsUI panel. Always visible regardless of selection state.
    /// Inherits all command-collection and rendering logic from ActionPanelBase.
    ///
    /// Wire in Inspector:
    ///   - actionButtons  : array of UIActionButton in the bottom bar panel
    ///
    /// This panel is NOT managed by RuntimeUI's selection logic — it subscribes
    /// to the same events independently and stays active at all times.
    /// </summary>
    public class BottomBarActionsUI : ActionPanelBase
    {
        private void Start()
        {
            // Ensure the panel is always active
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Called by RuntimeUI to sync this panel with the current selection.
        /// </summary>
        public void SyncSelection(HashSet<AbstractCommandable> selectedUnits)
        {
            if (selectedUnits != null && selectedUnits.Count > 0)
            {
                base.EnableFor(selectedUnits);
                gameObject.SetActive(true);
            }
            else
            {
                // No selection — clear buttons but keep panel active
                base.Disable();
                gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Override Disable to keep the panel active (just clear buttons).
        /// </summary>
        public new void Disable()
        {
            base.Disable();
            gameObject.SetActive(true);
        }
    }
}
