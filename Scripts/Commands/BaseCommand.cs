using UnityEngine;
using System.Linq;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Commands
{
    public abstract class BaseCommand : ScriptableObject, ICommand
    {
        [field: SerializeField] public string Name { get; private set; } = "Command";
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: Range(-1, 8)] [field: SerializeField] public int Slot { get; private set; }
        [field: SerializeField] public bool RequiresClickToActivate { get; private set; } = true;
        [field: SerializeField] public bool IsSingleUnitCommand { get; private set; }
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

        public bool AllRestrictionsPass(Vector3 point) =>
            Restrictions.Length == 0 || Restrictions.All(restriction => restriction.CanPlace(point));

        public bool IsHitColliderVisible(CommandContext context) => context.Hit.collider != null
            && context.Hit.collider.TryGetComponent(out IHideable hideable) && hideable.IsVisible;
    }
}
