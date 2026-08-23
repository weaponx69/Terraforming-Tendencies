import os

file_path = "Assets/Scripts/Environment/HexGridManager.cs"
with open(file_path, "r") as f:
    content = f.read()

# 1. Fix NameToLayer in field initializer
content = content.replace(
    'private LayerMask shroudLayer = LayerMask.NameToLayer("Shroud");',
    'private LayerMask shroudLayer;'
)

# 2. Fix Shroud layer to TransparentFX in CreateHexTile
if 'hexGO.layer = LayerMask.NameToLayer("Shroud");' in content:
    content = content.replace(
        'hexGO.layer = LayerMask.NameToLayer("Shroud");',
        'hexGO.layer = LayerMask.NameToLayer("TransparentFX"); // TransparentFX is ignored by PlanetGenerator NavMesh bake!'
    )
    
# 3. Fix the prefab instantiation layer!
prefab_instantiation = """hexGO = Instantiate(shroudTilePrefab, position, Quaternion.identity, gridRoot);"""
prefab_fixed = """hexGO = Instantiate(shroudTilePrefab, position, Quaternion.identity, gridRoot);
                hexGO.layer = LayerMask.NameToLayer("TransparentFX");"""
content = content.replace(prefab_instantiation, prefab_fixed)

with open(file_path, "w") as f:
    f.write(content)

print("Applied fix.py")
