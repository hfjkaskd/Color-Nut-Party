# Replay background configuration

The existing right replay control now has its own 85 × 85 cream circular background sprite. Artwork is supplied separately as `Assets/Resources/atlas/mainpanel/replay_bg.png`; this configuration operation did not create or edit that PNG.

## Exact prefab changes

Only the following fields on Image component `114450063812975771`, attached to GameObject `ReplayBg` (`1431740505123533`), were changed in each file:

- `Assets/Resources/prefabs/panels/MainPanel.prefab`
- `Assets/Resources/prefabs/panels/MainPanelBottom.prefab`
- `Assets/Resources/prefabs/panels/MainPanelComplete.prefab`

| Field | Before | After |
| --- | --- | --- |
| `m_Sprite` | `{fileID: 21300000, guid: ab17f362ed05132409da8bc07e1740fe, type: 2}` (`Assets/Sprite/kuang.asset`) | `{fileID: 21300000, guid: 9fa45da9c8c5439bb43c8de5dfc8ac0c, type: 2}` (`replay_bg.asset`) |
| `m_Color` | `{r: 0, g: 0, b: 0, a: 0.61960787}` | `{r: 1, g: 1, b: 1, a: 1}` |

`LocalGameplay.prefab` already nests `MainPanelBottom.prefab`, so it inherits this appearance without an override. The original `MainPanel` replay is hidden by the existing local gameplay setup; its serialized appearance remains consistent for other uses. `MainPanelComplete` retains its matching complete preview.

No GameObject, component, RectTransform, Button, event, material, runtime code or setting-button configuration was changed. The existing ReplayBg stays at `(480, 75.9)` with size `85 × 85`; its child ReplayBtn stays at `(0, -3.3)` with size `59 × 65`. Existing sprite type Simple, `m_UseSpriteMesh: 0` and raycast configuration remain unchanged. The shared `kuang` PNG, sprite and metadata are untouched.

## New asset metadata

| File | Identity |
| --- | --- |
| `Assets/Resources/atlas/mainpanel/replay_bg.png.meta` | Texture GUID `0513d34314154e4297b1d9f4ab2ffd98` |
| `Assets/Resources/atlas/mainpanel/replay_bg.asset` | Native Sprite, file ID `21300000`; references the texture GUID above |
| `Assets/Resources/atlas/mainpanel/replay_bg.asset.meta` | Sprite GUID `9fa45da9c8c5439bb43c8de5dfc8ac0c` |

The project-native 85 × 85 `kuang` texture importer and Sprite serialization were used as format templates. The new sprite has its own name, texture GUID and render-data identity. It retains an 85 × 85 rectangle, centered pivot, 100 pixels per unit and zero borders. The texture importer uses alpha transparency, bilinear filtering, clamp addressing and no mipmaps.

## Reproduce and verify

From the project root:

```powershell
python Docs/GardenReskin/bottom-toolbar-match/configure_replay_background.py --apply
python Docs/GardenReskin/bottom-toolbar-match/configure_replay_background.py
```

The first command applies the same scoped configuration; the second is read-only verification. Both check GUID ownership. The script asserts that normalizing just the two allowed Image fields makes each prefab byte-for-byte equivalent to its pre-operation state, preserving unrelated existing changes and newline style. It also checks block counts and the PNG dimensions when artwork is present. The first application hashes are saved in `replay-background-config-audit.json`.

Unity was not opened, closed or controlled for this operation. Texture import and runtime visual checks remain part of the artwork integration step.
