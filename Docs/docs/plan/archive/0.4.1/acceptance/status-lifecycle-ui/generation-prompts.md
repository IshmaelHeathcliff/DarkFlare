# 状态图标生成记录

以下为最终深色底家族调用的原始提示与固定参考路径。所有输出均采用完整画布 1254×1254 → 96×96 nearest-neighbor 导出，不裁剪或逐图缩放。

## status_opaque_pilot

```javascript
// @exec: {"yield_time_ms": 120000, "max_output_tokens": 150}
const r=await tools.image_gen__imagegen({prompt:"Production dark fantasy pixel-art BURNING status icon. ONE orange-red three-tongued flame, dark crimson outline, small pale-gold upper-left glint. Chunky deliberately visible pixel clusters. Match reference flame identity/style. This NEW FAMILY uses an OPAQUE perfectly uniform dark blue-black #111821 background filling entire1254x1254 square canvas. NO transparency, no checkerboard, no gradient or vignette in background. Center flame, symbol height900px width800px with clear dark margin on all sides. No frame, badge, lettering, smoke, particles, glow or extra objects. The entire square is one icon, downsampled uniformly to96x96 and shown40x40.",referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-9919d554-3fca-4bf3-876a-7aff509f177f.png"]});store("status_opaque_pilot",r);generatedImage(r);

```

## status_opaque_bleeding

```javascript
// @exec: {"yield_time_ms": 120000, "max_output_tokens": 150}
const r=await tools.image_gen__imagegen({prompt:"Create ONE BLEEDING status icon from the attached immutable pilot. Keep exact1254x1254 square and full opaque dark blue-black background the same as pilot, no transparency or checkerboard. Replace flame with a single BROAD round crimson blood drop, short pointed top, burgundy chunky outline, bright red blocks and pale red upper-left highlight. Subject centered same optical mass/size as flame, longest side about900px, no edge contact. Same chunky dark-fantasy pixel-art style. No text, border, badge, splash, flame or other objects. One icon only.",referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"]});store("status_opaque_bleeding",r);generatedImage(r);

```

## status_opaque_poison

```javascript
// @exec: {"yield_time_ms": 120000, "max_output_tokens": 150}
const r=await tools.image_gen__imagegen({prompt:"Create ONE POISON status icon matching attached immutable pilot's chunky dark-fantasy pixel style and scale. Opaque full1254x1254 dark blue-black background same as pilot, no transparency or checkerboard. Subject is a single sickly green skull emblem, two deep black angular eyes, compact teeth, broad clear silhouette, moss/dark green outline, small pale yellow-green upper-left highlight. Not realistic or gruesome. Center symbol longest side about900px, clear margins. No flames, text, frame, badge, bones outside skull, smoke, particles or extra objects. One complete icon to downsample full-canvas to96x96.",referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"]});store("status_opaque_poison",r);generatedImage(r);

```

## status_opaque_stun

```javascript
// @exec: {"yield_time_ms": 120000, "max_output_tokens": 150}
const r=await tools.image_gen__imagegen({prompt:"ONE STUN status game icon. Replace attached pilot's flame with a centered eight-point GOLD IMPACT STAR, bold angular points and broad bright pale-gold center, dark ochre outline, golden block shading, sharp hard pixels. Symbol represents dizziness/impact, no text or face. Same dark-fantasy chunky pixel style, exact1254x1254 opaque square full uniform dark blue-black backdrop identical to pilot. Symbol approx850x850 centered, >140px clear background margins. No flames, spark particles, border, badge, transparency, checkerboard, letters or additional objects. One complete icon for96x96 export.",referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"]});store("status_opaque_stun",r);generatedImage(r);

```

## status_opaque_chill

```javascript
// @exec: {"yield_time_ms": 1000,"max_output_tokens":150}
const r = await tools.image_gen__imagegen({referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"],prompt:"Create ONE CHILL status icon matching attached immutable pilot chunky dark fantasy pixel art, same full opaque dark blue-black uniform backdrop, exact 1254x1254 square. Replace flame with one centered pale ice-blue six-point snowflake crystal, longest dimension about 900px, crisp stepped hard pixel clusters. One symbol only, bold readable at 40px. No flame, border, text, badge, particles, transparency, checkerboard or gradients. Keep generous dark empty margin and identical background."});store("status_opaque_chill",r);generatedImage(r);

```

## status_opaque_shock

```javascript
// @exec: {"yield_time_ms": 120000,"max_output_tokens":150}
const r=await tools.image_gen__imagegen({referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"],prompt:"Create ONE SHOCK status icon matching attached immutable pilot chunky dark fantasy pixel art, same full opaque dark blue-black uniform backdrop, exact 1254x1254 square. Replace flame with one centered bright golden lightning bolt, lavender-blue edge shadow, longest dimension about 900px. Crisp stepped hard pixel clusters. One symbol only, bold readable at 40px. No flame, border, text, badge, particles, transparency, checkerboard or gradients. Keep generous dark empty margin and identical background."});store("status_opaque_shock",r);generatedImage(r);

```

## status_opaque_weakness

```javascript
// @exec: {"yield_time_ms": 120000,"max_output_tokens":150}
const r=await tools.image_gen__imagegen({referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"],prompt:"Create ONE WEAKNESS status icon matching attached immutable pilot chunky dark fantasy pixel art, same full opaque dark blue-black uniform backdrop, exact 1254x1254 square. Replace flame with one centered BROKEN SWORD pointing diagonally downward, dull grey blade with a clear jagged crack and desaturated burgundy hilt. Longest dimension about 900px, crisp stepped hard pixel clusters. One connected symbolic composition only, bold readable at 40px. No flame, border, text, badge, particles, transparency, checkerboard or gradients. Keep generous dark empty margin and identical background."});store("status_opaque_weakness",r);generatedImage(r);

```

## status_opaque_equipment_guard

```javascript
// @exec: {"yield_time_ms": 120000,"max_output_tokens":150}
const r=await tools.image_gen__imagegen({referenced_image_paths:["C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/exec-242eb332-fb8e-484b-8562-3c07da532cac.png"],prompt:"Create ONE EQUIPMENT GUARD status icon matching attached immutable pilot chunky dark fantasy pixel art, same full opaque dark blue-black uniform backdrop, exact 1254x1254 square. Replace flame with one centered medieval iron shield, blue-grey steel and muted turquoise central boss, longest dimension about 900px. Crisp stepped hard pixel clusters. One shield only, bold readable at 40px. No flame, border framing, text, particles, transparency, checkerboard or gradients. Keep generous dark empty margin and identical background."});store("status_opaque_equipment_guard",r);generatedImage(r);

```

## 最终生成原图

原图保留于 `C:/Users/darkf/.codex/generated_images/01a095d4-1a4c-7593-9e32-7f2c7b839867/`；原图不移动、不删除。

| 状态 | 原图文件 |
| --- | --- |
| burning | `exec-242eb332-fb8e-484b-8562-3c07da532cac.png` |
| bleeding | `exec-6daacca4-17c5-4300-8eaf-6fb15fd66e15.png` |
| poison | `exec-3fa68309-f35b-417a-8b67-58850e5e7710.png` |
| stun | `exec-6c2c7777-bf98-4a91-bde6-4d29e7cd1216.png` |
| chill | `exec-59c8808e-4940-4eb0-9b48-df9b2533959e.png` |
| shock | `exec-a0fa4897-b1ac-469b-91f8-9c6ab2bb3bdc.png` |
| weakness | `exec-11310965-567c-45c9-958c-db55cba10459.png` |
| equipment_guard | `exec-e5726571-9c98-4d51-9df2-ef906f681808.png` |
