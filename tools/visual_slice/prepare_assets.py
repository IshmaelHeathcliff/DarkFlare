from __future__ import annotations

import argparse
import hashlib
import io
import json
import re
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Docs" / "docs" / "assets" / "visual-style" / "source"
ART = ROOT / "Assets" / "Art"
LAYOUT_PATH = Path(__file__).with_name("asset_pipeline_manifest.json")
ALPHA_THRESHOLD = 8

ACTOR_CANVAS = (128, 128)
ACTOR_PPU = 64.0
ACTOR_PIVOT_PIXELS = (64.0, 12.0)
UI_ICON_CANVAS = (96, 96)
UI_ICON_EXPORT_SCALE = 0.30
WORLD_PROP_CANVAS = (384, 384)


def standalone_asset(
    path: str,
    canvas: tuple[int, int],
    pixels_per_unit: float,
    *,
    wrap_mode: str = "Clamp",
    alpha_required: bool = True,
    production: bool = True,
) -> dict[str, object]:
    return {
        "path": path,
        "canvasWidth": canvas[0],
        "canvasHeight": canvas[1],
        "pixelsPerUnit": pixels_per_unit,
        "pivotX": 0.5,
        "pivotY": 0.5,
        "borderLeft": 0.0,
        "borderBottom": 0.0,
        "borderRight": 0.0,
        "borderTop": 0.0,
        "wrapMode": wrap_mode,
        "alphaRequired": alpha_required,
        "production": production,
    }


def build_standalone_assets() -> list[dict[str, object]]:
    assets = [
        standalone_asset(
            "Assets/Art/Sprites/Effects/effect_loot_rarity_ring.png",
            (96, 96),
            100.0,
        ),
        standalone_asset(
            "Assets/Art/Sprites/Environment/VisualSlice/ground_slice.png",
            (512, 512),
            64.0,
            wrap_mode="Repeat",
            alpha_required=False,
        ),
        standalone_asset(
            "Assets/Art/Sprites/UI/Phase5/world_health_bar_background.png",
            (64, 8),
            64.0,
        ),
        standalone_asset(
            "Assets/Art/Sprites/UI/Phase5/world_health_bar_fill.png",
            (64, 8),
            64.0,
        ),
        standalone_asset(
            "Assets/Art/Textures/Prototype/PrototypeSquare.png",
            (64, 64),
            100.0,
            alpha_required=False,
            production=False,
        ),
    ]

    for name in (
        "armor_leather",
        "armor_plate",
        "ring_iron",
        "ring_jade",
        "ring_obsidian",
        "weapon_greatsword",
        "weapon_war_axe",
    ):
        assets.append(
            standalone_asset(
                f"Assets/Art/Sprites/Items/Equipment/{name}.png",
                (96, 96),
                64.0,
            )
        )

    return assets


@dataclass(frozen=True)
class SheetDefinition:
    id: str
    asset_path: str
    source_path: str
    kind: str


SHEET_DEFINITIONS = (
    SheetDefinition(
        "player",
        "Assets/Art/SpriteSheets/Phase5/player_sheet.png",
        "Docs/docs/assets/visual-style/source/player-sheet-alpha.png",
        "actor",
    ),
    SheetDefinition(
        "monster_basic",
        "Assets/Art/SpriteSheets/Phase5/monster_basic_sheet.png",
        "Docs/docs/assets/visual-style/source/monster-sheet-alpha.png",
        "actor",
    ),
    SheetDefinition(
        "monster_swift",
        "Assets/Art/SpriteSheets/Phase5/monster_swift_sheet.png",
        "Docs/docs/assets/visual-style/source/phase-5/razor-hound-sheet-transparent.png",
        "actor",
    ),
    SheetDefinition(
        "monster_heavy",
        "Assets/Art/SpriteSheets/Phase5/monster_heavy_sheet.png",
        "Docs/docs/assets/visual-style/source/phase-5/iron-husk-sheet-transparent.png",
        "actor",
    ),
    SheetDefinition(
        "merchant",
        "Assets/Art/SpriteSheets/Phase5/merchant_idle_sheet.png",
        "Docs/docs/assets/visual-style/source/phase-5/merchant-idle-sheet-transparent.png",
        "actor",
    ),
    SheetDefinition(
        "ui_icons",
        "Assets/Art/SpriteSheets/Phase5/ui_icons_sheet.png",
        "Docs/docs/assets/visual-style/source/phase-5/ui-icons-sheet-transparent.png",
        "ui_icon",
    ),
    SheetDefinition(
        "ui_frames",
        "Assets/Art/SpriteSheets/Phase5/ui_frames_sheet.png",
        "Docs/docs/assets/visual-style/source/ui-sheet-alpha.png",
        "ui_frame",
    ),
    SheetDefinition(
        "world_props",
        "Assets/Art/SpriteSheets/Phase5/world_props_sheet.png",
        "Docs/docs/assets/visual-style/source/phase-5/world-props-sheet-transparent.png",
        "world_prop",
    ),
)

