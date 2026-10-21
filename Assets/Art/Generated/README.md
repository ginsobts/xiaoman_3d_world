# Generated Fairytale Art

Created by `Assets/Editor/GeneratedArtImporter.cs` from the six generated JPG sheets. Re-run with **Tools > Generated Art > Import and Process**.

## Contents
- `Source/`: untouched copies of all six original JPG files.
- `Environment/`: 16 named 256x256 PNG texture cells: Grass, Dirt, Cobblestone, LavenderStone, WoodPlanks, RoofTiles, CreamPlaster, Water, Bark, Leaves, Wildflowers, Clouds, Sand, CaveStone, Crystal, Parchment. Imported as Default textures with Repeat wrapping.
- `VFX/`: `VFX_01.png` through `VFX_16.png`, 256x256 Sprite PNGs. Black is transparent and alpha follows luminance.
- `Characters/`: 6 full-body and 6 portrait Sprite PNG crops. Edge-connected paper backgrounds were made transparent.
- `UI/`: dialogue/quest frames, E badge, cursor, and common icon Sprite PNG crops. Bright green chroma was removed.
- `Sky/FairytaleSkyPanorama.jpg`: panorama copy imported as a clamped Default texture.
- `Reference/PropsConceptAtlas.jpg`: intact props concept sheet imported as a clamped Default texture.

## Notes and limitations
- Character and UI crops are derived from a single AI-generated concept sheet, so edges can retain slight paper/chroma fringing and are not rigged or animation-ready.
- The props source has no regular grid and remains an intact reference atlas rather than risking destructive automatic crops.
- The sky image is 16:9 artwork, not a seamless 360-degree equirectangular panorama.
- VFX RGB is preserved while alpha uses perceived luminance; additive materials may still give the best result.
