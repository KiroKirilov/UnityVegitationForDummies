# Vegetation System - Setup & Usage Guide

## Prerequisites

- **Unity 2022.3+** (tested on 2022.3 LTS)
- **Universal Render Pipeline (URP)** - the shaders are URP-only
- **Physics colliders on terrain/surfaces** - the brush uses `Physics.Raycast` to place vegetation

## Project Setup

### 1. Import the Folder

Copy the entire `VegetationSystem/` folder into your Unity project's `Assets/` directory. Unity will compile the scripts automatically.

### 2. Create Materials

You need two types of materials:

**Scene Material (for brush-spawned GameObjects):**
- Create a material using the `DaisyParty/VegetationWind` shader
- Assign your grass/flower albedo texture to `Base Map`
- Set `Alpha Cutoff` (typically 0.5 for alpha-tested foliage)
- Adjust wind settings to taste (Speed, Strength, Direction, Noise Scale, Frequency)

**Indirect Material (for GPU-rendered baked instances):**
- Create a material using the `DaisyParty/VegetationWindIndirect` shader
- Same property setup as the scene material
- You may want separate indirect materials for grass vs flowers if they use different textures
- The baker will ask for these when baking

### 3. Create Vegetation Prefabs

For each grass/flower variant:
1. Create a GameObject with a `MeshFilter` and `MeshRenderer`
2. Assign your grass/flower mesh and the **scene material** (VegetationWind shader)
3. Save as a prefab
4. The prefab **name** is used as the mesh type identifier throughout the system - keep names unique and descriptive (e.g. `Grass3D_a_01`, `Daisy_small`)

### 4. Create a Vegetation Palette

1. Right-click in Project window > `Create > DaisyParty > Vegetation Palette`
2. Add your grass prefabs to the **Grass Prefabs** list
3. Add flower prefabs to the **Flower Prefabs** list
4. Adjust **Grass Ratio** (0 = all flowers, 1 = all grass)
5. Adjust per-category settings (rotation, scale ranges, tilt)
6. Set **Weight** per prefab to control relative frequency (higher = more common)

### 5. Set Up the GrassRenderer (for baked/indirect rendering)

1. Create an empty GameObject in your scene, name it `GrassRenderer`
2. Add the `GrassRenderer` component (namespace: `DaisyParty.Rendering`)
3. Assign the `GrassCulling` compute shader to the **Culling Shader** field
4. Adjust **Max Render Distance** and **Density Falloff Start** as needed
5. The **Instance Data** and **Mesh Variants** fields will be populated automatically by the baker

## Workflow

The system supports two workflows that can be used together:

### Workflow A: Paint → Bake → Edit

This is the standard workflow for building vegetation from scratch.

**Step 1: Paint with the Vegetation Brush**
1. Open `Tools > Vegetation Brush`
2. Assign your palette
3. Set **Target** to `Scene` (default)
4. Assign or create a **Parent Container** (keeps the hierarchy tidy)
5. Configure brush settings:
   - **Mode**: Circle (click/drag) or Line (click-drag-release)
   - **Brush Size** / **Line Thickness**: area of effect
   - **Density**: instances per stroke
   - **Paint Mask**: which layers to raycast against (set to your terrain layer)
   - **Only Up Faces** + **Max Slope Angle**: prevent painting on cliffs
6. Click **Enable Paint Mode** and paint in the Scene View
   - `LMB` = paint / set line start
   - `LMB + Drag` = continuous paint / preview line
   - `Alt + LMB` = orbit camera (always works)

**Step 2: Bake to GPU-Indirect Data**
1. Open `Tools > Grass Data Baker`
2. Assign:
   - **Grass Container**: the parent Transform holding all your painted GameObjects
   - **Grass Renderer**: the GrassRenderer in your scene
   - **Existing Data**: leave empty to create new, or assign existing to overwrite
   - **Grass Indirect**: your indirect material for grass
   - **Flower Indirect**: your indirect material for flowers
3. Click **Bake Grass Data**
4. Choose a save location for the `.asset` file
5. The baker will:
   - Extract all transforms from child GameObjects
   - Group them by mesh type (matched by prefab name)
   - Create a `GrassInstanceData` asset
   - Deactivate the original GameObjects
   - Configure the GrassRenderer automatically

**Step 3: Edit Baked Instances (Play Mode)**
1. Open `Tools > Grass Instance Editor`
2. Assign the `GrassInstanceData` asset
3. Enter **Play Mode**
4. Click **Enable Edit Mode**
5. Use Scene View to select and transform instances:
   - `LMB` = select instance
   - `Ctrl + LMB` = add/remove from selection
   - `W` / `E` / `R` = Move / Rotate / Scale
   - `Delete` = delete selected
   - `Escape` = deselect all
