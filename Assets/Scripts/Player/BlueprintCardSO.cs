using System.Collections.Generic;
using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public abstract class BlueprintCardSO : ScriptableObject
    {
        [Header("Card Metadata")]
        public string cardName;
        [TextArea(3, 5)]
        public string cardDescription;
        public Sprite icon;

        [Header("Economy")]
        [Tooltip("Materials spent when this card is played. Building cards use BuildingSO.Cost when set; this is the fallback / non-building play cost.")]
        [SerializeField] private int materialsCost;
        public int MaterialsCost => materialsCost;

        [Header("Disaster / Hazard Settings")]
        [Tooltip("The negative hazard/disaster prefabs that this card can register to the NaturalEventManager's pool when played.")]
        [SerializeField] private List<GameObject> hazardEventPrefabs = new List<GameObject>();
        public List<GameObject> HazardEventPrefabs => hazardEventPrefabs;

        public abstract void Apply();

        /// <summary>Returns false when the card cannot be played right now (e.g. insufficient energy).</summary>
        public virtual bool CanApply() => true;

        public virtual bool IsGateMet() => true;
        public virtual string GetCardGoal() => "BLUEPRINT";

        /// <summary>Materials required to play this card (shown on the hand UI).</summary>
        public virtual int GetMaterialsPlayCost()
        {
            return Mathf.Max(0, materialsCost);
        }

        /// <summary>True when the player can pay <see cref="GetMaterialsPlayCost"/>.</summary>
        public bool CanAffordMaterials()
        {
            int cost = GetMaterialsPlayCost();
            if (cost <= 0) return true;
            if (Supplies.Materials == null) return false;
            return Supplies.Materials.TryGetValue(Owner.Player1, out int have) && have >= cost;
        }

        /// <summary>Spend materials for this card play. No-op when cost is 0.</summary>
        public bool TrySpendMaterials()
        {
            int cost = GetMaterialsPlayCost();
            if (cost <= 0) return true;
            if (Supplies.Materials == null) return false;
            if (!Supplies.Materials.TryGetValue(Owner.Player1, out int have) || have < cost)
                return false;

            int remaining = have - cost;
            Supplies.Materials[Owner.Player1] = remaining;
            Supplies.RaiseMaterialsChanged(Owner.Player1, remaining);
            Debug.Log($"[Blueprint] Spent {cost} materials to play '{cardName}'. Remaining: {remaining}");
            return true;
        }

        protected void SetMaterialsCost(int cost) => materialsCost = Mathf.Max(0, cost);
    }
}