UI_ICON_NAMES = {
    "ui_icons_sheet_0": "ui_icon_health",
    "ui_icons_sheet_1": "ui_icon_gold",
    "ui_icons_sheet_2": "ui_icon_weapon",
    "ui_icons_sheet_3": "ui_icon_armor",
    "ui_icons_sheet_4": "ui_icon_ring",
    "ui_icons_sheet_5": "ui_icon_inventory",
    "ui_icons_sheet_6": "ui_icon_shop",
    "ui_icons_sheet_7": "ui_icon_crafting",
    "ui_icons_sheet_8": "ui_icon_close",
    "ui_icons_sheet_9": "ui_icon_back",
    "ui_icons_sheet_10": "ui_icon_weapon_empty",
    "ui_icons_sheet_11": "ui_icon_missing",
}

UI_FRAME_NAMES = {
    "ui_frames_sheet_0": "ui_inventory_panel_base",
    "ui_frames_sheet_1": "ui_inventory_slot_default",
    "ui_frames_sheet_2": "ui_inventory_slot_focused",
    "ui_frames_sheet_3": "ui_inventory_slot_disabled",
}

UI_FRAME_BORDERS = {
    "ui_frames_sheet_0": 98,
    "ui_frames_sheet_1": 78,
    "ui_frames_sheet_2": 157,
    "ui_frames_sheet_3": 157,
}

WORLD_PROP_NAMES = {
    "world_props_sheet_0": "world_prop_ground_cracked",
    "world_props_sheet_1": "world_prop_ground_dirt",
    "world_props_sheet_2": "world_prop_ground_stone_path",
    "world_props_sheet_3": "world_prop_ruined_wall",
    "world_props_sheet_5": "world_prop_rock_small",
    "world_props_sheet_6": "world_prop_rock_large",
    "world_props_sheet_8": "world_prop_dead_tree",
    "world_props_sheet_10": "world_prop_tattered_banner",
    "world_props_sheet_12": "world_prop_supplies",
    "world_props_sheet_13": "world_prop_brazier",
    "world_props_sheet_14": "world_prop_crafting_station",
    "world_props_sheet_16": "world_prop_tent",
}

EXPECTED_SPRITE_COUNTS = {
    "player": 12,
    "monster_basic": 12,
    "monster_swift": 12,
    "monster_heavy": 12,
    "merchant": 4,
    "ui_icons": 12,
    "ui_frames": 4,
    "world_props": 12,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate the alpha single-Sprite production assets deterministically."
    )
    parser.add_argument(
        "--capture-layout",
        action="store_true",
        help="Rebuild the immutable legacy GUID/fileID/Rect/Pivot manifest from current .meta files.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Validate inputs and outputs without writing PNG or manifest files.",
    )
    return parser.parse_args()


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def parse_scalar(content: str, pattern: str, label: str) -> str:
    match = re.search(pattern, content, re.MULTILINE)

    if match is None:
        raise ValueError(f"Missing {label}")

    return match.group(1).strip()


