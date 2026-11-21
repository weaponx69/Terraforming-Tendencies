# Planet Surface DEM + Photo Albedo Implementation Plan

> **For agentic workers:** implement this plan task-by-task, in order, checking off each step as it lands. Steps use checkbox (`- [ ]`) syntax for tracking. Dispatch a fresh subagent per task where the tasks are independent (see dispatching-parallel-agents), otherwise execute inline with a review checkpoint after each task.

**Goal:** Make Easy/Prologue ground look like the **After** reference — curated Mars DEM heights + photo albedo, colony flats — while keeping `SampleGroundY` / placement grounding.

**Architecture:** Extend `PlanetConfig` with heightmap/albedo textures. Refactor `PlanetTerrainBuilder` so `SampleWorldY` reads the DEM (bilinear) with landing flatten; Perlin remains fallback only when `Heightmap` is null. `PlanetGenerator` builds mesh Y from that sampler, uses world UVs, and applies URP Lit with the albedo map. Ship textures under `Assets/Art/Planets/Easy/` (visible Unity folders).

**Tech Stack:** Unity 6000.4, URP Lit, existing `PlanetGenerator` mesh + `ColonyTileGrid` grounding, EditMode tests under `Assets/Scripts/Tests/`, Unity CLI for Play verification.

**Spec:** [`docs/specs/2026-11-21-planet-surface-dem-design.md`](../specs/2026-11-21-planet-surface-dem-design.md)  
**Visual target:** `Assets/Art/References/PlanetSurface/planet-before-after-dem.jpg` (After)

---

## File map

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Environment/PlanetConfig.cs` | Authoring fields: Heightmap, HeightAmplitude, AlbedoMap, NormalMap |
| `Assets/Scripts/Environment/PlanetTerrainBuilder.cs` | Shared height sampling (DEM or Perlin fallback) + landing flatten |
| `Assets/Scripts/Environment/PlanetGenerator.cs` | Mesh verts/UVs/material from config; call shared sampler |
| `Assets/Scripts/Tests/PlanetHeightFieldTests.cs` | EditMode tests for DEM sample + flatten + fallback |
| `Assets/Art/Planets/Easy/` | Heightmap + albedo (+ SOURCES.md) |
| `Assets/Resources/Planet 1 - Easy.asset` + `Assets/Settings/Planet 1 - Easy.asset` | Assign texture refs + amplitude |
| `PROJECT_KNOWLEDGE.md` | Point at shipped DEM surface behavior |
| `docs/specs/2026-11-21-planet-surface-dem-design.md` | Already approved — do not relocate under `.summer` |

---

### Task 1: PlanetConfig surface fields

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetConfig.cs`

- [x] **Step 1: Add DEM / photo fields**

Under `[Header("Terrain Generation")]`, after `LandingFlatRadius`, add:

```csharp
[Header("Surface maps (Easy: curated DEM + photo)")]
[Tooltip("Grayscale DEM. Bright = high. Null → Perlin fallback.")]
public Texture2D Heightmap;
[Tooltip("World-unit vertical span from black (0) to white (1) on Heightmap.")]
public float HeightAmplitude = 12f;
[Tooltip("Photo albedo for the planet mesh.")]
public Texture2D AlbedoMap;
[Tooltip("Optional normal map; may be null in v1.")]
public Texture2D NormalMap;
```

Keep existing `NoiseScale`, `HeightMultiplier`, `TerrainSeed`, `LandingFlatRadius` for fallback / flatten.

- [x] **Step 2: Commit**

```bash
git add Assets/Scripts/Environment/PlanetConfig.cs
git commit -m "feat(planet): add DEM heightmap and albedo fields to PlanetConfig"
```

---

### Task 2: Height sampling from DEM (with tests)

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetTerrainBuilder.cs`
- Create: `Assets/Scripts/Tests/PlanetHeightFieldTests.cs`

- [ ] **Step 1: Write failing EditMode tests**

Create `Assets/Scripts/Tests/PlanetHeightFieldTests.cs`:

```csharp
using GameDevTV.RTS.Environment;
using NUnit.Framework;
using UnityEngine;

public class PlanetHeightFieldTests
{
    Texture2D MakeRampHeightmap()
    {
        // 2x2: left dark (low), right bright (high)
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels(new[]
        {
            Color.black, Color.white,
            Color.black, Color.white
        });
        t.Apply(false, false);
        return t;
    }

