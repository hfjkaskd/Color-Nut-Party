# Guide screw artwork

Built-in imagegen was used to extract/recreate the yellow screw as an independent transparent sprite from the original combined tutorial background.

Input: `original-tip.png` (copied unchanged from the original export).
Output: `Assets/Resources/atlas/newbieguidepanel/yellow-screw.png`.
Transparent PNG: 1213 × 1297; Unity import rectangle (276, 70, 658, 1111), maximum imported texture dimension 256. PNG pixels were not edited after generation.

Prompt:
> Use case: background-extraction. Edit target: attached game UI image. Extract ONLY the yellow bolt icon on the far left: a short silver threaded vertical rod on a round silver base, with exactly three chunky glossy golden-yellow hexagonal nuts stacked on it, seen from the same slight elevated front view. Preserve the original mobile puzzle-game cartoon rendering, proportions, colors, silver thread, nut faces, edge highlights and shadows. Remove the ENTIRE white-and-purple rectangular message panel and all background. Output a single isolated complete icon on true transparent alpha, tightly framed with a small even transparent margin. No text, no frame, no panel, no additional elements. The icon must be centered and fully visible, no cut edges.

Implementation: `LocalGuideScrewBuilder.Rebuild` authors the existing shared NewbieGuidePanel prefab. Text uses a bounded rectangle with auto-sizing, replacing the previous vertical ContentSizeFitter. The decoration is a non-interactive Image; existing guide routing, spotlight target and standard Buttons are unchanged.

Validation: PASS. Real scene screenshots at 720x1600 and 720x1280, 7 shared English text cases per viewport, actual guide Button raycast/click, persistence and scene reload: 106 checks. Original preferences restored and ProjectSettings hash unchanged. No APK built. Each viewport starts a fresh scene so the guide target uses the actual launch resolution.
