#!/usr/bin/env python3
"""Deterministic full-canvas export for the ground tile families."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


SOURCE_SIZE = 1254
DELIVERY_SIZE = 512
PERIODIC_BAND = 16
EDGE_HARD = 16
EDGE_FEATHER = 64


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--mode", required=True, choices=("base", "detail"))
    parser.add_argument("--edge-reference", type=Path)
    return parser.parse_args()


def require_size(image: Image.Image, path: Path, expected: int) -> None:
    if image.size != (expected, expected):
        raise ValueError(f"{path} 必须是 {expected}x{expected}，实际为 {image.size}")


def synchronize_opposite_edges(image: Image.Image, hard_band: int, feather_band: int) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    width, height = result.size

    for y in range(height):
        for offset in range(feather_band):
            left = pixels[offset, y]
            right = pixels[width - 1 - offset, y]
            average = tuple((left[channel] + right[channel]) // 2 for channel in range(len(left)))
            weight = 1.0 if offset < hard_band else (feather_band - offset) / (feather_band - hard_band)
            pixels[offset, y] = tuple(
                round(left[channel] * (1.0 - weight) + average[channel] * weight)
                for channel in range(len(left))
            )
            pixels[width - 1 - offset, y] = tuple(
                round(right[channel] * (1.0 - weight) + average[channel] * weight)
                for channel in range(len(right))
            )

    for x in range(width):
        for offset in range(feather_band):
            top = pixels[x, offset]
            bottom = pixels[x, height - 1 - offset]
            average = tuple((top[channel] + bottom[channel]) // 2 for channel in range(len(top)))
            weight = 1.0 if offset < hard_band else (feather_band - offset) / (feather_band - hard_band)
            pixels[x, offset] = tuple(
                round(top[channel] * (1.0 - weight) + average[channel] * weight)
                for channel in range(len(top))
            )
            pixels[x, height - 1 - offset] = tuple(
                round(bottom[channel] * (1.0 - weight) + average[channel] * weight)
                for channel in range(len(bottom))
            )

    return result


def make_periodic_pilot(image: Image.Image) -> Image.Image:
    return synchronize_opposite_edges(image, PERIODIC_BAND, EDGE_FEATHER)


def inherit_pilot_edges(image: Image.Image, reference: Image.Image) -> Image.Image:
    require_size(reference, Path("edge-reference"), DELIVERY_SIZE)
    mask = Image.new("L", image.size, 0)
    pixels = mask.load()
    width, height = image.size

    for y in range(height):
        for x in range(width):
            distance = min(x, y, width - 1 - x, height - 1 - y)
            if distance < EDGE_HARD:
                alpha = 255
            elif distance >= EDGE_FEATHER:
                alpha = 0
            else:
                alpha = round(255 * (EDGE_FEATHER - distance) / (EDGE_FEATHER - EDGE_HARD))
            pixels[x, y] = alpha

    inherited = Image.composite(reference, image, mask)
    inherited = synchronize_opposite_edges(inherited, PERIODIC_BAND, EDGE_FEATHER)
    inherited_pixels = inherited.load()
    reference_pixels = reference.load()

    for y in range(height):
        for x in range(width):
            if min(x, y, width - 1 - x, height - 1 - y) < EDGE_HARD:
                inherited_pixels[x, y] = reference_pixels[x, y]

    return inherited


def clean_transparent_pixels(image: Image.Image) -> Image.Image:
    result = image.copy()
    pixels = result.load()
    width, height = result.size

    for y in range(height):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            if alpha <= 3:
                pixels[x, y] = (0, 0, 0, 0)
            else:
                pixels[x, y] = (red, green, blue, alpha)

    return result


def main() -> None:
    args = parse_args()
    source = Image.open(args.input)
    require_size(source, args.input, SOURCE_SIZE)
    args.output.parent.mkdir(parents=True, exist_ok=True)

    if args.mode == "base":
        delivery = source.convert("RGB").resize(
            (DELIVERY_SIZE, DELIVERY_SIZE),
            Image.Resampling.LANCZOS,
        )
        if args.edge_reference is None:
            delivery = make_periodic_pilot(delivery)
        else:
            reference = Image.open(args.edge_reference).convert("RGB")
            delivery = inherit_pilot_edges(delivery, reference)
    else:
        if source.mode != "RGBA":
            raise ValueError("细节 Tile 必须先完成色键去背并提供 RGBA 源图")
        delivery = source.resize(
            (DELIVERY_SIZE, DELIVERY_SIZE),
            Image.Resampling.LANCZOS,
        )
        delivery = clean_transparent_pixels(delivery)

    delivery.save(args.output, format="PNG", optimize=False, compress_level=9)
    print(f"已导出：{args.output} ({delivery.size[0]}x{delivery.size[1]}, {delivery.mode})")


if __name__ == "__main__":
    main()
