from __future__ import annotations

import math
from pathlib import Path


BOTTLE_CAPACITY = 4
BOTTLE_AREA_SAMPLE_COLUMNS = 24
BOTTLE_AREA_SAMPLE_ROWS = 48
BOTTLE_LIQUID_MAX_HEIGHT_ANCHOR = 3.0
BOTTLE_FULL_FILL_RATIO = 0.94
BOTTLE_LIQUID_HORIZONTAL_OVERFLOW = 8.0
BOTTLE_LIQUID_LOOKUP_ANGLE_STEP_DEGREES = 1
BOTTLE_LIQUID_LOOKUP_MAX_ANGLE_DEGREES = 90
BOTTLE_LIQUID_LOOKUP_ANGLE_COUNT = BOTTLE_LIQUID_LOOKUP_MAX_ANGLE_DEGREES // BOTTLE_LIQUID_LOOKUP_ANGLE_STEP_DEGREES + 1
BOTTLE_LIQUID_LOOKUP_FILL_SAMPLES = 32
BOTTLE_LIQUID_HEIGHT_SEARCH_ITERATIONS = 12


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def normalize_angle(degrees: float) -> float:
    degrees %= 360.0
    if degrees > 180.0:
        degrees -= 360.0
    return degrees


def rotate(x: float, y: float, degrees: float) -> tuple[float, float]:
    radians = math.radians(degrees)
    c = math.cos(radians)
    s = math.sin(radians)
    return x * c - y * s, x * s + y * c


def is_point_inside_bottle_mask(mask_width: float, mask_height: float, x: float, y: float) -> bool:
    if x < 0.0 or x > mask_width or y < 0.0 or y > mask_height:
        return False

    radius = mask_width * 0.5
    center_y = radius
    if y >= center_y:
        return True

    dx = x - mask_width * 0.5
    dy = y - center_y
    return dx * dx + dy * dy <= radius * radius


def calculate_mask_area(mask_width: float, mask_height: float, extra_test) -> float:
    step_x = mask_width / BOTTLE_AREA_SAMPLE_COLUMNS
    step_y = mask_height / BOTTLE_AREA_SAMPLE_ROWS
    cell_area = step_x * step_y
    area = 0.0
    for row in range(BOTTLE_AREA_SAMPLE_ROWS):
        sample_y = (row + 0.5) * step_y
        for col in range(BOTTLE_AREA_SAMPLE_COLUMNS):
            sample_x = (col + 0.5) * step_x
            if is_point_inside_bottle_mask(mask_width, mask_height, sample_x, sample_y) and extra_test(sample_x, sample_y):
                area += cell_area
    return area


def calculate_visible_liquid_area(mask_width: float, mask_height: float, liquid_height: float, angle_degrees: float) -> float:
    fill_width = mask_width * (1.0 + BOTTLE_LIQUID_HORIZONTAL_OVERFLOW * 2.0)
    fill_height = mask_height * clamp01(liquid_height / BOTTLE_LIQUID_MAX_HEIGHT_ANCHOR)
    if mask_width <= 0.1 or mask_height <= 0.1 or fill_width <= 0.1 or fill_height <= 0.1:
        return 0.0

    fill_pivot_x = mask_width * 0.5
    fill_pivot_y = 0.0
    inverse_angle = -angle_degrees

    def extra_test(point_x: float, point_y: float) -> bool:
        delta_x = point_x - fill_pivot_x
        delta_y = point_y - fill_pivot_y
        fill_x, fill_y = rotate(delta_x, delta_y, inverse_angle)
        return abs(fill_x) <= fill_width * 0.5 and 0.0 <= fill_y <= fill_height

    return calculate_mask_area(mask_width, mask_height, extra_test)


def calculate_capacity_area(mask_width: float, mask_height: float) -> float:
    top_limit = lerp(0.0, mask_height, BOTTLE_FULL_FILL_RATIO)

    def extra_test(point_x: float, point_y: float) -> bool:
        return point_y <= top_limit

    return calculate_mask_area(mask_width, mask_height, extra_test)


def resolve_area_preserving_liquid_height(mask_width: float, mask_height: float, fill_count: float, angle_degrees: float) -> float:
    if fill_count <= 0.0001:
        return 0.0

    capacity_area = calculate_capacity_area(mask_width, mask_height)
    if capacity_area <= 0.0001:
        return fill_count / BOTTLE_CAPACITY * BOTTLE_FULL_FILL_RATIO

    target_area = capacity_area * clamp01(fill_count / BOTTLE_CAPACITY)
    low = 0.0
    high = BOTTLE_LIQUID_MAX_HEIGHT_ANCHOR
    if calculate_visible_liquid_area(mask_width, mask_height, high, angle_degrees) < target_area:
        return high

    for _ in range(BOTTLE_LIQUID_HEIGHT_SEARCH_ITERATIONS):
        mid = (low + high) * 0.5
        if calculate_visible_liquid_area(mask_width, mask_height, mid, angle_degrees) < target_area:
            low = mid
        else:
            high = mid
    return high


def build_table() -> list[list[float]]:
    # Canonical bottle size from the runtime layout.
    bottle_width = 140.0
    bottle_height = 230.0
    mask_width = bottle_width - 44.0 * 2.0
    mask_height = bottle_height - 17.0 - 31.0

    table: list[list[float]] = []
    for fill_index in range(BOTTLE_LIQUID_LOOKUP_FILL_SAMPLES + 1):
        fill_count = BOTTLE_CAPACITY * (fill_index / BOTTLE_LIQUID_LOOKUP_FILL_SAMPLES)
        row: list[float] = []
        for angle_index in range(BOTTLE_LIQUID_LOOKUP_ANGLE_COUNT):
            angle_degrees = angle_index * BOTTLE_LIQUID_LOOKUP_ANGLE_STEP_DEGREES
            row.append(resolve_area_preserving_liquid_height(mask_width, mask_height, fill_count, angle_degrees))
        table.append(row)
    return table


def format_float(value: float) -> str:
    return f"{value:.6f}f"


def write_csharp(table: list[list[float]], output_path: Path) -> None:
    lines = []
    lines.append("// <auto-generated />")
    lines.append("using System;")
    lines.append("")
    lines.append("namespace HuanYouYu.MiniGameHall")
    lines.append("{")
    lines.append("    internal static class WaterSortLookupData")
    lines.append("    {")
    lines.append(f"        internal const int AngleStepDegrees = {BOTTLE_LIQUID_LOOKUP_ANGLE_STEP_DEGREES};")
    lines.append(f"        internal const int MaxAngleDegrees = {BOTTLE_LIQUID_LOOKUP_MAX_ANGLE_DEGREES};")
    lines.append(f"        internal const int AngleCount = {BOTTLE_LIQUID_LOOKUP_ANGLE_COUNT};")
    lines.append(f"        internal const int FillSamples = {BOTTLE_LIQUID_LOOKUP_FILL_SAMPLES};")
    lines.append("        internal static readonly float[] LiquidHeightLookup = new float[]")
    lines.append("        {")

    for row in table:
        values = ", ".join(format_float(value) for value in row)
        lines.append(f"            {values},")

    lines.append("        };")
    lines.append("    }")
    lines.append("}")
    lines.append("")
    output_path.write_text("\n".join(lines), encoding="utf-8-sig")


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    output_path = repo_root / "Assets" / "Games" / "WaterSort" / "Scripts" / "WaterSortLookupData.cs"
    table = build_table()
    write_csharp(table, output_path)
    print(f"Wrote {output_path}")


if __name__ == "__main__":
    main()
