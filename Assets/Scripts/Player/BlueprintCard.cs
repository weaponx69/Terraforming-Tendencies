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

        public abstract void Apply();
    }

    [CreateAssetMenu(fileName = "Unlock Building Card", menuName = "Blueprints/Unlock Building Card")]
    public class UnlockBuildingCardSO : BlueprintCardSO
    {
        public BuildingSO buildingToUnlock;

        public override void Apply()
        {
            if (buildingToUnlock != null)
            {
                BlueprintDraftManager.UnlockBuilding(buildingToUnlock.Name);
                Debug.Log($"[Blueprint] Unlocked building: {buildingToUnlock.Name}");
            }
        }
    }

    [CreateAssetMenu(fileName = "Resource Shipment Card", menuName = "Blueprints/Resource Shipment Card")]
    public class ResourceShipmentCardSO : BlueprintCardSO
    {
        public int materialsAmount = 0;
        public int biomassAmount = 0;
        public int oxygenAmount = 0;

        public override void Apply()
        {
            if (materialsAmount > 0)
            {
                int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                Supplies.Materials[Owner.Player1] = cur + materialsAmount;
                Supplies.RaiseMaterialsChanged(Owner.Player1, cur + materialsAmount);
                Debug.Log($"[Blueprint] Materials shipment delivered: +{materialsAmount}");
            }
            if (biomassAmount > 0)
            {
                int cur = Supplies.Biomass.TryGetValue(Owner.Player1, out int b) ? b : 0;
                Supplies.UpdateBiomass(Owner.Player1, cur + biomassAmount);
                Debug.Log($"[Blueprint] Biomass shipment delivered: +{biomassAmount}");
            }
            if (oxygenAmount > 0)
            {
                float cur = Supplies.Oxygen.TryGetValue(Owner.Player1, out float o) ? o : 0f;
                Supplies.UpdateOxygen(Owner.Player1, cur + oxygenAmount);
                Debug.Log($"[Blueprint] Oxygen shipment delivered: +{oxygenAmount}");
            }
        }
    }

    [CreateAssetMenu(fileName = "Spawn Unit Card", menuName = "Blueprints/Spawn Unit Card")]
    public class SpawnUnitCardSO : BlueprintCardSO
    {
        public GameObject unitPrefab;

        public override void Apply()
        {
            if (unitPrefab == null) return;

            // Find command post to spawn at
            var bldgs = UnityEngine.Object.FindObjectsByType<BaseBuilding>(FindObjectsInactive.Exclude);
            BaseBuilding spawnBase = null;
            foreach (var b in bldgs)
            {
                if (b != null && b.Owner == Owner.Player1 && b.BuildingSO != null && b.BuildingSO.Name.Contains("Command"))
                {
                    spawnBase = b;
                    break;
                }
            }

            Vector3 spawnPos = Vector3.zero;
            if (spawnBase != null)
            {
                spawnPos = spawnBase.transform.position + Vector3.forward * 4f;
            }
            else
            {
                // Fallback to active camera projection center or origin
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        spawnPos = hit.point;
                    }
                }
            }

            GameObject spawnedUnit = UnityEngine.Object.Instantiate(unitPrefab, spawnPos, Quaternion.identity);
            
            // Set Owner to Player1
            var abstractUnit = spawnedUnit.GetComponent<AbstractUnit>();
            if (abstractUnit != null)
            {
                abstractUnit.Owner = Owner.Player1;
            }
            
            Debug.Log($"[Blueprint] Spawned free unit: {unitPrefab.name} at {spawnPos}");
        }
    }

    [CreateAssetMenu(fileName = "Passive Buff Card", menuName = "Blueprints/Passive Buff Card")]
    public class PassiveBuffCardSO : BlueprintCardSO
    {
        public enum BuffType { GatherSpeed, PowerGeneration }
        public BuffType buffType;
        public float multiplier = 1.2f;

        public override void Apply()
        {
            if (buffType == BuffType.GatherSpeed)
            {
                BlueprintDraftManager.GatherSpeedMultiplier *= multiplier;
                Debug.Log($"[Blueprint] Active gather speed multiplier is now: {BlueprintDraftManager.GatherSpeedMultiplier}");
            }
            else if (buffType == BuffType.PowerGeneration)
            {
                BlueprintDraftManager.PowerGenMultiplier *= multiplier;
                Debug.Log($"[Blueprint] Active power generation multiplier is now: {BlueprintDraftManager.PowerGenMultiplier}");
            }
        }
    }
}