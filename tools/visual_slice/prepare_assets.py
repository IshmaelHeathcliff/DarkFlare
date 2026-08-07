import shutil
from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs" / "assets" / "visual-style" / "source"
ART = ROOT / "Assets" / "Art" / "Sprites"


def ensure_directories() -> None:
    paths = [
        ART / "Environment" / "VisualSlice",
        ART / "Effects",
        ART / "Items" / "Equipment",
        ART.parent / "SpriteSheets" / "Phase5",
    ]

    for path in paths:
        path.mkdir(parents=True, exist_ok=True)


def normalize_cutout(
    image: Image.Image,
    size: tuple[int, int],
    padding: int,
    bottom_anchor: bool = False,
    mirror: bool = False,
) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    bounds = alpha.getbbox()

    if bounds is None:
        raise ValueError("Cutout has no opaque pixels")

    subject = rgba.crop(bounds)
    subject = remove_alpha_islands(subject)
    cleaned_bounds = subject.getchannel("A").getbbox()

    if cleaned_bounds is None:
        raise ValueError("Cutout became empty after alpha cleanup")

    subject = subject.crop(cleaned_bounds)

    if mirror:
        subject = ImageOps.mirror(subject)

    available_width = size[0] - padding * 2
    available_height = size[1] - padding * 2
    scale = min(available_width / subject.width, available_height / subject.height)
    target_width = max(1, round(subject.width * scale))
    target_height = max(1, round(subject.height * scale))
    subject = subject.resize((target_width, target_height), Image.Resampling.LANCZOS)

    result = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - target_width) // 2
    y = size[1] - padding - target_height if bottom_anchor else (size[1] - target_height) // 2
    result.alpha_composite(subject, (x, y))
    return result


def remove_alpha_islands(image: Image.Image, minimum_relative_area: float = 0.02) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    width, height = alpha.size
    values = bytearray(alpha.tobytes())
    visited = bytearray(width * height)
    components: list[list[int]] = []

    for start in range(width * height):
        if visited[start] or values[start] <= 8:
            continue

        visited[start] = 1
        stack = [start]
        component: list[int] = []

        while stack:
            index = stack.pop()
            component.append(index)
            x = index % width
            y = index // width

            for offset_y in (-1, 0, 1):
                neighbor_y = y + offset_y

                if neighbor_y < 0 or neighbor_y >= height:
                    continue

                for offset_x in (-1, 0, 1):
                    if offset_x == 0 and offset_y == 0:
                        continue

                    neighbor_x = x + offset_x

                    if neighbor_x < 0 or neighbor_x >= width:
                        continue

                    neighbor = neighbor_y * width + neighbor_x

                    if not visited[neighbor] and values[neighbor] > 8:
                        visited[neighbor] = 1
                        stack.append(neighbor)

        components.append(component)

    if not components:
        return rgba

    largest_area = max(len(component) for component in components)
    minimum_area = max(4, round(largest_area * minimum_relative_area))

    for component in components:
        if len(component) >= minimum_area:
            continue

        for index in component:
            values[index] = 0

    rgba.putalpha(Image.frombytes("L", (width, height), bytes(values)))
    return rgba


def save_ground() -> Path:
    source = Image.open(SOURCE / "ground-source.png").convert("RGB")
    square_size = min(source.size)
    left = (source.width - square_size) // 2
    top = (source.height - square_size) // 2
    tile = source.crop((left, top, left + square_size, top + square_size))
    tile = tile.resize((256, 256), Image.Resampling.LANCZOS)

    result = Image.new("RGB", (512, 512))
    result.paste(tile, (0, 0))
    result.paste(ImageOps.mirror(tile), (256, 0))
    result.paste(ImageOps.flip(tile), (0, 256))
    result.paste(ImageOps.flip(ImageOps.mirror(tile)), (256, 256))

    output = ART / "Environment" / "VisualSlice" / "ground_slice.png"
    result.save(output)
    return output


def save_single_cutouts() -> list[Path]:
    projectile_source = Image.open(SOURCE / "projectile-alpha.png")
    projectile = normalize_cutout(projectile_source, (64, 64), 4, mirror=True)
    projectile_path = ART / "Effects" / "projectile_arcane.png"
    projectile.save(projectile_path)

    return [projectile_path]


def copy_retained_sheets() -> list[Path]:
    sheet_root = ART.parent / "SpriteSheets" / "Phase5"
    definitions = [
        (SOURCE / "player-sheet-alpha.png", sheet_root / "player_sheet.png"),
        (SOURCE / "monster-sheet-alpha.png", sheet_root / "monster_basic_sheet.png"),
        (SOURCE / "phase-5" / "ui-icons-sheet-transparent.png", sheet_root / "ui_icons_sheet.png"),
        (SOURCE / "ui-sheet-alpha.png", sheet_root / "ui_frames_sheet.png"),
    ]
    outputs: list[Path] = []

    for source, output in definitions:
        shutil.copy2(source, output)
        outputs.append(output)

    return outputs


def validate_alpha(path: Path) -> tuple[float, int]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    corners = [
        alpha.getpixel((0, 0)),
        alpha.getpixel((image.width - 1, 0)),
        alpha.getpixel((0, image.height - 1)),
        alpha.getpixel((image.width - 1, image.height - 1)),
    ]

    if max(corners) > 8:
        raise ValueError(f"{path} has opaque corners: {corners}")

    opaque_pixels = sum(1 for value in alpha.get_flattened_data() if value > 8)
    coverage = opaque_pixels / (image.width * image.height)

    if coverage < 0.02 or coverage > 0.95:
        raise ValueError(f"{path} has implausible alpha coverage: {coverage:.3f}")

    return coverage, max(corners)


def main() -> None:
    ensure_directories()
    outputs = [save_ground()]
    outputs.extend(copy_retained_sheets())
    outputs.extend(save_single_cutouts())

    print(f"Prepared {len(outputs)} visual slice assets")

    for path in outputs:
        if path.suffix.lower() == ".png" and path.name != "ground_slice.png":
            coverage, corner_alpha = validate_alpha(path)
            relative_path = path.relative_to(ROOT)
            print(f"ALPHA_OK {relative_path} coverage={coverage:.3f} corner_max={corner_alpha}")


if __name__ == "__main__":
    main()
