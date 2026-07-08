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
    
            [Header("Disaster / Hazard Settings")]
            [Tooltip("The negative hazard/disaster prefabs that this card can register to the NaturalEventManager's pool when played.")]
            [SerializeField] private List<GameObject> hazardEventPrefabs = new List<GameObject>();
            public List<GameObject> HazardEventPrefabs => hazardEventPrefabs;
    
            public abstract void Apply();
            public virtual bool IsGateMet() => true;
            public virtual string GetCardGoal() => "BLUEPRINT";
        }
}
