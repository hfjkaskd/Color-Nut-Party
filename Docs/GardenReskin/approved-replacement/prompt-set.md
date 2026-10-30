# 最终素材生成规格

方式：内置 `image_gen.imagegen`，非 CLI。以下均以用户确认的 `previews-clean-boundaries` 图片为风格参考；不把 AI 图中的文字、金额或布局烘焙进游戏。

## sources/panel.png

Create ONE production Unity UI background sprite on a genuinely transparent RGBA background, not a full mockup. Style references: approved light honey wood and cream garden skin. Single empty tall portrait panel shell, width:height approximately 0.58. Cream smooth near-flat interior, straight thin honey wood rails, modest rounded outer corners. Small green leaf clusters and tiny cream flowers integrated into top-left and bottom-right corners only. One completely continuous blank cream field. No title, text, icons, buttons, input fields, trays, inner frames or horizontal separators. Fine brown outline; avoid inflated thick orange tubular bevels. Entire panel visible.

## sources/controls.png

Production game UI sprite sheet, transparent RGBA outside each sprite. Exactly 2 columns by 4 rows, separate centered assets with transparent gaps, no text/letters/numbers/currency. Row 1: green ribbon; green primary pill with wood rim. Row 2: wooden title plaque; cream input field with thin single honey border. Row 3: neutral warm gray amount card; selected golden amount card. Row 4: dark brown recessed card with slender honey edge and tiny corner leaves; cream stats panel. Low-detail interiors, front-on orthographic, no nested frames. Final use: input, conversion, amount states, cash/instructions card, tool icon card, stats small cards and records strip. Ribbon remains from this sheet.

## sources/clean-controls.png

Production Unity UI sheet, 1 column by 2 rows on transparent RGBA, no text or icons. Top: vivid green glossy capsule button, width:height4:1, slim honey wooden rim, smooth semicircular ends, no leaves. Bottom: blank warm honey wood title plaque, width:height5:1, rounded ends, unobstructed surface, no leaves. Preserve warm approved wood/green palette. Final edit: preserve shapes, colours, positions and material artwork; remove outside glow and shadow; outside silhouette fully transparent with small antialiased perimeter. Alpha was inspected before mechanical crop. Final use: primary button and three title sizes.

## sources/stats.png

Only an empty statistics background sprite, horizontal rounded rectangle, aspect about2.9:1. Honey wood outer frame and plain continuous cream interior. Integrated flat honey header band in the upper23% for dynamic white text centered around10% down from top; no line through the heading. Remaining area empty cream. Slender side/bottom rims and tiny green leaves at far ends of header inside bounds. No text, numbers, icons or three inner cards. Transparent outside silhouette, no glow or cast shadow. Same approved garden materials.

Mechanical import preserves generated artwork and alpha. It only crops, scales and slices; it does not paint or recolour the source. Final sprites are independent Unity resources rather than whole-screen mockups.