    PlanetConfig MakeConfig(Texture2D hm, float amp = 10f, float flatR = 0f)
    {
        var c = ScriptableObject.CreateInstance<PlanetConfig>();
        c.MapWidth = 10;
        c.MapHeight = 10;
        c.Heightmap = hm;
        c.HeightAmplitude = amp;
        c.LandingFlatRadius = flatR;
        c.HeightMultiplier = 5f;
        c.NoiseScale = 20f;
        return c;
    }

    [Test]
    public void SampleWorldY_WithHeightmap_HigherOnBrightSide()
    {
        var hm = MakeRampHeightmap();
        var cfg = MakeConfig(hm, amp: 10f, flatR: 0f);
        var p = PlanetTerrainBuilder.MakeParams(cfg, cellSize: 1f, landingLocal: new Vector3(5f, 0f, 5f));

        float yLow = PlanetTerrainBuilder.SampleWorldY(0.25f, 5f, in p);
        float yHigh = PlanetTerrainBuilder.SampleWorldY(9.75f, 5f, in p);
        Assert.Greater(yHigh, yLow + 2f);
    }

    [Test]
    public void SampleWorldY_LandingFlatten_NearZeroAtCenter()
    {
        var hm = MakeRampHeightmap();
        var cfg = MakeConfig(hm, amp: 20f, flatR: 8f);
        var landing = new Vector3(5f, 0f, 5f);
        var p = PlanetTerrainBuilder.MakeParams(cfg, 1f, landing);
        float y = PlanetTerrainBuilder.SampleWorldY(landing.x, landing.z, in p);
        Assert.That(y, Is.EqualTo(0f).Within(0.05f));
    }

    [Test]
    public void SampleWorldY_NullHeightmap_FallsBackWithoutThrowing()
    {
        var cfg = MakeConfig(null, amp: 10f, flatR: 0f);
        var p = PlanetTerrainBuilder.MakeParams(cfg, 1f, new Vector3(5f, 0f, 5f));
        float y = PlanetTerrainBuilder.SampleWorldY(3f, 4f, in p);
        Assert.That(float.IsNaN(y), Is.False);
    }
}
```

- [ ] **Step 2: Run tests — expect FAIL** (MakeParams / SampleWorldY do not read Heightmap yet)

```bash
export UNITY_PROJECT_PATH="/home/brian/UnityProjects/Terraforming Tendencies"
# Prefer live Editor test runner if exposed; otherwise note FAIL until DEM sampling lands.
# If Pipeline exposes tests:
unity command --help 2>/dev/null | head -5
```

If CLI test invocation is unavailable without spawning a second Editor, compile-check via `unity command eval` after implementation instead; still write the tests first and run them when the connected Editor can execute EditMode tests safely per `.zoo/rules/UnityCLI-Automation.md` (no `unity test` / second Editor).

- [ ] **Step 3: Extend `HeightParams` and `MakeParams` / `SampleWorldY`**

In `PlanetTerrainBuilder.cs`, extend `HeightParams`:

```csharp
public struct HeightParams
{
    public float TerrainSize;
    public float MapWidthWorld;
    public float MapHeightWorld;
    public float Wavelength;
    public float HeightAmp;
    public float HeightMapMax;
    public float Ox;
    public float Oz;
    public float FlatRadius;
    public Vector3 LandingLocal;
    public Texture2D Heightmap;       // null → Perlin
    public float HeightAmplitude;     // DEM black→white span
}
```

Update `MakeParams` to set:

```csharp
MapWidthWorld = Mathf.Max(16f, config.MapWidth * cellSize),
MapHeightWorld = Mathf.Max(16f, config.MapHeight * cellSize),
TerrainSize = Mathf.Max(MapWidthWorld, MapHeightWorld),
Heightmap = config.Heightmap,
HeightAmplitude = Mathf.Max(0.5f, config.HeightAmplitude > 0.1f ? config.HeightAmplitude : config.HeightMultiplier),
// keep Wavelength / HeightAmp / Ox / Oz for Perlin fallback
```

Replace `SampleWorldY` body with:

```csharp
public static float SampleWorldY(float wx, float wz, in HeightParams p)
{
    float y;
    if (p.Heightmap != null)
    {
        float u = Mathf.Clamp01(wx / Mathf.Max(0.001f, p.MapWidthWorld));
        float v = Mathf.Clamp01(wz / Mathf.Max(0.001f, p.MapHeightWorld));
        // Bilinear via Texture2D.GetPixelBilinear (requires readable texture — see Task 4 import settings)
        Color c = p.Heightmap.GetPixelBilinear(u, v);
        float h01 = c.r; // grayscale DEM stored in R (or RGB equal)
        y = (h01 - 0.5f) * p.HeightAmplitude; // mid-gray ≈ 0 so flats sit near pad height
    }
    else
    {
        float h = SampleHills(wx, wz, p.Ox, p.Oz, p.Wavelength, p.HeightAmp);
        y = h * 0.85f;
    }

    float dx = wx - p.LandingLocal.x;
    float dz = wz - p.LandingLocal.z;
    float dist = Mathf.Sqrt(dx * dx + dz * dz);
    if (dist < p.FlatRadius)
    {
        float t = dist / Mathf.Max(0.001f, p.FlatRadius);
        float w = t * t * (3f - 2f * t);
        y = Mathf.Lerp(0f, y, w);
    }
    return y;
}
```

Update the file header comment to point at `docs/specs/2026-11-21-planet-surface-dem-design.md`.

- [ ] **Step 4: Re-run tests — expect PASS** (or compile + eval smoke if EditMode runner blocked)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Environment/PlanetTerrainBuilder.cs Assets/Scripts/Tests/PlanetHeightFieldTests.cs
git commit -m "feat(planet): sample mesh height from DEM heightmap with Perlin fallback"
```

