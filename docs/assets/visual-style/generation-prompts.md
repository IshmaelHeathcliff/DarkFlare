# 阶段 0 视觉生成提示词

## 说明

- 生成模式：内置 imagegen。
- 风格板无参考图；其余资产均以 `phase-0-style-board.png` 作为严格风格参考。
- 透明资产先生成均匀色键背景，再使用 imagegen 技能提供的 `remove_chroma_key.py` 本地移除背景。
- 最终裁帧、尺寸归一和 Alpha 验证由 `tools/visual_slice/prepare_assets.py` 完成。

## 风格板

```text
Use case: stylized-concept
Asset type: 2D top-down dark fantasy game visual style board for a Unity vertical slice
Primary request: create one cohesive production reference board showing a compact battlefield tile sample, one player adventurer silhouette, one basic corrupted monster silhouette, one cyan magical projectile, one oversized greatsword loot icon, and one inventory panel component set
Scene/backdrop: neutral charcoal presentation board with clearly separated asset groups, no full scene illustration
Subject: a readable top-down 3/4 world kit; the player is a compact hooded warden with a teal cloth accent and steel weapon; the monster is a hunched ember-red corrupted creature with a distinct asymmetrical silhouette; the battlefield uses cracked cold-gray stone, ash soil and sparse brass camp details; UI uses dark slate panels, worn iron borders and restrained brass corners
Style/medium: low-resolution hand-painted 2D game art with pixel-like crisp edges, limited detail, strong silhouettes, consistent three-quarter top-down lighting; production-oriented, not painterly concept art
Composition/framing: organized square style board, equal emphasis on environment, characters, item, effect and UI; generous separation between groups
Lighting/mood: dim cold ambient light, subtle warm highlights, bleak but readable
Color palette: charcoal #111821, cold slate #26313A, ash gray #59616A, teal #35B9A5 for player readability, ember red #C64B3C for monster readability, brass #B58A45 for interaction, cyan #62D8FF for magic
Materials/textures: worn stone, oxidized steel, faded cloth, restrained glow
Constraints: no text, no logos, no watermark, no photorealism, no 3D render, no isometric camera, no excessive ornament, no tiny illegible details; keep all elements stylistically consistent and suitable for 64-96 pixel sprites and 64 pixel icons
Avoid: bright saturated background, chibi proportions, anime line art, glossy mobile-game rendering, detailed scenic illustration
```

## 地表切片

```text
Use case: stylized-concept
Asset type: seamless tileable 2D game ground texture for a Unity top-down battlefield
Input images: Image 1: strict visual style and palette reference
Primary request: create a square seamless ground texture combining cracked cold slate paving stones with compact ash soil in a balanced irregular pattern
Scene/backdrop: texture fills the whole canvas edge to edge
Subject: worn flat stone slabs, narrow dark seams, a small amount of dry muted grass and tiny brass-brown gravel; no props, no characters, no objects
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game art with pixel-like crisp edges and restrained detail
Composition/framing: orthographic top-down texture, evenly distributed details, no central focal point, seamless on all four edges
Lighting/mood: flat neutral ambient lighting suitable for tiling, no directional cast shadows
Color palette: cold slate #26313A, charcoal #111821, ash gray #59616A, sparse muted brown; no teal, red or cyan accents
Constraints: seamless tile, no border, no vignette, no perspective, no large unique crack crossing the center, no text, no logos, no watermark, no scene objects
Avoid: isometric blocks, deep relief, photorealism, glossy wet stone, dramatic lighting
```

## 玩家动画表

```text
Use case: stylized-concept
Asset type: production sprite sheet for a 2D Unity top-down player character
Input images: Image 1: strict character style, palette and material reference
Primary request: create a consistent 4-column by 3-row sprite sheet of the same hooded warden character facing down-right in every cell
Scene/backdrop: perfectly flat solid #00ff00 chroma-key background for local background removal; each cell uses the same uniform background
Subject: compact hooded warden with dark oxidized steel armor, teal scarf and short split cloak, one-handed straight sword; exact same body proportions, outfit, face shadow, weapon, scale and lighting in every frame
Animation layout: row 1 four subtle idle breathing frames; row 2 four walking frames forming one clean loop; row 3 cell 1 cast/attack anticipation, cell 2 attack release, cell 3 hit recoil, cell 4 death pose
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game sprite, pixel-like crisp edges, strong readable silhouette, restrained detail
Composition/framing: exact 4 by 3 grid with equal cell sizes; character centered on identical foot anchor in every cell; full body visible; generous padding; no grid lines, no labels
Lighting/mood: identical dim cold top-left lighting in all frames
Color palette: charcoal armor, teal #35B9A5 cloth accent, muted steel and tiny brass details
Constraints: the background must be one perfectly uniform #00ff00 color with no shadows, gradients, texture, reflections, floor plane or lighting variation; no cast shadow, no contact shadow; do not use #00ff00 in the character; preserve exact character identity across all 12 cells; no extra weapons, no text, no logos, no watermark
Avoid: frame-to-frame costume drift, changing sword size, changing camera angle, isometric view, oversized head, anime style, motion blur, glow, particles
```

## 基础怪物动画表

