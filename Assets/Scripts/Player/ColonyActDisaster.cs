using GameDevTV.RTS.Environment;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// Colony Acts: stronger card plays have a chance to spawn a hazard strike.
    /// </summary>
    public static class ColonyActDisaster
    {
        private const float MaxChance = 0.50f;

        public static void TryProc(BlueprintCardSO card)
        {
            var acts = ColonyActManager.Instance;
            if (acts == null || !acts.IsRunActive || acts.IsBetweenActs || acts.IsRunEnded)
                return;
            if (card == null) return;

            float chance = ResolveChance(card);
            if (chance <= 0.001f) return;
            if (Random.value > chance) return;

            GameObject preferred = null;
            if (card.HazardEventPrefabs != null)
            {
                for (int i = 0; i < card.HazardEventPrefabs.Count; i++)
                {
                    if (card.HazardEventPrefabs[i] != null)
                    {
                        preferred = card.HazardEventPrefabs[i];
                        break;
                    }
                }
            }

            NaturalEventManager.EnsureExists();
            NaturalEventManager.Instance?.TriggerStrike(preferred);

            string name = preferred != null ? preferred.name : "Meteor";
            acts.ShowStatusBanner(
                $"<color=#FF8A4A><b>DISASTER</b></color>  {name} inbound — {card.cardName} drew Corp attention ({chance:P0})",
                4.5f);
            Debug.Log($"[ColonyActDisaster] Proc from '{card.cardName}' chance={chance:P0}");
        }

        public static float ResolveChance(BlueprintCardSO card)
        {
            if (card == null) return 0f;

            if (card.DisasterChance > 0.001f)
                return Mathf.Clamp(card.DisasterChance * RarityMultiplier(card.Rarity), 0f, MaxChance);

            int weeks = CardDeckController.GetWeekCost(card);
            float chance = weeks switch
            {
                <= 0 => 0.03f,
                1 => 0.15f,
                _ => 0.30f
            };

            if (card is UnlockBuildingCardSO unlock && unlock.buildingToUnlock != null)
            {
                ColonyActManager.GetTileValues(unlock.buildingToUnlock, out _, out _, out string tag);
                if (tag == "Industry" || tag == "Heat" || tag == "Air" || tag == "Water")
                    chance += 0.10f;
                if (BuildingSiteRegistry.IsCommandPostBuilding(unlock.buildingToUnlock))
                    chance = Mathf.Min(chance, 0.08f);
            }

            chance *= RarityMultiplier(card.Rarity);
            return Mathf.Clamp(chance, 0f, MaxChance);
        }

        private static float RarityMultiplier(CardRarity rarity) => rarity switch
        {
            CardRarity.Uncommon => 1.2f,
            CardRarity.Rare => 1.5f,
            CardRarity.Epic => 1.75f,
            _ => 1f
        };
    }
}
