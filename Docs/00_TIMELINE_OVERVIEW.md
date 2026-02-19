# Vegetation System - AI-Assisted Development Timeline

## Project Context
The vegetation system was built as part of **DaisyParty**, a 3D platformer game developed in Unity (URP). The system was created iteratively across 17 Claude Code sessions over 8 days (Jan 31 - Feb 7, 2026), evolving from simple scene decoration into a full GPU-accelerated rendering pipeline with custom editor tools.

## Key Files (Final System)
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

---

## Development Phases

### Phase 1: Scene Environment Setup (Jan 31)
**Sessions:** 01 (`2dae9824`)
**What happened:** Created the intro scene environment from scratch - a bright sunny field with flowers, grass, mushrooms, trees, and fences. Used Unity MCP to place hundreds of objects from imported asset packs (Kenney, Polyperfect, LowPoly). This session created the initial dense vegetation that later needed optimization.

**Key prompts:**
- "I want you to create the environment/background/playground... bright sunny day, happy vibes, greenery, flowers"
- "add grass and bushes and the like its still 95%+ of just flat one tone green"
- "DONT LIMIT TO JUST THE LOW POLY PACK THERES SO MUCH BETTER ASSETS IN THE PROJECT USE AT LEAST 8 PACKS"

**AI techniques used:** MCP (Model Context Protocol) for direct Unity Editor manipulation, asset exploration, scene hierarchy management

---

### Phase 2: Vegetation Brush Tool (Feb 3)
**Sessions:** 02 (`2cc56583`)
**What happened:** Designed and built a custom vegetation brush editor tool for painting grass and flowers onto meshes in the Unity Scene view. Created the VegetationPalette ScriptableObject system with separate grass/flower collections and weighted random selection.

**Key prompts:**
- "plan about implementing a simple vegetation brush -> it should support grass prefabs as inputs, also flowers... configurable density and radius"
- "Rework the SO to be more user friendly - I want separate grass/flowers prefabs on purpose. I want global controls for grass/flowers instead of having to manually setup each one"

**AI techniques used:** Editor tool design, ScriptableObject architecture, custom Inspector UI

---

### Phase 3: Wind Shader System (Feb 5)
**Sessions:** 03 (`db4ecbfb`)
**What happened:** Added wind animation to all vegetation. Started with online research on efficient wind techniques, then built an HLSL vertex shader (VegetationWind) that applies wind displacement. Iterated on the shader to fix issues where grass was "sliding on the ground" - changed from translation-based to rotation-based bending where only upper portions of blades move.

**Key prompts:**
- "I have a few hundred grass objects + flowers... perform deep online research on efficient ways to add 'wind' to it, so it isnt so static and feels more alive"
- "The vegetation material isnt even the one being used, read the three grass palettes... to see what prefabs are being used"
- "make it more like real wind where the blades dont just sway like on a sine wave, but bend... the bottom should stay put"

**AI techniques used:** Online research, HLSL shader development, URP vertex shader manipulation

---

### Phase 3b: Falling Leaves (Feb 5)
**Sessions:** 03b (`558e071f`)
**What happened:** Redesigned the falling leaves particle system for trees. Replaced flat billboard particles with mesh-based 3D particles with proper tumbling rotation on all axes. Added wind-driven movement and emission zone shaping.

**Key prompts:**
- "Implement the following plan: Falling Leaves - Complete Redesign" (detailed plan with mesh particles, 3D rotation, wind velocity)
- "theres still a wavyness to the movement that is extremely predictable and synchronized and looks awful"
- "Can i control the emission zone shape? right now its a circle but my tree is an oval"

**AI techniques used:** Particle system design, 3D physics simulation concepts, iterative visual refinement

---

### Phase 4: Player Grass Interaction (Feb 5, continued from Phase 3)
**Sessions:** 03 (`db4ecbfb`, later prompts)
**What happened:** Extended the wind shader to include player interaction - grass rustles/bends when the player walks through it, with directionally-correct bending based on player position relative to each blade.

**Key prompts:**
- "plan another expansion of the feature - I want the grass to be ruffled when the player passes through it. It should make sense direction wise based on where the player is and how the blade itself is positioned relatively"

**AI techniques used:** Player-world interaction via shader parameters, directional vector math in HLSL

---

### Phase 5: Vegetation Brush Extensions (Feb 6)
**Sessions:** 04 (`a0b51bae`), 05 (`0301e44a`)
**What happened:** Extended the vegetation brush with OnlyUpFace filtering (prevents painting on platform sides) and layer mask support. Also planned grass-rustle sound effects.

**Key prompts:**
- "make it so it has two extra optional settings -> OnlyUpFace... and layer mask"
- "plan an expansion of the grass-player rustle interaction - I want to have the ability to setup a sound to play when that happens"

**AI techniques used:** Raycast filtering, layer mask systems, audio design research

---