def parse_sprite_entries(content: str) -> list[dict[str, object]]:
    sprite_section_match = re.search(
        r"^  spriteSheet:\r?\n(?P<body>.*?)(?=^  mipmapLimitGroupName:)",
        content,
        re.MULTILINE | re.DOTALL,
    )

    if sprite_section_match is None:
        raise ValueError("TextureImporter spriteSheet section is missing")

    body = sprite_section_match.group("body")
    entry_pattern = re.compile(
        r"^    - serializedVersion: 2\r?\n"
        r"      name: (?P<name>[^\r\n]+)\r?\n"
        r"      rect:\r?\n"
        r"        serializedVersion: 2\r?\n"
        r"        x: (?P<x>[^\r\n]+)\r?\n"
        r"        y: (?P<y>[^\r\n]+)\r?\n"
        r"        width: (?P<width>[^\r\n]+)\r?\n"
        r"        height: (?P<height>[^\r\n]+)\r?\n"
        r"      alignment: (?P<alignment>[^\r\n]+)\r?\n"
        r"      pivot: \{x: (?P<pivot_x>[^,]+), y: (?P<pivot_y>[^}]+)\}"
        r".*?^      internalID: (?P<local_id>-?\d+)\r?$",
        re.MULTILINE | re.DOTALL,
    )
    entries: list[dict[str, object]] = []

    for match in entry_pattern.finditer(body):
        entries.append(
            {
                "legacySpriteName": match.group("name"),
                "legacyLocalId": int(match.group("local_id")),
                "rect": {
                    "x": int(float(match.group("x"))),
                    "y": int(float(match.group("y"))),
                    "width": int(float(match.group("width"))),
                    "height": int(float(match.group("height"))),
                },
                "pivot": {
                    "x": float(match.group("pivot_x")),
                    "y": float(match.group("pivot_y")),
                },
            }
        )

    return entries


def actor_target(sheet_id: str, legacy_name: str) -> str:
    if sheet_id == "player":
        root = "Assets/Art/Sprites/Characters/Player"
        prefix = "actor_player"
    elif sheet_id == "monster_basic":
        root = "Assets/Art/Sprites/Monsters/Basic"
        prefix = "monster_basic"
    elif sheet_id == "monster_swift":
        root = "Assets/Art/Sprites/Monsters/Swift"
        prefix = "monster_swift"
    elif sheet_id == "monster_heavy":
        root = "Assets/Art/Sprites/Monsters/Heavy"
        prefix = "monster_heavy"
    elif sheet_id == "merchant":
        root = "Assets/Art/Sprites/NPCs/Merchant"
        prefix = "npc_merchant"
    else:
        raise ValueError(f"Unknown actor sheet: {sheet_id}")

    if sheet_id in {"player", "monster_basic"}:
        suffix = legacy_name.removeprefix(
            "player_" if sheet_id == "player" else "monster_basic_"
        )

        if suffix == "attack_anticipation_se":
            normalized = "attack_se_00"
        elif suffix == "attack_release_se":
            normalized = "attack_se_01"
        elif suffix == "hit_se":
            normalized = "hit_se_00"
        elif suffix == "death_se":
            normalized = "death_se_00"
        else:
            normalized = re.sub(r"_(\d+)$", lambda match: f"_{int(match.group(1)):02d}", suffix)
    elif sheet_id in {"monster_swift", "monster_heavy"}:
        source_prefix = "monster_swift_" if sheet_id == "monster_swift" else "monster_heavy_"
        suffix = legacy_name.removeprefix(source_prefix)
        state, index = suffix.rsplit("_", 1)
        normalized = f"{state}_se_{int(index):02d}"
    else:
        index = int(legacy_name.rsplit("_", 1)[1])
        normalized = f"idle_se_{index:02d}"

    return f"{root}/{prefix}_{normalized}.png"


def target_for(definition: SheetDefinition, legacy_name: str) -> tuple[str, str]:
    if definition.kind == "actor":
        return "actors", actor_target(definition.id, legacy_name)

    if definition.kind == "ui_icon":
        semantic_name = UI_ICON_NAMES.get(legacy_name)

        if semantic_name is None:
            raise ValueError(f"Unknown UI icon slice: {legacy_name}")

        return "ui_icons", f"Assets/Art/Sprites/UI/Icons/{semantic_name}.png"

    if definition.kind == "ui_frame":
        semantic_name = UI_FRAME_NAMES.get(legacy_name)

        if semantic_name is None:
            raise ValueError(f"Unknown UI frame slice: {legacy_name}")

        return legacy_name, f"Assets/Art/Sprites/UI/Frames/{semantic_name}.png"

    if definition.kind == "world_prop":
        semantic_name = WORLD_PROP_NAMES.get(legacy_name)

        if semantic_name is None:
            raise ValueError(f"Unknown world prop slice: {legacy_name}")

        return "world_props", f"Assets/Art/Sprites/Environment/WorldProps/{semantic_name}.png"

    raise ValueError(f"Unknown sheet kind: {definition.kind}")


