---
name: artifact-template-game-ui-kit
description: "Create dark-fantasy game UI reference images or independent production UI assets using the Game UI Kit retained reference. Use when the user selects this template, names Game UI Kit, explicitly invokes $artifact-template-game-ui-kit, or requests UI visuals that should match its style. For Unity production output, generate one fixed-canvas PNG per component or interaction state, use the retained board only as a visual-language reference, and apply $unity-2d-sprite-workflow; never generate a production component sheet or rely on manual slicing."
---

# Game UI Kit

Keep the retained reference file unchanged. Treat it as a visual-language reference, not as a production SpriteSheet or mandatory output layout.

## Output mode

Choose one mode before generation:

- **Reference / mockup**: create a composed UI presentation image for review. It is not imported into Unity and must be labeled as non-production output in the handoff.
- **Production asset**: load `$unity-2d-sprite-workflow`, freeze a family contract, and generate exactly one independent component or one interaction state per fixed-canvas PNG.

If the user asks for several production components or states, approve one pilot first, then generate and audit them sequentially. Do not combine them into a grid, contact sheet, atlas, or SpriteSheet.

Do not invent a generation canvas, delivery canvas, resize factor, resampler, visible occupancy, Border, Pivot, PPU, filter mode, runtime control size, acceptance resolution, or state mapping. Measure approved project assets and actual consumers first. If a required value cannot be discovered, stop and ask for that one decision before generation.

## Workflow

1. Read `artifact-template.json` and resolve its paths relative to this skill directory.
2. Classify the request as reference/mockup or production before invoking image generation.
3. For production, load `$unity-2d-sprite-workflow`; inspect approved UI assets and actual Unity consumers, then declare the exact canvas, visible occupancy, visual center, safe margins, state, destination, Pivot, PPU, filter, runtime sizes, acceptance resolution, and nine-slice borders before generation. Do not substitute template-derived numbers for missing project requirements.
4. Invoke `$imagegen` with the retained PNG as a style reference and the user's requested content as the brief.
5. For production, request one isolated component with no labels, sample inventory contents, extra icons, neighboring states, or presentation board. Generate states such as Default, Hover, Focused, Pressed, and Disabled as separate files derived from the approved pilot.
6. Preserve the user's content and the reference's visual language. Do not invent text or factual claims to fill the composition.
7. Visually inspect every output for style, silhouette, material, lighting, interaction-state clarity, center, edge safety, and nine-slice safety.
8. For production, run the single-image audit after each PNG and reject any result that needs manual crop, per-image scaling, Sprite Editor slicing, or Pivot repair. A family-wide export transform is allowed only when it was approved in the contract before the pilot.

## Fidelity

Preserve the reference image's palette, material treatment, lighting, border language, focus language, and restrained ornament.

For reference/mockup output, preserve its composition, hierarchy, and typography when compatible with the request. For production assets, do not preserve the board layout or baked text; use the reference only to control visual language.

User instructions control requested content and explicit deviations. A production component that cannot satisfy both the frozen asset contract and the style reference must be regenerated or escalated for a decision, not repaired manually.