---

### Task 3: Mesh UVs + albedo material in PlanetGenerator

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetGenerator.cs` (vertex UV loop ~391–396 and material setup ~448–456)

- [ ] **Step 1: World-space UVs for photo mapping**

When building vertices, also set UVs from map normalized XZ (not height gradient):

```csharp
float u = width > 0 ? x / (float)width : 0f;
float v = height > 0 ? y / (float)height : 0f;
uvs[i] = new Vector2(u, v);
```

Remove the later loop that overwrites UVs from `InverseLerp(minHeight, maxHeight, …)` (or change that loop to only compute min/max for logging).

- [ ] **Step 2: Apply albedo material when `Config.AlbedoMap` is set**

Replace / extend the material block after mesh assign:

```csharp
MeshRenderer renderer = GetComponent<MeshRenderer>();
Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit");
if (shader != null)
{
    Material mat = new Material(shader);
    if (Config.AlbedoMap != null)
    {
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Config.AlbedoMap);
        else mat.mainTexture = Config.AlbedoMap;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (Config.NormalMap != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", Config.NormalMap);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.05f);
    }
    else
    {
        mat.mainTexture = GenerateHeightGradient();
        mat.SetFloat("_Smoothness", 0.1f);
    }
    renderer.sharedMaterial = mat;
}
```

Ensure Terrain Ghost children still receive `renderer.sharedMaterial`.

- [ ] **Step 3: Keep SampleGroundY on shared sampler**

Confirm `SampleGroundY` still builds `MakeParams` + `SampleWorldY` (already present). No NavMesh Y for placement.

- [ ] **Step 4: Play/compile smoke via connected Editor**

```bash
export UNITY_PROJECT_PATH="/home/brian/UnityProjects/Terraforming Tendencies"
unity command eval --json 'return UnityEditor.EditorApplication.isCompiling;'
```

Expected: not compiling / no CS errors in new code.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Environment/PlanetGenerator.cs
git commit -m "feat(planet): world UVs and photo albedo material on planet mesh"
```

---

### Task 4: Author Easy DEM + albedo assets

**Files:**
- Create: `Assets/Art/Planets/Easy/SOURCES.md`
- Create: `Assets/Art/Planets/Easy/Heightmap.png` (or `.tga`)
- Create: `Assets/Art/Planets/Easy/Albedo.jpg` (or `.png`)
- Modify: texture `.meta` import settings (Readable = true for Heightmap)
- Modify: `Assets/Resources/Planet 1 - Easy.asset`
- Modify: `Assets/Settings/Planet 1 - Easy.asset`

- [ ] **Step 1: Create folder + SOURCES.md**

```markdown
# Easy planet surface sources

- Heightmap: public-domain Mars elevation crop (NASA / USGS MOLA or HRSC derivative). Prefer a **plains-leaning** region.
- Albedo: public-domain Mars surface imagery (dusty ochre regolith), compatible look with
  `Assets/Art/References/PlanetSurface/planet-before-after-dem.jpg` (After).

Record exact download URLs and retrieval date here when importing.
```

- [ ] **Step 2: Obtain / place textures**

Preferred: download a real Mars DEM crop + albedo (agent or human). If network/license tooling blocks, generate **temporary stand-ins** that still beat Perlin blobs:

- Heightmap: 512×512 grayscale PNG with **large mid-gray plains**, soft low ridges near edges (not tileable Perlin mounds). Must look closer to a DEM plateau than `SampleHills`.
- Albedo: high-res dusty orange rock/dirt photo (royalty-free / NASA). Can start from a NASA image saved into `Assets/Art/Planets/Easy/Albedo.jpg`.