def build_families() -> list[dict[str, object]]:
    families: list[dict[str, object]] = [
        {
            "id": "actors",
            "canvasWidth": ACTOR_CANVAS[0],
            "canvasHeight": ACTOR_CANVAS[1],
            "pixelsPerUnit": ACTOR_PPU,
            "pivotX": ACTOR_PIVOT_PIXELS[0] / ACTOR_CANVAS[0],
            "pivotY": ACTOR_PIVOT_PIXELS[1] / ACTOR_CANVAS[1],
            "borderLeft": 0.0,
            "borderBottom": 0.0,
            "borderRight": 0.0,
            "borderTop": 0.0,
        },
        {
            "id": "ui_icons",
            "canvasWidth": UI_ICON_CANVAS[0],
            "canvasHeight": UI_ICON_CANVAS[1],
            "pixelsPerUnit": 64.0,
            "pivotX": 0.5,
            "pivotY": 0.5,
            "borderLeft": 0.0,
            "borderBottom": 0.0,
            "borderRight": 0.0,
            "borderTop": 0.0,
        },
        {
            "id": "world_props",
            "canvasWidth": WORLD_PROP_CANVAS[0],
            "canvasHeight": WORLD_PROP_CANVAS[1],
            "pixelsPerUnit": 181.0,
            "pivotX": 0.5,
            "pivotY": 0.5,
            "borderLeft": 0.0,
            "borderBottom": 0.0,
            "borderRight": 0.0,
            "borderTop": 0.0,
        },
    ]

    for legacy_name, semantic_name in UI_FRAME_NAMES.items():
        border = float(UI_FRAME_BORDERS[legacy_name])
        families.append(
            {
                "id": legacy_name,
                "canvasWidth": 0,
                "canvasHeight": 0,
                "pixelsPerUnit": 100.0,
                "pivotX": 0.5,
                "pivotY": 0.5,
                "borderLeft": border,
                "borderBottom": border,
                "borderRight": border,
                "borderTop": border,
                "semanticName": semantic_name,
            }
        )

    return families