6. Changes persist after exiting Play Mode

**Unbaking (going back to GameObjects):**
- In the Grass Data Baker, click **Unbake** to re-enable the original GameObjects for brush editing

### Workflow B: Paint Directly to Baked Data (Play Mode)

Once you have an initial bake with established mesh types, you can paint new instances directly into the baked data without creating GameObjects.

1. Open `Tools > Vegetation Brush`
2. Assign your palette
3. Set **Target** to `BakedData`
4. Assign your existing `GrassInstanceData` asset
5. Enter **Play Mode**
6. Click **Enable Paint Mode** and paint in the Scene View
7. Instances appear when you release the mouse button (not during drag)
8. `Ctrl + Z` undoes the last stroke
9. Changes persist after exiting Play Mode

**Requirements for Baked Data mode:**
- The palette prefab names must match mesh type names in the baked data
- Any unmatched prefabs are listed in a warning and silently skipped
- A `GrassRenderer` must exist in the scene with the same data asset to see results
- Must be in Play Mode

## File Reference

| File | Purpose |
|------|---------|
| **Runtime** | |
| `GrassInstanceData.cs` | ScriptableObject storing baked instance transforms, mesh types, and bounds |
| `GrassRenderer.cs` | GPU indirect renderer with compute-shader frustum + distance culling |
| `VegetationPalette.cs` | ScriptableObject defining prefab lists with weighted random selection |
| **Editor - Brush** | |
| `VegetationBrushWindow.cs` | Main brush UI window (Tools > Vegetation Brush) |
| `VegetationBrushPainter.cs` | Raycast + spawn/entry-creation logic for both Scene and BakedData modes |
| `VegetationBrushSceneHandler.cs` | Scene View mouse input handling and brush visualization |
| `BakedDataPaintTarget.cs` | Baked data mode: prefab-to-meshtype mapping, entry buffering, GPU refresh |
| `VegetationPaletteEditor.cs` | Custom Inspector for VegetationPalette with reorderable lists |
| **Editor - Instance Editor** | |
| `GrassDataBaker.cs` | Bake tool: GameObjects to GrassInstanceData (Tools > Grass Data Baker) |
| `GrassInstanceEditorWindow.cs` | Instance editor UI (Tools > Grass Instance Editor) |
| `GrassInstancePicker.cs` | Raycasting against baked instances for selection |
| `GrassInstanceSceneHandler.cs` | Scene View input + gizmos for instance editing |
| **Shaders** | |
| `VegetationWind.shader` | URP Lit shader with wind + player interaction (for scene GameObjects) |
| `VegetationWindIndirect.shader` | URP shader reading from StructuredBuffers (for GPU indirect rendering) |
| `GrassCulling.compute` | Compute shader: frustum culling + distance culling with density falloff |

## Performance Notes

- The compute culling shader runs per-frame and efficiently handles tens of thousands of instances
- `RefreshGPUData()` releases and rebuilds ALL GPU buffers - it is called once per edit operation or paint stroke, never per-frame
- **Density Falloff**: instances between `Density Falloff Start` and `Max Render Distance` are probabilistically culled (up to 70% removed at max distance) to reduce overdraw
- When painting to baked data, GPU refresh only happens on mouse-up (stroke end), not during drag
- The `VegetationWind` shader has `DisableBatching = True` which breaks SRP batching - this is intentional for correct object-space wind. For large numbers of scene instances, bake them to use the indirect path instead

## Troubleshooting

**Nothing renders in Play Mode:**
- Check that `GrassRenderer` has a valid `Instance Data` asset with instances
- Check that `Culling Shader` is assigned (the `GrassCulling` compute shader)
- Check that `Mesh Variants` array has valid meshes and materials
- Verify `Max Render Distance` is large enough

**Brush doesn't paint:**
- Ensure your terrain/surface has physics colliders
- Check the **Paint Mask** includes your terrain's layer
- Verify the palette has valid prefab entries

**Baked Data mode skips all prefabs:**
- Prefab names in the palette must exactly match `MeshTypeInfo.name` values in the baked data
- These names come from the original prefab source names used during baking
- Check the warning in the brush window listing unmatched names

**GPU crash / DXGI_ERROR_DEVICE_HUNG (D3D12):**
- Never use `VegetationWind.shader` (which has `DisableBatching = True`) with `Graphics.DrawMeshInstanced` on D3D12 - use the indirect path instead
- If you have extremely large instance counts, reduce `Max Render Distance`
