using UnityEngine;
using GameDevTV.RTS.Player;

namespace GameDevTV.RTS.Commands
{
    /// <summary>
    /// Simple command that plays a card from the player's hand when activated.
    /// Used by the bottom bar for non-building cards (resource, spawn, buff).
    /// RequiresClickToActivate is false so it fires immediately on click.
    /// </summary>
    [CreateAssetMenu(fileName = "Play Card", menuName = "Units/Commands/Play Card")]
    public class PlayCardCommand : BaseCommand
    {
        public int HandIndex { get; set; }

        public override bool RequiresClickToActivate => false;

        public override bool CanHandle(CommandContext context) => true;

        public override void Handle(CommandContext context)
        {
            if (CardDeckController.Instance != null)
            {
                CardDeckController.Instance.PlayCard(HandIndex);
            }
        }

        public override bool IsLocked(CommandContext context) => false;
    }
}