def capture_layout() -> dict[str, object]:
    sheets: list[dict[str, object]] = []
    mappings: list[dict[str, object]] = []

    for definition in SHEET_DEFINITIONS:
        asset_path = ROOT / definition.asset_path
        meta_path = Path(f"{asset_path}.meta")

        if not asset_path.is_file() or not meta_path.is_file():
            raise FileNotFoundError(
                f"Cannot capture {definition.id}; legacy asset or .meta is missing: {asset_path}"
            )

        content = read_text(meta_path)
        guid = parse_scalar(content, r"^guid: ([^\r\n]+)", f"{definition.id} GUID")
        sprite_mode = int(
            parse_scalar(content, r"^  spriteMode: ([^\r\n]+)", f"{definition.id} sprite mode")
        )
        old_ppu = float(
            parse_scalar(
                content,
                r"^  spritePixelsToUnits: ([^\r\n]+)",
                f"{definition.id} PPU",
            )
        )

        if sprite_mode != 2:
            raise ValueError(f"{definition.asset_path} must be Sprite Mode Multiple during capture")

        entries = parse_sprite_entries(content)
        expected_count = EXPECTED_SPRITE_COUNTS[definition.id]

        if len(entries) != expected_count:
            raise ValueError(
                f"{definition.id} expected {expected_count} active slices, found {len(entries)}"
            )

        source_path = ROOT / definition.source_path

        if not source_path.is_file():
            raise FileNotFoundError(source_path)

        source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
        runtime_hash = hashlib.sha256(asset_path.read_bytes()).hexdigest()

        if source_hash != runtime_hash:
            raise ValueError(
                f"Immutable source differs from the runtime legacy sheet: {definition.asset_path}"
            )

        sheets.append(
            {
                "id": definition.id,
                "kind": definition.kind,
                "assetPath": definition.asset_path,
                "sourcePath": definition.source_path,
                "guid": guid,
                "oldPixelsPerUnit": old_ppu,
                "expectedSpriteCount": expected_count,
                "sourceSha256": source_hash,
            }
        )

        for entry in entries:
            family_id, target_path = target_for(definition, str(entry["legacySpriteName"]))
            mapping = {
                "sheetId": definition.id,
                "familyId": family_id,
                "legacySheetGuid": guid,
                "legacyLocalId": entry["legacyLocalId"],
                "legacySpriteName": entry["legacySpriteName"],
                "rect": entry["rect"],
                "pivot": entry["pivot"],
                "targetPath": target_path,
                "allowUnreferenced": (
                    entry["legacySpriteName"] == "ui_icons_sheet_9"
                    or entry["legacySpriteName"]
                    in {
                        "world_props_sheet_0",
                        "world_props_sheet_1",
                        "world_props_sheet_2",
                        "world_props_sheet_3",
                    }
                ),
            }
            mappings.append(mapping)

    if len(mappings) != 80:
        raise ValueError(f"Expected 80 mappings, found {len(mappings)}")

    target_paths = [str(mapping["targetPath"]) for mapping in mappings]
    old_keys = [
        (str(mapping["legacySheetGuid"]), int(mapping["legacyLocalId"]))
        for mapping in mappings
    ]

    if len(target_paths) != len(set(target_paths)):
        raise ValueError("Target paths are not unique")

    if len(old_keys) != len(set(old_keys)):
        raise ValueError("Legacy GUID/fileID keys are not unique")

    return {
        "schemaVersion": 1,
        "expectedMappingCount": 80,
        "families": build_families(),
        "standaloneAssets": build_standalone_assets(),
        "legacySheets": sheets,
        "mappings": mappings,
    }


def load_layout(capture: bool) -> dict[str, object]:
    if capture or not LAYOUT_PATH.is_file():
        return capture_layout()

    layout = json.loads(read_text(LAYOUT_PATH))

    if layout.get("schemaVersion") != 1:
        raise ValueError("Unsupported asset pipeline manifest schema")

    if len(layout.get("mappings", [])) != layout.get("expectedMappingCount"):
        raise ValueError("Asset pipeline manifest mapping count is invalid")

    layout["standaloneAssets"] = build_standalone_assets()

    return layout


def crop_unity_rect(image: Image.Image, rect: dict[str, int]) -> Image.Image:
    left = int(rect["x"])
    bottom = int(rect["y"])
    width = int(rect["width"])
    height = int(rect["height"])
    top = image.height - bottom - height

    if left < 0 or top < 0 or left + width > image.width or top + height > image.height:
        raise ValueError(f"Slice is outside source canvas: {rect}")

    return image.crop((left, top, left + width, top + height)).convert("RGBA")


def affine_export(
    source: Image.Image,
    canvas: tuple[int, int],
    scale: float,
    left: float,
    top: float,
) -> Image.Image:
    inverse_scale = 1.0 / scale
    return source.transform(
        canvas,
        Image.Transform.AFFINE,
        (
            inverse_scale,
            0.0,
            -left * inverse_scale,
            0.0,
            inverse_scale,
            -top * inverse_scale,
        ),
        resample=Image.Resampling.BICUBIC,
        fillcolor=(0, 0, 0, 0),
    )


def generate_actor(
    source: Image.Image,
    mapping: dict[str, object],
    old_ppu: float,
) -> Image.Image:
    rect = mapping["rect"]
    pivot = mapping["pivot"]
    frame = crop_unity_rect(source, rect)
    scale = ACTOR_PPU / old_ppu
    scaled_width = frame.width * scale
    scaled_height = frame.height * scale
    pivot_x = float(pivot["x"]) * scaled_width
    pivot_y_from_bottom = float(pivot["y"]) * scaled_height
    left = ACTOR_PIVOT_PIXELS[0] - pivot_x
    bottom = ACTOR_PIVOT_PIXELS[1] - pivot_y_from_bottom
    top = ACTOR_CANVAS[1] - bottom - scaled_height
    return affine_export(frame, ACTOR_CANVAS, scale, left, top)


