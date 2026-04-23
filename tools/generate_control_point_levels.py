#!/usr/bin/env python3
import argparse
import json
import math
import random
import sys
from pathlib import Path


DEFAULT_COUNT = 100
DEFAULT_SEED = 20260422
DEFAULT_OUTPUT = Path("Assets/Games/ControlPoint/Resources/Levels/control-point.levels.json")
MIN_POINT_COUNT = 5
MAX_POINT_COUNT = 10
MIN_POINT_DISTANCE = 128.0
MIN_X = -285
MAX_X = 285
MIN_Y = -305
MAX_Y = 245
ENEMY_OWNERS = ["Enemy", "EnemyTwo", "EnemyThree"]
OWNERS = ["Neutral", "Player"] + ENEMY_OWNERS


FIRST_LEVEL = {
    "points": [
        {"owner": "Neutral", "units": 8, "x": 0, "y": 230},
        {"owner": "Player", "units": 12, "x": -238, "y": -214},
        {"owner": "Enemy", "units": 12, "x": 238, "y": -214},
        {"owner": "Neutral", "units": 8, "x": -244, "y": 32},
        {"owner": "Neutral", "units": 8, "x": 244, "y": 32},
        {"owner": "EnemyTwo", "units": 12, "x": 0, "y": -282},
        {"owner": "EnemyThree", "units": 12, "x": 0, "y": -36},
    ]
}


def main():
    parser = argparse.ArgumentParser(description="Generate and validate ControlPoint levels.")
    parser.add_argument("--count", type=int, default=DEFAULT_COUNT)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--validate-only", type=Path, default=None)
    args = parser.parse_args()

    if args.validate_only is not None:
        payload = load_json(args.validate_only)
        validate_payload(payload, expected_count=args.count)
        print("ControlPoint level validation passed: {0}".format(args.validate_only))
        return 0

    levels = generate_levels(args.count, args.seed)
    payload = {"levels": levels}
    validate_payload(payload, expected_count=args.count)
    write_json_bom(args.output, payload)
    print("Generated {0} ControlPoint levels: {1}".format(args.count, args.output))
    return 0


def generate_levels(count, seed):
    if count <= 0:
        raise ValueError("--count must be positive")

    rng = random.Random(seed)
    levels = [clone_level(FIRST_LEVEL)]
    while len(levels) < count:
        index = len(levels)
        best = None
        best_score = None
        target = target_score(index)
        for attempt in range(240):
            candidate_rng = random.Random(rng.randint(0, 2**31 - 1) + attempt * 31 + index * 997)
            candidate = generate_one_level(index, count, candidate_rng)
            try:
                validate_level(candidate, index + 1)
            except ValueError:
                continue

            score = score_level(candidate)
            if best is None or abs(score - target) < abs(best_score - target):
                best = candidate
                best_score = score
                if score >= target:
                    break

        if best is None:
            raise RuntimeError("Failed to generate ControlPoint level {0}".format(index + 1))
        levels.append(best)

    return enforce_decade_growth(levels)


def generate_one_level(index, total_count, rng):
    progress = index / float(max(1, total_count - 1))
    point_count = point_count_for(index, progress)
    enemy_owner_count = enemy_owner_count_for(index, progress)
    enemy_point_count = enemy_point_count_for(point_count, progress, rng)
    player_point_count = 1 if progress < 0.58 else rng.choice([1, 2])
    if player_point_count + enemy_point_count > point_count:
        player_point_count = 1
    neutral_count = point_count - player_point_count - enemy_point_count

    owners = []
    for i in range(player_point_count):
        owners.append("Player")
    for i in range(enemy_point_count):
        owners.append(ENEMY_OWNERS[i % enemy_owner_count])
    for i in range(neutral_count):
        owners.append("Neutral")
    rng.shuffle(owners)

    if "Player" not in owners:
        owners[0] = "Player"
    if not any(owner in ENEMY_OWNERS for owner in owners):
        owners[-1] = "Enemy"

    positions = generate_positions(point_count, progress, rng)
    points = []
    for point_index, owner in enumerate(owners):
        points.append(
            {
                "owner": owner,
                "units": units_for_owner(owner, index, progress, rng),
                "x": positions[point_index][0],
                "y": positions[point_index][1],
            }
        )

    return {"points": points}


