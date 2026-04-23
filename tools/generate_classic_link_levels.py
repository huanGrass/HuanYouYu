#!/usr/bin/env python3
import argparse
import json
import random
import sys
from collections import deque
from pathlib import Path


ROWS = 10
COLUMNS = 8
ICON_TYPES = 14
DEFAULT_COUNT = 100
DEFAULT_SEED = 20260421
DEFAULT_OUTPUT = Path("Assets/Games/ClassicLink/Resources/Levels/classic-link.levels.json")


def main():
    parser = argparse.ArgumentParser(description="Generate and validate ClassicLink levels.")
    parser.add_argument("--count", type=int, default=DEFAULT_COUNT)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--validate-only", type=Path, default=None)
    args = parser.parse_args()

    if args.validate_only is not None:
        payload = load_json(args.validate_only)
        validate_payload(payload, expected_count=args.count, require_shape_variety=True)
        print("ClassicLink level validation passed: {0}".format(args.validate_only))
        return 0

    levels = generate_levels(args.count, args.seed)
    payload = {"levels": levels}
    validate_payload(payload, expected_count=args.count, require_shape_variety=True)
    write_json_bom(args.output, payload)
    print("Generated {0} ClassicLink levels: {1}".format(args.count, args.output))
    return 0


def generate_levels(count, seed):
    if count <= 0:
        raise ValueError("--count must be positive")

    rng = random.Random(seed)
    levels = []
    previous_decade_average = None
    decade_scores = []

    for index in range(count):
        min_tiles, max_tiles, min_icons, max_icons = difficulty_band(index)
        target_score = 22.0 + index * 1.7
        best_level = None
        best_delta = None

        for attempt in range(18):
            level_rng = random.Random(rng.randint(0, 2**31 - 1) + attempt)
            mask = build_mask(index, level_rng, min_tiles, max_tiles)
            tile_count = sum(1 for row in mask for value in row if value)
            icon_count = level_rng.randint(min_icons, min(max_icons, ICON_TYPES, tile_count // 2))
            cells = fill_mask(mask, icon_count, level_rng)
            metrics = score_level(cells)
            try:
                validate_cells(cells)
            except ValueError:
                continue
            if metrics["available_pairs"] < 1:
                continue

            delta = abs(metrics["score"] - target_score)
            if best_level is None or delta < best_delta:
                best_level = cells
                best_delta = delta
                if delta < 2.5:
                    break

        if best_level is None:
            best_level = build_fallback_level(index, min_tiles, max_tiles, min_icons, max_icons, seed + index)

        level_score = score_level(best_level)["score"]
        decade_scores.append(level_score)
        levels.append({"rows": [{"cells": row} for row in best_level]})

        if (index + 1) % 10 == 0:
            current_average = sum(decade_scores[-10:]) / 10.0
            if previous_decade_average is not None and current_average <= previous_decade_average:
                lift_decade_difficulty(levels, index - 9, index, seed + index)
                decade_scores[-10:] = [
                    score_level([[cell for cell in row["cells"]] for row in levels[level]["rows"]])["score"]
                    for level in range(index - 9, index + 1)
                ]
                current_average = sum(decade_scores[-10:]) / 10.0
            previous_decade_average = current_average

    return levels


def difficulty_band(index):
    if index < 20:
        return 24, 40, 4, 7
    if index < 50:
        return 38, 56, 6, 10
    if index < 80:
        return 50, 68, 8, 12
    return 60, 76, 10, 14


def build_mask(index, rng, min_tiles, max_tiles):
    families = [
        rectangle_mask,
        cross_mask,
        diamond_mask,
        ring_mask,
        double_island_mask,
        corridor_mask,
        stairs_mask,
        center_hole_mask,
        irregular_mask,
    ]

    start = index % len(families)
    for offset in range(len(families) * 80):
        family = families[(start + offset) % len(families)]
        mask = family(index, rng)
        mask = tune_mask(mask, rng, min_tiles, max_tiles)
        tile_count = sum(1 for row in mask for value in row if value)
        if min_tiles <= tile_count <= max_tiles and tile_count % 2 == 0:
            return mask

    raise RuntimeError("Failed to build ClassicLink mask")


def rectangle_mask(index, rng):
    top = rng.randint(0, 2)
    bottom = rng.randint(0, 2)
    left = rng.randint(0, 2)
    right = rng.randint(0, 2)
    return [
        [top <= row < ROWS - bottom and left <= column < COLUMNS - right for column in range(COLUMNS)]
        for row in range(ROWS)
    ]


def cross_mask(index, rng):
    center_row = rng.randint(3, 6)
    center_column = rng.randint(2, 5)
    arm_width = 1 + index // 45
    return [
        [abs(row - center_row) <= arm_width or abs(column - center_column) <= arm_width for column in range(COLUMNS)]
        for row in range(ROWS)
    ]


def diamond_mask(index, rng):
    center_row = rng.uniform(4.0, 5.0)
    center_column = rng.uniform(3.0, 4.0)
    radius = 4.2 + min(index, 80) / 45.0
    return [
        [abs(row - center_row) + abs(column - center_column) <= radius for column in range(COLUMNS)]
        for row in range(ROWS)
    ]


def ring_mask(index, rng):
    thickness = 1 + index // 45
    return [
        [
            row < thickness or row >= ROWS - thickness or column < thickness or column >= COLUMNS - thickness
            for column in range(COLUMNS)
        ]
        for row in range(ROWS)
    ]


def double_island_mask(index, rng):
    mask = [[False for _ in range(COLUMNS)] for _ in range(ROWS)]
    for row in range(ROWS):
        for column in range(COLUMNS):
            left = row in range(1, 8) and column in range(0, 3)
            right = row in range(2, 9) and column in range(5, 8)
            bridge = index > 45 and row in range(4, 6) and column in range(3, 5)
            mask[row][column] = left or right or bridge
    return mask


def corridor_mask(index, rng):
    mask = [[False for _ in range(COLUMNS)] for _ in range(ROWS)]
    column = rng.randint(1, COLUMNS - 2)
    for row in range(ROWS):
        mask[row][column] = True
        if row % 2 == 0:
            for extra in range(rng.randint(2, COLUMNS - 1)):
                mask[row][extra] = True
        else:
            for extra in range(COLUMNS - rng.randint(2, COLUMNS - 1), COLUMNS):
                mask[row][extra] = True
    return mask


def stairs_mask(index, rng):
    mask = [[False for _ in range(COLUMNS)] for _ in range(ROWS)]
    width = 3 + index // 30
    for row in range(ROWS):
        start = max(0, min(COLUMNS - 1, row // 2 - 1))
        for column in range(start, min(COLUMNS, start + width)):
            mask[row][column] = True
    return mask


def center_hole_mask(index, rng):
    mask = [[True for _ in range(COLUMNS)] for _ in range(ROWS)]
    hole_height = rng.randint(2, 4)
    hole_width = rng.randint(2, 4)
    top = rng.randint(2, ROWS - hole_height - 2)
    left = rng.randint(2, COLUMNS - hole_width - 2)
    for row in range(top, top + hole_height):
        for column in range(left, left + hole_width):
            mask[row][column] = False
    return mask


def irregular_mask(index, rng):
    mask = [[rng.random() < 0.72 for _ in range(COLUMNS)] for _ in range(ROWS)]
    for row in range(ROWS):
        for column in range(COLUMNS):
            if row in (0, ROWS - 1) or column in (0, COLUMNS - 1):
                if rng.random() < 0.28:
                    mask[row][column] = False
    return mask


def tune_mask(mask, rng, min_tiles, max_tiles):
    tuned = [row[:] for row in mask]
    cells = [(row, column) for row in range(ROWS) for column in range(COLUMNS)]

    while count_mask(tuned) > max_tiles:
        filled = [(row, column) for row, column in cells if tuned[row][column]]
        if not filled:
            break
        row, column = rng.choice(filled)
        tuned[row][column] = False

    while count_mask(tuned) < min_tiles:
        empty = [(row, column) for row, column in cells if not tuned[row][column]]
        if not empty:
            break
        row, column = rng.choice(empty)
        tuned[row][column] = True

    if count_mask(tuned) % 2 != 0:
        filled = [(row, column) for row, column in cells if tuned[row][column]]
        if filled and count_mask(tuned) > min_tiles:
            row, column = rng.choice(filled)
            tuned[row][column] = False
        else:
            empty = [(row, column) for row, column in cells if not tuned[row][column]]
            row, column = rng.choice(empty)
            tuned[row][column] = True

    return tuned


def count_mask(mask):
    return sum(1 for row in mask for value in row if value)


def fill_mask(mask, icon_count, rng):
    positions = [(row, column) for row in range(ROWS) for column in range(COLUMNS) if mask[row][column]]
    pair_count = len(positions) // 2
    values = []
    for index in range(pair_count):
        value = (index % icon_count) + 1
        values.append(value)
        values.append(value)

    rng.shuffle(values)
    cells = [[0 for _ in range(COLUMNS)] for _ in range(ROWS)]
    for (row, column), value in zip(positions, values):
        cells[row][column] = value

    return cells


def build_fallback_level(index, min_tiles, max_tiles, min_icons, max_icons, seed):
    rng = random.Random(seed)
    target = min(max_tiles, min_tiles + ((index * 2) % max(2, max_tiles - min_tiles + 1)))
    if target % 2 != 0:
        target -= 1
    target = max(min_tiles, target)

    mask = [[False for _ in range(COLUMNS)] for _ in range(ROWS)]
    remaining = target
    for row in range(ROWS):
        if remaining <= 0:
            break
        width = min(COLUMNS, remaining)
        if width % 2 != 0:
            width -= 1
        if width <= 0:
            continue
        start = 0 if row % 2 == 0 else COLUMNS - width
        for column in range(start, start + width):
            mask[row][column] = True
        remaining -= width

    icon_count = rng.randint(min_icons, min(max_icons, ICON_TYPES, max(1, target // 2)))
    cells = fill_mask(mask, icon_count, rng)
    try:
        validate_cells(cells)
    except ValueError as exception:
        raise RuntimeError("Failed to generate fallback ClassicLink level {0}: {1}".format(index + 1, exception))
    if score_level(cells)["available_pairs"] < 1:
        raise RuntimeError("Failed to generate fallback ClassicLink level {0}".format(index + 1))
    return cells


def tile_mask_with_dominoes(mask, rng):
    remaining = {(row, column) for row in range(ROWS) for column in range(COLUMNS) if mask[row][column]}
    return tile_remaining_cells(remaining, rng, [])


def tile_remaining_cells(remaining, rng, dominoes):
    if not remaining:
        rng.shuffle(dominoes)
        return dominoes

    best_cell = None
    best_neighbors = None
    for row, column in remaining:
        neighbors = []
        for d_row, d_column in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            neighbor = (row + d_row, column + d_column)
            if neighbor in remaining:
                neighbors.append(neighbor)
        if not neighbors:
            return None
        if best_neighbors is None or len(neighbors) < len(best_neighbors):
            best_cell = (row, column)
            best_neighbors = neighbors

    rng.shuffle(best_neighbors)
    for neighbor in best_neighbors:
        next_remaining = set(remaining)
        next_remaining.remove(best_cell)
        next_remaining.remove(neighbor)
        result = tile_remaining_cells(next_remaining, rng, dominoes + [(best_cell, neighbor)])
        if result is not None:
            return result

    return None


def lift_decade_difficulty(levels, start, end, seed):
    rng = random.Random(seed)
    for index in range(start, end + 1):
        cells = [[cell for cell in row["cells"]] for row in levels[index]["rows"]]
        filled = [(row, column) for row in range(ROWS) for column in range(COLUMNS) if cells[row][column] != 0]
        empty = [(row, column) for row in range(ROWS) for column in range(COLUMNS) if cells[row][column] == 0]
        if len(empty) >= 2 and len(filled) < 76:
            value = rng.randint(1, ICON_TYPES)
            for row, column in rng.sample(empty, 2):
                cells[row][column] = value
        if is_valid_level(cells, require_solver=True):
            levels[index] = {"rows": [{"cells": row} for row in cells]}


def score_level(cells):
    tile_count = sum(1 for row in cells for value in row if value != 0)
    icon_count = len({value for row in cells for value in row if value != 0})
    available_pairs = count_available_pairs(cells)
    holes = count_internal_holes(cells)
    edges = count_edges(cells)
    components = count_components(cells)
    scarcity = 20.0 / max(1, available_pairs)
    score = tile_count * 0.65 + icon_count * 2.2 + holes * 1.4 + edges * 0.18 + components * 2.0 + scarcity
    return {
        "score": score,
        "tile_count": tile_count,
        "icon_count": icon_count,
        "available_pairs": available_pairs,
        "holes": holes,
        "edges": edges,
        "components": components,
    }


def count_internal_holes(cells):
    count = 0
    for row in range(1, ROWS - 1):
        for column in range(1, COLUMNS - 1):
            if cells[row][column] != 0:
                continue
            if (
                cells[row - 1][column] != 0
                and cells[row + 1][column] != 0
                and cells[row][column - 1] != 0
                and cells[row][column + 1] != 0
            ):
                count += 1
    return count


def count_edges(cells):
    edges = 0
    for row in range(ROWS):
        for column in range(COLUMNS):
            if cells[row][column] == 0:
                continue
            for d_row, d_column in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                next_row = row + d_row
                next_column = column + d_column
                if next_row < 0 or next_row >= ROWS or next_column < 0 or next_column >= COLUMNS:
                    edges += 1
                elif cells[next_row][next_column] == 0:
                    edges += 1
    return edges


def count_components(cells):
    visited = set()
    components = 0
    for row in range(ROWS):
        for column in range(COLUMNS):
            if cells[row][column] == 0 or (row, column) in visited:
                continue
            components += 1
            queue = deque([(row, column)])
            visited.add((row, column))
            while queue:
                current_row, current_column = queue.popleft()
                for d_row, d_column in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    next_row = current_row + d_row
                    next_column = current_column + d_column
                    if (
                        0 <= next_row < ROWS
                        and 0 <= next_column < COLUMNS
                        and cells[next_row][next_column] != 0
                        and (next_row, next_column) not in visited
                    ):
                        visited.add((next_row, next_column))
                        queue.append((next_row, next_column))
    return components


def is_valid_level(cells, require_solver):
    try:
        validate_cells(cells)
    except ValueError:
        return False
    if count_available_pairs(cells) < 1:
        return False
    if require_solver and not can_solve_greedily(cells):
        return False
    return True


def validate_cells(cells):
    if len(cells) != ROWS:
        raise ValueError("Level row count must be {0}".format(ROWS))
    counts = {}
    tile_count = 0
    for row in cells:
        if len(row) != COLUMNS:
            raise ValueError("Level column count must be {0}".format(COLUMNS))
        for value in row:
            if value < 0 or value > ICON_TYPES:
                raise ValueError("Icon value out of range: {0}".format(value))
            if value == 0:
                continue
            tile_count += 1
            counts[value] = counts.get(value, 0) + 1
    if tile_count <= 0 or tile_count % 2 != 0:
        raise ValueError("Non-empty tile count must be positive and even")
    for value, count in counts.items():
        if count % 2 != 0:
            raise ValueError("Icon value {0} appears an odd number of times".format(value))


def count_available_pairs(cells):
    count = 0
    for first_row in range(ROWS):
        for first_column in range(COLUMNS):
            value = cells[first_row][first_column]
            if value == 0:
                continue
            for second_row in range(first_row, ROWS):
                start_column = first_column + 1 if second_row == first_row else 0
                for second_column in range(start_column, COLUMNS):
                    if cells[second_row][second_column] != value:
                        continue
                    if try_find_path(cells, (first_row, first_column), (second_row, second_column)):
                        count += 1
    return count


def can_solve_greedily(cells):
    probe = [row[:] for row in cells]
    remaining = sum(1 for row in probe for value in row if value != 0)
    while remaining > 0:
        pair = find_first_available_pair(probe)
        if pair is None:
            return False
        (first_row, first_column), (second_row, second_column) = pair
        probe[first_row][first_column] = 0
        probe[second_row][second_column] = 0
        remaining -= 2
    return True


def find_first_available_pair(cells):
    for first_row in range(ROWS):
        for first_column in range(COLUMNS):
            value = cells[first_row][first_column]
            if value == 0:
                continue
            for second_row in range(first_row, ROWS):
                start_column = first_column + 1 if second_row == first_row else 0
                for second_column in range(start_column, COLUMNS):
                    if cells[second_row][second_column] != value:
                        continue
                    if try_find_path(cells, (first_row, first_column), (second_row, second_column)):
                        return (first_row, first_column), (second_row, second_column)
    return None


def try_find_path(cells, start, target):
    if start == target:
        return False

    board = [[0 for _ in range(COLUMNS + 2)] for _ in range(ROWS + 2)]
    for row in range(ROWS):
        for column in range(COLUMNS):
            board[row + 1][column + 1] = cells[row][column]

    start_row, start_column = start[0] + 1, start[1] + 1
    target_row, target_column = target[0] + 1, target[1] + 1
    directions = [(-1, 0), (1, 0), (0, -1), (0, 1)]
    queue = deque()
    best = {}

    for direction_index, (d_row, d_column) in enumerate(directions):
        row = start_row + d_row
        column = start_column + d_column
        while inside(row, column) and can_pass(board, row, column, target_row, target_column):
            state = (row, column, direction_index)
            if best.get(state, 99) > 0:
                best[state] = 0
                queue.append((row, column, direction_index, 0))
            if row == target_row and column == target_column:
                return True
            row += d_row
            column += d_column

    while queue:
        row, column, direction_index, turns = queue.popleft()
        for next_direction, (d_row, d_column) in enumerate(directions):
            next_turns = turns if next_direction == direction_index else turns + 1
            if next_turns > 2:
                continue
            next_row = row + d_row
            next_column = column + d_column
            while inside(next_row, next_column) and can_pass(board, next_row, next_column, target_row, target_column):
                state = (next_row, next_column, next_direction)
                if best.get(state, 99) > next_turns:
                    best[state] = next_turns
                    if next_row == target_row and next_column == target_column:
                        return True
                    queue.append((next_row, next_column, next_direction, next_turns))
                next_row += d_row
                next_column += d_column
    return False


def inside(row, column):
    return 0 <= row <= ROWS + 1 and 0 <= column <= COLUMNS + 1


def can_pass(board, row, column, target_row, target_column):
    return (row == target_row and column == target_column) or board[row][column] == 0


def validate_payload(payload, expected_count, require_shape_variety):
    levels = payload.get("levels") if isinstance(payload, dict) else None
    if not isinstance(levels, list) or len(levels) != expected_count:
        raise ValueError("Expected {0} levels".format(expected_count))

    scores = []
    non_full_count = 0
    hole_count = 0
    edge_cut_count = 0
    irregular_count = 0

    for index, level in enumerate(levels):
        rows = level.get("rows") if isinstance(level, dict) else None
        if not isinstance(rows, list):
            raise ValueError("Level {0} rows are invalid".format(index + 1))
        cells = [row.get("cells") for row in rows]
        metrics = score_level(cells)
        validate_cells(cells)
        if metrics["available_pairs"] < 1:
            raise ValueError("Level {0} has no initial pair".format(index + 1))
        scores.append(metrics["score"])
        if metrics["tile_count"] < ROWS * COLUMNS:
            non_full_count += 1
        if metrics["holes"] > 0:
            hole_count += 1
        if has_edge_cut(cells):
            edge_cut_count += 1
        if metrics["components"] > 1 or metrics["edges"] > 36:
            irregular_count += 1

    for decade_start in range(10, len(scores), 10):
        previous_average = sum(scores[decade_start - 10:decade_start]) / 10.0
        current_average = sum(scores[decade_start:decade_start + 10]) / 10.0
        if current_average <= previous_average:
            raise ValueError("Difficulty average does not increase at decade {0}".format(decade_start // 10 + 1))

    if require_shape_variety:
        if non_full_count < max(1, expected_count // 2):
            raise ValueError("Not enough non-full ClassicLink levels")
        if hole_count < max(1, expected_count // 10):
            raise ValueError("Not enough ClassicLink levels with holes")
        if edge_cut_count < max(1, expected_count // 5):
            raise ValueError("Not enough ClassicLink levels with edge cuts")
        if irregular_count < max(1, expected_count // 5):
            raise ValueError("Not enough irregular ClassicLink levels")


def has_edge_cut(cells):
    for column in range(COLUMNS):
        if cells[0][column] == 0 or cells[ROWS - 1][column] == 0:
            return True
    for row in range(ROWS):
        if cells[row][0] == 0 or cells[row][COLUMNS - 1] == 0:
            return True
    return False


def load_json(path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def write_json_bom(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(payload, ensure_ascii=False, indent=2)
    with path.open("w", encoding="utf-8-sig", newline="\n") as handle:
        handle.write(text)
        handle.write("\n")


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exception:
        print("ClassicLink level generation failed: {0}".format(exception), file=sys.stderr)
        sys.exit(1)