def alpha_weighted_center(image: Image.Image) -> tuple[float, float]:
    alpha = image.convert("RGBA").getchannel("A")
    values = list(alpha.get_flattened_data())
    total = sum(values)

    if total <= 0:
        raise ValueError("Image has no visible pixels")

    center_x = sum((index % image.width) * value for index, value in enumerate(values)) / total
    center_y = sum((index // image.width) * value for index, value in enumerate(values)) / total
    return center_x, center_y


def generate_ui_icon(source: Image.Image, mapping: dict[str, object]) -> Image.Image:
    icon = crop_unity_rect(source, mapping["rect"])
    center_x, center_y = alpha_weighted_center(icon)
    target_x = UI_ICON_CANVAS[0] / 2.0
    target_y = UI_ICON_CANVAS[1] / 2.0
    left = target_x - center_x * UI_ICON_EXPORT_SCALE
    top = target_y - center_y * UI_ICON_EXPORT_SCALE
    return affine_export(
        icon,
        UI_ICON_CANVAS,
        UI_ICON_EXPORT_SCALE,
        left,
        top,
    )


def generate_ui_frame(source: Image.Image, mapping: dict[str, object]) -> Image.Image:
    return crop_unity_rect(source, mapping["rect"])


def generate_world_prop(source: Image.Image, mapping: dict[str, object]) -> Image.Image:
    prop = crop_unity_rect(source, mapping["rect"])
    result = Image.new("RGBA", WORLD_PROP_CANVAS, (0, 0, 0, 0))
    left = (WORLD_PROP_CANVAS[0] - prop.width) // 2
    top = (WORLD_PROP_CANVAS[1] - prop.height) // 2
    result.alpha_composite(prop, (left, top))
    return result


def png_bytes(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", compress_level=9, optimize=False)
    return buffer.getvalue()


def write_bytes_if_changed(path: Path, content: bytes, check: bool) -> bool:
    current = path.read_bytes() if path.is_file() else None

    if current == content:
        return False

    if check:
        raise ValueError(f"Generated file is stale or missing: {path.relative_to(ROOT)}")

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return True


def write_json_if_changed(path: Path, data: dict[str, object], check: bool) -> bool:
    content = (json.dumps(data, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return write_bytes_if_changed(path, content, check)


def process_sheet_assets(
    layout: dict[str, object],
    check: bool,
) -> tuple[int, list[Path]]:
    changed = 0
    output_paths: list[Path] = []
    sheets = {sheet["id"]: sheet for sheet in layout["legacySheets"]}
    source_images: dict[str, Image.Image] = {}

    for sheet_id, sheet in sheets.items():
        source_path = ROOT / sheet["sourcePath"]
        source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()

        if source_hash != sheet["sourceSha256"]:
            raise ValueError(f"Immutable source hash changed: {sheet['sourcePath']}")

        source_images[sheet_id] = Image.open(source_path).convert("RGBA")

    for mapping in layout["mappings"]:
        sheet = sheets[mapping["sheetId"]]
        source = source_images[mapping["sheetId"]]

        if sheet["kind"] == "actor":
            output = generate_actor(source, mapping, float(sheet["oldPixelsPerUnit"]))
        elif sheet["kind"] == "ui_icon":
            output = generate_ui_icon(source, mapping)
        elif sheet["kind"] == "ui_frame":
            output = generate_ui_frame(source, mapping)
        elif sheet["kind"] == "world_prop":
            output = generate_world_prop(source, mapping)
        else:
            raise ValueError(f"Unknown sheet kind: {sheet['kind']}")

        output_path = ROOT / mapping["targetPath"]
        output_content = png_bytes(output)
        changed += int(write_bytes_if_changed(output_path, output_content, check))
        mapping["targetSha256"] = hashlib.sha256(output_content).hexdigest()
        output_paths.append(output_path)

        if sheet["kind"] == "ui_frame":
            family = next(
                family for family in layout["families"] if family["id"] == mapping["familyId"]
            )
            family["canvasWidth"] = output.width
            family["canvasHeight"] = output.height

    return changed, output_paths


def pad_existing_image(
    path: Path,
    source_size: tuple[int, int],
    target_size: tuple[int, int],
    check: bool,
) -> bool:
    image = Image.open(path).convert("RGBA")

    if image.size == target_size:
        return False

    if image.size != source_size:
        raise ValueError(f"Unexpected source canvas for {path.relative_to(ROOT)}: {image.size}")

    output = Image.new("RGBA", target_size, (0, 0, 0, 0))
    left = (target_size[0] - source_size[0]) // 2
    top = (target_size[1] - source_size[1]) // 2
    output.alpha_composite(image, (left, top))
    return write_bytes_if_changed(path, png_bytes(output), check)


def center_existing_icon(path: Path, check: bool) -> bool:
    image = Image.open(path).convert("RGBA")

    if image.size != (96, 96):
        raise ValueError(f"Equipment icon must be 96x96: {path.relative_to(ROOT)}")

    center_x, center_y = alpha_weighted_center(image)
    shift_x = round(48.0 - center_x)
    shift_y = round(48.0 - center_y)

    if shift_x == 0 and shift_y == 0:
        return False

    output = Image.new("RGBA", image.size, (0, 0, 0, 0))
    output.alpha_composite(image, (shift_x, shift_y))
    return write_bytes_if_changed(path, png_bytes(output), check)


def process_existing_singles(check: bool) -> int:
    changed = 0
    changed += int(
        pad_existing_image(
            ART / "Sprites" / "UI" / "Phase5" / "world_health_bar_fill.png",
            (62, 6),
            (64, 8),
            check,
        )
    )
    changed += int(
        center_existing_icon(
            ART / "Sprites" / "Items" / "Equipment" / "weapon_war_axe.png",
            check,
        )
    )
    return changed


def validate_alpha(path: Path, alpha_required: bool = True) -> None:
    image = Image.open(path)

    if alpha_required and image.mode not in {"RGBA", "LA"}:
        raise ValueError(f"Alpha channel is required: {path.relative_to(ROOT)}")

    if not alpha_required:
        return

    alpha = image.convert("RGBA").getchannel("A")
    bounds = alpha.point(lambda value: 255 if value > ALPHA_THRESHOLD else 0).getbbox()

    if bounds is None:
        raise ValueError(f"No visible pixels: {path.relative_to(ROOT)}")


def validate_outputs(layout: dict[str, object], outputs: list[Path]) -> None:
    if len(outputs) != 80 or len(set(outputs)) != 80:
        raise ValueError("Expected 80 unique generated sheet outputs")

    family_by_id = {family["id"]: family for family in layout["families"]}

    for mapping in layout["mappings"]:
        path = ROOT / mapping["targetPath"]
        family = family_by_id[mapping["familyId"]]
        image = Image.open(path)
        expected_size = (int(family["canvasWidth"]), int(family["canvasHeight"]))

        if image.size != expected_size:
            raise ValueError(
                f"Canvas mismatch for {mapping['targetPath']}: {image.size} != {expected_size}"
            )

        validate_alpha(path)

    for contract in layout["standaloneAssets"]:
        path = ROOT / contract["path"]

        if not path.is_file():
            raise FileNotFoundError(path)

        expected_size = (
            int(contract["canvasWidth"]),
            int(contract["canvasHeight"]),
        )
        image = Image.open(path)

        if image.size != expected_size:
            raise ValueError(
                f"Canvas mismatch for {contract['path']}: {image.size} != {expected_size}"
            )

        validate_alpha(path, bool(contract["alphaRequired"]))
        contract["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    args = parse_args()
    layout = load_layout(args.capture_layout)
    changed = 0

    if args.check and not LAYOUT_PATH.is_file():
        raise FileNotFoundError(LAYOUT_PATH)

    sheet_changes, outputs = process_sheet_assets(layout, args.check)
    changed += sheet_changes
    changed += process_existing_singles(args.check)

    validate_outputs(layout, outputs)

    if not args.check:
        changed += int(write_json_if_changed(LAYOUT_PATH, layout, False))

    print(
        f"ASSET_PIPELINE_OK generated={len(outputs)} changed={changed} "
        f"manifest={LAYOUT_PATH.relative_to(ROOT).as_posix()}"
    )


if __name__ == "__main__":
    main()