def point_count_for(index, progress):
    if index == 1:
        return 6
    if progress < 0.2:
        return 5 + (index % 2)
    if progress < 0.5:
        return 6 + (index % 3)
    if progress < 0.78:
        return 7 + (index % 3)
    return 8 + (index % 3)


def enemy_owner_count_for(index, progress):
    if progress < 0.28:
        return 1
    if progress < 0.72:
        return 2
    return 3


def enemy_point_count_for(point_count, progress, rng):
    minimum = 1 if progress < 0.18 else 2
    maximum = max(minimum, point_count - 2)
    target = minimum + int(progress * 3.2)
    return max(minimum, min(maximum, target + rng.randint(0, 1)))


def units_for_owner(owner, index, progress, rng):
    if owner == "Player":
        return max(6, int(16 + (index * 0.14) - progress * 4) + rng.randint(-2, 3))
    if owner == "Neutral":
        return max(6, int(8 + (index * 0.22) + progress * 10) + rng.randint(-2, 3))
    return max(8, int(10 + (index * 0.34) + progress * 18) + rng.randint(-2, 4))


def generate_positions(point_count, progress, rng):
    radius_x = 190 + int(progress * 55)
    radius_y = 178 + int(progress * 40)
    center_y = -20 + int(progress * -10)
    base_rotation = rng.uniform(-0.4, 0.4)
    positions = []

    for i in range(point_count):
        angle = base_rotation + (math.tau * i / point_count)
        x = int(round(math.cos(angle) * radius_x + rng.randint(-28, 28)))
        y = int(round(math.sin(angle) * radius_y + center_y + rng.randint(-24, 24)))
        positions.append((clamp(x, MIN_X, MAX_X), clamp(y, MIN_Y, MAX_Y)))

    for _ in range(80):
        if positions_are_valid(positions):
            rng.shuffle(positions)
            return positions
        positions = relax_positions(positions)

    raise ValueError("Could not place points without overlap")


def relax_positions(positions):
    adjusted = [list(position) for position in positions]
    for i in range(len(adjusted)):
        for j in range(i + 1, len(adjusted)):
            dx = adjusted[j][0] - adjusted[i][0]
            dy = adjusted[j][1] - adjusted[i][1]
            distance = math.hypot(dx, dy)
            if distance >= MIN_POINT_DISTANCE:
                continue
            if distance < 0.001:
                dx = 1
                dy = 0
                distance = 1
            push = (MIN_POINT_DISTANCE - distance) * 0.5 + 3
            nx = dx / distance
            ny = dy / distance
            adjusted[i][0] = clamp(int(round(adjusted[i][0] - nx * push)), MIN_X, MAX_X)
            adjusted[i][1] = clamp(int(round(adjusted[i][1] - ny * push)), MIN_Y, MAX_Y)
            adjusted[j][0] = clamp(int(round(adjusted[j][0] + nx * push)), MIN_X, MAX_X)
            adjusted[j][1] = clamp(int(round(adjusted[j][1] + ny * push)), MIN_Y, MAX_Y)
    return [(position[0], position[1]) for position in adjusted]


def enforce_decade_growth(levels):
    current = [clone_level(level) for level in levels]
    for _ in range(8):
        scores = [score_level(level) for level in current]
        failed_decade = find_failed_decade(scores)
        if failed_decade is None:
            return current
        for index in range(failed_decade * 10, min(len(current), failed_decade * 10 + 10)):
            lift_level(current[index], 2 + failed_decade)

    scores = [score_level(level) for level in current]
    failed_decade = find_failed_decade(scores)
    if failed_decade is not None:
        raise RuntimeError("Failed to increase ControlPoint difficulty at decade {0}".format(failed_decade + 1))
    return current


def find_failed_decade(scores):
    for decade_start in range(10, len(scores), 10):
        previous = sum(scores[decade_start - 10:decade_start]) / 10.0
        current = sum(scores[decade_start:decade_start + 10]) / len(scores[decade_start:decade_start + 10])
        if current <= previous:
            return decade_start // 10
    return None


