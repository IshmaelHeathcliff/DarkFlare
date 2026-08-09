---
name: unity-2d-sprite-workflow
description: Define, generate, audit, import, bind, and validate production Unity 2D raster assets as one fixed-canvas PNG per Sprite, animation frame, icon, UI state, panel, or world prop. Use for characters, monsters, NPC animation frames, equipment and item icons, UI icons, nine-slice panels, effects, world props, visual-size normalization, Pivot or PPU setup, animation or Prefab binding, USS sprite references, and fixes for clipping, distortion, inconsistent apparent scale, frame drift, off-center subjects, stale references, or missing assets. Enforce single-image production, measurable family contracts, automatic image audits, Sprite Mode Single, and no manual Sprite Editor slicing or production SpriteSheet assembly.
---

# Unity 2D Single-Sprite Workflow

## Hard rules

1. Freeze a measurable family contract before generation.
2. Produce exactly one runtime Sprite, animation frame, icon, UI state, panel, or prop per PNG.
3. Do not generate, assemble, import, or require production SpriteSheets. Do not use `Sprite Mode: Multiple` for new generated assets.
4. Do not depend on manual Sprite Editor slicing, Rect repair, Pivot nudging, or per-asset Transform adjustment.
5. Do not stretch one axis, crop to fit, or scale each subject independently. Reject and regenerate a result that misses its contract.
6. Generate and approve one pilot before any family expansion. Generate and audit every later image individually.
7. Keep the immutable approved pilot as the identity and style reference for every frame; use the previous accepted frame only as an optional motion-continuity reference.

Existing project SpriteSheets are legacy runtime assets. Preserve them until an explicitly planned migration replaces their consumers, but never use them as the production pattern for new or regenerated art.

Read [references/asset-contract.md](references/asset-contract.md) before defining or changing a family contract. Run [scripts/audit_sprite_assets.py](scripts/audit_sprite_assets.py) after every generated PNG and before Unity import.

## 1. Audit references and consumers

- Select one approved visual reference and, for recurring characters or components, one identity reference.
- Measure canvas, alpha-visible bounds, alpha-weighted visual center, margins, ground baseline, palette, outline weight, camera, lighting, PPU, Pivot, Transform scale, and runtime size.
- Inspect every consumer: Animation Clips, Animator Controllers, Prefabs, ScriptableObjects, Addressables, USS/UXML, and scenes.
- Use CodeGraph before text search for code consumers. Use Unity MCP or AssetDatabase-backed tools for Unity assets, Prefabs, UI, and open scenes.
- Record existing GUIDs and references when replacing an asset. Prefer replacing an independent Sprite in place when its contract and role are unchanged.

Stop if there is no approved reference or measurable target. Ask for the single missing decision instead of inventing coupled dimensions.

## 2. Freeze the family contract

Record:

- role, family, name pattern, destination, and consumers;
- immutable style and identity references;
- exact generation canvas and exact delivery canvas;
- visible width, height, coverage, margins, and visual-center ranges;
- actor ground baseline or icon optical-center policy;
- PPU, Pivot, filter, compression, color space, alpha, and mesh policy;
- Unity Transform scale, which defaults to `(1, 1, 1)`;
- state, direction, frame order, and continuity requirements;
- nine-slice borders and forbidden decoration zones when applicable;
- acceptance scene, UI size, and resolution.

Generation and delivery canvases should be identical. If the generator cannot emit the delivery resolution, declare one family-wide full-canvas uniform export transform before generation and apply the same transform to every family member. Never calculate a different scale from each subject’s alpha bounds.

## 3. Approve one pilot

- Generate one neutral asset first: Idle frame 0, one representative item, one UI icon, one UI state, one panel, or one prop.
- Request one isolated subject on the exact family canvas. Do not request a grid, contact sheet, component board, labels, or multiple variants.
- Use approved images as actual image references. Fix camera, facing, silhouette language, outline, material, palette, lighting, and transparent padding.
- Run the image audit immediately. Visually inspect identity, proportions, material, style, and center.
- Reject the pilot if any quantitative or visual requirement fails. Do not repair it with manual crop, per-subject scaling, or Pivot adjustment.
- Preserve the accepted pilot and generation prompt as immutable family inputs.

## 4. Expand one image at a time

- Derive each animation frame, icon variant, UI state, or related prop by editing from the immutable pilot.
- For animation, always include the pilot reference; optionally include the immediately previous accepted frame for motion continuity. Do not build a chain that forgets the pilot.
- Keep anatomy, costume, equipment, camera, light, body scale, and canvas fixed. Change only the requested pose or state.
- Audit and visually approve each image before generating the next. Stop the family at the first failure.
- Store every accepted image under its final deterministic filename. Do not merge accepted images into a Sheet afterward.

