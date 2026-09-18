# 泥土 Pilot 生成记录

2026-09-18，内置 `image_gen`，未使用 API / CLI。先按[家族合同](../../terrain-art-contract.md)生成和审计，再通过 Unity API 导入；源 PNG 没有做裁切、缩放、调色或周期化。

## 输入与迭代

已批准世界风格参考：`Assets/Art/Sprites/Environment/GroundTiles/Base/ground_base_stone_00.png`，SHA256 `D568E41EBDE34E392616EB78F86E1DEEB873CC04EF68368E1039D4EC36FD9408`。仅继承暗色、低饱和与光照，不继承石板结构。

1. 首次请求 1024×1024，工具返回 1254×1254，[画布审计拒绝](./rejected-canvas-audit.json)。依据工具实际行为和旧地表记录，将整个家族修订为原生 1254，并重新生成；没有对该图单独缩放。保留[第一张草稿](./inputs/dirt-draft-01.png)，它后来作为第三次编辑的图像输入，不是批准的生产 Pilot。
2. 第二张画布 / Alpha 审计通过，但纹理过细，目视拒绝，未导入工程；[量化记录](./rejected-detail-audit.json)说明量化通过不能代替风格判断。
3. 第三次以第一张草稿为唯一编辑目标，要求简化为粗像素块面；本张通过审计和角色同框复核，保留为不可变 Pilot。后续材质与变体必须直接引用批准的这张 Pilot，不能沿草稿继续扩展。

批准输出：`Assets/Art/Sprites/Environment/TerrainSources/terrain_dirt_source_00.png`，SHA256 `29f81593da1906ebbb85038ea286ce261b432557504dfbae40852b818e114a05`。Unity GUID `8ec21afb5dd6a194dbc020c251646696`。生成工具输出文件名为 `exec-c9a7e569-0591-40de-9ecd-9578f0d8c934.png`，复制入项目后保持字节一致。

## 最终编辑提示词

输入图像：上述 `inputs/dirt-draft-01.png`，角色为编辑目标。完整提示词如下：

```text
Edit this single dirt texture. Output ONE opaque square 1254x1254 image. Change ONLY the visual texture: simplify drastically into authentic very chunky 64x64 pixel-art material then enlarged by nearest-neighbor. Each smallest square color pixel should be about TWENTY output pixels wide; broad low-detail clusters, about 64 logical pixels across entire image. Use ONLY 6-8 flat colors with absolutely no shading inside any logical pixel. Keep dark desaturated gray brown soil palette. Flat compacted earth, a few tiny soft earthen clumps represented by 2-4 logical pixels, scattered short shallow cracks 1 pixel thick. Remove large flagstone-looking plates and deep outlines. Uniform low-contrast diffuse lighting, mean RGB about (47,43,38), brightest palette color no more than (75,72,68). Do not create photographic texture, tiny grain, lots of cracks, high-frequency noise, bevels, smooth gradations or antialiasing. The whole texture is a single meter of plain dirt, made for an old-school low-resolution top-down game. Full bleed opaque everywhere. Seamless repeating intent. No props, objects, grass, pebbles, stone paving, water, perspective, central patch, vignette, borders, grid LINES, text or multiple tiles. Crisp enormous blocky pixels, simple quiet soil.
```

提示词中的逻辑像素数与色数是风格引导，不是工具保证的实际离散像素 / 色板编码；批准依据是家族合同的画布、明度和目视材质尺度。最终编译才以确定性算法生成真实 64×64 Tile，源图不承担精确边签名。
