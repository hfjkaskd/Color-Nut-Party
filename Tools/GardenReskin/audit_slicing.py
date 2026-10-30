"""Read-only Sprite/PNG/Image slicing audit; writes documentation only."""
import json
import re
import struct
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"


def relative(path):
    return path.relative_to(ROOT).as_posix()


def read(path):
    return path.read_text(encoding="utf-8-sig", errors="replace")


def scalar(text, key, default=None):
    match = re.search(r"^\s*" + re.escape(key) + r":\s*([^\n]*)", text, re.M)
    return match.group(1).strip() if match else default


def vector(text, key):
    value = scalar(text, key, "")
    return {k: float(v) for k, v in re.findall(r"(\w+): ([+-]?[\d.eE]+)", value)}


def rect(text, key):
    match = re.search(r"^\s*" + re.escape(key) + r":\s*\n(?:\s*serializedVersion: [^\n]*\n)?\s*x: ([^\n]*)\n\s*y: ([^\n]*)\n\s*width: ([^\n]*)\n\s*height: ([^\n]*)", text, re.M)
    return dict(zip(("x", "y", "width", "height"), map(float, match.groups()))) if match else None


def guid(value):
    match = re.search(r"guid: ([0-9a-f]{32})", value or "")
    return match.group(1) if match else None


def file_id(value):
    match = re.search(r"fileID: (-?\d+)", value or "")
    return match.group(1) if match else None