## 5. Normalize only by contract

- Keep the complete fixed canvas. Do not tight-crop individual outputs.
- Prefer regeneration or image editing when subject occupancy or center is wrong.
- If a predeclared family-wide export transform is required, resize the whole canvas uniformly with the same factor and resampling policy for every member, then audit the delivery files again.
- Align standing actors and props through the generated ground baseline. Align icons through the measured alpha-weighted visual center.
- Measure the persistent body region for Attack, Hit, or Death frames whose effects expand the alpha bounds; keep effects inside declared safe margins.
- Keep gameplay Collider and interaction ranges unchanged unless the user separately authorizes gameplay changes.
- Never use non-uniform scaling, runtime scale Tween, parent compensation, or per-frame Transform changes to conceal an asset defect.

## 6. Import without manual cutting

- Import each generated runtime PNG as `Texture Type: Sprite (2D and UI)` and `Sprite Mode: Single`.
- Apply PPU, Pivot, filter, compression, mesh, mipmap, alpha, and color-space settings through an importer preset, Editor automation, or Unity MCP.
- Use one Pivot rule for the family. A required exception must be declared before generation and encoded by automation, not adjusted by hand.
- Keep each nine-slice panel or state as an independent PNG. Configure its borders through importer automation and preview minimum, normal, and maximum control sizes.
- Keep text, icons, and unique corner decorations separate from stretchable panel bases when one safe nine-slice region cannot preserve them.
- Do not open Sprite Editor as a required production step.

## 7. Bind every consumer

- Bind individual frame Sprites to Animation Clips in deterministic filename order.
- Bind SpriteRenderers, Prefabs, ScriptableObjects, Addressables, USS/UXML, and scenes to independent assets.
- Save Prefabs and open scenes through Unity MCP or Unity Editor APIs, not direct scene-YAML edits.
- Scan Prefabs, scenes, animations, data assets, and styles for missing references, old GUIDs, legacy sub-sprite dependencies, and fixed placeholders.
- Delete superseded assets only after the dependency scan reports zero consumers.

## 8. Validate in layers

1. Run the bundled image audit after every image and once for the complete family.
2. Compare the pilot and all family members side by side at native pixels.
3. Validate Unity importer settings, `Sprite Mode: Single`, Pivot, PPU, border, GUID binding, and Addressables membership.
4. Compare renderer bounds and collider alignment in the runtime scene without transient scale animation.
5. Exercise every item icon in world drop, inventory, equipment, shop, crafting, details, and HUD.
6. Exercise UI at minimum, normal, and maximum control sizes at every required resolution.
7. Play every animation transition and check identity, motion continuity, frame scale, baseline, clipping, Hit/Death visibility, and cleanup.
8. Re-enter the scene and check missing handles, duplicate loads, stale references, and Console output.
9. Run targeted tests, full EditMode/PlayMode tests, assembly compilation, and diff checks appropriate to the project.

Do not report completion from importer or compile success alone. Include quantitative image results, native-pixel comparisons, and runtime screenshots.

## Stop conditions

Stop the batch and fix the earliest failed stage when any of these occurs:

- canvas dimensions differ from the contract;
- a production output contains more than one Sprite, frame, state, or component;
- visible bounds, coverage, visual center, baseline, or safe margins miss tolerance;
- stable frames exceed group size, center, or baseline deltas;
- identity, anatomy, costume, equipment, camera, palette, or lighting drifts;
- an axis is stretched, a subject is individually scaled to fit, or a manual crop changes family scale;
- a nine-slice line crosses unique decoration;
- Unity import requires manual Rect or Pivot correction;
- a replaced asset leaves stale or missing references;
- runtime Transform changes mask the true asset size.

## Audit command

Run:

```powershell
python scripts/audit_sprite_assets.py path/to/manifest.json
```

The script requires Pillow. It validates each full PNG and family-level deltas, exits nonzero on contract violations, and can write machine-readable output with `--json-report path`. Quantitative success does not replace visual identity and motion-continuity review.

## Handoff

Report:

- the frozen family contract and immutable references;
- one-file-per-Sprite mapping and final destinations;
- generation and delivery canvas decisions;
- visible bounds, visual center, baseline, margins, PPU, Pivot, border, and scale results;
- per-image and family audit output;
- bound consumers and zero-missing-reference result;
- native-pixel previews, runtime screenshots, and test results;
- intentional exceptions and unresolved visual judgments.
