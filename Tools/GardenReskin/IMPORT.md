# Generated sprite import

This is a mechanical importer for ImageGen PNGs. It does not paint, recolour, generate decorations, or change Unity metadata.

```powershell
pwsh -NoProfile -File Tools/GardenReskin/ImportGeneratedSprites.ps1 -Manifest Docs/GardenReskin/import-manifest.json -DryRun
pwsh -NoProfile -File Tools/GardenReskin/ImportGeneratedSprites.ps1 -Manifest Docs/GardenReskin/import-manifest.json
```

Use `import-manifest.example.json` as the schema example. Paths are relative to the project root unless absolute. Each target must be an existing PNG under Assets and must exist in `Docs/GardenReskin/OriginalAssets/before-reskin.zip`. Original dimensions and alpha bounds always come from that ZIP, so repeated imports cannot progressively shrink an asset.

Each entry requires `target`, `source`, and `mode` (`grid` or `full`). Grid entries use integer `columns`, `rows`, `cell`; cell is zero-based, left-to-right and top-to-bottom. Uneven canvas divisions use integer cell boundaries without dropping trailing pixels. `full` resamples the whole generated image into the full original canvas and does not crop or nine-slice.

For grid mode 8-connected regions with alpha > `alphaThreshold` (default 100) define the subject bounds. Components with at least max(64 pixels, 3% of the largest component) are retained; the largest component is always retained even for a tiny source. Their union preserves disconnected arrows, plus signs and similar substantial details while excluding small isolated noise. A `padding` margin (default 2 source pixels) is added, clipped to the grid cell. The original generated RGBA within those bounds is retained. No colour replacement or noise repainting is applied to output pixels. The generated subject fills the original PNG's nonzero-alpha rectangle; the outside canvas remains transparent. Entry-level `alphaThreshold: 200` can avoid a title's strong surrounding halo when identifying the main bounds.

Optional `preserveOriginalAlpha: true` multiplies the resized generated alpha by the ZIP original's alpha, preserving original transparent holes and antialiased mask edges. Use it only for existing transparent frames, masks or line assets that must retain those cutouts. It is false by default; generated RGB values are never recoloured.

Existing nonzero borders are discovered from PNG `.meta` `spriteBorder` and serialized Sprite `m_Border` referencing the PNG's GUID. Conflicts fail preflight instead of choosing silently. Override with `oldBorder: [left, bottom, right, top]` or an object with those four property names. A zero override disables nine-slice. Source and destination split axes use Unity's bottom-origin border convention, converted to image coordinates. Destination borders retain the original whole-canvas positions after subtracting original transparent margins. `sourceSliceRatio` defaults to 0.16 of the generated subject's width/height for every active edge, and may be a scalar, four-element array, or named-border object. The ratio must be >= 0 and < 0.5. All nine regions are resampled separately so corners and edges occupy the original slice zones. Asymmetric source designs should specify asymmetric ratios.

The importer preflights and stages every output outside Assets before writing any target. `-DryRun` performs the same decode/crop/resample and computes hashes in memory, writes only the report, and does not create a staging folder or modify Assets. Normal execution retains staged PNGs under `Docs/GardenReskin/ImportStaging/` for review. No `.meta` or Sprite `.asset` file is written; hashes are verified unchanged after processing. `import-report.json` records source/original/current/output hashes, unchanged dimensions, subject bounds, slice values and protected metadata hashes. If preflight fails, no target is changed. An operating-system failure during final copying can leave a partial import; rerun the same manifest after fixing the error.
