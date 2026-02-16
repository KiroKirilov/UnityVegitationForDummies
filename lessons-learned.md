# Lessons Learned

Hard-won debugging insights and gotchas for this project. Updated as issues are discovered.

## Unity / URP

- Scene file (.unity) is 86MB+ - never edit directly, always tell user what to change in Inspector
- URP is mandatory - all shaders depend on it. Don't write Built-in RP shader code
- Project targets Unity 6 (6000.3.2f1) - APIs may differ from older Unity versions

## GPU Rendering Pipeline

- GrassRenderer uses indirect rendering with compute shader culling
- Compute shader dispatches 128 threads per group - buffer sizes must account for this
- After modifying GrassInstanceData, call `GrassRenderer.RefreshGPUData()` to push changes to GPU
- Density falloff culling is probabilistic (hash-based) - don't expect deterministic results at distance

## Editor Tools

- Brush painting has two targets: Scene (GameObjects) and BakedData (GPU buffer)
- BakedDataPaintTarget validates prefab-to-meshtype mapping - new prefabs need matching mesh types
- Instance editing only works in Play Mode
- Undo integration exists - don't bypass it with direct data manipulation

## Shaders

- VegetationWind.shader has `DisableBatching = True` - this is intentional for correct object-space wind
- Both shaders read global wind properties set by WindController - they must stay in sync
- Player interaction uses world-space position/velocity passed as globals
