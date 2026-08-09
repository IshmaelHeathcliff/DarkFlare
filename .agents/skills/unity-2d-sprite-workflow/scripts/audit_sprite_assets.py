#!/usr/bin/env python3
"""Audit independent PNG assets against a fixed-canvas family contract."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

try:
    from PIL import Image
except ImportError as exc:
    raise SystemExit("Pillow is required: python -m pip install Pillow") from exc


@dataclass
class AssetResult:
    name: str
    image: str
    canvas: list[int]
    visible_bbox: list[int] | None
    visible_width: int
    visible_height: int
    margins: dict[str, int] | None
    alpha_coverage: float
    visual_center: list[float] | None
    errors: list[str]
    warnings: list[str]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Validate full-image canvas, alpha bounds, visual center, margins, "
            "and family consistency for independent Sprite PNGs."
        )
    )
    parser.add_argument(
        "manifest",
        help="Path to the JSON family contract, or - to read JSON from stdin",
    )
    parser.add_argument("--json-report", type=Path, help="Write full results as JSON")
    return parser.parse_args()


def require_int(value: Any, field: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"{field} must be an integer")
    return value


def require_number(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be a number")
    return float(value)


def parse_range(value: Any, field: str) -> tuple[float, float] | None:
    if value is None:
        return None
    if not isinstance(value, list) or len(value) != 2:
        raise ValueError(f"{field} must be [min, max]")
    minimum = require_number(value[0], f"{field}[0]")
    maximum = require_number(value[1], f"{field}[1]")
    if minimum > maximum:
        raise ValueError(f"{field} minimum exceeds maximum")
    return minimum, maximum


def add_range_error(
    errors: list[str],
    value: float,
    expected: tuple[float, float] | None,
    field: str,
) -> None:
    if expected is None:
        return
    if not expected[0] <= value <= expected[1]:
        errors.append(f"{field}={value:.4f} outside [{expected[0]}, {expected[1]}]")


def alpha_metrics(
    alpha: Image.Image, alpha_threshold: int
) -> tuple[list[int] | None, int, int, dict[str, int] | None, float, list[float] | None]:
    mask = alpha.point(lambda value: 255 if value > alpha_threshold else 0)
    bbox = mask.getbbox()
    if bbox is None:
        return None, 0, 0, None, 0.0, None

    visible_width = bbox[2] - bbox[0]
    visible_height = bbox[3] - bbox[1]
    margins = {
        "left": bbox[0],
        "top": bbox[1],
        "right": alpha.width - bbox[2],
        "bottom": alpha.height - bbox[3],
    }

    total_weight = 0
    weighted_x = 0.0
    weighted_y = 0.0
    visible_pixels = 0
    pixels = (
        alpha.get_flattened_data()
        if hasattr(alpha, "get_flattened_data")
        else alpha.getdata()
    )
    for index, value in enumerate(pixels):
        if value <= alpha_threshold:
            continue
        x = index % alpha.width
        y = index // alpha.width
        total_weight += value
        weighted_x += (x + 0.5) * value
        weighted_y += (y + 0.5) * value
        visible_pixels += 1

    coverage = visible_pixels / float(alpha.width * alpha.height)
    visual_center = [weighted_x / total_weight, weighted_y / total_weight]
    return (
        [bbox[0], bbox[1], bbox[2], bbox[3]],
        visible_width,
        visible_height,
        margins,
        coverage,
        visual_center,
    )


def audit_asset(
    base_dir: Path,
    spec: dict[str, Any],
    expected_canvas: tuple[int, int],
    alpha_threshold: int,
    alpha_required: bool,
) -> AssetResult:
    name = str(spec.get("name", "")).strip()
    relative_path = str(spec.get("image", "")).strip()
    errors: list[str] = []
    warnings: list[str] = []

    if not relative_path:
        raise ValueError(f"asset {name or '<unnamed>'}.image must be non-empty")
    image_path = (base_dir / relative_path).resolve()
    if image_path.suffix.lower() != ".png":
        errors.append("production asset must be a PNG")

    with Image.open(image_path) as opened:
        has_alpha = "A" in opened.getbands()
        if alpha_required and not has_alpha:
            errors.append("image has no alpha channel")
        image = opened.convert("RGBA")

    if image.size != expected_canvas:
        errors.append(
            f"canvas={image.width}x{image.height}, "
            f"expected={expected_canvas[0]}x{expected_canvas[1]}"
        )

    alpha = image.getchannel("A")
    (
        visible_bbox,
        visible_width,
        visible_height,
        margins,
        alpha_coverage,
        visual_center,
    ) = alpha_metrics(alpha, alpha_threshold)

    if visible_bbox is None:
        errors.append("image contains no visible pixels")
    else:
        add_range_error(
            errors,
            visible_width,
            parse_range(spec.get("visible_width"), f"asset {name}.visible_width"),
            "visible_width",
        )
        add_range_error(
            errors,
            visible_height,
            parse_range(spec.get("visible_height"), f"asset {name}.visible_height"),
            "visible_height",
        )
        add_range_error(
            errors,
            alpha_coverage,
            parse_range(spec.get("alpha_coverage"), f"asset {name}.alpha_coverage"),
            "alpha_coverage",
        )
        add_range_error(
            errors,
            visual_center[0],
            parse_range(
                spec.get("visual_center_x"), f"asset {name}.visual_center_x"
            ),
            "visual_center_x",
        )
        add_range_error(
            errors,
            visual_center[1],
            parse_range(
                spec.get("visual_center_y"), f"asset {name}.visual_center_y"
            ),
            "visual_center_y",
        )
        add_range_error(
            errors,
            margins["bottom"],
            parse_range(spec.get("bottom_margin"), f"asset {name}.bottom_margin"),
            "bottom_margin",
        )

        edge_clearance = spec.get("edge_clearance")
        if edge_clearance is not None:
            edge_clearance = require_int(edge_clearance, f"asset {name}.edge_clearance")
            if edge_clearance < 0:
                raise ValueError(f"asset {name}.edge_clearance must be non-negative")
            for side, margin in margins.items():
                if margin < edge_clearance:
                    errors.append(
                        f"{side} margin={margin} below edge_clearance={edge_clearance}"
                    )
        elif any(margin == 0 for margin in margins.values()):
            warnings.append("visible pixels touch the canvas edge")

    return AssetResult(
        name=name,
        image=str(image_path),
        canvas=[image.width, image.height],
        visible_bbox=visible_bbox,
        visible_width=visible_width,
        visible_height=visible_height,
        margins=margins,
        alpha_coverage=alpha_coverage,
        visual_center=visual_center,
        errors=errors,
        warnings=warnings,
    )


def audit_groups(
    manifest: dict[str, Any],
    asset_specs: list[dict[str, Any]],
    results: dict[str, AssetResult],
) -> list[str]:
    errors: list[str] = []
    members_by_group: dict[str, list[str]] = {}
    for spec in asset_specs:
        group = spec.get("group")
        name = str(spec.get("name", "")).strip()
        if group:
            members_by_group.setdefault(str(group), []).append(name)

    group_specs = manifest.get("groups", [])
    if not isinstance(group_specs, list):
        raise ValueError("groups must be a list")

    for group_spec in group_specs:
        if not isinstance(group_spec, dict):
            raise ValueError("each group must be an object")
        name = str(group_spec.get("name", "")).strip()
        if not name:
            raise ValueError("every group requires a non-empty name")
        members = group_spec.get("members", members_by_group.get(name, []))
        if not isinstance(members, list):
            raise ValueError(f"group {name}.members must be a list")
        missing = [str(member) for member in members if str(member) not in results]
        if missing:
            errors.append(f"group {name} has unknown members: {', '.join(missing)}")
        valid = [results[str(member)] for member in members if str(member) in results]
        valid = [result for result in valid if result.visible_bbox is not None]
        if len(valid) < 2:
            errors.append(f"group {name} needs at least two valid members")
            continue

        checks = (
            ("max_width_delta", lambda result: float(result.visible_width)),
            ("max_height_delta", lambda result: float(result.visible_height)),
            (
                "max_visual_center_x_delta",
                lambda result: result.visual_center[0],
            ),
            (
                "max_visual_center_y_delta",
                lambda result: result.visual_center[1],
            ),
            (
                "max_bottom_margin_delta",
                lambda result: float(result.margins["bottom"]),
            ),
            ("max_alpha_coverage_delta", lambda result: result.alpha_coverage),
        )
        for limit_name, selector in checks:
            if limit_name not in group_spec:
                continue
            limit = require_number(group_spec[limit_name], f"group {name}.{limit_name}")
            if limit < 0:
                raise ValueError(f"group {name}.{limit_name} must be non-negative")
            values = [selector(result) for result in valid]
            delta = max(values) - min(values)
            if delta > limit:
                errors.append(f"group {name} {limit_name} delta={delta:.4f} exceeds {limit}")

    return errors


def read_manifest(source: str) -> tuple[dict[str, Any], Path, str]:
    if source == "-":
        return json.load(sys.stdin), Path.cwd(), "<stdin>"
    manifest_path = Path(source).resolve()
    return (
        json.loads(manifest_path.read_text(encoding="utf-8")),
        manifest_path.parent,
        str(manifest_path),
    )


def main() -> int:
    args = parse_args()
    try:
        manifest, base_dir, manifest_name = read_manifest(args.manifest)
        if require_int(manifest.get("schema_version"), "schema_version") != 1:
            raise ValueError("schema_version must be 1")
        family = str(manifest.get("family", "")).strip()
        if not family:
            raise ValueError("family must be non-empty")

        alpha_threshold = require_int(manifest.get("alpha_threshold", 8), "alpha_threshold")
        if not 0 <= alpha_threshold <= 254:
            raise ValueError("alpha_threshold must be between 0 and 254")
        alpha_required = manifest.get("alpha_required", True)
        if not isinstance(alpha_required, bool):
            raise ValueError("alpha_required must be a boolean")

        canvas = manifest.get("canvas")
        if not isinstance(canvas, dict):
            raise ValueError("canvas must be an object")
        expected_canvas = (
            require_int(canvas.get("width"), "canvas.width"),
            require_int(canvas.get("height"), "canvas.height"),
        )
        if expected_canvas[0] <= 0 or expected_canvas[1] <= 0:
            raise ValueError("canvas width and height must be positive")

        asset_specs = manifest.get("assets")
        if not isinstance(asset_specs, list) or not asset_specs:
            raise ValueError("assets must be a non-empty list")

        names: set[str] = set()
        ordered_results: list[AssetResult] = []
        for spec in asset_specs:
            if not isinstance(spec, dict):
                raise ValueError("each asset must be an object")
            name = str(spec.get("name", "")).strip()
            if not name:
                raise ValueError("every asset requires a non-empty name")
            if name in names:
                raise ValueError(f"duplicate asset name: {name}")
            names.add(name)
            ordered_results.append(
                audit_asset(
                    base_dir,
                    spec,
                    expected_canvas,
                    alpha_threshold,
                    alpha_required,
                )
            )

        results_by_name = {result.name: result for result in ordered_results}
        global_errors = audit_groups(manifest, asset_specs, results_by_name)
        error_count = len(global_errors) + sum(
            len(result.errors) for result in ordered_results
        )
        warning_count = sum(len(result.warnings) for result in ordered_results)
        report = {
            "manifest": manifest_name,
            "family": family,
            "canvas": [expected_canvas[0], expected_canvas[1]],
            "errors": global_errors,
            "assets": [asdict(result) for result in ordered_results],
            "summary": {
                "assets": len(ordered_results),
                "errors": error_count,
                "warnings": warning_count,
                "passed": error_count == 0,
            },
        }

        if args.json_report:
            args.json_report.parent.mkdir(parents=True, exist_ok=True)
            args.json_report.write_text(
                json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )

        print(f"Family: {family} canvas={expected_canvas[0]}x{expected_canvas[1]}")
        for result in ordered_results:
            center = result.visual_center if result.visual_center is not None else "empty"
            print(
                f"- {result.name}: visible={result.visible_width}x{result.visible_height} "
                f"center={center} coverage={result.alpha_coverage:.4f} "
                f"margins={result.margins} errors={len(result.errors)} "
                f"warnings={len(result.warnings)}"
            )
            for message in result.errors:
                print(f"  ERROR: {message}")
            for message in result.warnings:
                print(f"  WARN: {message}")
        for message in global_errors:
            print(f"ERROR: {message}")
        print(
            f"Summary: assets={len(ordered_results)} errors={error_count} "
            f"warnings={warning_count} passed={error_count == 0}"
        )
        return 0 if error_count == 0 else 1
    except (OSError, TypeError, ValueError, json.JSONDecodeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
