#!/usr/bin/env python3
import argparse
import json
import random
import sys
from pathlib import Path


BOTTLE_CAPACITY = 4
MAX_COLOR_COUNT = 12
DEFAULT_COUNT = 100
DEFAULT_SEED = 20260421
DEFAULT_OUTPUT = Path("Assets/Games/WaterSort/Resources/Levels/water-sort.levels.json")


def main():
    parser = argparse.ArgumentParser(description="Generate and validate WaterSort levels.")
    parser.add_argument("--count", type=int, default=DEFAULT_COUNT)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--validate-only", type=Path, default=None)
    args = parser.parse_args()

    if args.validate_only is not None:
        payload = load_json(args.validate_only)
        validate_payload(payload, expected_count=args.count)
        print("WaterSort level validation passed: {0}".format(args.validate_only))
        return 0

    levels = generate_levels(args.count, args.seed)
    payload = {"levels": levels}
    validate_payload(payload, expected_count=args.count)
    write_json_bom(args.output, payload)
    print("Generated {0} WaterSort levels: {1}".format(args.count, args.output))
    return 0


def generate_levels(count, seed):
    if count <= 0:
        raise ValueError("--count must be positive")

    rng = random.Random(seed)
    levels = []
    previous_decade_average = None

    for decade in range((count + 9) // 10):
        start = decade * 10
        end = min(count, start + 10)
        best_decade = None
        best_average = None
        minimum_average = 0.0 if previous_decade_average is None else previous_decade_average + 0.75

        for decade_attempt in range(18):
            decade_rng = random.Random(rng.randint(0, 2**31 - 1) + decade_attempt * 97)
            candidates = []
            for index in range(start, end):
                candidates.append(generate_one_level(index, count, decade_rng, minimum_average))

            average = sum(score_level(level_to_layout(level)) for level in candidates) / len(candidates)
            if average >= minimum_average:
                best_decade = candidates
                best_average = average
                break
            if best_decade is None or average > best_average:
                best_decade = candidates
                best_average = average

        if best_average < minimum_average:
            best_decade = lift_decade_difficulty(best_decade, minimum_average)
            best_average = sum(score_level(level_to_layout(level)) for level in best_decade) / len(best_decade)
            if best_average < minimum_average:
                raise RuntimeError("Failed to increase WaterSort difficulty at decade {0}".format(decade + 1))

        levels.extend(best_decade)
        previous_decade_average = best_average

    return levels


def generate_one_level(index, total_count, rng, minimum_score):
    color_count = difficulty_color_count(index, total_count)
    empty_min, empty_max, final_empty_max, require_all_bottles_filled, scramble_min, scramble_max = difficulty_band(index)
    minimum_solution_length = difficulty_minimum_solution_length(index)
    target_score = max(minimum_score, 10.0 + color_count * 2.2 + minimum_solution_length * 2.4)
    best = None
    best_delta = None

    for attempt in range(800):
        level_rng = random.Random(rng.randint(0, 2**31 - 1) + attempt)
        empty_count = level_rng.randint(empty_min, empty_max)
        scramble_count = level_rng.randint(scramble_min, scramble_max)
        layout = scramble_from_solution(color_count, empty_count, scramble_count, level_rng)
        if layout is None:
            continue
        if has_completed_bottle(layout):
            continue
        if not validate_layout(layout, expected_color_count=color_count, max_empty_count=final_empty_max):
            continue
        if require_all_bottles_filled and any(len(bottle) == 0 for bottle in layout):
            continue
        solution = solve_layout(layout, max_states=45000)
        if solution is None:
            continue
        if len(solution) < minimum_solution_length:
            continue

        score = score_level(layout, len(solution))
        delta = abs(score - target_score)
        if best is None or delta < best_delta:
            best = layout
            best_delta = delta
            if delta < 1.2:
                break

    if best is None:
        raise RuntimeError("Failed to generate WaterSort level {0}".format(index + 1))

    return layout_to_level(best)


def difficulty_color_count(index, total_count):
    if index <= 0 or total_count <= 1:
        return 3
    if index <= 4:
        return 4
    if index <= 9:
        return 5

    progress = (index - 10) / float(max(1, total_count - 11))
    return min(MAX_COLOR_COUNT, 5 + int(progress * (MAX_COLOR_COUNT - 5) + 0.0001))


def difficulty_minimum_solution_length(index):
    if index <= 0:
        return 3
    if index <= 2:
        return 5 + index
    if index <= 9:
        return 7 + (index - 2) // 2
    if index <= 19:
        return 8 + (index - 10) // 5
    return min(28, 10 + (index - 20) // 5)


def difficulty_band(index):
    decade = index // 10
    if decade == 0:
        return 0, 1, 1, False, 18, 34
    if decade == 1:
        return 0, 1, 1, False, 22, 38
    if decade <= 3:
        return 0, 1, 1, False, 26, 44
    if decade <= 5:
        return 0, 2, 2, False, 30, 52
    if decade <= 7:
        return 0, 2, 2, False, 36, 60
    return 0, 1, 1, False, 30, 44


def scramble_from_solution(color_count, empty_count, scramble_count, rng):
    extra_partial_bottles = rng.randint(1, 3 if color_count >= 7 else 2)
    bottle_count = color_count + empty_count + extra_partial_bottles
    layout = [[color] * BOTTLE_CAPACITY for color in range(color_count)]
    layout.extend([[] for _ in range(bottle_count - color_count)])

    last_move = None
    for _ in range(scramble_count):
        moves = list_reverse_moves(layout)
        if last_move is not None:
            moves = [move for move in moves if not (move[0] == last_move[1] and move[1] == last_move[0])] or moves
        if not moves:
            return None
        source, target, amount = rng.choice(moves)
        color = layout[source][-1]
        for _ in range(amount):
            layout[target].append(color)
            layout[source].pop()
        last_move = (source, target)

    rng.shuffle(layout)
    normalize_layout(layout)
    if is_solved(layout):
        return None
    return layout


def list_reverse_moves(layout):
    moves = []
    for source_index, source in enumerate(layout):
        if not source:
            continue
        color = source[-1]
        same_count = top_group_count(source)
        for target_index, target in enumerate(layout):
            if source_index == target_index or len(target) >= BOTTLE_CAPACITY:
                continue
            space = BOTTLE_CAPACITY - len(target)
            max_amount = min(same_count, space)
            for amount in range(1, max_amount + 1):
                if len(source) == amount and not target:
                    continue
                moves.append((source_index, target_index, amount))
    return moves


def solve_layout(layout, max_states):
    start = canonical(layout)
    if is_solved_state(start):
        return []

    best_seen = {start: 0}
    path = []
    result = depth_first_search(start, path, best_seen, max_depth=96, max_states=max_states)
    return result


def resolve_solution_length(solution, index):
    if solution is None:
        return None

    solution_length = len(solution)
    minimum_solution_length = difficulty_minimum_solution_length(index)
    return solution_length if solution_length >= minimum_solution_length else None


def depth_first_search(state, path, best_seen, max_depth, max_states):
    if len(best_seen) > max_states or len(path) > max_depth:
        return None
    if is_solved_state(state):
        return list(path)

    moves = list_forward_moves_state(state)
    moves.sort(key=lambda move: move_score(state, move), reverse=True)
    for move in moves:
        next_state = apply_move_state(state, move)
        next_depth = len(path) + 1
        if best_seen.get(next_state, 9999) <= next_depth:
            continue
        best_seen[next_state] = next_depth
        path.append(move)
        result = depth_first_search(next_state, path, best_seen, max_depth, max_states)
        if result is not None:
            return result
        path.pop()

    return None


def list_forward_moves_state(state):
    moves = []
    for source_index, source in enumerate(state):
        if not source or is_completed(source):
            continue
        color = source[-1]
        amount = top_group_count(source)
        for target_index, target in enumerate(state):
            if source_index == target_index or len(target) >= BOTTLE_CAPACITY or is_completed(target):
                continue
            if target and target[-1] != color:
                continue
            pour_amount = min(amount, BOTTLE_CAPACITY - len(target))
            if pour_amount <= 0:
                continue
            if not target and len(source) == pour_amount:
                continue
            moves.append((source_index, target_index, pour_amount))
    return moves


def move_score(state, move):
    source_index, target_index, amount = move
    source = state[source_index]
    target = state[target_index]
    score = amount * 4
    if target:
        score += 8 + len(target)
    if len(target) + amount == BOTTLE_CAPACITY:
        score += 16
    if len(source) == amount:
        score += 3
    return score


def apply_move_state(state, move):
    source_index, target_index, amount = move
    mutable = [list(bottle) for bottle in state]
    color = mutable[source_index][-1]
    for _ in range(amount):
        mutable[target_index].append(color)
        mutable[source_index].pop()
    normalize_layout(mutable)
    return canonical(mutable)


def validate_payload(payload, expected_count):
    levels = payload.get("levels") if isinstance(payload, dict) else None
    if not isinstance(levels, list) or len(levels) != expected_count:
        raise ValueError("Expected {0} levels".format(expected_count))

    scores = []
    has_zero_empty_level = False
    for index, level in enumerate(levels):
        layout = level_to_layout(level)
        color_count = validate_level_layout(layout, index + 1)
        empty_count = sum(1 for bottle in layout if len(bottle) == 0)
        if empty_count == 0:
            has_zero_empty_level = True
        if empty_count > 3:
            raise ValueError("Level {0} has too many empty bottles".format(index + 1))
        if has_completed_bottle(layout):
            raise ValueError("Level {0} starts with a completed bottle".format(index + 1))
        if color_count > MAX_COLOR_COUNT:
            raise ValueError("Level {0} has too many colors".format(index + 1))
        solution = solve_layout(layout, max_states=70000)
        if solution is None:
            raise ValueError("Level {0} is not solvable".format(index + 1))
        solution_length = resolve_solution_length(solution, index)
        if solution_length is None:
            raise ValueError("Level {0} solution is too short".format(index + 1))
        minimum_solution_length = difficulty_minimum_solution_length(index)
        if solution_length < minimum_solution_length:
            raise ValueError(
                "Level {0} solution is too short: {1} < {2}".format(
                    index + 1,
                    solution_length,
                    minimum_solution_length))
        scores.append(score_level(layout, solution_length))

    for decade_start in range(10, len(scores), 10):
        previous_average = sum(scores[decade_start - 10:decade_start]) / 10.0
        current_average = sum(scores[decade_start:decade_start + 10]) / 10.0
        if current_average <= previous_average:
            raise ValueError("Difficulty average does not increase at decade {0}".format(decade_start // 10 + 1))

    if not has_zero_empty_level:
        raise ValueError("Generated WaterSort levels should include at least one zero-empty-bottle level")


def validate_level_layout(layout, level_number):
    if not isinstance(layout, list) or len(layout) < 3:
        raise ValueError("Level {0} bottle count is invalid".format(level_number))

    max_color = -1
    counts = [0 for _ in range(MAX_COLOR_COUNT)]
    for bottle in layout:
        if len(bottle) > BOTTLE_CAPACITY:
            raise ValueError("Level {0} bottle capacity is invalid".format(level_number))
        for color in bottle:
            if color < 0 or color >= MAX_COLOR_COUNT:
                raise ValueError("Level {0} color is out of range".format(level_number))
            counts[color] += 1
            max_color = max(max_color, color)

    color_count = max_color + 1
    if color_count <= 0:
        raise ValueError("Level {0} has no colors".format(level_number))
    for color in range(color_count):
        if counts[color] != BOTTLE_CAPACITY:
            raise ValueError("Level {0} color {1} appears {2} times".format(level_number, color, counts[color]))
    for color in range(color_count, MAX_COLOR_COUNT):
        if counts[color] != 0:
            raise ValueError("Level {0} colors must be contiguous".format(level_number))
    return color_count


def validate_layout(layout, expected_color_count, max_empty_count):
    try:
        color_count = validate_level_layout(layout, 0)
    except ValueError:
        return False
    empty_count = sum(1 for bottle in layout if len(bottle) == 0)
    return color_count == expected_color_count and empty_count <= max_empty_count


def score_level(layout, solution_length=None):
    color_count = max((color for bottle in layout for color in bottle), default=-1) + 1
    bottle_count = len(layout)
    empty_count = sum(1 for bottle in layout if len(bottle) == 0)
    free_space = bottle_count * BOTTLE_CAPACITY - color_count * BOTTLE_CAPACITY
    mixed_bottles = sum(1 for bottle in layout if len(set(bottle)) > 1)
    top_moves = len(list_forward_moves_state(canonical(layout)))
    solved_groups = sum(1 for bottle in layout if is_completed(tuple(bottle)))
    if solution_length is None:
        solution = solve_layout(layout, max_states=45000)
        solution_length = len(solution) if solution is not None else 0
    return (
        color_count * 4.5
        + bottle_count * 1.1
        + mixed_bottles * 2.0
        + solution_length * 1.6
        - empty_count * 1.5
        - free_space * 0.35
        - top_moves * 0.25
        - solved_groups * 2.0
    )


def lift_decade_difficulty(levels, minimum_average):
    current = levels
    for _ in range(6):
        boosted = []
        for level in current:
            layout = level_to_layout(level)
            layout = [bottle[:] for bottle in layout]
            layout.sort(key=lambda bottle: (len(set(bottle)) <= 1, len(bottle)))
            boosted.append(layout_to_level(layout))
        average = sum(score_level(level_to_layout(level)) for level in boosted) / len(boosted)
        if average >= minimum_average:
            return boosted
        current = boosted
    return current


def top_group_count(bottle):
    if not bottle:
        return 0
    color = bottle[-1]
    count = 0
    for index in range(len(bottle) - 1, -1, -1):
        if bottle[index] != color:
            break
        count += 1
    return count


def is_completed(bottle):
    return len(bottle) == BOTTLE_CAPACITY and all(color == bottle[0] for color in bottle)


def has_completed_bottle(layout):
    return any(is_completed(tuple(bottle)) for bottle in layout)


def is_solved(layout):
    return all(len(bottle) == 0 or is_completed(tuple(bottle)) for bottle in layout)


def is_solved_state(state):
    return all(len(bottle) == 0 or is_completed(bottle) for bottle in state)


def normalize_layout(layout):
    for bottle in layout:
        if len(bottle) > BOTTLE_CAPACITY:
            raise ValueError("Bottle overflow")


def canonical(layout):
    return tuple(tuple(bottle) for bottle in layout)


def layout_to_level(layout):
    return {"bottles": [{"layers": bottle[:]} for bottle in layout]}


def level_to_layout(level):
    bottles = level.get("bottles") if isinstance(level, dict) else None
    if not isinstance(bottles, list):
        raise ValueError("Level bottles are invalid")
    layout = []
    for bottle in bottles:
        layers = bottle.get("layers") if isinstance(bottle, dict) else None
        if layers is None:
            layers = []
        if not isinstance(layers, list):
            raise ValueError("Bottle layers are invalid")
        layout.append([int(color) for color in layers])
    return layout


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
        print("WaterSort level generation failed: {0}".format(exception), file=sys.stderr)
        sys.exit(1)