### Phase 6: Performance Crisis & GPU Indirect Rendering (Feb 6)
**Sessions:** 06 (`83ee6997`), 07 (`a2165f23`), 08 (`0958d99e`)
**What happened:** The dense vegetation caused severe performance issues (~11,431 GameObjects = 11,431 draw calls). After failed Phase 1 optimizations (removing shadows looked terrible, GPU Instancing incompatible with LODGroups), designed and implemented a complete GPU indirect rendering pipeline:
- `GrassDataBaker`: Bakes all grass GameObjects into a single ScriptableObject
- `GrassRenderer`: Renders all grass via `Graphics.RenderMeshIndirect` (~7 draw calls instead of 11,431)
- `GrassCulling.compute`: GPU-side frustum + distance culling with density falloff
- `VegetationWindIndirect.shader`: Reads transforms from StructuredBuffers

**Key prompts:**
- "I've used the VegetationBrush and have a lot of grass... starting to lag. research and plan optimizations to make this less laggy while still keeping the dense grass look"
- "it looks like pure dogshit with no shadows and the other change seemed to do nothing, we get like 2M verts and 700k tris with 20k shadow casters"
- "Implement the following plan: Grass System Optimization - GPU Instanced Indirect Rendering" (detailed 8-file plan)
- "The grass isnt receiving light from the sun, plan a way to introduce that"

**AI techniques used:** Performance profiling analysis, GPU compute shader development, indirect rendering pipeline design, online research for URP lighting in custom shaders

---

### Phase 7: Bake Quality & Debugging (Feb 6)
**Sessions:** 09 (`ea1d921e`), 10 (`c2119a8f`)
**What happened:** Debugged issues with the baked indirect rendering - some instances were placed wrong, flowers lost their colors, not all baked data was rendering. Also increased culling range and explained the rendering technique.

**Key prompts:**
- "take a very deep look at the current grass baker and renderer and the shaders... when i baked and setup the renderer - all data is baked (~11k items) but when rendering not all of it is there and it's placed wrong and some flowers lose shape, color"
- "What is the technique we used for the grass? other than the baking what do the shaders/renderer do"
- "Can we increase the grass culling range a bit, i can see some grass disappear behind the player"

**AI techniques used:** Debugging GPU rendering pipelines, StructuredBuffer data analysis, culling parameter tuning

---

### Phase 8: Instance Editor Tool (Feb 7)
**Sessions:** 11 (`60b8902d`), 12 (`2591a346`), 13 (`2cf613d2`), 14 (`9361390b`)
**What happened:** Built a custom editor tool to select, move, rotate, scale, and delete individual baked grass instances. This was a multi-session effort that involved:
1. Initial design with play-mode rendering + scene view interaction
2. Fixing D3D12 crashes caused by `Graphics.DrawMeshInstanced` during IMGUI
3. Complete redesign to work in play mode with ghost GameObjects for manipulation
4. Improving click precision for dense grass areas (mesh-level raycasting vs bounding box)

**Key prompts:**
- "come up with an easy, visual way for me to edit/remove single assets from the bake... click to select and then move/rotate/scale and delete"
- "look at the grass rendering... the shaders, baker, renderer. And look at the custom editor... The problem is when I enable edit mode Unity crashes" (with D3D12 crash logs)
- "plan a complete redesign - I want it to work in play mode when the grass is already being rendered"
- "when a grass is selected -> create a ghost gameobject copy of it that can be rotated/moved/scaled and on confirm -> write back to the SO"
- "in dense areas of grass its almost impossible to pick the one i want. Is it possible to have the clickable area be closer to the actual mesh instead of just a bounding box"

**AI techniques used:** Custom Unity editor tools, D3D12 crash diagnosis, play-mode editor integration, mesh-level raycasting, GPU buffer synchronization

---

### Phase 9: Brush-to-Bake Pipeline & System Export (Feb 7)
**Sessions:** 15 (`e91526de`)
**What happened:** Connected the vegetation brush to paint directly into baked data ScriptableObjects (instead of spawning GameObjects). Also exported the entire vegetation system as a standalone package for reuse in other projects.

**Key prompts:**
- "I want the vegetation brush to be usable on the baked data directly... in play mode I can turn on the brush and it can have a toggle/setup to put stuff in a baked data SO instead of on the scene directly"
- "create a new folder and put a COPY of the entire veg system so I can use it in a different project"
- "add an md file detailing step by step setup and usage and requirements/prerequisites"

**AI techniques used:** Pipeline integration, GPU buffer hot-reload, system packaging/documentation

---

### Phase 10: Grass Rustle Audio (Feb 7)
**Sessions:** 16 (`1ca36dda`)
**What happened:** Investigated adding sound effects for grass rustling when the player walks through. Researched best practices for environmental audio (single clips vs. continuous loops vs. layered approach).

**Key prompts:**
- "investigate our grass system and how the player interacts with it causing it to rustle. The goal is to add a sound effect... should it be one rustle clip, many different clips or one continuous loop"

**AI techniques used:** Audio design research, environmental sound implementation patterns

---

## Summary Statistics
- **Total Claude Code sessions analyzed:** 61
- **Sessions with vegetation content:** 17
- **Date range:** January 31 - February 7, 2026 (8 days)
- **Development phases:** 10 major phases
- **Key evolution:** Simple placed objects -> Custom brush tool -> Wind shaders -> Player interaction -> GPU indirect rendering -> Custom editor tools -> Standalone system

## Individual Session Files
Each session's full prompts are available in the numbered files in this folder (01-16 + 03b).