Do **not** store these under `Assets/.summer/` or `.summer/`.

- [ ] **Step 3: Heightmap import settings (Readable)**

In Unity (or by editing `.meta`): Texture Type Default, **Read/Write Enabled**, no mip streaming issues, sRGB **off** for heightmap, Filter Mode Bilinear, Wrap Clamp.

Albedo: sRGB on, Read/Write optional off.

- [ ] **Step 4: Assign on Easy planet assets**

On both `Assets/Resources/Planet 1 - Easy.asset` and `Assets/Settings/Planet 1 - Easy.asset`:

- `Heightmap` → Easy height texture  
- `HeightAmplitude` → start `10`–`14` (tune in Play)  
- `AlbedoMap` → Easy albedo  
- `LandingFlatRadius` → keep ~`16`–`28`  
- Leave `NoiseScale`/`HeightMultiplier` as unused fallback

YAML/object refs: assign via Unity Editor Inspector (recommended) so fileIDs are correct — do not hand-write broken GUIDs.

- [ ] **Step 5: Commit**

```bash
git add Assets/Art/Planets/Easy/ "Assets/Resources/Planet 1 - Easy.asset" "Assets/Settings/Planet 1 - Easy.asset"
git commit -m "art(planet): Easy DEM heightmap and photo albedo wired on PlanetConfig"
```

---

### Task 5: Sync optional Terrain child + grounding verify

**Files:**
- Modify: `Assets/Scripts/Environment/PlanetTerrainBuilder.cs` (`BuildOrUpdate` height fill)
- Modify: `PROJECT_KNOWLEDGE.md`

- [ ] **Step 1: BuildOrUpdate uses same SampleWorldY**

Ensure Terrain heightmap loop already calls `SampleWorldY` (it should). If it still bakes Perlin-only paths, switch to `MakeParams` + `SampleWorldY` only. Keep `drawHeightmap = false` and TerrainCollider disabled if MeshCollider is authoritative.

- [ ] **Step 2: Play Mode verify (connected Editor only)**

```bash
export UNITY_PROJECT_PATH="/home/brian/UnityProjects/Terraforming Tendencies"
unity command eval --json 'UnityEditor.EditorApplication.isPlaying=true; return "play";'
# wait for HasGenerated
unity command eval --json '
var pg = UnityEngine.Object.FindFirstObjectByType<GameDevTV.RTS.Environment.PlanetGenerator>();
if (pg == null || !pg.HasGenerated) return new { ok=false };
var mat = pg.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial;
bool hasAlbedo = mat != null && (
  (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) || mat.mainTexture != null);
float y0 = pg.SampleGroundY(pg.StartingAreaCenter);
float yFar = pg.SampleGroundY(pg.StartingAreaCenter + new UnityEngine.Vector3(60f,0f,60f));
int floating=0;
foreach (var b in GameDevTV.RTS.Units.BaseBuilding.ActiveBuildings) {
  if (b==null) continue;
  if (UnityEngine.Mathf.Abs(b.transform.position.y - pg.SampleGroundY(b.transform.position)) > 1.5f) floating++;
}
return new { ok=true, hasAlbedo, y0, yFar, floating, amp=pg.Config.HeightAmplitude };
'
```

Expected: `hasAlbedo=true`, `floating=0`, `|yFar|` can differ from near-zero `y0` (landing flat).

Visual: compare Play view to After reference — photo dust, flats, natural ridges.

- [ ] **Step 3: Update PROJECT_KNOWLEDGE**

State that Easy uses DEM + photo albedo per `docs/specs/2026-11-21-planet-surface-dem-design.md`, grounding via `SampleGroundY`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Environment/PlanetTerrainBuilder.cs PROJECT_KNOWLEDGE.md
git commit -m "docs: note Easy DEM photo surface shipped behavior"
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| Curated DEM height | 2, 4 |
| Photo albedo | 3, 4 |
| Landing flatten + colony flats | 2 (flatten), 4 (plains crop) |
| Keep SampleGroundY / placement | 3 (confirm), 5 (verify) |
| Easy first; Resources/Settings assets | 4 |
| Fallback if maps missing | 2 (null Heightmap) |
| No `.summer` for art | 4 |
| After visual target | 4–5 |

## Placeholder scan

No TBD steps; texture acquisition has an explicit stand-in path if real NASA download is blocked, with SOURCES.md update when replaced.

---

## Execution handoff

Plan complete and saved to `docs/plans/2026-11-21-planet-surface-dem.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — run tasks in this session with checkpoints after each task  

Which approach?