def lift_level(level, amount):
    for point in level["points"]:
        if point["owner"] in ENEMY_OWNERS:
            point["units"] += amount * 2
        elif point["owner"] == "Neutral":
            point["units"] += amount
        else:
            point["units"] = max(6, point["units"] - amount)


def score_level(level):
    points = level["points"]
    point_count = len(points)
    enemy_points = [point for point in points if point["owner"] in ENEMY_OWNERS]
    player_units = sum(point["units"] for point in points if point["owner"] == "Player")
    enemy_units = sum(point["units"] for point in enemy_points)
    neutral_units = sum(point["units"] for point in points if point["owner"] == "Neutral")
    enemy_owner_count = len(set(point["owner"] for point in enemy_points))
    player_positions = [(point["x"], point["y"]) for point in points if point["owner"] == "Player"]
    enemy_pressure = 0.0
    if player_positions:
        for enemy in enemy_points:
            nearest = min(math.hypot(enemy["x"] - x, enemy["y"] - y) for x, y in player_positions)
            enemy_pressure += max(0.0, 330.0 - nearest) / 24.0

    return (
        point_count * 5.0
        + len(enemy_points) * 9.5
        + enemy_owner_count * 8.0
        + enemy_units * 1.15
        + neutral_units * 0.55
        + enemy_pressure
        - player_units * 0.75
    )


def target_score(index):
    return 36.0 + index * 2.8 + (index // 10) * 12.0


def validate_payload(payload, expected_count):
    levels = payload.get("levels") if isinstance(payload, dict) else None
    if not isinstance(levels, list) or len(levels) != expected_count:
        raise ValueError("Expected {0} levels".format(expected_count))

    for index, level in enumerate(levels):
        validate_level(level, index + 1)

    scores = [score_level(level) for level in levels]
    failed_decade = find_failed_decade(scores)
    if failed_decade is not None:
        raise ValueError("Difficulty average does not increase at decade {0}".format(failed_decade + 1))


def validate_level(level, level_number):
    points = level.get("points") if isinstance(level, dict) else None
    if not isinstance(points, list) or len(points) < MIN_POINT_COUNT or len(points) > MAX_POINT_COUNT:
        raise ValueError("Level {0} point count is invalid".format(level_number))

    has_player = False
    has_enemy = False
    positions = []
    for point_index, point in enumerate(points):
        owner = point.get("owner") if isinstance(point, dict) else None
        if owner not in OWNERS:
            raise ValueError("Level {0} point {1} owner is invalid".format(level_number, point_index + 1))
        units = int(point.get("units", 0))
        if units < 1:
            raise ValueError("Level {0} point {1} units are invalid".format(level_number, point_index + 1))
        x = float(point.get("x", 0))
        y = float(point.get("y", 0))
        if x < MIN_X or x > MAX_X or y < MIN_Y or y > MAX_Y:
            raise ValueError("Level {0} point {1} position is out of range".format(level_number, point_index + 1))
        for previous in positions:
            if math.hypot(x - previous[0], y - previous[1]) < MIN_POINT_DISTANCE:
                raise ValueError("Level {0} point {1} is too close".format(level_number, point_index + 1))
        positions.append((x, y))
        has_player = has_player or owner == "Player"
        has_enemy = has_enemy or owner in ENEMY_OWNERS

    if not has_player or not has_enemy:
        raise ValueError("Level {0} must contain player and enemy points".format(level_number))


def positions_are_valid(positions):
    for i in range(len(positions)):
        for j in range(i + 1, len(positions)):
            if math.hypot(positions[i][0] - positions[j][0], positions[i][1] - positions[j][1]) < MIN_POINT_DISTANCE:
                return False
    return True


def clone_level(level):
    return {"points": [dict(point) for point in level["points"]]}


def clamp(value, minimum, maximum):
    return max(minimum, min(maximum, value))


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
        print("ControlPoint level generation failed: {0}".format(exception), file=sys.stderr)
        sys.exit(1)
