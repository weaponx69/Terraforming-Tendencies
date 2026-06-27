using UnityEngine;
using UnityEditor;
using GameDevTV.RTS.TechTree;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class CardDeckSetup
{
    [MenuItem("Tools/Card Deck/Create Card Assets from Unlockables")]
    public static void CreateCardAssetsFromUnlockables()
    {
        string cardsDir = "Assets/Resources/Cards";
        if (!AssetDatabase.IsValidFolder(cardsDir))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Cards");
        }

        // Find all UnlockableSO assets
        string[] guids = AssetDatabase.FindAssets("t:UnlockableSO", new[] { "Assets/Resources" });
        int created = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnlockableSO unlockable = AssetDatabase.LoadAssetAtPath<UnlockableSO>(path);
            if (unlockable == null) continue;

            // Check if a card already exists for this unlockable
            string cardName = unlockable.Name.Replace(" ", "") + "Card";
            string cardPath = $"{cardsDir}/{cardName}.asset";
            if (AssetDatabase.LoadAssetAtPath<CardSO>(cardPath) != null) continue;

            // Determine effect type from name
            CardEffectType effectType = CardEffectType.None;
            float effectAmount = 0f;
            CardRarity rarity = CardRarity.Common;
            int playCost = 50;

            string nameLower = unlockable.Name.ToLower();
            if (nameLower.Contains("solar") || nameLower.Contains("power"))
            {
                effectType = CardEffectType.Power;
                effectAmount = 25f;
                rarity = CardRarity.Common;
                playCost = 50;
            }
            else if (nameLower.Contains("oxygen") || nameLower.Contains("atmosphere"))
            {
                effectType = CardEffectType.Oxygen;
                effectAmount = 20f;
                rarity = CardRarity.Common;
                playCost = 50;
            }
            else if (nameLower.Contains("habitat") || nameLower.Contains("population") || nameLower.Contains("colonist"))
            {
                effectType = CardEffectType.Population;
                effectAmount = 5f;
                rarity = CardRarity.Uncommon;
                playCost = 75;
            }
            else if (nameLower.Contains("command") || nameLower.Contains("post"))
            {
                effectType = CardEffectType.CommandPost;
                effectAmount = 1f;
                rarity = CardRarity.Rare;
                playCost = 100;
            }
            else if (nameLower.Contains("biomass") || nameLower.Contains("plant"))
            {
                effectType = CardEffectType.Biomass;
                effectAmount = 15f;
                rarity = CardRarity.Common;
                playCost = 40;
            }
            else if (nameLower.Contains("water"))
            {
                effectType = CardEffectType.Water;
                effectAmount = 10f;
                rarity = CardRarity.Common;
                playCost = 40;
            }
            else if (nameLower.Contains("temperature") || nameLower.Contains("heat"))
            {
                effectType = CardEffectType.Temperature;
                effectAmount = 10f;
                rarity = CardRarity.Uncommon;
                playCost = 60;
            }
            else if (nameLower.Contains("mining") || nameLower.Contains("drone") || nameLower.Contains("material"))
            {
                effectType = CardEffectType.Materials;
                effectAmount = 100f;
                rarity = CardRarity.Common;
                playCost = 30;
            }
            else
            {
                effectType = CardEffectType.None;
                effectAmount = 0f;
                rarity = CardRarity.Common;
                playCost = 50;
            }

            CardSO card = ScriptableObject.CreateInstance<CardSO>();
            // Use reflection or serialized properties to set private fields
            var so = new SerializedObject(card);
            so.FindProperty("m_Name").stringValue = unlockable.Name;
            so.FindProperty("<CardName>k__BackingField").stringValue = unlockable.Name;
            so.FindProperty("<Icon>k__BackingField").objectReferenceValue = unlockable.Icon;
            so.FindProperty("<Description>k__BackingField").stringValue = $"Unlock {unlockable.Name} tech.";
            so.FindProperty("<WrappedUnlockable>k__BackingField").objectReferenceValue = unlockable;
            so.FindProperty("<Rarity>k__BackingField").enumValueIndex = (int)rarity;
            so.FindProperty("<DrawWeight>k__BackingField").floatValue = 1.0f;
            so.FindProperty("<PlayCost>k__BackingField").intValue = playCost;
            so.FindProperty("<EffectType>k__BackingField").enumValueIndex = (int)effectType;
            so.FindProperty("<EffectAmount>k__BackingField").floatValue = effectAmount;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(card, cardPath);
            created++;
            Debug.Log($"[CardDeckSetup] Created card: {cardPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CardDeckSetup] Created {created} card assets.");
    }

    [MenuItem("Tools/Card Deck/Setup Main Deck")]
    public static void SetupMainDeck()
    {
        string cardsDir = "Assets/Resources/Cards";
        CardDeckSO deck = AssetDatabase.LoadAssetAtPath<CardDeckSO>($"{cardsDir}/MainDeck.asset");
        if (deck == null)
        {
            Debug.LogError("[CardDeckSetup] MainDeck.asset not found. Run CreateCardAssetsFromUnlockables first.");
            return;
        }

        // Find all CardSO assets
        string[] cardGuids = AssetDatabase.FindAssets("t:CardSO", new[] { cardsDir });
        List<CardSO> allCards = new();

        foreach (string guid in cardGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardSO card = AssetDatabase.LoadAssetAtPath<CardSO>(path);
            if (card != null) allCards.Add(card);
        }

        // Set the AllCards list via serialized properties
        var so = new SerializedObject(deck);
        var allCardsProp = so.FindProperty("<AllCards>k__BackingField");
        allCardsProp.arraySize = allCards.Count;
        for (int i = 0; i < allCards.Count; i++)
        {
            allCardsProp.GetArrayElementAtIndex(i).objectReferenceValue = allCards[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(deck);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CardDeckSetup] Setup MainDeck with {allCards.Count} cards.");
    }
}
