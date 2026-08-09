# Single-Sprite asset contract and manifest

## Contents

1. Family contract
2. Measurement model
3. Role-specific contracts
4. Audit manifest
5. Integration checklist

## Family contract

The family is the unit of consistency. Freeze one contract before generation and do not change it per image:

- one isolated runtime asset per PNG;
- exact generation and delivery canvas;
- immutable style and identity references;
- visible-size, center, anchor, margin, and coverage tolerances;
- one PPU, Pivot, import policy, and Transform scale;
- deterministic state and frame naming;
- one optional family-wide full-canvas export transform;
- acceptance consumers, scene, UI sizes, and resolution.

Do not add a SpriteSheet layout, cell Rect, or manual slicing step. If an output misses the contract, regenerate or edit the image; do not create a per-image normalization rule.

## Measurement model

Record all five layers independently:

| Layer | Measurement | Purpose |
| --- | --- | --- |
| Generation | Model output width × height | Keep every generated family member on one canvas |
| Delivery | Imported PNG width × height | Prove no per-image crop or resize changed family scale |
| Visible | Alpha bounding box, margins, and thresholded coverage | Control apparent size and clipping risk |
| Center / anchor | Alpha-weighted center and ground baseline | Control optical alignment and frame stability |
| Runtime | `visible px / PPU × uniform Transform scale` | Compare actual world-space size |

Two 96×96 images can look very different when one subject is 40 px tall and another is 88 px tall. Canvas equality is necessary but not sufficient; visible bounds, alpha-weighted center, baseline, PPU, and runtime scale must also match the contract.

## Role-specific contracts

### Actors and NPCs

- Approve Idle frame 0 before other states.
- Store every state and frame in an independent, equal-size PNG.
- Record body width/height range separately from transient effects.
- Define common ground baseline and Pivot.
- Limit stable Idle/Move visible size, visual-center, and baseline deltas.
- Always reference the immutable pilot when editing later frames.

### Equipment and fixed icons

- Use one canvas policy per icon family.
- Constrain visible longest side and optical padding, not canvas alone.
- Preserve transparent corners and avoid edge contact.
- Verify the same item in every runtime presentation.

### UI frames

- Store each icon, panel, and interaction state in a separate PNG.
- List four border values for every nine-slice image.
- Mark corners, rivets, notches, emblems, and bevel transitions as forbidden slice zones.
- If no safe repeatable edge exists, split the visual into a stretchable base and independent decorations.

### World props

- Record ground contact, footprint, visual height, occlusion risk, and sorting expectations.
- Let props differ intentionally in size; require consistency with gameplay scale and nearby actors.
- Define placement semantics: functional zones, primary path clearance, small clusters, and boundary landmarks.

## Audit manifest

Use top-left image coordinates. A manifest describes one family of independent PNGs:

```json
{
  "schema_version": 1,
  "family": "warden-idle",
  "alpha_threshold": 8,
  "canvas": { "width": 96, "height": 96 },
  "alpha_required": true,
  "assets": [
    {
      "name": "idle_0",
      "image": "warden_idle_00.png",
      "visible_width": [52, 66],
      "visible_height": [86, 90],
      "alpha_coverage": [0.20, 0.50],
      "visual_center_x": [46.0, 50.0],
      "visual_center_y": [48.0, 58.0],
      "bottom_margin": [3, 6],
      "edge_clearance": 3,
      "group": "idle"
    },
    {
      "name": "idle_1",
      "image": "warden_idle_01.png",
      "visible_width": [52, 66],
      "visible_height": [86, 90],
      "alpha_coverage": [0.20, 0.50],
      "visual_center_x": [46.0, 50.0],
      "visual_center_y": [48.0, 58.0],
      "bottom_margin": [3, 6],
      "edge_clearance": 3,
      "group": "idle"
    }
  ],
  "groups": [
    {
      "name": "idle",
      "max_width_delta": 6,
      "max_height_delta": 6,
      "max_visual_center_x_delta": 2.0,
      "max_visual_center_y_delta": 3.0,
      "max_bottom_margin_delta": 2,
      "max_alpha_coverage_delta": 0.03
    }
  ]
}
```

Fields:

- `schema_version`: must be `1`.
- `family`: stable family name used in reports.
- `alpha_threshold`: pixels with alpha above this value count as visible.
- `alpha_required`: defaults to `true`; set `false` only for intentional full-bleed textures.
- `canvas`: required exact dimensions shared by every asset in the family.
- `assets`: non-empty list; every entry is one complete PNG and one runtime Sprite.
- `image`: PNG path relative to the manifest, or relative to the current directory when the manifest is read from stdin.
- `visible_width`, `visible_height`, `bottom_margin`: optional inclusive pixel ranges.
- `alpha_coverage`: optional inclusive ratio of pixels above the alpha threshold to full canvas pixels.
- `visual_center_x`, `visual_center_y`: optional inclusive pixel ranges for the alpha-weighted center in top-left coordinates.
- `edge_clearance`: minimum transparent pixels on every image edge. Omit for intentional full-bleed assets.
- `group`: stable frames measured together.
- group delta fields: maximum allowed spread across successful members for visible size, visual center, bottom margin, and alpha coverage.

Do not place Attack/Death frames with large effects in a body-size group unless their persistent body is separately masked. Run the audit after each asset and again after the family is complete.

## Integration checklist

- Every production PNG contains exactly one runtime asset.
- Texture type and `Sprite Mode: Single` match the contract.
- PPU, filter, compression, alpha, mesh type, Pivot, border, and color space are explicit and automated.
- No non-uniform Transform exists between renderer and visual root.
- Animator states bind independent frame files in deterministic order and preserve the parameter contract.
- Prefabs, data assets, Addressables, USS/UXML, and scenes resolve the current independent assets.
- Missing-object and old-dependency scans return zero.
- Old derived files are deleted only after the scans pass.
- Runtime screenshots cover side-by-side world scale and min/normal/max UI sizes.
