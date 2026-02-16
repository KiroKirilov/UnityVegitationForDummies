# Instructions

READ AND FOLLOW THESE INSTRUCTIONS LINE BY LINE BEFORE DOING ANYTHING.

NUMBER ONE RULE IS DONT BE LAZY - THINK THROUGH PROBLEMS, ANALYZE, INSPECT CODE EVEN IF IT SEEMS OBVIOUS OR THE CODE LOOKS IRRELEVANT.

USE AS MANY RESOURCES AS YOU PLEASE. Research, look for info, look things up online if needed, analyze everything available before making any changes.

ACKNOWLEDGE THESE INSTRUCTIONS AT THE START OF YOUR THOUGHT PROCESS EVERY SINGLE TIME BY SAYING "ACKNOWLEDGED INSTRUCTIONS".

NEVER edit Unity scene files (.unity) unless EXPLICITLY told to. Only tell the user what needs changing there.

> **See also: [Lessons Learned](lessons-learned.md)** - Hard-won debugging insights and gotchas specific to this project.

## Core Behavior: Knowledge-Seeking First

Before ANY implementation, act as a curious investigator. There are interconnected systems in this Unity C# project (editor tools, GPU rendering, compute shaders, wind simulation), so you really need to build up context and a mental model of how things work before making changes. Doing the understanding upfront saves time and prevents mistakes later:

1. **Understand the full system** - Don't just read one file. Trace the entire data flow:
   - What calls this code?
   - What does this code call?
   - What data flows through?
   - What other systems touch this?

2. **Map the feature's structure** - Before changing anything:
   - Read ALL related files, not just the obvious one
   - Understand how components interact
   - Identify shared state, events, dependencies

3. **Consider cross-feature effects** - Ask yourself:
   - What else uses this code/data?
   - Could this change break something unrelated?
   - Are there similar patterns elsewhere that should stay consistent?

4. **Only then implement** - After you truly understand:
   - How the existing feature works end-to-end
   - How the new request fits into that structure
   - What the minimal change is to achieve the goal

## Implementation Quality: No Shortcuts

When implementing, **do it properly**:

1. **Use Unity's built-in features** - Don't reinvent the wheel. If you can't do something, let the user know what needs to be set up in the editor:
   - Use the Animation system properly (state machines, blend trees, events)
   - Use Physics layers/masks instead of manual tag checks
   - Use Unity Events, UnityAction, or C# events for decoupling
   - Use ScriptableObjects for shared data/configuration
   - Use URP features (shader graph, render features) where appropriate

2. **Separation of Concerns** - Each component should do ONE thing:
   - Don't combine unrelated logic in one script
   - Create dedicated components with clear responsibilities
   - Use composition over inheritance where possible

3. **No duct-tape solutions**:
   - Don't hardcode magic numbers - use serialized fields or constants
   - Don't use `GameObject.Find()` or `GetComponent()` in Update loops
   - Don't bypass systems with workarounds - fix the actual architecture
   - Don't copy-paste code - extract shared functionality

4. **No comment spam**:
   - NO `<summary>` XML doc comments unless it's a public API
   - NO verbose comments explaining obvious code
   - NO section header comments like `// === SECTION NAME ===`
   - Make code readable through good naming, not comments
   - If you need a comment to explain what code does, the code is bad - refactor it
   - Only comment WHY something non-obvious is done, never WHAT

5. **Think about future maintainability**:
   - Will this be easy to modify later?
   - Will this be easy to debug?
   - Will this work if requirements change slightly?
   - Is this the way an experienced Unity developer would do it?

6. **Leverage the engine**:
   - Coroutines, async/await for timing
   - Animator parameters and state machine behaviors
   - Physics callbacks (OnCollision, OnTrigger) over manual distance checks
   - ComputeShader dispatches for GPU work
   - GraphicsBuffer and indirect rendering for mass instancing
   - Object pooling for frequently spawned objects

**The goal is clean, maintainable, engine-idiomatic code** - not just "code that works right now."

## CRITICAL: Before Making Any Changes

1. **READ THE ACTUAL CODE FILES** - Don't assume. Read the relevant scripts.
2. When debugging, trace the FULL pipeline - if something isn't working, check ALL components involved, not just the obvious ones.
3. Don't just look at the file mentioned - look at what it CALLS and what CALLS it.

## Unity Project Context

