using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HuanYouYu.MiniGameHall
{
    public sealed class ArrowEscapeGameView : MiniGameBase
    {
        public const string GameIdConstant = "arrow-escape";

        private const float FlySpeed = 360f;
        private const float FlyExitPadding = 160f;
        private const float BlockFlashDuration = 0.16f;
        private const float MaxBoardZoom = 2.35f;
        private const float BoardPanViewportPaddingRatio = 0.5f;
        private const int RuntimePuzzleSeedAttempts = 200;
        private static readonly Color PanelColor = new Color32(255, 255, 255, 245);
        private static readonly Color BoardColor = new Color32(255, 255, 255, 255);
        private static readonly Color MazeLineColor = new Color32(18, 28, 52, 255);
        private static readonly Color PlayableColor = MazeLineColor;
        private static readonly Color BlockedColor = MazeLineColor;
        private static readonly Color HintColor = new Color32(49, 115, 205, 255);
        private static readonly Color EmptyColor = new Color32(18, 28, 52, 0);
        private static readonly Color FlashColor = new Color32(223, 84, 78, 255);

        private static readonly LevelDefinition[] LevelDefinitions =
        {
            new LevelDefinition("arrow-escape.level.easy", 11, 3, 5, new[]
            {
                "11111",
                "11111",
                "11011",
                "11111",
                "11111"
            }),
            new LevelDefinition("arrow-escape.level.hard", 59, 3, 5, new[]
            {
                "1111111111111111111",
                "1111111111111111111",
                "1110111111111111111",
                "1111111111111111111",
                "1111011111111111111",
                "1111111111111111111",
                "1111110111111111111",
                "1111111111111111111",
                "1111111101111111111",
                "1111111111111111111",
                "1111111111011111111",
                "1111111111111111111",
                "1110111111111111111",
                "1111111111111111111",
                "1111110111111111111",
                "1111111111111111111",
                "1111111110111111111",
                "1111111111111111111",
                "1111111111110111111",
                "1111111111111111111",
                "1111011111111111111",
                "1111111111111111111",
                "1111111111111101111",
                "1111111111111111111",
                "1111111011111111111",
                "1111111111111111111"
            })
        };

        private TextMeshProUGUI titleLabel;
        private TextMeshProUGUI scoreLabel;
        private TextMeshProUGUI statusLabel;
        private RectTransform boardPanel;
        private RectTransform boardRoot;
        private RectTransform pieceLayer;
        private GridLayoutGroup boardGrid;
        private Button restartButton;
        private Button hintButton;
        private Button undoButton;
        private Button zoomOutButton;
        private Button zoomInButton;
        private Slider zoomSlider;
        private MiniGameLevelProgressController levelProgress;
        private ArrowEscapePuzzleData currentPuzzle;
        private TileView[,] tiles;
        private bool[,] activeTiles;
        private ArrowPiece[,] pieceByCell;
        private readonly List<ArrowPiece> arrowPieces = new List<ArrowPiece>();
        private readonly List<MoveRecord> moveHistory = new List<MoveRecord>();
        private int currentLevelIndex;
        private int remainingTileCount;
        private int moveCount;
        private int blockedTapCount;
        private int hintCount;
        private int score;
        private int combo;
        private float boardCellSize;
        private float boardZoom = 1f;
        private float minBoardZoom = 1f;
        private Vector2 boardPan;
        private int activeFlyAnimationCount;
        private bool isAnimating;
        private bool settlementShown;
        private Vector2Int hintedCell = new Vector2Int(-1, -1);
        private static int runtimePuzzleSequence;

        public ArrowEscapeGameView(
            MonoBehaviour hostBehaviour,
            Transform parent,
            Action<MiniGameSettlement> onComplete,
            Action onExit)
            : base(GameIdConstant, "ArrowEscapeView", hostBehaviour, parent, onComplete, onExit)
        {
        }

        public static ArrowEscapePuzzleData GeneratePuzzle(string[] maskRows, int seed)
        {
            return GeneratePuzzle(maskRows, seed, 3, 5);
        }

        private static ArrowEscapePuzzleData GeneratePuzzle(string[] maskRows, int seed, int minPieceLength, int maxPieceLength)
        {
            var layout = ParseLayout(maskRows);
            var directions = new int[layout.GetLength(0), layout.GetLength(1)];
            for (var y = 0; y < directions.GetLength(1); y++)
            {
                for (var x = 0; x < directions.GetLength(0); x++)
                {
                    directions[x, y] = -1;
                }
            }

            var pieces = BuildGeneratedPieces(layout, seed, minPieceLength, maxPieceLength);
            var solution = AssignGeneratedPieceDirections(layout, pieces, directions, seed);
            var pieceCells = BuildNormalizedGeneratedPieceCells(pieces, directions, solution);
            var originalDirections = (int[,])directions.Clone();
            var originalSolution = new List<Vector2Int>(solution);
            if (ResolveSameDirectionFrontRays(layout, directions, pieceCells, solution)
                && !TryRebuildGeneratedSolution(layout, directions, pieceCells, solution))
            {
                directions = originalDirections;
                solution = originalSolution;
            }

            return new ArrowEscapePuzzleData(layout, directions, solution.ToArray(), pieceCells);
        }

        private static ArrowEscapePuzzleData GenerateRuntimePuzzle(LevelDefinition level, int levelIndex)
        {
            var hasFallback = false;
            var fallback = default(ArrowEscapePuzzleData);
            var hasCleanFallback = false;
            var cleanFallback = default(ArrowEscapePuzzleData);
            var cleanFallbackScore = int.MaxValue;
            for (var attempt = 0; attempt < RuntimePuzzleSeedAttempts; attempt++)
            {
                var seed = CreateRuntimePuzzleSeed(level, levelIndex, attempt);
                try
                {
                    var maskRows = GenerateRuntimeMaskRows(level.MaskRows, seed);
                    var puzzle = GeneratePuzzle(maskRows, seed, level.MinPieceLength, level.MaxPieceLength);
                    if (!hasFallback)
                    {
                        fallback = puzzle;
                        hasFallback = true;
                    }

                    if (!HasConnectedSameDirectionPieceInFront(puzzle))
                    {
                        var cleanScore = GetInitialPlayableSurfaceScore(puzzle);
                        if (!hasCleanFallback || cleanScore < cleanFallbackScore)
                        {
                            cleanFallback = puzzle;
                            cleanFallbackScore = cleanScore;
                            hasCleanFallback = true;
                        }
                    }

                    if (PuzzleMatchesRuntimeQuality(puzzle))
                    {
                        return puzzle;
                    }
                }
                catch (InvalidOperationException)
                {
                }
            }

            var stablePuzzle = GeneratePuzzle(level.MaskRows, level.Seed, level.MinPieceLength, level.MaxPieceLength);
            if (PuzzleMatchesRuntimeQuality(stablePuzzle))
            {
                return stablePuzzle;
            }

            if (!HasConnectedSameDirectionPieceInFront(stablePuzzle))
            {
                return stablePuzzle;
            }

            if (hasCleanFallback)
            {
                return cleanFallback;
            }

            return hasFallback ? fallback : stablePuzzle;
        }

        private static int GetInitialPlayableSurfaceScore(ArrowEscapePuzzleData puzzle)
        {
            var active = Copy(puzzle.Layout);
            var playablePieceCount = 0;
            var playableCellCount = 0;
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                if (cells == null || cells.Length == 0 || !CanEscapePiece(active, puzzle.Directions, cells))
                {
                    continue;
                }

                playablePieceCount++;
                playableCellCount += cells.Length;
            }

            return playablePieceCount * 10000 + playableCellCount;
        }

        private static int CreateRuntimePuzzleSeed(LevelDefinition level, int levelIndex, int attempt)
        {
            unchecked
            {
                runtimePuzzleSequence++;
                var ticks = DateTime.UtcNow.Ticks;
                var randomBits = (UnityEngine.Random.Range(0, 1 << 16) << 16) ^ UnityEngine.Random.Range(0, 1 << 16);
                var seed = (int)ticks ^ (int)(ticks >> 32);
                seed = (seed * 397) ^ randomBits;
                seed = (seed * 397) ^ runtimePuzzleSequence;
                seed = (seed * 397) ^ level.Seed;
                seed = (seed * 397) ^ levelIndex;
                seed = (seed * 397) ^ attempt;
                return seed;
            }
        }

        private static string[] GenerateRuntimeMaskRows(string[] templateRows, int seed)
        {
            var template = ParseLayout(templateRows);
            var width = template.GetLength(0);
            var height = template.GetLength(1);
            var emptyCount = CountInactive(template);
            if (emptyCount <= 0)
            {
                return (string[])templateRows.Clone();
            }

            var rows = new char[height][];
            for (var y = 0; y < height; y++)
            {
                rows[y] = new char[width];
                for (var x = 0; x < width; x++)
                {
                    rows[y][x] = '1';
                }
            }

            var candidates = new List<Vector2Int>();
            AddRuntimeMaskCandidates(candidates, width, height, true);
            if (candidates.Count < emptyCount)
            {
                candidates.Clear();
                AddRuntimeMaskCandidates(candidates, width, height, false);
            }

            var random = new System.Random(unchecked(seed + 151));
            for (var i = 0; i < emptyCount && candidates.Count > 0; i++)
            {
                var index = random.Next(candidates.Count);
                var cell = candidates[index];
                candidates[index] = candidates[candidates.Count - 1];
                candidates.RemoveAt(candidates.Count - 1);
                rows[cell.y][cell.x] = '0';
            }

            var result = new string[height];
            for (var y = 0; y < height; y++)
            {
                result[y] = new string(rows[y]);
            }

            return result;
        }

        private static void AddRuntimeMaskCandidates(List<Vector2Int> candidates, int width, int height, bool interiorOnly)
        {
            var minX = interiorOnly && width > 2 ? 1 : 0;
            var maxX = interiorOnly && width > 2 ? width - 1 : width;
            var minY = interiorOnly && height > 2 ? 1 : 0;
            var maxY = interiorOnly && height > 2 ? height - 1 : height;
            for (var y = minY; y < maxY; y++)
            {
                for (var x = minX; x < maxX; x++)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        private static bool PuzzleMatchesRuntimeQuality(ArrowEscapePuzzleData puzzle)
        {
            if (puzzle.Layout == null || puzzle.Directions == null || puzzle.Pieces == null || puzzle.Pieces.Length == 0)
            {
                return false;
            }

            var activeCellCount = CountActive(puzzle.Layout);
            if (activeCellCount <= 0)
            {
                return false;
            }

            var active = Copy(puzzle.Layout);
            var playablePieceCount = 0;
            var playableCellCount = 0;
            var hasPlayableBody = false;
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                if (cells == null || cells.Length == 0)
                {
                    return false;
                }

                if (!CanEscapePiece(active, puzzle.Directions, cells))
                {
                    continue;
                }

                playablePieceCount++;
                playableCellCount += cells.Length;
                hasPlayableBody |= cells.Length > 1;
            }

            if (!hasPlayableBody)
            {
                return false;
            }

            if (HasConnectedSameDirectionPieceInFront(puzzle))
            {
                return false;
            }

            if (activeCellCount >= 100)
            {
                if (playablePieceCount > Mathf.Max(16, puzzle.Pieces.Length / 8))
                {
                    return false;
                }

                if (playableCellCount > activeCellCount / 2)
                {
                    return false;
                }
            }
            else
            {
                if (playablePieceCount > Mathf.Max(3, puzzle.Pieces.Length / 5))
                {
                    return false;
                }

                if (playableCellCount > Mathf.Max(8, activeCellCount / 5))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasConnectedSameDirectionPieceInFront(ArrowEscapePuzzleData puzzle)
        {
            var pieceByCell = BuildPuzzlePieceIndexByCell(puzzle);
            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                var head = cells[0];
                var direction = puzzle.Directions[head.x, head.y];
                if (direction < 0)
                {
                    continue;
                }

                var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
                if (frontPieceIndex < 0 || frontPieceIndex == i)
                {
                    continue;
                }

                var frontHead = puzzle.Pieces[frontPieceIndex][0];
                if (puzzle.Directions[frontHead.x, frontHead.y] == direction
                    && CanConnectSameDirectionPieces(puzzle.Pieces[frontPieceIndex], cells, direction))
                {
                    return true;
                }
            }

            return false;
        }

        private static int[,] BuildPuzzlePieceIndexByCell(ArrowEscapePuzzleData puzzle)
        {
            var pieceByCell = new int[puzzle.Layout.GetLength(0), puzzle.Layout.GetLength(1)];
            for (var y = 0; y < pieceByCell.GetLength(1); y++)
            {
                for (var x = 0; x < pieceByCell.GetLength(0); x++)
                {
                    pieceByCell[x, y] = -1;
                }
            }

            for (var i = 0; i < puzzle.Pieces.Length; i++)
            {
                var cells = puzzle.Pieces[i];
                if (cells == null)
                {
                    continue;
                }

                for (var c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    pieceByCell[cell.x, cell.y] = i;
                }
            }

            return pieceByCell;
        }

        private static Vector2Int[][] BuildNormalizedGeneratedPieceCells(List<GeneratedPiece> pieces, int[,] directions, List<Vector2Int> solution)
        {
            var pieceCells = new List<Vector2Int[]>(pieces.Count);
            var removed = new bool[pieces.Count];
            for (var i = 0; i < pieces.Count; i++)
            {
                pieceCells.Add(pieces[i].Cells);
            }

            var changed = true;
            while (changed)
            {
                changed = false;
                var pieceByCell = BuildPieceIndexByCell(pieceCells, removed, directions.GetLength(0), directions.GetLength(1));
                for (var i = 0; i < pieceCells.Count; i++)
                {
                    if (removed[i] || pieceCells[i].Length == 0)
                    {
                        continue;
                    }

                    var head = pieceCells[i][0];
                    var direction = directions[head.x, head.y];
                    if (direction < 0)
                    {
                        continue;
                    }

                    var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
                    if (frontPieceIndex < 0 || frontPieceIndex == i)
                    {
                        continue;
                    }

                    var frontHead = pieceCells[frontPieceIndex][0];
                    if (directions[frontHead.x, frontHead.y] != direction
                        || !TryCombineConnectedSameDirectionPieces(pieceCells[frontPieceIndex], pieceCells[i], direction, out var combinedCells))
                    {
                        continue;
                    }

                    pieceCells[frontPieceIndex] = combinedCells;
                    removed[i] = true;
                    changed = true;
                    break;
                }
            }

            RemoveMergedSolutionSteps(solution, pieces, removed);

            var normalizedPieces = new List<Vector2Int[]>(pieceCells.Count);
            for (var i = 0; i < pieceCells.Count; i++)
            {
                if (!removed[i])
                {
                    normalizedPieces.Add(pieceCells[i]);
                }
            }

            return normalizedPieces.ToArray();
        }

        private static int[,] BuildPieceIndexByCell(List<Vector2Int[]> pieceCells, bool[] removed, int width, int height)
        {
            var pieceByCell = new int[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pieceByCell[x, y] = -1;
                }
            }

            for (var i = 0; i < pieceCells.Count; i++)
            {
                if (removed[i] || pieceCells[i].Length == 0)
                {
                    continue;
                }

                for (var c = 0; c < pieceCells[i].Length; c++)
                {
                    var cell = pieceCells[i][c];
                    pieceByCell[cell.x, cell.y] = i;
                }
            }

            return pieceByCell;
        }

        private static bool ResolveSameDirectionFrontRays(bool[,] layout, int[,] directions, Vector2Int[][] pieceCells, List<Vector2Int> solution)
        {
            var anyChanged = false;
            for (var pass = 0; pass < pieceCells.Length * 4; pass++)
            {
                var changed = false;
                var pieceByCell = BuildPieceIndexByCell(pieceCells, layout.GetLength(0), layout.GetLength(1));
                for (var i = 0; i < pieceCells.Length; i++)
                {
                    if (pieceCells[i] == null || pieceCells[i].Length == 0)
                    {
                        continue;
                    }

                    var head = pieceCells[i][0];
                    var direction = directions[head.x, head.y];
                    if (direction < 0)
                    {
                        continue;
                    }

                    var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
                    if (frontPieceIndex < 0 || frontPieceIndex == i)
                    {
                        continue;
                    }

                    var frontHead = pieceCells[frontPieceIndex][0];
                    if (directions[frontHead.x, frontHead.y] != direction)
                    {
                        continue;
                    }

                    if (TryAssignAlternativeDirection(layout, pieceCells, directions, solution, i, direction)
                        || TryAssignAlternativeDirection(layout, pieceCells, directions, solution, frontPieceIndex, direction))
                    {
                        changed = true;
                        anyChanged = true;
                        break;
                    }
                }

                if (!changed)
                {
                    return anyChanged;
                }
            }

            return anyChanged;
        }

        private static bool TryAssignAlternativeDirection(
            bool[,] layout,
            Vector2Int[][] pieceCells,
            int[,] directions,
            List<Vector2Int> solution,
            int pieceIndex,
            int originalDirection)
        {
            var cells = pieceCells[pieceIndex];
            var head = cells[0];
            var originalSameDirectionCount = CountSameDirectionFrontRays(pieceCells, directions);
            for (var offset = 1; offset < 4; offset++)
            {
                var direction = (originalDirection + offset) & 3;
                if (BodyStartsInFront(cells, direction))
                {
                    continue;
                }

                directions[head.x, head.y] = direction;
                if (!PieceHasSameDirectionInFront(pieceCells, directions, pieceIndex)
                    && CountSameDirectionFrontRays(pieceCells, directions) < originalSameDirectionCount
                    && TryRebuildGeneratedSolution(layout, directions, pieceCells, solution))
                {
                    return true;
                }
            }

            directions[head.x, head.y] = originalDirection;
            return false;
        }

        private static int CountSameDirectionFrontRays(Vector2Int[][] pieceCells, int[,] directions)
        {
            var pieceByCell = BuildPieceIndexByCell(pieceCells, directions.GetLength(0), directions.GetLength(1));
            var count = 0;
            for (var i = 0; i < pieceCells.Length; i++)
            {
                var cells = pieceCells[i];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                var head = cells[0];
                var direction = directions[head.x, head.y];
                if (direction < 0)
                {
                    continue;
                }

                var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
                if (frontPieceIndex < 0 || frontPieceIndex == i)
                {
                    continue;
                }

                var frontHead = pieceCells[frontPieceIndex][0];
                if (directions[frontHead.x, frontHead.y] == direction
                    && CanConnectSameDirectionPieces(pieceCells[frontPieceIndex], cells, direction))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool PieceHasSameDirectionInFront(Vector2Int[][] pieceCells, int[,] directions, int pieceIndex)
        {
            var cells = pieceCells[pieceIndex];
            if (cells == null || cells.Length == 0)
            {
                return false;
            }

            var head = cells[0];
            var direction = directions[head.x, head.y];
            var pieceByCell = BuildPieceIndexByCell(pieceCells, directions.GetLength(0), directions.GetLength(1));
            var frontPieceIndex = FindFrontPieceIndexInRay(head, direction, pieceByCell);
            if (frontPieceIndex < 0 || frontPieceIndex == pieceIndex)
            {
                return false;
            }

            var frontHead = pieceCells[frontPieceIndex][0];
            return directions[frontHead.x, frontHead.y] == direction
                && CanConnectSameDirectionPieces(pieceCells[frontPieceIndex], cells, direction);
        }

        private static bool TryRebuildGeneratedSolution(bool[,] layout, int[,] directions, Vector2Int[][] pieceCells, List<Vector2Int> solution)
        {
            var active = Copy(layout);
            var removed = new bool[pieceCells.Length];
            var rebuiltSolution = new List<Vector2Int>(pieceCells.Length);
            for (var step = 0; step < pieceCells.Length; step++)
            {
                var bestPieceIndex = -1;
                var bestCellCount = -1;
                for (var i = 0; i < pieceCells.Length; i++)
                {
                    if (removed[i])
                    {
                        continue;
                    }

                    var cells = pieceCells[i];
                    if (cells == null || cells.Length == 0 || !CanEscapePiece(active, directions, cells))
                    {
                        continue;
                    }

                    if (cells.Length > bestCellCount)
                    {
                        bestPieceIndex = i;
                        bestCellCount = cells.Length;
                    }
                }

                if (bestPieceIndex < 0)
                {
                    return false;
                }

                var bestCells = pieceCells[bestPieceIndex];
                rebuiltSolution.Add(bestCells[0]);
                removed[bestPieceIndex] = true;
                for (var c = 0; c < bestCells.Length; c++)
                {
                    var cell = bestCells[c];
                    active[cell.x, cell.y] = false;
                }
            }

            if (CountActive(active) != 0)
            {
                return false;
            }

            solution.Clear();
            solution.AddRange(rebuiltSolution);
            return true;
        }

        private static int[,] BuildPieceIndexByCell(Vector2Int[][] pieceCells, int width, int height)
        {
            var pieceByCell = new int[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pieceByCell[x, y] = -1;
                }
            }

            for (var i = 0; i < pieceCells.Length; i++)
            {
                var cells = pieceCells[i];
                if (cells == null)
                {
                    continue;
                }

                for (var c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    pieceByCell[cell.x, cell.y] = i;
                }
            }

            return pieceByCell;
        }

        private static int FindFrontPieceIndexInRay(Vector2Int head, int direction, int[,] pieceByCell)
        {
            var delta = DirectionDelta(direction);
            var x = head.x + delta.x;
            var y = head.y + delta.y;
            while (x >= 0 && y >= 0 && x < pieceByCell.GetLength(0) && y < pieceByCell.GetLength(1))
            {
                var pieceIndex = pieceByCell[x, y];
                if (pieceIndex >= 0)
                {
                    return pieceIndex;
                }

                x += delta.x;
                y += delta.y;
            }

            return -1;
        }

        private static bool TryCombineConnectedSameDirectionPieces(
            Vector2Int[] frontPieceCells,
            Vector2Int[] backPieceCells,
            int direction,
            out Vector2Int[] combinedCells)
        {
            combinedCells = Array.Empty<Vector2Int>();
            if (frontPieceCells == null || backPieceCells == null || frontPieceCells.Length == 0 || backPieceCells.Length == 0)
            {
                return false;
            }

            if (!CanConnectSameDirectionPieces(frontPieceCells, backPieceCells, direction))
            {
                return false;
            }

            var allCells = new List<Vector2Int>(frontPieceCells.Length + backPieceCells.Length);
            allCells.AddRange(frontPieceCells);
            allCells.AddRange(backPieceCells);

            for (var i = 1; i < allCells.Count; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    if (allCells[i] == allCells[j])
                    {
                        return false;
                    }
                }
            }

            for (var i = 1; i < allCells.Count; i++)
            {
                if (ManhattanDistance(allCells[i - 1], allCells[i]) != 1)
                {
                    return false;
                }
            }

            combinedCells = allCells.ToArray();
            return true;
        }

        private static bool CanConnectSameDirectionPieces(Vector2Int[] frontPieceCells, Vector2Int[] backPieceCells, int direction)
        {
            if (frontPieceCells == null || backPieceCells == null || frontPieceCells.Length == 0 || backPieceCells.Length == 0)
            {
                return false;
            }

            return frontPieceCells[frontPieceCells.Length - 1] == backPieceCells[0] + DirectionDelta(direction);
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static void RemoveMergedSolutionSteps(List<Vector2Int> solution, List<GeneratedPiece> pieces, bool[] removed)
        {
            for (var i = solution.Count - 1; i >= 0; i--)
            {
                if (IsMergedPieceHead(solution[i], pieces, removed))
                {
                    solution.RemoveAt(i);
                }
            }
        }

        private static bool IsMergedPieceHead(Vector2Int head, List<GeneratedPiece> pieces, bool[] removed)
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                if (removed[i] && pieces[i].Head == head)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GeneratedPiece> BuildGeneratedPieces(bool[,] layout, int seed, int minPieceLength, int maxPieceLength)
        {
            minPieceLength = Mathf.Max(1, minPieceLength);
            maxPieceLength = Mathf.Max(minPieceLength, maxPieceLength);
            var width = layout.GetLength(0);
            var height = layout.GetLength(1);
            var assigned = new bool[width, height];
            var pieces = new List<GeneratedPiece>();
            var random = new System.Random(unchecked(seed + 313));
            for (var y = 0; y < height; y++)
            {
                var start = (y & 1) == 0 ? 0 : width - 1;
                var end = (y & 1) == 0 ? width : -1;
                var step = (y & 1) == 0 ? 1 : -1;
                for (var x = start; x != end; x += step)
                {
                    if (!layout[x, y] || assigned[x, y])
                    {
                        continue;
                    }

                    var head = new Vector2Int(x, y);
                    var targetLength = minPieceLength + random.Next(maxPieceLength - minPieceLength + 1);
                    var shapeDirection = random.Next(4);
                    var cells = BuildGeneratedPieceCells(layout, assigned, head, targetLength, shapeDirection, random);
                    var piece = new GeneratedPiece(pieces.Count, cells[0], cells.ToArray());
                    pieces.Add(piece);
                    for (var i = 0; i < cells.Count; i++)
                    {
                        var cell = cells[i];
                        assigned[cell.x, cell.y] = true;
                    }
                }
            }

            return pieces;
        }

        private static List<Vector2Int> BuildGeneratedPieceCells(
            bool[,] layout,
            bool[,] assigned,
            Vector2Int head,
            int targetLength,
            int shapeDirection,
            System.Random random)
        {
            var width = layout.GetLength(0);
            var height = layout.GetLength(1);
            var cells = new List<Vector2Int>(targetLength) { head };
            var cursor = head;
            var travelFromHead = -DirectionDelta(shapeDirection);
            var reserved = new bool[width, height];
            reserved[head.x, head.y] = true;
            while (cells.Count < targetLength)
            {
                var directions = BuildSnakeBodyDirectionPriority(cursor, travelFromHead, shapeDirection, cells.Count > 1);
                var offset = random.Next(directions.Length);
                var foundNext = false;
                for (var i = 0; i < directions.Length; i++)
                {
                    var candidate = cursor + directions[(offset + i) % directions.Length];
                    if (!CanUseGeneratedPieceCell(layout, candidate, assigned, reserved)
                        || TouchesReservedNonCursor(candidate, cursor, reserved))
                    {
                        continue;
                    }

                    cells.Add(candidate);
                    reserved[candidate.x, candidate.y] = true;
                    travelFromHead = candidate - cursor;
                    cursor = candidate;
                    foundNext = true;
                    break;
                }

                if (!foundNext)
                {
                    break;
                }
            }

            return cells;
        }

        private static List<Vector2Int> AssignGeneratedPieceDirections(bool[,] layout, List<GeneratedPiece> pieces, int[,] directions, int seed)
        {
            var width = layout.GetLength(0);
            var height = layout.GetLength(1);
            var active = Copy(layout);
            var pieceByCell = new GeneratedPiece[width, height];
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                piece.Active = true;
                for (var c = 0; c < piece.Cells.Length; c++)
                {
                    var cell = piece.Cells[c];
                    pieceByCell[cell.x, cell.y] = piece;
                }
            }

            var random = new System.Random(unchecked(seed + 991));
            var solution = new List<Vector2Int>();
            var remaining = pieces.Count;
            var stepIndex = 0;
            while (remaining > 0)
            {
                var candidates = new List<GeneratedCandidate>();
                for (var i = 0; i < pieces.Count; i++)
                {
                    var piece = pieces[i];
                    if (!piece.Active)
                    {
                        continue;
                    }

                    var clearDirections = FindClearDirections(active, pieceByCell, piece);
                    if (clearDirections.Count > 0)
                    {
                        candidates.Add(new GeneratedCandidate(piece, clearDirections, MaxOriginalBlockers(layout, pieceByCell, piece, clearDirections)));
                    }
                }

                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException("Arrow escape level cannot be peeled.");
                }

                if (stepIndex == 0)
                {
                    candidates.Sort(CompareOpeningGeneratedCandidates);
                }
                else
                {
                    candidates.Sort(CompareStagedGeneratedCandidates);
                }
                var groupLimit = 1;
                if (stepIndex == 0)
                {
                    var firstOptionCount = candidates[0].Directions.Count;
                    while (groupLimit < candidates.Count && candidates[groupLimit].Directions.Count == firstOptionCount)
                    {
                        groupLimit++;
                    }
                }
                else
                {
                    var firstBlockerCount = candidates[0].MaxOriginalBlockers;
                    while (groupLimit < candidates.Count && candidates[groupLimit].MaxOriginalBlockers == firstBlockerCount)
                    {
                        groupLimit++;
                    }
                }

                var candidate = candidates[random.Next(groupLimit)];
                candidate.Directions.Sort(delegate(int left, int right)
                {
                    var conflictCompare = CountAssignedSameDirectionConflicts(pieceByCell, directions, candidate.Piece, left)
                        .CompareTo(CountAssignedSameDirectionConflicts(pieceByCell, directions, candidate.Piece, right));
                    if (conflictCompare != 0)
                    {
                        return conflictCompare;
                    }

                    var blockerCompare = CountOriginalBlockers(layout, pieceByCell, candidate.Piece, right)
                        .CompareTo(CountOriginalBlockers(layout, pieceByCell, candidate.Piece, left));
                    return blockerCompare != 0 ? blockerCompare : left.CompareTo(right);
                });
                var bestConflictCount = CountAssignedSameDirectionConflicts(pieceByCell, directions, candidate.Piece, candidate.Directions[0]);
                var bestBlockerCount = CountOriginalBlockers(layout, pieceByCell, candidate.Piece, candidate.Directions[0]);
                var directionGroupLimit = 1;
                while (directionGroupLimit < candidate.Directions.Count
                    && CountAssignedSameDirectionConflicts(pieceByCell, directions, candidate.Piece, candidate.Directions[directionGroupLimit]) == bestConflictCount
                    && CountOriginalBlockers(layout, pieceByCell, candidate.Piece, candidate.Directions[directionGroupLimit]) == bestBlockerCount)
                {
                    directionGroupLimit++;
                }

                var direction = candidate.Directions[random.Next(directionGroupLimit)];
                candidate.Piece.Direction = direction;
                candidate.Piece.Active = false;
                directions[candidate.Piece.Head.x, candidate.Piece.Head.y] = direction;
                for (var i = 0; i < candidate.Piece.Cells.Length; i++)
                {
                    var cell = candidate.Piece.Cells[i];
                    active[cell.x, cell.y] = false;
                }

                remaining--;
                stepIndex++;
                solution.Add(candidate.Piece.Head);
            }

            return solution;
        }

        private static int CountAssignedSameDirectionConflicts(GeneratedPiece[,] pieceByCell, int[,] directions, GeneratedPiece piece, int direction)
        {
            var conflicts = 0;
            var frontPiece = FindFrontGeneratedPieceInRay(pieceByCell, piece.Head, direction);
            if (frontPiece != null && directions[frontPiece.Head.x, frontPiece.Head.y] == direction)
            {
                conflicts++;
            }

            for (var y = 0; y < pieceByCell.GetLength(1); y++)
            {
                for (var x = 0; x < pieceByCell.GetLength(0); x++)
                {
                    var other = pieceByCell[x, y];
                    if (other == null || other == piece || other.Head.x != x || other.Head.y != y)
                    {
                        continue;
                    }

                    if (directions[x, y] != direction)
                    {
                        continue;
                    }

                    var otherFrontPiece = FindFrontGeneratedPieceInRay(pieceByCell, other.Head, direction);
                    if (otherFrontPiece == piece)
                    {
                        conflicts++;
                    }
                }
            }

            return conflicts;
        }

        private static GeneratedPiece FindFrontGeneratedPieceInRay(GeneratedPiece[,] pieceByCell, Vector2Int head, int direction)
        {
            var delta = DirectionDelta(direction);
            var x = head.x + delta.x;
            var y = head.y + delta.y;
            while (x >= 0 && y >= 0 && x < pieceByCell.GetLength(0) && y < pieceByCell.GetLength(1))
            {
                var piece = pieceByCell[x, y];
                if (piece != null)
                {
                    return piece;
                }

                x += delta.x;
                y += delta.y;
            }

            return null;
        }

        public static bool CanEscape(bool[,] active, int[,] directions, int x, int y)
        {
            if (active == null || directions == null || x < 0 || y < 0 || x >= active.GetLength(0) || y >= active.GetLength(1) || !active[x, y])
            {
                return false;
            }

            var direction = directions[x, y];
            if (direction < 0)
            {
                return false;
            }

            var delta = DirectionDelta(direction);
            x += delta.x;
            y += delta.y;
            while (x >= 0 && y >= 0 && x < active.GetLength(0) && y < active.GetLength(1))
            {
                if (active[x, y])
                {
                    return false;
                }

                x += delta.x;
                y += delta.y;
            }

            return true;
        }

        public static bool CanEscapePiece(bool[,] active, int[,] directions, Vector2Int[] cells)
        {
            if (active == null || directions == null || cells == null || cells.Length == 0)
            {
                return false;
            }

            var head = cells[0];
            if (head.x < 0 || head.y < 0 || head.x >= active.GetLength(0) || head.y >= active.GetLength(1) || !active[head.x, head.y])
            {
                return false;
            }

            var direction = directions[head.x, head.y];
            if (direction < 0)
            {
                return false;
            }

            var delta = DirectionDelta(direction);
            var x = head.x + delta.x;
            var y = head.y + delta.y;
            while (x >= 0 && y >= 0 && x < active.GetLength(0) && y < active.GetLength(1))
            {
                if (active[x, y] && !ContainsCell(cells, x, y))
                {
                    return false;
                }

                x += delta.x;
                y += delta.y;
            }

            return true;
        }

        protected override MiniGameShellLayout CreateShellLayout()
        {
            return new MiniGameShellLayout(116f, 226f, MiniGameShellBottomMode.DefaultSlot);
        }

        protected override void BuildOrBindSections()
        {
            levelProgress = new MiniGameLevelProgressController(HostBehaviour, GameIdConstant, LevelDefinitions.Length);

            var topBarRefs = MiniGameShellTopBarBuilder.CreateTopBar(
                Shell.TopHost,
                MiniGameShellTopBarBuilder.CreateDefaultConfig("ArrowEscapeHeader"));
            titleLabel = topBarRefs.TitleText;
            scoreLabel = topBarRefs.ScoreText;

            BuildContent();
            BuildBottom();
        }

        protected override void ResetGame()
        {
            Shell.ClosePopup();
            CloseRewardSettlementPanel();
            isAnimating = false;
            activeFlyAnimationCount = 0;
            settlementShown = false;
            moveHistory.Clear();
            hintedCell = new Vector2Int(-1, -1);
            moveCount = 0;
            blockedTapCount = 0;
            hintCount = 0;
            combo = 0;
            score = 0;

            currentLevelIndex = Mathf.Clamp(levelProgress.CurrentLevelIndex, 0, LevelDefinitions.Length - 1);
            var level = LevelDefinitions[currentLevelIndex];
            currentPuzzle = GenerateRuntimePuzzle(level, currentLevelIndex);
            activeTiles = Copy(currentPuzzle.Layout);
            BuildArrowPieces();
            remainingTileCount = CountActive(activeTiles);
            RebuildBoard();
            HostBehaviour.StartCoroutine(ResizeBoardAfterLayout());
            RefreshAll(UiTextCatalog.Get("arrow-escape.status.ready"));
        }

        protected override (string helpKey, string creditsKey)? GetPauseHelpKeys()
        {
            return ("game.arrow-escape.help", null);
        }

        protected override void OnPauseRequested()
        {
            if (!settlementShown)
            {
                Shell.ShowPausePopup(ResumeFromPause, ConfirmExitToHall);
            }
        }

        protected override void OnBeforeDispose()
        {
            Shell.ClosePopup();
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (hintButton != null)
            {
                hintButton.onClick.RemoveListener(OnHintClicked);
            }

            if (undoButton != null)
            {
                undoButton.onClick.RemoveListener(OnUndoClicked);
            }

            if (zoomSlider != null)
            {
                zoomSlider.onValueChanged.RemoveListener(OnZoomSliderChanged);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.RemoveListener(OnZoomOutClicked);
            }

            if (zoomInButton != null)
            {
                zoomInButton.onClick.RemoveListener(OnZoomInClicked);
            }
        }

        private static int CompareCandidates(Candidate left, Candidate right)
        {
            var optionCompare = left.Directions.Count.CompareTo(right.Directions.Count);
            if (optionCompare != 0)
            {
                return optionCompare;
            }

            var yCompare = left.Y.CompareTo(right.Y);
            return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
        }

        private static int CompareOpeningGeneratedCandidates(GeneratedCandidate left, GeneratedCandidate right)
        {
            var optionCompare = left.Directions.Count.CompareTo(right.Directions.Count);
            if (optionCompare != 0)
            {
                return optionCompare;
            }

            var lengthCompare = right.Piece.Cells.Length.CompareTo(left.Piece.Cells.Length);
            if (lengthCompare != 0)
            {
                return lengthCompare;
            }

            var yCompare = left.Piece.Head.y.CompareTo(right.Piece.Head.y);
            return yCompare != 0 ? yCompare : left.Piece.Head.x.CompareTo(right.Piece.Head.x);
        }

        private static int CompareStagedGeneratedCandidates(GeneratedCandidate left, GeneratedCandidate right)
        {
            var blockerCompare = right.MaxOriginalBlockers.CompareTo(left.MaxOriginalBlockers);
            if (blockerCompare != 0)
            {
                return blockerCompare;
            }

            return CompareOpeningGeneratedCandidates(left, right);
        }

        private static List<int> FindClearDirections(bool[,] active, int x, int y)
        {
            var result = new List<int>();
            for (var direction = 0; direction < 4; direction++)
            {
                if (RayIsClear(active, x, y, direction))
                {
                    result.Add(direction);
                }
            }

            return result;
        }

        private static List<int> FindClearDirections(bool[,] active, GeneratedPiece[,] pieceByCell, GeneratedPiece piece)
        {
            var result = new List<int>();
            for (var direction = 0; direction < 4; direction++)
            {
                if (BodyStartsInFront(piece, direction))
                {
                    continue;
                }

                if (RayIsClear(active, pieceByCell, piece, direction))
                {
                    result.Add(direction);
                }
            }

            return result;
        }

        private static bool BodyStartsInFront(GeneratedPiece piece, int direction)
        {
            return piece.Cells.Length > 1 && piece.Cells[1] == piece.Head + DirectionDelta(direction);
        }

        private static bool BodyStartsInFront(Vector2Int[] cells, int direction)
        {
            return cells != null && cells.Length > 1 && cells[1] == cells[0] + DirectionDelta(direction);
        }

        private static bool RayIsClear(bool[,] active, int x, int y, int direction)
        {
            var delta = DirectionDelta(direction);
            x += delta.x;
            y += delta.y;
            while (x >= 0 && y >= 0 && x < active.GetLength(0) && y < active.GetLength(1))
            {
                if (active[x, y])
                {
                    return false;
                }

                x += delta.x;
                y += delta.y;
            }

            return true;
        }

        private static bool RayIsClear(bool[,] active, GeneratedPiece[,] pieceByCell, GeneratedPiece piece, int direction)
        {
            var delta = DirectionDelta(direction);
            var x = piece.Head.x + delta.x;
            var y = piece.Head.y + delta.y;
            while (x >= 0 && y >= 0 && x < active.GetLength(0) && y < active.GetLength(1))
            {
                if (active[x, y] && pieceByCell[x, y] != piece)
                {
                    return false;
                }

                x += delta.x;
                y += delta.y;
            }

            return true;
        }

        private static int MaxOriginalBlockers(bool[,] layout, GeneratedPiece[,] pieceByCell, GeneratedPiece piece, List<int> directions)
        {
            var maxBlockers = 0;
            for (var i = 0; i < directions.Count; i++)
            {
                maxBlockers = Mathf.Max(maxBlockers, CountOriginalBlockers(layout, pieceByCell, piece, directions[i]));
            }

            return maxBlockers;
        }

        private static int CountOriginalBlockers(bool[,] layout, GeneratedPiece[,] pieceByCell, GeneratedPiece piece, int direction)
        {
            var delta = DirectionDelta(direction);
            var x = piece.Head.x + delta.x;
            var y = piece.Head.y + delta.y;
            var blockers = 0;
            while (x >= 0 && y >= 0 && x < layout.GetLength(0) && y < layout.GetLength(1))
            {
                if (layout[x, y] && pieceByCell[x, y] != piece)
                {
                    blockers++;
                }

                x += delta.x;
                y += delta.y;
            }

            return blockers;
        }

        private void BuildContent()
        {
            var root = CreateRectObject("ArrowEscapeContent", Shell.ContentHost);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(14f, 2f), new Vector2(-14f, -6f));
            EnsureRoundedRectGraphic(root.gameObject, PanelColor, 8f, false);

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            statusLabel = CreateText("StatusLabel", root, 24f, FontStyles.Bold, new Color32(48, 68, 54, 255));
            statusLabel.enableAutoSizing = true;
            statusLabel.fontSizeMin = 17f;
            statusLabel.fontSizeMax = 24f;
            statusLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            boardPanel = CreateRectObject("ArrowEscapeBoardPanel", root);
            var boardLayout = boardPanel.gameObject.AddComponent<LayoutElement>();
            boardLayout.preferredHeight = 622f;
            boardLayout.flexibleHeight = 1f;
            EnsureRoundedRectGraphic(boardPanel.gameObject, BoardColor, 0f, true);
            boardPanel.gameObject.AddComponent<RectMask2D>();
            var resizeWatcher = boardPanel.gameObject.AddComponent<ArrowEscapeBoardPanelResizeWatcher>();
            resizeWatcher.Initialize(OnBoardPanelDimensionsChanged);
            var dragHandler = boardPanel.gameObject.AddComponent<ArrowEscapeBoardDragHandler>();
            dragHandler.OnDragDelta = OnBoardDragged;

            boardRoot = CreateRectObject("ArrowEscapeBoard", boardPanel);
            boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
            boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            boardRoot.pivot = new Vector2(0.5f, 0.5f);
            boardRoot.anchoredPosition = Vector2.zero;
            boardGrid = boardRoot.gameObject.AddComponent<GridLayoutGroup>();
            boardGrid.childAlignment = TextAnchor.MiddleCenter;

            pieceLayer = CreateRectObject("ArrowEscapePieceLayer", boardPanel);
            pieceLayer.anchorMin = new Vector2(0.5f, 0.5f);
            pieceLayer.anchorMax = new Vector2(0.5f, 0.5f);
            pieceLayer.pivot = new Vector2(0.5f, 0.5f);
            pieceLayer.anchoredPosition = Vector2.zero;
        }

        private void BuildBottom()
        {
            var root = CreateRectObject("ArrowEscapeControls", Shell.BottomHost);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -12f));

            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateZoomControl(root);

            var toolRow = CreateRectObject("ArrowEscapeToolRow", root);
            toolRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            var toolLayout = toolRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolLayout.spacing = 12f;
            toolLayout.childAlignment = TextAnchor.MiddleCenter;
            toolLayout.childControlWidth = false;
            toolLayout.childControlHeight = false;
            toolLayout.childForceExpandWidth = false;
            toolLayout.childForceExpandHeight = false;

            undoButton = CreateTextButton("ArrowEscapeUndoButton", toolRow, UiTextCatalog.Get("arrow-escape.action.undo"), new Vector2(126f, 74f), new Color32(87, 123, 178, 255));
            hintButton = CreateTextButton("ArrowEscapeHintButton", toolRow, UiTextCatalog.Get("common.action.hint"), new Vector2(126f, 74f), new Color32(76, 150, 119, 255));
            restartButton = CreateTextButton("RestartButton", toolRow, UiTextCatalog.Get("common.action.restart"), new Vector2(126f, 74f), new Color32(191, 128, 76, 255));

            undoButton.onClick.AddListener(OnUndoClicked);
            hintButton.onClick.AddListener(OnHintClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void CreateZoomControl(Transform parent)
        {
            var zoomRoot = CreateRectObject("ArrowEscapeZoomControl", parent);
            var layoutElement = zoomRoot.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 330f;
            layoutElement.preferredHeight = 52f;
            EnsureRoundedRectGraphic(zoomRoot.gameObject, new Color32(248, 251, 255, 245), 18f, true);

            var layout = zoomRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 6, 6);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            zoomOutButton = CreateZoomIconButton("ArrowEscapeZoomOutButton", zoomRoot, false);
            zoomSlider = CreateZoomSlider(zoomRoot);
            zoomInButton = CreateZoomIconButton("ArrowEscapeZoomInButton", zoomRoot, true);

            zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);
            zoomOutButton.onClick.AddListener(OnZoomOutClicked);
            zoomInButton.onClick.AddListener(OnZoomInClicked);
        }

        private void RebuildBoard()
        {
            ClearChildren(boardRoot);
            ClearChildren(pieceLayer);
            var width = currentPuzzle.Layout.GetLength(0);
            var height = currentPuzzle.Layout.GetLength(1);
            var cellSize = GetBoardCellSize(width, height, false);
            var spacing = 0f;
            boardCellSize = cellSize;
            boardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardGrid.constraintCount = width;
            boardGrid.cellSize = new Vector2(cellSize, cellSize);
            boardGrid.spacing = new Vector2(spacing, spacing);
            boardRoot.sizeDelta = new Vector2(width * cellSize + Mathf.Max(0, width - 1) * spacing, height * cellSize + Mathf.Max(0, height - 1) * spacing);
            boardPan = Vector2.zero;
            boardZoom = 1f;
            SyncPieceLayerToBoard();
            UpdateBoardZoomLimits(true);

            tiles = new TileView[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    tiles[x, y] = CreateTile(x, y, cellSize);
                }
            }

            RebuildPieceVisuals();
        }

        private IEnumerator ResizeBoardAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (currentPuzzle.Layout == null || boardGrid == null)
            {
                yield break;
            }

            var width = currentPuzzle.Layout.GetLength(0);
            var height = currentPuzzle.Layout.GetLength(1);
            var cellSize = GetBoardCellSize(width, height, true);
            boardCellSize = cellSize;
            boardGrid.cellSize = new Vector2(cellSize, cellSize);
            boardRoot.sizeDelta = new Vector2(width * cellSize, height * cellSize);
            SyncPieceLayerToBoard();
            UpdateBoardZoomLimits(true);
            RebuildPieceVisuals();
            RefreshBoard();
        }

        private void OnBoardPanelDimensionsChanged()
        {
            if (boardRoot == null || boardPanel == null || currentPuzzle.Layout == null)
            {
                return;
            }

            UpdateBoardZoomLimits(true);
        }

        private void SyncPieceLayerToBoard()
        {
            if (pieceLayer == null || boardRoot == null)
            {
                return;
            }

            pieceLayer.sizeDelta = boardRoot.sizeDelta;
            ApplyBoardTransform();
        }

        private float GetBoardCellSize(int width, int height, bool useActualPanel)
        {
            var maxSize = Mathf.Max(width, height);
            return maxSize <= 10 ? 50f : 30f;
        }

        private void UpdateBoardZoomLimits(bool resetSlider)
        {
            if (boardRoot == null || boardPanel == null)
            {
                return;
            }

            var boardSize = boardRoot.sizeDelta;
            var panelSize = boardPanel.rect.size;
            if (boardSize.x <= 1f || boardSize.y <= 1f || panelSize.x <= 1f || panelSize.y <= 1f)
            {
                minBoardZoom = 1f;
            }
            else
            {
                var fitWidth = Mathf.Max(0.1f, (panelSize.x - 24f) / boardSize.x);
                var fitHeight = Mathf.Max(0.1f, (panelSize.y - 24f) / boardSize.y);
                minBoardZoom = Mathf.Clamp(Mathf.Min(1f, fitWidth, fitHeight), 0.48f, 1f);
            }

            if (resetSlider && zoomSlider != null)
            {
                zoomSlider.SetValueWithoutNotify(0f);
                boardZoom = minBoardZoom;
                boardPan = Vector2.zero;
            }
            else
            {
                boardZoom = Mathf.Clamp(boardZoom, minBoardZoom, MaxBoardZoom);
                ClampBoardPan();
            }

            ApplyBoardTransform();
        }

        private void OnZoomSliderChanged(float value)
        {
            boardZoom = Mathf.Lerp(minBoardZoom, MaxBoardZoom, Mathf.Clamp01(value));
            ClampBoardPan();
            ApplyBoardTransform();
        }

        private void OnZoomOutClicked()
        {
            if (zoomSlider == null)
            {
                return;
            }

            zoomSlider.value = Mathf.Max(0f, zoomSlider.value - 0.16f);
        }

        private void OnZoomInClicked()
        {
            if (zoomSlider == null)
            {
                return;
            }

            zoomSlider.value = Mathf.Min(1f, zoomSlider.value + 0.16f);
        }

        private void OnBoardDragged(Vector2 screenDelta)
        {
            if (boardZoom <= minBoardZoom + 0.01f)
            {
                return;
            }

            var canvas = boardPanel != null ? boardPanel.GetComponentInParent<Canvas>() : null;
            var scaleFactor = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            boardPan += screenDelta / scaleFactor;
            ClampBoardPan();
            ApplyBoardTransform();
        }

        private void ClampBoardPan()
        {
            if (boardRoot == null || boardPanel == null)
            {
                boardPan = Vector2.zero;
                return;
            }

            var panelSize = boardPanel.rect.size;
            var scaledBoardSize = boardRoot.sizeDelta * boardZoom;
            var zoomProgress = Mathf.InverseLerp(minBoardZoom, MaxBoardZoom, boardZoom);
            var extraPan = panelSize * (BoardPanViewportPaddingRatio * zoomProgress);
            var maxX = Mathf.Max(0f, (scaledBoardSize.x - panelSize.x) * 0.5f + extraPan.x);
            var maxY = Mathf.Max(0f, (scaledBoardSize.y - panelSize.y) * 0.5f + extraPan.y);
            boardPan = new Vector2(
                Mathf.Clamp(boardPan.x, -maxX, maxX),
                Mathf.Clamp(boardPan.y, -maxY, maxY));
        }

        private void ApplyBoardTransform()
        {
            if (boardRoot != null)
            {
                boardRoot.localScale = Vector3.one * boardZoom;
                boardRoot.anchoredPosition = boardPan;
            }

            if (pieceLayer != null)
            {
                pieceLayer.localScale = Vector3.one * boardZoom;
                pieceLayer.anchoredPosition = boardPan;
            }
        }

        private void BuildArrowPieces()
        {
            arrowPieces.Clear();
            var width = currentPuzzle.Layout.GetLength(0);
            var height = currentPuzzle.Layout.GetLength(1);
            pieceByCell = new ArrowPiece[width, height];
            var assigned = new bool[width, height];
            for (var i = 0; i < currentPuzzle.Pieces.Length; i++)
            {
                var cells = currentPuzzle.Pieces[i];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }

                var head = cells[0];
                if (assigned[head.x, head.y] || !currentPuzzle.Layout[head.x, head.y])
                {
                    continue;
                }

                var direction = currentPuzzle.Directions[head.x, head.y];
                var piece = new ArrowPiece(arrowPieces.Count, direction, head, cells);
                arrowPieces.Add(piece);
                for (var c = 0; c < cells.Length; c++)
                {
                    var cell = cells[c];
                    if (!currentPuzzle.Layout[cell.x, cell.y] || assigned[cell.x, cell.y])
                    {
                        continue;
                    }

                    assigned[cell.x, cell.y] = true;
                    pieceByCell[cell.x, cell.y] = piece;
                }
            }
        }

        private static Vector2Int[] BuildSnakeBodyDirectionPriority(Vector2Int cursor, Vector2Int travelFromHead, int headDirection, bool allowReverse)
        {
            var left = new Vector2Int(-travelFromHead.y, travelFromHead.x);
            var right = new Vector2Int(travelFromHead.y, -travelFromHead.x);
            if (((cursor.x * 19 + cursor.y * 23 + headDirection * 7) & 1) != 0)
            {
                var temp = left;
                left = right;
                right = temp;
            }

            if (allowReverse)
            {
                return new[]
                {
                    travelFromHead,
                    left,
                    right,
                    -travelFromHead
                };
            }

            return new[]
            {
                travelFromHead,
                left,
                right
            };
        }

        private static bool CanUseGeneratedPieceCell(bool[,] layout, Vector2Int cell, bool[,] assigned, bool[,] reserved)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < layout.GetLength(0)
                && cell.y < layout.GetLength(1)
                && layout[cell.x, cell.y]
                && !assigned[cell.x, cell.y]
                && !reserved[cell.x, cell.y];
        }

        private static bool TouchesReservedNonCursor(Vector2Int candidate, Vector2Int cursor, bool[,] reserved)
        {
            for (var direction = 0; direction < 4; direction++)
            {
                var neighbor = candidate + DirectionDelta(direction);
                if (neighbor.x < 0 || neighbor.y < 0 || neighbor.x >= reserved.GetLength(0) || neighbor.y >= reserved.GetLength(1))
                {
                    continue;
                }

                if (neighbor != cursor && reserved[neighbor.x, neighbor.y])
                {
                    return true;
                }
            }

            return false;
        }

        private TileView CreateTile(int x, int y, float size)
        {
            var tileObject = new GameObject("ArrowEscapeTile_" + x + "_" + y, typeof(RectTransform), typeof(Button), typeof(CanvasGroup), typeof(LayoutElement), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            tileObject.transform.SetParent(boardRoot, false);
            var rect = tileObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            var layout = tileObject.GetComponent<LayoutElement>();
            layout.preferredWidth = size;
            layout.preferredHeight = size;

            var graphic = tileObject.GetComponent<RoundedRectGraphic>();
            graphic.CornerRadius = 0f;
            graphic.color = new Color(1f, 1f, 1f, 0f);
            graphic.raycastTarget = true;

            var button = tileObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            ConfigureButtonColors(button);
            var capturedX = x;
            var capturedY = y;
            button.onClick.AddListener(delegate { OnTileClicked(capturedX, capturedY); });

            return new TileView
            {
                X = x,
                Y = y,
                Rect = rect,
                Button = button,
                Background = graphic,
                CanvasGroup = tileObject.GetComponent<CanvasGroup>(),
                HomePosition = Vector2.zero
            };
        }

        private void RebuildPieceVisuals()
        {
            if (pieceLayer == null || arrowPieces.Count == 0)
            {
                return;
            }

            ClearChildren(pieceLayer);
            for (var i = 0; i < arrowPieces.Count; i++)
            {
                var piece = arrowPieces[i];
                var visualObject = new GameObject("ArrowEscapePiece_" + piece.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(ArrowEscapePiecePathGraphic));
                visualObject.transform.SetParent(pieceLayer, false);
                var rect = visualObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = pieceLayer.sizeDelta;

                var graphic = visualObject.GetComponent<ArrowEscapePiecePathGraphic>();
                graphic.raycastTarget = false;
                piece.VisualRoot = rect;
                piece.Visual = graphic;
                ResetPieceVisualToHome(piece);
            }

            RefreshPieceVisuals();
        }

        private void RefreshPieceVisuals()
        {
            if (arrowPieces.Count == 0)
            {
                return;
            }

            for (var i = 0; i < arrowPieces.Count; i++)
            {
                var piece = arrowPieces[i];
                if (piece.Visual == null)
                {
                    continue;
                }

                var visible = PieceHasVisibleCells(piece);
                piece.Visual.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                if (piece.Active || !piece.IsFlying)
                {
                    ResetPieceVisualToHome(piece);
                }

                var color = piece.IsFlying || (piece.Active && CanPieceEscape(piece))
                    ? PlayableColor
                    : BlockedColor;
                if (hintedCell.x == piece.Head.x && hintedCell.y == piece.Head.y)
                {
                    color = HintColor;
                }

                piece.Visual.color = color;
                piece.Visual.SetAllDirty();
            }
        }

        private void ResetPieceVisualToHome(ArrowPiece piece)
        {
            if (piece.Visual == null)
            {
                return;
            }

            if (piece.VisualRoot != null && pieceLayer != null)
            {
                piece.VisualRoot.sizeDelta = pieceLayer.sizeDelta;
                piece.VisualRoot.anchoredPosition = Vector2.zero;
            }

            piece.HomePoints = BuildPieceHomePoints(piece);
            piece.Visual.SetPath(piece.HomePoints, piece.Direction, boardCellSize);
        }

        private Vector2[] BuildPieceHomePoints(ArrowPiece piece)
        {
            var points = new Vector2[piece.Cells.Length];
            for (var i = 0; i < piece.Cells.Length; i++)
            {
                points[i] = CellToBoardLocal(piece.Cells[i]);
            }

            return points;
        }

        private Vector2 CellToBoardLocal(Vector2Int cell)
        {
            var width = currentPuzzle.Layout.GetLength(0);
            var height = currentPuzzle.Layout.GetLength(1);
            return new Vector2(
                (-width * boardCellSize * 0.5f) + boardCellSize * (cell.x + 0.5f),
                (height * boardCellSize * 0.5f) - boardCellSize * (cell.y + 0.5f));
        }

        private bool PieceHasVisibleCells(ArrowPiece piece)
        {
            if (piece.IsFlying)
            {
                return true;
            }

            if (activeTiles == null)
            {
                return false;
            }

            for (var i = 0; i < piece.Cells.Length; i++)
            {
                var cell = piece.Cells[i];
                if (activeTiles[cell.x, cell.y])
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTileClicked(int x, int y)
        {
            if (settlementShown || !currentPuzzle.Layout[x, y])
            {
                return;
            }

            var piece = pieceByCell[x, y];
            if (piece == null || !piece.Active)
            {
                return;
            }

            if (!CanPieceEscape(piece))
            {
                blockedTapCount++;
                combo = 0;
                score = Mathf.Max(0, score - 3);
                RefreshAll(UiTextCatalog.Get("arrow-escape.status.blocked"));
                HostBehaviour.StartCoroutine(FlashBlockedPiece(piece));
                MiniGameSfxPlayer.Play(MiniGameSfxType.MatchFail, 0.82f);
                return;
            }

            ClearPiece(piece);
        }

        private void ClearPiece(ArrowPiece piece)
        {
            hintedCell = new Vector2Int(-1, -1);
            piece.Active = false;
            piece.IsFlying = true;
            remainingTileCount -= piece.Cells.Length;
            moveCount++;
            combo++;
            score += piece.Cells.Length * 8 + Mathf.Min(combo, 8) * 2;
            moveHistory.Add(new MoveRecord(piece));
            for (var i = 0; i < piece.Cells.Length; i++)
            {
                var cell = piece.Cells[i];
                activeTiles[cell.x, cell.y] = false;
            }

            activeFlyAnimationCount++;
            isAnimating = true;
            RefreshAll(UiTextCatalog.Format("arrow-escape.status.cleared", remainingTileCount));
            HostBehaviour.StartCoroutine(FlyPieceOut(piece));
        }

        private IEnumerator FlyPieceOut(ArrowPiece piece)
        {
            var starts = piece.HomePoints != null && piece.HomePoints.Length == piece.Cells.Length
                ? (Vector2[])piece.HomePoints.Clone()
                : BuildPieceHomePoints(piece);
            for (var i = 0; i < piece.Cells.Length; i++)
            {
                var tile = tiles[piece.Cells[i].x, piece.Cells[i].y];
                tile.Button.interactable = false;
            }

            var delta = DirectionDelta(piece.Direction);
            var panelSize = boardPanel != null ? boardPanel.rect.size : boardRoot.rect.size;
            var baseFlyDistance = Mathf.Max(Mathf.Max(panelSize.x, panelSize.y), Mathf.Max(boardRoot.rect.width, boardRoot.rect.height)) + FlyExitPadding;
            var bodyPathDistance = CalculateBodyPathDistance(starts);
            var flyDistance = baseFlyDistance + bodyPathDistance;
            var path = BuildSnakeFlyPath(starts, new Vector2(delta.x, -delta.y), flyDistance);
            var pathDistances = BuildPathDistances(path);
            var positions = new Vector2[piece.Cells.Length];
            var sampleDistances = new float[piece.Cells.Length];
            var traveled = 0f;
            while (traveled < flyDistance)
            {
                var deltaTime = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 60f, 1f / 30f);
                traveled = Mathf.Min(flyDistance, traveled + FlySpeed * deltaTime);
                for (var i = 0; i < piece.Cells.Length; i++)
                {
                    var pathIndex = piece.Cells.Length - 1 - i;
                    sampleDistances[i] = pathDistances[pathIndex] + traveled;
                    positions[i] = SamplePath(path, pathDistances, sampleDistances[i]);
                }

                for (var i = 0; i < piece.Cells.Length; i++)
                {
                    var tile = tiles[piece.Cells[i].x, piece.Cells[i].y];
                    tile.CanvasGroup.alpha = 1f;
                }

                if (piece.Visual != null)
                {
                    piece.Visual.SetPath(
                        positions,
                        BuildSnakeVisualPath(path, pathDistances, sampleDistances, positions),
                        piece.Direction,
                        boardCellSize);
                    piece.Visual.color = PlayableColor;
                    piece.Visual.gameObject.SetActive(true);
                }

                yield return null;
            }

            for (var i = 0; i < piece.Cells.Length; i++)
            {
                var tile = tiles[piece.Cells[i].x, piece.Cells[i].y];
                tile.CanvasGroup.alpha = 0f;
            }

            if (piece.Visual != null)
            {
                piece.Visual.gameObject.SetActive(false);
                ResetPieceVisualToHome(piece);
            }

            piece.IsFlying = false;
            activeFlyAnimationCount = Mathf.Max(0, activeFlyAnimationCount - 1);
            isAnimating = activeFlyAnimationCount > 0;
            MiniGameSfxPlayer.Play(MiniGameSfxType.TileSelect, 0.9f);
            RefreshAll(remainingTileCount == 0
                ? UiTextCatalog.Get("arrow-escape.status.complete")
                : UiTextCatalog.Format("arrow-escape.status.cleared", remainingTileCount));

            if (remainingTileCount == 0 && activeFlyAnimationCount == 0)
            {
                FinishLevel();
            }
        }

        private static float CalculateBodyPathDistance(Vector2[] starts)
        {
            var distance = 0f;
            for (var i = 1; i < starts.Length; i++)
            {
                distance += Vector2.Distance(starts[i - 1], starts[i]);
            }

            return distance;
        }

        private static Vector2[] BuildSnakeFlyPath(Vector2[] starts, Vector2 exitDirection, float flyDistance)
        {
            var path = new Vector2[starts.Length + 1];
            for (var i = 0; i < starts.Length; i++)
            {
                path[i] = starts[starts.Length - 1 - i];
            }

            path[path.Length - 1] = starts[0] + exitDirection * flyDistance;
            return path;
        }

        private static Vector2[] BuildSnakeVisualPath(Vector2[] path, float[] pathDistances, float[] sampleDistances, Vector2[] samplePositions)
        {
            var visualPath = new List<Vector2>(samplePositions.Length + path.Length);
            AddVisualPathPoint(visualPath, samplePositions[0]);
            for (var i = 0; i + 1 < samplePositions.Length; i++)
            {
                var highDistance = sampleDistances[i];
                var lowDistance = sampleDistances[i + 1];
                if (highDistance >= lowDistance)
                {
                    for (var pathIndex = path.Length - 2; pathIndex >= 1; pathIndex--)
                    {
                        var vertexDistance = pathDistances[pathIndex];
                        if (vertexDistance < highDistance - 0.01f && vertexDistance > lowDistance + 0.01f)
                        {
                            AddVisualPathPoint(visualPath, path[pathIndex]);
                        }
                    }
                }
                else
                {
                    for (var pathIndex = 1; pathIndex + 1 < path.Length; pathIndex++)
                    {
                        var vertexDistance = pathDistances[pathIndex];
                        if (vertexDistance > highDistance + 0.01f && vertexDistance < lowDistance - 0.01f)
                        {
                            AddVisualPathPoint(visualPath, path[pathIndex]);
                        }
                    }
                }

                AddVisualPathPoint(visualPath, samplePositions[i + 1]);
            }

            return visualPath.ToArray();
        }

        private static void AddVisualPathPoint(List<Vector2> visualPath, Vector2 point)
        {
            if (visualPath.Count > 0 && (visualPath[visualPath.Count - 1] - point).sqrMagnitude < 0.01f)
            {
                return;
            }

            visualPath.Add(point);
        }

        private static float[] BuildPathDistances(Vector2[] path)
        {
            var distances = new float[path.Length];
            for (var i = 1; i < path.Length; i++)
            {
                distances[i] = distances[i - 1] + Vector2.Distance(path[i - 1], path[i]);
            }

            return distances;
        }

        private static Vector2 SamplePath(Vector2[] path, float[] distances, float distance)
        {
            if (path.Length == 0)
            {
                return Vector2.zero;
            }

            if (distance <= 0f)
            {
                return path[0];
            }

            var lastIndex = path.Length - 1;
            if (distance >= distances[lastIndex])
            {
                return path[lastIndex];
            }

            for (var i = 1; i < path.Length; i++)
            {
                if (distance > distances[i])
                {
                    continue;
                }

                var segmentDistance = distances[i] - distances[i - 1];
                if (segmentDistance <= Mathf.Epsilon)
                {
                    return path[i];
                }

                var t = (distance - distances[i - 1]) / segmentDistance;
                return Vector2.LerpUnclamped(path[i - 1], path[i], t);
            }

            return path[lastIndex];
        }

        private IEnumerator FlashBlockedPiece(ArrowPiece piece)
        {
            if (piece.Visual != null)
            {
                piece.Visual.color = FlashColor;
                piece.Visual.SetAllDirty();
            }

            yield return new WaitForSeconds(BlockFlashDuration);
            if (activeTiles != null)
            {
                RefreshPieceVisuals();
            }
        }

        private void OnUndoClicked()
        {
            if (isAnimating || settlementShown || moveHistory.Count == 0)
            {
                return;
            }

            var move = moveHistory[moveHistory.Count - 1];
            moveHistory.RemoveAt(moveHistory.Count - 1);
            move.Piece.Active = true;
            for (var i = 0; i < move.Piece.Cells.Length; i++)
            {
                var cell = move.Piece.Cells[i];
                activeTiles[cell.x, cell.y] = true;
            }

            remainingTileCount += move.Piece.Cells.Length;
            moveCount = Mathf.Max(0, moveCount - 1);
            combo = 0;
            score = Mathf.Max(0, score - 5);
            hintedCell = new Vector2Int(-1, -1);
            for (var i = 0; i < move.Piece.Cells.Length; i++)
            {
                var tile = tiles[move.Piece.Cells[i].x, move.Piece.Cells[i].y];
                tile.CanvasGroup.alpha = 1f;
                tile.Rect.localScale = Vector3.one;
            }

            ResetPieceVisualToHome(move.Piece);
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiBack, 0.88f);
            RefreshAll(UiTextCatalog.Get("arrow-escape.status.undo"));
        }

        private void OnHintClicked()
        {
            if (isAnimating || settlementShown)
            {
                return;
            }

            var hint = FindBestHint();
            if (!hint.HasValue)
            {
                RefreshAll(UiTextCatalog.Get("arrow-escape.status.no_hint"));
                return;
            }

            hintCount++;
            combo = 0;
            score = Mathf.Max(0, score - 2);
            hintedCell = hint.Value;
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.9f);
            RefreshAll(UiTextCatalog.Get("arrow-escape.status.hint"));
        }

        private Vector2Int? FindBestHint()
        {
            Vector2Int? best = null;
            var bestDistance = -1;
            for (var y = 0; y < activeTiles.GetLength(1); y++)
            {
                for (var x = 0; x < activeTiles.GetLength(0); x++)
                {
                    var piece = pieceByCell[x, y];
                    if (piece == null || piece.Head.x != x || piece.Head.y != y || !CanPieceEscape(piece))
                    {
                        continue;
                    }

                    var distance = DistanceToEdge(x, y, piece.Direction, activeTiles.GetLength(0), activeTiles.GetLength(1));
                    if (!best.HasValue || distance > bestDistance)
                    {
                        best = new Vector2Int(x, y);
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private void FinishLevel()
        {
            if (settlementShown)
            {
                return;
            }

            settlementShown = true;
            levelProgress.UnlockNext();
            var hasNext = currentLevelIndex + 1 < LevelDefinitions.Length;
            score += Mathf.Max(0, remainingTileCount == 0 ? 40 - moveCount - blockedTapCount * 2 - hintCount * 2 : 0);
            RefreshAll(UiTextCatalog.Get("arrow-escape.status.complete"));
            MiniGameSfxPlayer.Play(MiniGameSfxType.Settle, 1f);
            var settlement = CreateSettlement(true);
            ShowRewardSettlementPanel(
                settlement,
                new MiniGameRewardSettlementPanelParams
                {
                    RootName = "ArrowEscapeSettlementPanel",
                    Style = MiniGameRewardSettlementPanelStyle.Success,
                    PrimaryAction = hasNext ? MiniGameRewardSettlementPrimaryAction.NextLevel : MiniGameRewardSettlementPrimaryAction.Retry,
                    Title = UiTextCatalog.Get("arrow-escape.settlement.win_title"),
                    PrimaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("arrow-escape.settlement.score"), settlement.Score.ToString()),
                    SecondaryInfo = new MiniGameSettlementInfoRow(UiTextCatalog.Get("arrow-escape.settlement.moves"), moveCount.ToString()),
                    RewardLabel = UiTextCatalog.Get("settlement.reward_label"),
                    CoinCount = settlement.CoinCount,
                    ChestCount = settlement.ChestCount
                },
                delegate
                {
                    if (hasNext)
                    {
                        levelProgress.Select(currentLevelIndex + 1);
                    }

                    ResetGame();
                },
                delegate { CompleteGame?.Invoke(settlement); },
                true);
        }

        private void ConfirmExitToHall()
        {
            Shell.ClosePopup();
            settlementShown = true;
            combo = 0;
            score = Mathf.Max(score, moveCount * 4);
            var settlement = CreateSettlement(false);
            ShowBackHallRewardSettlementPanel(
                settlement,
                "ArrowEscapeSettlementPanel",
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("arrow-escape.settlement.score"), settlement.Score.ToString()),
                new MiniGameSettlementInfoRow(UiTextCatalog.Get("arrow-escape.settlement.remaining"), remainingTileCount.ToString()),
                delegate { CompleteGame?.Invoke(settlement); });
        }

        private MiniGameSettlement CreateSettlement(bool won)
        {
            return new MiniGameSettlement
            {
                Score = score,
                CoinCount = Mathf.Max(1, score / 12),
                ChestCount = won ? 1 : 0,
                Summary = UiTextCatalog.Format("arrow-escape.settlement.summary", currentLevelIndex + 1, score, moveCount)
            };
        }

        private void OnRestartClicked()
        {
            MiniGameSfxPlayer.Play(MiniGameSfxType.UiTap, 0.95f);
            ResetGame();
        }

        private void ResumeFromPause()
        {
            Shell.ClosePopup();
        }

        private void RefreshAll(string statusText)
        {
            if (titleLabel != null)
            {
                titleLabel.text = UiTextCatalog.Get("game.arrow-escape.name");
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = UiTextCatalog.Format("arrow-escape.hud", currentLevelIndex + 1, remainingTileCount, score);
            }

            if (statusLabel != null)
            {
                statusLabel.text = statusText;
            }

            if (undoButton != null)
            {
                undoButton.interactable = !isAnimating && !settlementShown && moveHistory.Count > 0;
            }

            if (hintButton != null)
            {
                hintButton.interactable = !isAnimating && !settlementShown && remainingTileCount > 0;
            }

            if (restartButton != null)
            {
                restartButton.interactable = !isAnimating;
            }

            RefreshBoard();
        }

        private void RefreshBoard()
        {
            if (tiles == null)
            {
                return;
            }

            for (var y = 0; y < tiles.GetLength(1); y++)
            {
                for (var x = 0; x < tiles.GetLength(0); x++)
                {
                    RefreshTile(tiles[x, y]);
                }
            }

            RefreshPieceVisuals();
        }

        private void RefreshTile(TileView tile)
        {
            var exists = currentPuzzle.Layout[tile.X, tile.Y];
            var active = exists && activeTiles[tile.X, tile.Y];
            var piece = exists ? pieceByCell[tile.X, tile.Y] : null;
            var visible = active || (piece != null && piece.IsFlying);
            tile.Button.interactable = active && piece != null && piece.Active && !settlementShown;
            tile.CanvasGroup.alpha = exists ? (visible ? 1f : 0f) : 1f;
            tile.Background.raycastTarget = exists;
            if (!exists)
            {
                tile.Background.color = EmptyColor;
                return;
            }

            if (!active)
            {
                tile.Background.color = EmptyColor;
                return;
            }

            tile.Background.color = new Color(1f, 1f, 1f, 0f);
        }

        private bool CanPieceEscape(ArrowPiece piece)
        {
            if (piece == null || !piece.Active)
            {
                return false;
            }

            var delta = DirectionDelta(piece.Direction);
            var x = piece.Head.x + delta.x;
            var y = piece.Head.y + delta.y;
            while (x >= 0 && y >= 0 && x < activeTiles.GetLength(0) && y < activeTiles.GetLength(1))
            {
                if (activeTiles[x, y] && pieceByCell[x, y] != piece)
                {
                    return false;
                }

                x += delta.x;
                y += delta.y;
            }

            return true;
        }

        private static int DistanceToEdge(int x, int y, int direction, int width, int height)
        {
            switch (direction)
            {
                case 0:
                    return y + 1;
                case 1:
                    return width - x;
                case 2:
                    return height - y;
                default:
                    return x + 1;
            }
        }

        private static string DirectionLabel(int direction)
        {
            switch (direction)
            {
                case 0:
                    return "^";
                case 1:
                    return ">";
                case 2:
                    return "v";
                case 3:
                    return "<";
                default:
                    return string.Empty;
            }
        }

        private static Vector2Int DirectionDelta(int direction)
        {
            switch (direction)
            {
                case 0:
                    return new Vector2Int(0, -1);
                case 1:
                    return new Vector2Int(1, 0);
                case 2:
                    return new Vector2Int(0, 1);
                default:
                    return new Vector2Int(-1, 0);
            }
        }

        private static bool[,] ParseLayout(string[] rows)
        {
            if (rows == null || rows.Length == 0)
            {
                throw new ArgumentException("Arrow escape layout is empty.", nameof(rows));
            }

            var width = rows[0].Length;
            var result = new bool[width, rows.Length];
            for (var y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != width)
                {
                    throw new ArgumentException("Arrow escape layout rows must have equal width.", nameof(rows));
                }

                for (var x = 0; x < width; x++)
                {
                    result[x, y] = rows[y][x] == '1';
                }
            }

            return result;
        }

        private static bool[,] Copy(bool[,] source)
        {
            var result = new bool[source.GetLength(0), source.GetLength(1)];
            for (var y = 0; y < source.GetLength(1); y++)
            {
                for (var x = 0; x < source.GetLength(0); x++)
                {
                    result[x, y] = source[x, y];
                }
            }

            return result;
        }

        private static int CountActive(bool[,] values)
        {
            var count = 0;
            for (var y = 0; y < values.GetLength(1); y++)
            {
                for (var x = 0; x < values.GetLength(0); x++)
                {
                    if (values[x, y])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountInactive(bool[,] values)
        {
            var count = 0;
            for (var y = 0; y < values.GetLength(1); y++)
            {
                for (var x = 0; x < values.GetLength(0); x++)
                {
                    if (!values[x, y])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool ContainsCell(Vector2Int[] cells, int x, int y)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell.x == x && cell.y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureButtonColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.disabledColor = new Color(0.56f, 0.56f, 0.56f, 0.58f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Button CreateZoomIconButton(string name, Transform parent, bool isPlus)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LayoutElement), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(38f, 38f);
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 38f;
            layout.preferredHeight = 38f;

            var background = buttonObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(255, 255, 255, 255);
            background.CornerRadius = 19f;
            background.raycastTarget = true;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(ArrowEscapeZoomIconGraphic));
            iconObject.transform.SetParent(rect, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(30f, 30f);
            var icon = iconObject.GetComponent<ArrowEscapeZoomIconGraphic>();
            icon.IsPlus = isPlus;
            icon.color = new Color32(73, 99, 138, 255);
            icon.raycastTarget = false;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            ConfigureButtonColors(button);
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.85f);
            return button;
        }

        private static Slider CreateZoomSlider(Transform parent)
        {
            var sliderObject = new GameObject("ArrowEscapeZoomSlider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(214f, 38f);
            var layout = sliderObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 214f;
            layout.preferredHeight = 38f;

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            backgroundObject.transform.SetParent(sliderRect, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.offsetMin = new Vector2(0f, -4f);
            backgroundRect.offsetMax = new Vector2(0f, 4f);
            var background = backgroundObject.GetComponent<RoundedRectGraphic>();
            background.color = new Color32(148, 148, 148, 255);
            background.CornerRadius = 4f;
            background.raycastTarget = false;

            var fillArea = CreateRectObject("Fill Area", sliderRect);
            Stretch(fillArea, Vector2.zero, Vector2.one, new Vector2(0f, 15f), new Vector2(0f, -15f));
            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            fillObject.transform.SetParent(fillArea, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            Stretch(fillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = fillObject.GetComponent<RoundedRectGraphic>();
            fill.color = new Color32(28, 219, 99, 255);
            fill.CornerRadius = 4f;
            fill.raycastTarget = false;

            var handleArea = CreateRectObject("Handle Slide Area", sliderRect);
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.offsetMin = new Vector2(0f, -16f);
            handleArea.offsetMax = new Vector2(0f, 16f);
            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            handleObject.transform.SetParent(handleArea, false);
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(32f, 0f);
            var handle = handleObject.GetComponent<RoundedRectGraphic>();
            handle.color = new Color32(64, 166, 230, 255);
            handle.CornerRadius = 16f;
            handle.raycastTarget = true;

            var slider = sliderObject.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(0f);
            return slider;
        }

        private static Button CreateTextButton(string name, Transform parent, string text, Vector2 size, Color color)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button), typeof(LayoutElement), typeof(CanvasRenderer), typeof(RoundedRectGraphic));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            var graphic = buttonObject.GetComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = 22f;
            graphic.raycastTarget = true;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = graphic;
            ConfigureButtonColors(button);

            var label = CreateText("Label", rect, 20f, FontStyles.Bold, Color.white);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 20f;
            label.text = text;
            MiniGameSfxPlayer.Attach(button, MiniGameSfxType.UiTap, 0.9f);
            return button;
        }

        private static void CreateEscapeLane(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            var lane = CreateRectObject(name, parent);
            lane.anchorMin = anchorMin;
            lane.anchorMax = anchorMax;
            lane.pivot = pivot;
            lane.sizeDelta = size;
            lane.anchoredPosition = position;
            EnsureRoundedRectGraphic(lane.gameObject, new Color32(208, 221, 203, 130), Mathf.Min(size.x, size.y) * 0.5f, false);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, FontStyles fontStyle, Color color)
        {
            var textObject = CreateRectObject(name, parent);
            var label = textObject.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = MiniGameFontProvider.DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateRectObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static RoundedRectGraphic EnsureRoundedRectGraphic(GameObject target, Color color, float radius, bool raycastTarget)
        {
            if (target.GetComponent<CanvasRenderer>() == null)
            {
                target.AddComponent<CanvasRenderer>();
            }

            var graphic = target.GetComponent<RoundedRectGraphic>() ?? target.AddComponent<RoundedRectGraphic>();
            graphic.color = color;
            graphic.CornerRadius = radius;
            graphic.raycastTarget = raycastTarget;
            return graphic;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        private sealed class TileView
        {
            public int X;
            public int Y;
            public RectTransform Rect;
            public Button Button;
            public RoundedRectGraphic Background;
            public CanvasGroup CanvasGroup;
            public Vector2 HomePosition;
        }

        private readonly struct MoveRecord
        {
            public readonly ArrowPiece Piece;

            public MoveRecord(ArrowPiece piece)
            {
                Piece = piece;
            }
        }

        private sealed class ArrowPiece
        {
            public readonly int Id;
            public readonly int Direction;
            public readonly Vector2Int Head;
            public Vector2Int[] Cells;
            public RectTransform VisualRoot;
            public ArrowEscapePiecePathGraphic Visual;
            public Vector2[] HomePoints;
            public bool Active = true;
            public bool IsFlying;

            public ArrowPiece(int id, int direction, Vector2Int head, Vector2Int[] cells)
            {
                Id = id;
                Direction = direction;
                Head = head;
                Cells = cells;
            }

        }

        private readonly struct Candidate
        {
            public readonly int X;
            public readonly int Y;
            public readonly List<int> Directions;

            public Candidate(int x, int y, List<int> directions)
            {
                X = x;
                Y = y;
                Directions = directions;
            }
        }

        private sealed class GeneratedPiece
        {
            public readonly int Id;
            public readonly Vector2Int Head;
            public readonly Vector2Int[] Cells;
            public int Direction;
            public bool Active = true;

            public GeneratedPiece(int id, Vector2Int head, Vector2Int[] cells)
            {
                Id = id;
                Head = head;
                Cells = cells;
                Direction = -1;
            }
        }

        private readonly struct GeneratedCandidate
        {
            public readonly GeneratedPiece Piece;
            public readonly List<int> Directions;
            public readonly int MaxOriginalBlockers;

            public GeneratedCandidate(GeneratedPiece piece, List<int> directions, int maxOriginalBlockers)
            {
                Piece = piece;
                Directions = directions;
                MaxOriginalBlockers = maxOriginalBlockers;
            }
        }

        private sealed class LevelDefinition
        {
            public readonly string NameKey;
            public readonly int Seed;
            public readonly int MinPieceLength;
            public readonly int MaxPieceLength;
            public readonly string[] MaskRows;

            public LevelDefinition(string nameKey, int seed, int minPieceLength, int maxPieceLength, string[] maskRows)
            {
                NameKey = nameKey;
                Seed = seed;
                MinPieceLength = minPieceLength;
                MaxPieceLength = maxPieceLength;
                MaskRows = maskRows;
            }
        }
    }

    public readonly struct ArrowEscapePuzzleData
    {
        public readonly bool[,] Layout;
        public readonly int[,] Directions;
        public readonly Vector2Int[] Solution;
        public readonly Vector2Int[][] Pieces;

        public ArrowEscapePuzzleData(bool[,] layout, int[,] directions, Vector2Int[] solution, Vector2Int[][] pieces)
        {
            Layout = layout;
            Directions = directions;
            Solution = solution;
            Pieces = pieces;
        }
    }

    public sealed class ArrowEscapePiecePathGraphic : MaskableGraphic
    {
        private Vector2[] points = Array.Empty<Vector2>();
        private Vector2[] renderPoints = Array.Empty<Vector2>();
        private int direction;
        private float cellSize = 32f;

        public void SetPath(Vector2[] sourcePoints, int newDirection, float newCellSize)
        {
            SetPath(sourcePoints, sourcePoints, newDirection, newCellSize);
        }

        public void SetPath(Vector2[] sourcePoints, Vector2[] sourceRenderPoints, int newDirection, float newCellSize)
        {
            points = sourcePoints == null ? Array.Empty<Vector2>() : (Vector2[])sourcePoints.Clone();
            renderPoints = sourceRenderPoints == null ? (Vector2[])points.Clone() : (Vector2[])sourceRenderPoints.Clone();
            if (renderPoints.Length == 0 && points.Length > 0)
            {
                renderPoints = (Vector2[])points.Clone();
            }

            direction = newDirection;
            cellSize = Mathf.Max(1f, newCellSize);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Length == 0)
            {
                return;
            }

            var lineThickness = Mathf.Max(5f, cellSize * 0.18f);
            var directionVector = DirectionToUiVector(direction);
            var headBase = points[0] + directionVector * (cellSize * 0.30f);
            var sourcePoints = renderPoints.Length > 0 ? renderPoints : points;
            var drawPoints = new List<Vector2>(sourcePoints.Length + 2);
            AddStrokePoint(drawPoints, headBase);
            for (var i = 0; i < sourcePoints.Length; i++)
            {
                AddStrokePoint(drawPoints, sourcePoints[i]);
            }

            if (sourcePoints.Length == 1)
            {
                AddStrokePoint(drawPoints, points[0] - directionVector * (cellSize * 0.42f));
            }

            AddStroke(vh, drawPoints, lineThickness);
            AddArrowHead(vh, points[0], directionVector, lineThickness);
        }

        private static Vector2 DirectionToUiVector(int direction)
        {
            switch (direction)
            {
                case 0:
                    return Vector2.up;
                case 1:
                    return Vector2.right;
                case 2:
                    return Vector2.down;
                default:
                    return Vector2.left;
            }
        }

        private static void AddStrokePoint(List<Vector2> strokePoints, Vector2 point)
        {
            if (strokePoints.Count > 0 && (strokePoints[strokePoints.Count - 1] - point).sqrMagnitude < 0.01f)
            {
                return;
            }

            strokePoints.Add(point);
        }

        private void AddStroke(VertexHelper vh, List<Vector2> strokePoints, float thickness)
        {
            if (strokePoints.Count < 2)
            {
                return;
            }

            var halfThickness = thickness * 0.5f;
            for (var i = 0; i + 1 < strokePoints.Count; i++)
            {
                AddSegment(vh, strokePoints[i], strokePoints[i + 1], halfThickness);
            }

            for (var i = 0; i < strokePoints.Count; i++)
            {
                AddRoundJoin(vh, strokePoints[i], halfThickness);
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float halfThickness)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var normal = new Vector2(-delta.y, delta.x).normalized * halfThickness;
            AddQuad(vh, start + normal, end + normal, end - normal, start - normal);
        }

        private void AddRoundJoin(VertexHelper vh, Vector2 center, float radius)
        {
            const int SegmentCount = 12;
            var startIndex = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(center, vertexColor, Vector2.zero);
            for (var i = 0; i <= SegmentCount; i++)
            {
                var angle = (Mathf.PI * 2f * i) / SegmentCount;
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vh.AddVert(point, vertexColor, Vector2.zero);
            }

            for (var i = 1; i <= SegmentCount; i++)
            {
                vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
            }
        }

        private void AddArrowHead(VertexHelper vh, Vector2 headCenter, Vector2 directionVector, float thickness)
        {
            var perpendicular = new Vector2(-directionVector.y, directionVector.x);
            var tip = headCenter + directionVector * (cellSize * 0.56f);
            var baseCenter = headCenter + directionVector * (cellSize * 0.30f);
            var halfWidth = Mathf.Max(thickness * 0.95f, cellSize * 0.17f);
            AddTriangle(vh, tip, baseCenter + perpendicular * halfWidth, baseCenter - perpendicular * halfWidth);
        }

        private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var startIndex = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(a, vertexColor, Vector2.zero);
            vh.AddVert(b, vertexColor, Vector2.zero);
            vh.AddVert(c, vertexColor, Vector2.zero);
            vh.AddVert(d, vertexColor, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        private void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            var startIndex = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(a, vertexColor, Vector2.zero);
            vh.AddVert(b, vertexColor, Vector2.zero);
            vh.AddVert(c, vertexColor, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        }
    }

    public sealed class ArrowEscapeZoomIconGraphic : MaskableGraphic
    {
        public bool IsPlus { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.26f;
            var center = new Vector2(rect.center.x - radius * 0.12f, rect.center.y + radius * 0.08f);
            var thickness = Mathf.Max(2.4f, radius * 0.18f);
            AddRing(vh, center, radius, thickness);

            var handleStart = center + new Vector2(radius * 0.58f, -radius * 0.58f);
            var handleEnd = center + new Vector2(radius * 1.22f, -radius * 1.22f);
            AddSegment(vh, handleStart, handleEnd, thickness);

            var markHalf = radius * 0.42f;
            AddSegment(vh, center + Vector2.left * markHalf, center + Vector2.right * markHalf, thickness);
            if (IsPlus)
            {
                AddSegment(vh, center + Vector2.down * markHalf, center + Vector2.up * markHalf, thickness);
            }
        }

        private void AddRing(VertexHelper vh, Vector2 center, float radius, float thickness)
        {
            const int SegmentCount = 28;
            var innerRadius = radius - thickness;
            for (var i = 0; i < SegmentCount; i++)
            {
                var a0 = Mathf.PI * 2f * i / SegmentCount;
                var a1 = Mathf.PI * 2f * (i + 1) / SegmentCount;
                var outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                var inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;
                var inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                AddQuad(vh, outer0, outer1, inner1, inner0);
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float thickness)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
            AddQuad(vh, start + normal, end + normal, end - normal, start - normal);
        }

        private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var startIndex = vh.currentVertCount;
            var vertexColor = color;
            vh.AddVert(a, vertexColor, Vector2.zero);
            vh.AddVert(b, vertexColor, Vector2.zero);
            vh.AddVert(c, vertexColor, Vector2.zero);
            vh.AddVert(d, vertexColor, Vector2.zero);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }

    public sealed class ArrowEscapeBoardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public Action<Vector2> OnDragDelta;

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            OnDragDelta?.Invoke(eventData.delta);
        }
    }

    public sealed class ArrowEscapeBoardPanelResizeWatcher : MonoBehaviour
    {
        private Action onDimensionsChanged;
        private Vector2 lastSize;

        public void Initialize(Action callback)
        {
            onDimensionsChanged = callback;
            var rectTransform = transform as RectTransform;
            lastSize = rectTransform != null ? rectTransform.rect.size : Vector2.zero;
        }

        private void OnRectTransformDimensionsChange()
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            var currentSize = rectTransform.rect.size;
            if (Vector2.SqrMagnitude(currentSize - lastSize) < 0.25f)
            {
                return;
            }

            lastSize = currentSize;
            onDimensionsChanged?.Invoke();
        }
    }
}