def main():
    guid_paths, metadata, textures, sprites, usage = {}, {}, {}, {}, defaultdict(list)
    for path in ASSETS.rglob("*.meta"):
        content = read(path)
        identity = scalar(content, "guid")
        if identity:
            guid_paths[identity] = Path(str(path)[:-5])
        metadata[relative(path)] = content
    for path in ASSETS.rglob("*.png"):
        header = path.read_bytes()[:24]
        if header[:8] != b"\x89PNG\r\n\x1a\n":
            continue
        width, height = struct.unpack(">II", header[16:24])
        meta = metadata.get(relative(path) + ".meta", "")
        textures[relative(path)] = {"path": relative(path), "width": width, "height": height,
                                   "spriteMode": int(scalar(meta, "spriteMode", "0")),
                                   "spriteBorder": vector(meta, "spriteBorder"),
                                   "guid": scalar(meta, "guid"), "sprites": []}
    for path in ASSETS.rglob("*.asset"):
        content = read(path)
        if "--- !u!213 &" not in content:
            continue
        identity = scalar(metadata.get(relative(path) + ".meta", ""), "guid")
        texture_guid = guid(scalar(content, "texture"))
        texture = relative(guid_paths[texture_guid]) if texture_guid in guid_paths else None
        record = {"path": relative(path), "guid": identity, "texture": texture,
                  "rect": rect(content, "m_Rect"), "textureRect": rect(content, "textureRect"),
                  "border": vector(content, "m_Border"),
                  "pixelsPerUnit": float(scalar(content, "m_PixelsToUnits", "100")), "uses": []}
        sprites[identity] = record
        if texture in textures:
            textures[texture]["sprites"].append(record["path"])

    prefab_sources = defaultdict(list)
    for path in list(ASSETS.rglob("*.prefab")) + list(ASSETS.rglob("*.unity")):
        content = read(path)
        blocks = re.split(r"^--- !u!(\d+) &(-?\d+)[^\n]*\n", content, flags=re.M)
        documents = {blocks[i+1]: (int(blocks[i]), blocks[i+2]) for i in range(1, len(blocks), 3)}
        game_names, transforms, go_to_transform = {}, {}, {}
        for identity, (kind, body) in documents.items():
            if kind == 1:
                game_names[identity] = scalar(body, "m_Name", identity)
            if kind in (4, 224):
                transforms[identity] = body
                go_to_transform[file_id(scalar(body, "m_GameObject"))] = identity
            if kind == 1001:
                source = guid(scalar(body, "m_SourcePrefab"))
                if source in guid_paths:
                    prefab_sources[relative(guid_paths[source])].append(relative(path))

        def node_path(go):
            names, visited = [], set()
            while go and go not in visited:
                visited.add(go)
                names.append(game_names.get(go, go))
                transform = transforms.get(go_to_transform.get(go), "")
                parent = transforms.get(file_id(scalar(transform, "m_Father")), "")
                go = file_id(scalar(parent, "m_GameObject"))
            return "/".join(reversed(names))

        for identity, (kind, body) in documents.items():
            if kind != 114 or not re.search(r"^  m_Sprite:", body, re.M):
                continue
            sprite_guid = guid(scalar(body, "m_Sprite"))
            if not sprite_guid:
                continue
            go = file_id(scalar(body, "m_GameObject"))
            transform = transforms.get(go_to_transform.get(go), "")
            image_type = int(scalar(body, "m_Type", "0"))
            usage[sprite_guid].append({"prefabOrScene": relative(path), "node": node_path(go),
                                      "componentFileId": identity, "imageType": image_type,
                                      "imageTypeName": {0: "Simple", 1: "Sliced", 2: "Tiled", 3: "Filled"}.get(image_type, "unknown"),
                                      "useSpriteMesh": scalar(body, "m_UseSpriteMesh", "0") == "1",
                                      "preserveAspect": scalar(body, "m_PreserveAspect", "0") == "1",
                                      "sizeDelta": vector(transform, "m_SizeDelta"),
                                      "anchorMin": vector(transform, "m_AnchorMin"), "anchorMax": vector(transform, "m_AnchorMax")})
    for identity, record in sprites.items():
        record["uses"] = usage[identity]
        record["variantsOrSceneInstances"] = sorted({variant for item in record["uses"] for variant in prefab_sources[item["prefabOrScene"]]})
    for texture in textures.values():
        texture["directUses"] = usage[texture["guid"]]
        texture["uses"] = texture["directUses"] + [use for record in sprites.values() if record["texture"] == texture["path"] for use in record["uses"]]

    sliced, tiny, long_strip, multi, mismatches, zero_border_sliced = [], [], [], [], [], []
    for record in sprites.values():
        if any(record["border"].values()):
            tex = textures.get(record["texture"], {})
            result = dict(record)
            result["textureSize"] = [tex.get("width"), tex.get("height")]
            result["importerBorder"] = tex.get("spriteBorder")
            border = record["border"]
            bounds = record["rect"]
            result["horizontalCutsInSpritePixels"] = [0, border["x"], bounds["width"] - border["z"], bounds["width"]]
            result["verticalCutsBottomToTopInSpritePixels"] = [0, border["y"], bounds["height"] - border["w"], bounds["height"]]
            result["recommendation"] = "Retain original PNG dimensions, Sprite rect/UV, border and importer metadata. Fit generated art separately into original corner/edge/center regions; stretch only existing center and edge axes. Do not whole-image scale decorative frames into these textures. Decorations must stay inside non-stretched border regions and original alpha silhouette."
            sliced.append(result)
            if tex.get("spriteBorder") != border:
                mismatches.append(record["path"])
    for texture in textures.values():
        if texture["spriteMode"] == 2 or len(texture["sprites"]) > 1:
            multi.append(dict(texture))
        small = min(texture["width"], texture["height"]) <= 40
        atlas = texture["path"].startswith("Assets/Resources/atlas/")
        relevant = atlas or texture["uses"] or texture["sprites"]
        if not relevant:
            continue
        if small:
            result = dict(texture)
            result["recommendation"] = "Keep original dimensions and alpha silhouette. Preserve the semantic shape for small icons/markers, and use tint or low-frequency fill for line/mask assets. Do not downscale a complete decorated frame/button into this small texture: existing Simple/Sliced/Tiled/Filled behavior cannot retain tiny borders or decorations."
            tiny.append(result)
        if max(texture["width"], texture["height"]) / min(texture["width"], texture["height"]) >= 5:
            result = dict(texture)
            result["recommendation"] = "Preserve exact long-strip aspect and original caps/alpha. Keep center visually quiet; Filled images crop art with progress, so avoid words, icons, or decorative landmarks in the fill."
            long_strip.append(result)
        if not any(texture["spriteBorder"].values()):
            sliced_uses = [use for use in texture["uses"] if use["imageType"] in (1, 2)]
            if sliced_uses:
                zero_border_sliced.append({"texture": texture["path"], "size": [texture["width"], texture["height"]],
                                          "uses": sliced_uses,
                                          "recommendation": "Existing Sliced/Tiled Image has no border caps. Preserve its full-image aspect and alpha; adding visible corners to an arbitrary resized image will stretch them. Do not add borders or change Image type during texture-only reskin."})
    atlas_sprites = [record for record in sprites.values() if record["path"].startswith("Assets/Resources/atlas/")]
    report = {"schema": 1, "readOnlyAudit": True,
              "scope": "All exported Sprite assets and PNG metadata; Image references in all Assets prefabs/scenes. Resources/atlas alone misses shared Assets/Sprite borders.",
              "summary": {"exportedSprites": len(sprites), "atlasSprites": len(atlas_sprites),
                          "atlasSpritesWithNonzeroBorder": sum(any(x["border"].values()) for x in atlas_sprites),
                          "pngTextures": len(textures), "preserveSlicedBorders": len(sliced),
                          "tinyTextures": len(tiny), "longStrips": len(long_strip), "multiSpriteTextures": len(multi),
                          "zeroBorderSlicedOrTiledTextures": len(zero_border_sliced),
                          "spriteImporterBorderMismatches": mismatches},
              "imageTypeKey": {"0": "Simple", "1": "Sliced", "2": "Tiled", "3": "Filled"},
              "borderOrder": {"x": "left", "y": "bottom", "z": "right", "w": "top"},
              "smallTextureThreshold": "minimum side <= 40 pixels; includes 34x34 tutorial markers",
              "globalRecommendations": [
                  "Never change PNG size, spriteBorder, m_Border, pixelsPerUnit, Sprite rect/UV, Image type, or RectTransform to accommodate generated artwork.",
                  "For nonzero borders preserve each corner and cap dimension. Redesign only inside the existing nine-slice cells; middle cells remain low-frequency and stretchable.",
                  "Cut coordinates are relative to the original Sprite rect, not necessarily the PNG origin; apply existing rect offsets and preserve exported UV/alpha padding.",
                  "With zero borders, Image type Sliced behaves as a regular full-quad image; changing to a decorated button requires matching the existing texture aspect and target aspect, not inventing nine-slice borders.",
                  "3x3/16x16/32x32 fills and masks are not miniature frame canvases. Keep simple fill, original alpha/shape and color role.",
                  "Exported Sprite assets can retain tight meshes/trimmed rects. Whole replacement art outside the original alpha footprint may clip when useSpriteMesh is enabled; preserve that footprint.",
                  "Generated decorative edges must not contain dynamic text, amount, progress numbers, or currency symbols used as dynamic content.",
                  "Variant/scene-instance references are listed but local inherited overrides are not flattened by this audit; final Unity rendering remains necessary."],
              "mustPreserveSliceCells": sliced, "tinyTextures": tiny,
              "longStripTextures": long_strip, "multipleSpriteTextures": multi,
              "zeroBorderSlicedOrTiledTextures": zero_border_sliced,
              "atlasTextureInventory": [record for record in textures.values() if record["path"].startswith("Assets/Resources/atlas/")],
              "atlasSpriteInventory": atlas_sprites}
    destination = ROOT / "Docs/GardenReskin/slicing-audit.json"
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report["summary"], indent=2))
    for item in sliced:
        print(item["path"], item["textureSize"], item["border"], [(use["node"], use["imageTypeName"]) for use in item["uses"]])
    for item in tiny:
        print("TINY", item["path"], item["width"], item["height"], [(use["node"], use["imageTypeName"]) for use in item["uses"]])


if __name__ == "__main__":
    main()
