using UnityEngine;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    public class DiscoveryCardSO : BlueprintCardSO
        {
            public enum DiscoveryType
            {
                IronVein,        // Reveals Iron deposits in explored sectors
                GasPocket,       // Reveals Gas deposits
                RegolithField,   // Reveals Regolith deposits
                MineralSurvey,   // Reveals Minerals deposits
                DeepCoreScan,    // Reveals hidden high-value deposits + bonus
                DebrisField      // Drones can salvage debris
            }
    
            public DiscoveryType discoveryType;
            public int bonusMaterials = 0;
    
            public override string GetCardGoal()
            {
                return discoveryType switch
                {
                    DiscoveryType.IronVein => "MATERIALS",
                    DiscoveryType.GasPocket => "GAS",
                    DiscoveryType.RegolithField => "MATERIALS",
                    DiscoveryType.MineralSurvey => "MATERIALS",
                    DiscoveryType.DeepCoreScan => "MATERIALS",
                    DiscoveryType.DebrisField => "SALVAGE",
                    _ => "DISCOVERY"
                };
            }
    
            public override void Apply()
            {
                string typeName = discoveryType switch
                {
                    DiscoveryType.IronVein => "Iron",
                    DiscoveryType.GasPocket => "Gas",
                    DiscoveryType.RegolithField => "Regolith",
                    DiscoveryType.MineralSurvey => "Minerals",
                    DiscoveryType.DeepCoreScan => null, // Handled specially
                    DiscoveryType.DebrisField => null,   // Handled specially
                    _ => null
                };
    
                if (discoveryType == DiscoveryType.DeepCoreScan)
                {
                    // Reveal ALL resource types in explored sectors
                    var allTypes = Environment.DiscoverySystem.GetResourceTypesInExploredSectors();
                    foreach (var t in allTypes)
                    {
                        Environment.DiscoverySystem.RevealResourceType(t);
                    }
                    Debug.Log("[Blueprint] Deep Core Scan: All resource types in explored sectors revealed!");
                }
                else if (discoveryType == DiscoveryType.DebrisField)
                {
                    // Enable salvage mechanic — flag set for WorkerBrainController to check
                    BlueprintDraftManager.SalvageEnabled = true;
                    Debug.Log("[Blueprint] Debris Field Spotted: Salvage enabled for destroyed buildings.");
                }
                else if (!string.IsNullOrEmpty(typeName))
                {
                    Environment.DiscoverySystem.RevealResourceType(typeName);
                    Debug.Log($"[Blueprint] Discovery: {typeName} deposits now visible in explored sectors!");
                }
    
                // Grant any bonus materials
                if (bonusMaterials > 0)
                {
                    int cur = Supplies.Materials.TryGetValue(Owner.Player1, out int m) ? m : 0;
                    Supplies.Materials[Owner.Player1] = cur + bonusMaterials;
                    Supplies.RaiseMaterialsChanged(Owner.Player1, cur + bonusMaterials);
                    Debug.Log($"[Blueprint] Bonus salvage: +{bonusMaterials} Materials");
                }
            }
    
            public override bool IsGateMet()
            {
                // Discovery cards only appear for types that exist in explored sectors
                if (discoveryType == DiscoveryType.DeepCoreScan || discoveryType == DiscoveryType.DebrisField)
                    return true;
    
                string typeName = discoveryType switch
                {
                    DiscoveryType.IronVein => "Iron",
                    DiscoveryType.GasPocket => "Gas",
                    DiscoveryType.RegolithField => "Regolith",
                    DiscoveryType.MineralSurvey => "Minerals",
                    _ => null
                };
    
                if (string.IsNullOrEmpty(typeName)) return true;
    
                // Only show this card if the resource type exists in explored sectors
                var existingTypes = Environment.DiscoverySystem.GetResourceTypesInExploredSectors();
                return existingTypes.Contains(typeName);
            }
        }
}
