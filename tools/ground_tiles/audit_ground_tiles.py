#!/usr/bin/env python3
"""Ground-specific seam and family contract audit."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--json-report", type=Path)
    return parser.parse_args()


def maximum_difference(first: Image.Image, second: Image.Image) -> int:
    difference = ImageChops.difference(first, second)
    extrema = difference.getextrema()
    return max(channel[1] for channel in extrema)


def alpha_metrics(image: Image.Image) -> dict[str, object]:
    alpha = image.getchannel("A")
    bounding_box = alpha.getbbox()
    if bounding_box is None:
        return {"coverage": 0.0, "bbox": None, "center": None, "margins": None}

    width, height = image.size
    alpha_values = list(alpha.get_flattened_data())
    weight = sum(alpha_values)
    center_x = sum((index % width) * value for index, value in enumerate(alpha_values)) / weight
    center_y = sum((index // width) * value for index, value in enumerate(alpha_values)) / weight
    left, top, right, bottom = bounding_box
    visible_pixels = sum(1 for value in alpha_values if value > 8)
    return {
        "coverage": visible_pixels / (width * height),
        "bbox": [left, top, right, bottom],
        "center": [center_x, center_y],
        "margins": [left, top, width - right, height - bottom],
    }


def audit(manifest_path: Path) -> dict[str, object]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    root = manifest_path.parent
    canvas = tuple(manifest["canvas"])
    edge_band = int(manifest["base_edge_band"])
    errors: list[str] = []
    assets: list[dict[str, object]] = []
    pilot_edges: dict[str, Image.Image] | None = None

    for index, relative_path in enumerate(manifest["base_assets"]):
        path = (root / relative_path).resolve()
        image = Image.open(path).convert("RGB")
        if image.size != canvas:
            errors.append(f"{relative_path}: 画布 {image.size} != {canvas}")
            continue

        width, height = image.size
        edges = {
            "left": image.crop((0, 0, edge_band, height)),
            "right": image.crop((width - edge_band, 0, width, height)).transpose(Image.Transpose.FLIP_LEFT_RIGHT),
            "top": image.crop((0, 0, width, edge_band)),
            "bottom": image.crop((0, height - edge_band, width, height)).transpose(Image.Transpose.FLIP_TOP_BOTTOM),
        }
        horizontal_delta = maximum_difference(edges["left"], edges["right"])
        vertical_delta = maximum_difference(edges["top"], edges["bottom"])
        if horizontal_delta != 0 or vertical_delta != 0:
            errors.append(f"{relative_path}: 对边不连续，水平 {horizontal_delta}，垂直 {vertical_delta}")

        if index == 0:
            pilot_edges = edges
        else:
            for name, edge in edges.items():
                delta = maximum_difference(edge, pilot_edges[name])
                if delta != 0:
                    errors.append(f"{relative_path}: {name} 边未继承 Pilot，差值 {delta}")

        assets.append({
            "path": relative_path,
            "type": "base",
            "horizontal_edge_delta": horizontal_delta,
            "vertical_edge_delta": vertical_delta,
            "mean_rgb": ImageStat.Stat(image).mean,
        })

    detail_contract = manifest["detail_contract"]
    for relative_path in manifest["detail_assets"]:
        path = (root / relative_path).resolve()
        image = Image.open(path).convert("RGBA")
        if image.size != canvas:
            errors.append(f"{relative_path}: 画布 {image.size} != {canvas}")
            continue

        metrics = alpha_metrics(image)
        coverage = metrics["coverage"]
        center = metrics["center"]
        margins = metrics["margins"]
        if center is None or margins is None:
            errors.append(f"{relative_path}: 没有可见像素")
        else:
            if not detail_contract["coverage"][0] <= coverage <= detail_contract["coverage"][1]:
                errors.append(f"{relative_path}: Alpha 覆盖率 {coverage:.4f} 超出合同")
            if min(margins) < detail_contract["edge_clearance"]:
                errors.append(f"{relative_path}: 最小透明边距 {min(margins)} 小于合同")
            for axis, value in zip(("x", "y"), center):
                lower, upper = detail_contract[f"center_{axis}"]
                if not lower <= value <= upper:
                    errors.append(f"{relative_path}: 视觉中心 {axis}={value:.2f} 超出合同")

        assets.append({"path": relative_path, "type": "detail", **metrics})

    return {
        "passed": len(errors) == 0,
        "errors": errors,
        "asset_count": len(assets),
        "assets": assets,
    }


def main() -> None:
    args = parse_args()
    report = audit(args.manifest)
    encoded = json.dumps(report, ensure_ascii=False, indent=2)
    if args.json_report is not None:
        args.json_report.parent.mkdir(parents=True, exist_ok=True)
        args.json_report.write_text(encoded + "\n", encoding="utf-8")
    print(encoded)
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
