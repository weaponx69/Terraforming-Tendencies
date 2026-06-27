using UnityEngine;
using System.Linq;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.VisualScriptingStubs;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Base class for all RTS commands. Abstract logic (CanHandle/Handle/IsLocked)
    /// stays in C#. Inspector fields are decorated for VS Type Options visibility.
    /// </summary>
    [IncludeInSettings(true)]
    public abstract class BaseCommand : ScriptableObject, ICommand
    {
        [Inspectable]
        [field: SerializeField] public string Name { get; set; } = "Command";
        [Inspectable]
        [field: SerializeField] public Sprite Icon { get; set; }
        [Inspectable]
        [field: Range(-1, 8)] [field: SerializeField] public int Slot { get; set; }
        [Inspectable]
        [field: SerializeField] public virtual bool RequiresClickToActivate { get; protected set; } = true;
        [Inspectable]
        [field: SerializeField] public bool IsSingleUnitCommand { get; private set; }
        [Inspectable]
        [field: SerializeField] public GameObject GhostPrefab { get; private set; }
        [field: SerializeField] public BuildingRestrictionSO[] Restrictions { get; private set; }

        public abstract bool CanHandle(CommandContext context);
        public abstract void Handle(CommandContext context);
        /// <summary>
        /// Whether or not this item should be enabled on the UI when displayed.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract bool IsLocked(CommandContext context);

        /// <summary>
        /// Whether or not this item is eligible to show up on the UI.
        /// For example, Upgrades may have multiple items assigned to the same slot.
        /// This function should differentiate which one will show up at a given time.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual bool IsAvailable(CommandContext context) => true;

        public virtual bool StaysActive => false;

        public virtual bool AllRestrictionsPass(Vector3 point) =>
            Restrictions.Length == 0 || Restrictions.All(restriction => restriction.CanPlace(point));

        public bool IsHitColliderVisible(CommandContext context) => context.Hit.collider != null
            && context.Hit.collider.TryGetComponent(out IHideable hideable) && hideable.IsVisible;
    }
}
