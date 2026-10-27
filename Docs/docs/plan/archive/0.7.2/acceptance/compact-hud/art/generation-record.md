# HUD 图标生成记录

生成工具：内置 ImageGen，透明背景；实际材质参考为现有 inventory 图标。天赋第二次试产通过后作为暂停的固定参考。未使用 CLI / API。两张独立生产 PNG，经合同预先规定的全画布1254→96、LANCZOS导出，未按主体裁切 / 移动 / 补偿。

首张天赋试产请求1024但工具返回1254，未接受或扩展。按工具原生1254重新冻结合同后，重新生成并审计Pilot。天赋交付可见64×68、中心48.08/49.06、边距16/14/16/14；暂停49×68、中心48.41/49.86、边距24/16/23/12，均无错误或警告。

## 天赋 Pilot 提示词

Create ONE isolated production icon for a dark-fantasy loot ARPG TALENT BOARD entry. Use the supplied inventory icon strictly as the actual style/material reference: aged copper-bronze, dark iron, crisp pixel-art hard edges, top-left highlights, very limited warm gold accents, no smooth glossy mobile-game look. Change the subject to ONE simple six-sided talent sigil: a bold hexagonal iron ring with six compact bronze nodes and ONE centered flame silhouette. Coarse clear shape, not complex circuitry. No labels, lettering, numbers, backdrop, floor, shadow outside subject, grids, sheets or extra objects. Transparent background. EXACT square 1254x1254 full canvas. Composition centered with alpha-weighted center near (627,627), tolerance28. Complete subject bounding box width AND height between 880 and 970 pixels, ideally 930; at least 140 pixels of transparent padding at all four edges, even transparent padding around it. It will be exported uniformly full-canvas to96x96 and displayed at28x28, so keep few thick recognizable shapes and no tiny texture noise. This single icon only, unframed outside the hexagon.

## 暂停提示词

Derive ONE PAUSE icon from the supplied approved talent icon as immutable style/material reference (second supplied image is original inventory style). Preserve its crisp dark-fantasy pixel-art, aged bronze/copper bevels, deep iron recesses, coarse block highlights from top left, limited warm gold. Replace hexagon/flame completely with exactly TWO thick symmetric parallel vertical metal bars, equally tall, with a clear transparent gap. Readable universal PAUSE symbol, no outer frame, no glyph text, no extra objects, no sheet or variants. ONE runtime icon on transparent background, exact1254x1254 full canvas. Entire two-bar subject centered (alpha-weighted center627,627 tolerance25px). Total width about760px (range670-860), height about930px (range880-970). Keep generous even clear transparent padding. Bars bold and simple, each around240px wide, bevel thick but no tiny noise. Final whole canvas will be uniformly exported to96x96, runtime28x28; silhouette and gap must stay obvious. No cast shadow outside symbol. One independent PNG only.