This is a **Unity 6 C# project** using **URP (Universal Render Pipeline)**. You have access to:
- **Scene files** (`.unity`) - Examine hierarchy, component setups, references between objects
- **Prefabs** - Reusable object templates with preset configurations
- **Assets** - Materials, animations, ScriptableObjects, shaders
- **Project Settings** - Physics, input, layers, tags

When troubleshooting, don't just look at code - **check the scene setup**:
- Are references assigned in the Inspector?
- What's the hierarchy structure?
- Are layers/tags configured correctly?
- What colliders/rigidbodies are involved?

## Project Dependencies

Check `VegitationSystemDemo.slnx`, `Packages/manifest.json`, and `.csproj` files to understand available packages. Key dependencies:

- **URP 17.3.0** - Universal Render Pipeline (mandatory for all shaders)
- **Input System 1.17.0** - New input system
- **AI Navigation 2.0.9** - NavMesh pathfinding
- **Timeline 1.8.9** - Cinematic sequences

Always leverage existing packages rather than reinventing functionality.

## Project Structure

```
VegitationSystemDemo/Assets/
  Scripts/
    Runtime/           # Play-mode scripts
      GrassRenderer.cs       - GPU indirect renderer with compute culling
      GrassInstanceData.cs   - ScriptableObject for baked vegetation data
      WindController.cs      - Global wind + player interaction
      VegetationPalette.cs   - Brush palette with weighted prefab selection
    Editor/            # Editor-only scripts
      VegetationBrushWindow.cs       - Main brush tool UI
      VegetationBrushPainter.cs      - Raycast + spawn logic
      VegetationBrushSceneHandler.cs - Scene View input handling
      BakedDataPaintTarget.cs        - Paint-to-GPU-data target
      GrassDataBaker.cs              - GameObject-to-asset baking
      GrassInstanceEditorWindow.cs   - Play mode instance editing
      GrassInstancePicker.cs         - Ray-mesh intersection picking
      GrassInstanceSceneHandler.cs   - Instance selection/manipulation
      VegetationPaletteEditor.cs     - Custom Inspector for palettes
  Shaders/
    VegetationWind.shader        - Lit shader for scene GameObjects
    VegetationWindIndirect.shader - GPU indirect rendering shader
    GrassCulling.compute         - Frustum + distance + density culling
  Settings/            # URP pipeline and renderer configurations
  SETUP.md             # Comprehensive setup and usage guide
```

## Architecture Overview

**Two-Workflow System:**
1. Paint as GameObjects -> Bake to GrassInstanceData asset -> Edit in play mode
2. Paint directly into baked data buffer (real-time GPU refresh)

**Runtime Pipeline:** GrassRenderer reads GrassInstanceData, uploads to GPU buffers, dispatches GrassCulling compute shader each frame, then issues indirect draw calls per mesh type.

**Wind Pipeline:** WindController sets global shader properties each frame. Both shaders (scene + indirect) read these globals for vertex displacement.

## Debugging Approach

1. Read the code FIRST
2. Understand the data flow
3. Check ALL components in the chain
4. Don't add logging/debug code unless asked - fix the actual problem

## Reset Knowledge Mode

When asked to "reset knowledge" or "start fresh":
- Treat the request as if it's the FIRST interaction
- Do NOT rely on any assumptions from previous conversation
- Go deep into the codebase from scratch
- Build a complete mental model before suggesting anything

## Troubleshooting Mode

When asked to debug or troubleshoot:

### 1. Logic Understanding First
- Map the ENTIRE code path involved in the problem
- Identify every component, method, and data transformation
- Don't stop at the obvious file - trace upstream AND downstream

### 2. Define What Data Matters
- What variables/state determine the outcome?
- What conditions trigger the problem vs success?
- What timing or sequence matters?

### 3. Design Targeted Logging (if needed)
- Structured format - CSV or clearly delimited data
- State changes - Log WHEN things change, not continuous spam
- Decision logging - Log WHY something happened, not just THAT it happened
  - Good: `"CULL: Distance 120.5 > MaxRender 100.0, skipping instance 42"`
  - Bad: `"Instance culled"`

## Code Style

- Keep changes minimal and focused
- Don't add unnecessary complexity
- Test assumptions by reading the actual implementation
- Match existing conventions in the codebase (namespace `DaisyParty.Rendering`, etc.)
