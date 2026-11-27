# Easy planet surface sources

- **Albedo.jpg**: Terrain photo assigned as `AlbedoMap`. This is the hills and valleys.
- **GroundMacro.jpg**: Unused soft blur of that photo. Not assigned.
- **DetailAlbedo.jpg** / **DetailNormal.png**: Seamless dust, tiled 32 times
  across the planet. The dust normal is kept weak so it does not replace the terrain.
- **AlbedoFromAfterRef.jpg**: Archive of the earlier After reference crop.
- **AlbedoPlainsRef.jpg**: Source photo from `planet-look-colony-plains.jpg`.
- **Heightmap.png**: Stand-in DEM (basin + crater rim); opposite edges averaged for wrap.
  Replace with NASA DEM when available.
- **NormalFromDEM.png**: Assigned normal for the terrain photo.
- **Occlusion.png**: Unused.

Hills and valleys come from the terrain photo and its normal. The repeating map is only the dust on top.

**Runtime import:** Source files can stay large on disk; Unity caps **Albedo** and **NormalFromDEM** at **2048** on import. Planet maps use **mipmap streaming** (Quality settings + per-texture importer flag). Reference JPGs in this folder are not assigned on `Planet 1 - Easy`.