```text
Use case: stylized-concept
Asset type: production sprite sheet for a 2D Unity top-down basic monster
Input images: Image 1: strict monster style, palette and material reference
Primary request: create a consistent 4-column by 3-row sprite sheet of the same hunched corrupted ember creature facing down-left in every cell
Scene/backdrop: perfectly flat solid #00ff00 chroma-key background for local background removal; each cell uses the same uniform background
Subject: lean asymmetrical corrupted humanoid made of charcoal bark-like flesh and oxidized armor fragments, one longer claw arm, small ember-red cracks; exact same body proportions, silhouette, armor fragments, claw length, scale and lighting in every frame
Animation layout: row 1 four subtle idle breathing frames; row 2 four stalking walk frames forming one clean loop; row 3 cell 1 attack anticipation, cell 2 claw attack release, cell 3 hit recoil, cell 4 collapsed death pose
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game sprite, pixel-like crisp edges, strong readable silhouette, restrained detail
Composition/framing: exact 4 by 3 grid with equal cell sizes; creature centered on identical foot anchor in every cell; full body visible; generous padding; no grid lines, no labels
Lighting/mood: identical dim cold top-left lighting in all frames
Color palette: charcoal #111821 and #26313A body, muted iron, ember red #C64B3C cracks; keep red accents restrained
Constraints: the background must be one perfectly uniform #00ff00 color with no shadows, gradients, texture, reflections, floor plane or lighting variation; no cast shadow, no contact shadow; do not use #00ff00 in the creature; preserve exact monster identity across all 12 cells; no weapons, no text, no logos, no watermark
Avoid: frame-to-frame body drift, changing limb count, changing claw size, changing camera angle, isometric view, cartoon or anime style, motion blur, large flames, particles
```

## 青光投射物

```text
Use case: background-extraction
Asset type: 2D Unity projectile sprite cutout
Input images: Image 1: strict visual style and palette reference
Primary request: isolate one compact right-facing arcane bolt with a sharp crystal-like cyan core and a short tapered pixel trail
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Subject: a single horizontal magical projectile, readable at 64 by 64 pixels, strong arrow-like silhouette, no surrounding particles beyond the compact tail
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game sprite with pixel-like crisp edges
Composition/framing: centered, facing right, generous padding, full sprite visible
Lighting/mood: restrained internal cyan highlight, opaque readable core
Color palette: cyan #62D8FF, pale blue-white core, deep navy edge; do not use magenta
Constraints: background must be one uniform #ff00ff color with no shadows, gradients, texture, reflections or lighting variation; crisp silhouette; no cast shadow, no floor plane, no soft transparent bloom, no smoke, no text, no logos, no watermark
Avoid: large explosion, circular fireball, long wispy glow, photorealism, 3D render
```

## 大剑图标

```text
Use case: background-extraction
Asset type: 2D Unity equipment and loot icon cutout
Input images: Image 1: strict visual style, material and palette reference
Primary request: isolate one heavy two-handed greatsword with a broad worn steel blade, dark leather grip and restrained brass crossguard
Scene/backdrop: perfectly flat solid #00ff00 chroma-key background for local background removal
Subject: a single complete greatsword, no sheath, no hands, no character; strong readable silhouette at 64 by 64 pixels
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game icon with pixel-like crisp edges and controlled highlights
Composition/framing: centered diagonally from lower-left to upper-right, entire sword visible, generous equal padding
Lighting/mood: dim cold top-left light, subtle brass edge highlight
Color palette: cold steel gray, charcoal grip, brass #B58A45 detail; do not use green
Constraints: background must be one uniform #00ff00 color with no shadows, gradients, texture, reflections or floor plane; crisp silhouette; no cast shadow, no glow, no frame, no text, no logos, no watermark
Avoid: ornate fantasy filigree, gemstones, blood, photorealism, 3D render, multiple weapons
```

## 背包 UI 组件

```text
Use case: stylized-concept
Asset type: production 2D Unity UI component sheet
Input images: Image 1: strict UI style, palette and material reference
Primary request: create an exact 2-column by 2-row sheet of four square dark-fantasy inventory UI components
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local background removal
Subject and layout: top-left is a large square panel plate with opaque charcoal slate center, worn iron border and restrained brass corner brackets; top-right is a normal inventory slot with opaque dark center and thin iron border; bottom-left is the same slot in keyboard/gamepad focus state with a thicker teal inner line plus brass corner notch; bottom-right is the same slot disabled with muted gray border and one small diagonal corner bar
Style/medium: match Image 1 exactly; low-resolution hand-painted 2D game UI, pixel-like crisp edges, practical shippable components, restrained ornament
Composition/framing: exact 2 by 2 grid with equal cells; each component centered with generous padding; no grid lines, no labels
Lighting/mood: consistent dim cool lighting and subtle top-left bevel highlight
Color palette: charcoal #111821, cold slate #26313A, iron gray #59616A, brass #B58A45, teal focus #35B9A5; do not use magenta
Constraints: background must be one uniform #ff00ff color with no shadows, gradients or texture; components have clean square silhouettes and opaque centers; panel border must have a wide stretch-safe center and uncluttered edges; no text, no icons inside slots, no logos, no watermark
Avoid: ornate gothic spikes, glossy mobile UI, rounded modern cards, blue holograms, perspective, 3D mockup
```
