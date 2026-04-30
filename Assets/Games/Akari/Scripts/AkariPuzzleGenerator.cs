using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HuanYouYu.MiniGameHall
{
    internal enum AkariCellKind
    {
        White,
        Black,
        NumberedBlack
    }

    internal enum AkariDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    internal sealed class AkariPuzzle
    {
        public int QuestionNumber;
        public int GridSize;
        public AkariDifficulty Difficulty;
        public AkariCellKind[] Cells;
        public int[] Numbers;
        public bool[] SolutionBulbs;
        public int ReferenceSteps;
        public int Seed;
    }

    internal sealed class AkariEvaluation
    {
        public bool[] LitCells;
        public bool[] BulbConflicts;
        public bool[] NumberConflicts;
        public bool IsSolved;
        public int BulbCount;
        public int UnlitWhiteCount;
    }

    internal static class AkariPuzzleGenerator
    {
        internal const int MinGridSize = 5;
        internal const int MaxGridSize = 14;

        private const int MaxAttempts = 700;
        private const int SolverSolutionLimit = 2;

        internal static AkariPuzzle Generate(int questionNumber, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var safeQuestionNumber = Mathf.Max(1, questionNumber);
            var gridSize = ResolveRandomGridSize(random);
            var difficulty = ResolveRandomDifficulty(random);
            return Generate(safeQuestionNumber, gridSize, difficulty, random);
        }

        internal static AkariPuzzle Generate(int questionNumber, int gridSize, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return Generate(questionNumber, gridSize, ResolveRandomDifficulty(random), random);
        }

        internal static AkariPuzzle Generate(int questionNumber, int gridSize, AkariDifficulty difficulty, Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var safeQuestionNumber = Mathf.Max(1, questionNumber);
            var safeGridSize = Mathf.Clamp(gridSize, MinGridSize, MaxGridSize);
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var cells = CreateRandomBlackLayout(safeGridSize, difficulty, random);
                bool[] solution;
                if (!AkariPuzzleSolver.TryCreateLightingSolution(cells, safeGridSize, random, out solution))
                {
                    continue;
                }

                var numbers = CreateFullClues(cells, solution, safeGridSize);
                MarkAllBlackCellsNumbered(cells);
                var puzzle = CreatePuzzle(safeQuestionNumber, safeGridSize, difficulty, cells, numbers, solution, random.Next());
                if (!HasUniqueSolution(puzzle, out solution))
                {
                    continue;
                }

                puzzle.SolutionBulbs = solution;
                RemoveCluesWhileUnique(puzzle, random);
                puzzle.ReferenceSteps = CountBulbs(puzzle.SolutionBulbs);
                return puzzle;
            }

            return CreateFallbackPuzzle(safeQuestionNumber, safeGridSize, difficulty);
        }

        internal static int ResolveRandomGridSize(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return random.Next(MinGridSize, MaxGridSize + 1);
        }

        internal static AkariDifficulty ResolveRandomDifficulty(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            return (AkariDifficulty)random.Next(0, 3);
        }

        internal static AkariEvaluation Evaluate(AkariPuzzle puzzle, bool[] bulbs)
        {
            var cellCount = puzzle != null && puzzle.Cells != null ? puzzle.Cells.Length : 0;
            var evaluation = new AkariEvaluation
            {
                LitCells = new bool[cellCount],
                BulbConflicts = new bool[cellCount],
                NumberConflicts = new bool[cellCount],
                IsSolved = false
            };

            if (puzzle == null || puzzle.Cells == null || puzzle.Numbers == null || bulbs == null || bulbs.Length != cellCount)
            {
                return evaluation;
            }

            var hasConflict = false;
            for (var i = 0; i < cellCount; i++)
            {
                if (bulbs[i])
                {
                    evaluation.BulbCount++;
                }

                if (bulbs[i] && puzzle.Cells[i] != AkariCellKind.White)
                {
                    evaluation.BulbConflicts[i] = true;
                    hasConflict = true;
                }
            }

            for (var i = 0; i < cellCount; i++)
            {
                if (!bulbs[i] || puzzle.Cells[i] != AkariCellKind.White)
                {
                    continue;
                }

                evaluation.LitCells[i] = true;
                MarkLightRay(puzzle, bulbs, evaluation, i, -1, 0, ref hasConflict);
                MarkLightRay(puzzle, bulbs, evaluation, i, 1, 0, ref hasConflict);
                MarkLightRay(puzzle, bulbs, evaluation, i, 0, -1, ref hasConflict);
                MarkLightRay(puzzle, bulbs, evaluation, i, 0, 1, ref hasConflict);
            }

            for (var i = 0; i < cellCount; i++)
            {
                if (puzzle.Cells[i] != AkariCellKind.White)
                {
                    continue;
                }

                if (!evaluation.LitCells[i])
                {
                    evaluation.UnlitWhiteCount++;
                }
            }

            for (var i = 0; i < cellCount; i++)
            {
                if (puzzle.Cells[i] != AkariCellKind.NumberedBlack)
                {
                    continue;
                }

                var adjacentBulbs = CountAdjacentBulbs(puzzle.GridSize, bulbs, i);
                if (adjacentBulbs != puzzle.Numbers[i])
                {
                    evaluation.NumberConflicts[i] = true;
                    hasConflict = true;
                }
            }

            evaluation.IsSolved = evaluation.UnlitWhiteCount == 0 && !hasConflict;
            return evaluation;
        }

        internal static int CountSolutions(AkariPuzzle puzzle, int solutionLimit, out bool[] firstSolution)
        {
            return AkariPuzzleSolver.CountSolutions(puzzle, solutionLimit, out firstSolution);
        }

        private static AkariPuzzle CreatePuzzle(
            int questionNumber,
            int gridSize,
            AkariDifficulty difficulty,
            AkariCellKind[] cells,
            int[] numbers,
            bool[] solution,
            int seed)
        {
            return new AkariPuzzle
            {
                QuestionNumber = questionNumber,
                GridSize = gridSize,
                Difficulty = difficulty,
                Cells = CopyCells(cells),
                Numbers = CopyNumbers(numbers),
                SolutionBulbs = CopyBulbs(solution),
                ReferenceSteps = CountBulbs(solution),
                Seed = seed
            };
        }

        private static AkariCellKind[] CreateRandomBlackLayout(int gridSize, AkariDifficulty difficulty, Random random)
        {
            var cellCount = gridSize * gridSize;
            var cells = new AkariCellKind[cellCount];
            var density = ResolveBlackDensity(gridSize, difficulty);
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = random.NextDouble() < density ? AkariCellKind.Black : AkariCellKind.White;
            }

            cells[0] = AkariCellKind.White;
            cells[cellCount - 1] = AkariCellKind.White;

            var minWhiteCount = Mathf.CeilToInt(cellCount * 0.58f);
            while (CountWhiteCells(cells) < minWhiteCount)
            {
                cells[random.Next(0, cells.Length)] = AkariCellKind.White;
            }

            return cells;
        }

        private static float ResolveBlackDensity(int gridSize, AkariDifficulty difficulty)
        {
            var gridFactor = Mathf.InverseLerp(MinGridSize, MaxGridSize, gridSize);
            var density = Mathf.Lerp(0.24f, 0.32f, gridFactor);
            if (difficulty == AkariDifficulty.Easy)
            {
                density -= 0.03f;
            }
            else if (difficulty == AkariDifficulty.Hard)
            {
                density += 0.03f;
            }

            return Mathf.Clamp(density, 0.20f, 0.38f);
        }

        private static void MarkAllBlackCellsNumbered(AkariCellKind[] cells)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == AkariCellKind.Black)
                {
                    cells[i] = AkariCellKind.NumberedBlack;
                }
            }
        }

        private static int[] CreateFullClues(AkariCellKind[] cells, bool[] solution, int gridSize)
        {
            var numbers = new int[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                numbers[i] = -1;
                if (cells[i] != AkariCellKind.White)
                {
                    numbers[i] = CountAdjacentBulbs(gridSize, solution, i);
                }
            }

            return numbers;
        }

        private static void RemoveCluesWhileUnique(AkariPuzzle puzzle, Random random)
        {
            var numberedCells = new List<int>();
            for (var i = 0; i < puzzle.Cells.Length; i++)
            {
                if (puzzle.Cells[i] == AkariCellKind.NumberedBlack)
                {
                    numberedCells.Add(i);
                }
            }

            Shuffle(numberedCells, random);
            var targetKeepCount = Mathf.Max(2, Mathf.CeilToInt(numberedCells.Count * ResolveClueKeepRatio(puzzle.GridSize, puzzle.Difficulty)));
            for (var i = 0; i < numberedCells.Count && numberedCells.Count > targetKeepCount; i++)
            {
                var index = numberedCells[i];
                var previousKind = puzzle.Cells[index];
                var previousNumber = puzzle.Numbers[index];
                puzzle.Cells[index] = AkariCellKind.Black;
                puzzle.Numbers[index] = -1;

                bool[] uniqueSolution;
                if (!HasUniqueSolution(puzzle, out uniqueSolution))
                {
                    puzzle.Cells[index] = previousKind;
                    puzzle.Numbers[index] = previousNumber;
                    continue;
                }

                puzzle.SolutionBulbs = uniqueSolution;
                numberedCells.RemoveAt(i);
                i--;
            }
        }

        private static float ResolveClueKeepRatio(int gridSize, AkariDifficulty difficulty)
        {
            var gridFactor = Mathf.InverseLerp(MinGridSize, MaxGridSize, gridSize);
            var ratio = Mathf.Lerp(0.70f, 0.48f, gridFactor);
            if (difficulty == AkariDifficulty.Easy)
            {
                ratio += 0.14f;
            }
            else if (difficulty == AkariDifficulty.Hard)
            {
                ratio -= 0.12f;
            }

            return Mathf.Clamp(ratio, 0.28f, 0.88f);
        }

        private static bool HasUniqueSolution(AkariPuzzle puzzle, out bool[] solution)
        {
            return AkariPuzzleSolver.CountSolutions(puzzle, SolverSolutionLimit, out solution) == 1;
        }

        private static AkariPuzzle CreateFallbackPuzzle(int questionNumber, int gridSize, AkariDifficulty difficulty)
        {
            var cells = new AkariCellKind[gridSize * gridSize];
            var numbers = new int[cells.Length];
            var solution = new bool[cells.Length];
            for (var y = 0; y < gridSize; y++)
            {
                for (var x = 0; x < gridSize; x++)
                {
                    var index = ToIndex(gridSize, x, y);
                    if ((x % 2) == 0 && (y % 2) == 0)
                    {
                        cells[index] = AkariCellKind.White;
                        numbers[index] = -1;
                        solution[index] = true;
                    }
                    else
                    {
                        cells[index] = AkariCellKind.NumberedBlack;
                    }
                }
            }

            for (var i = 0; i < cells.Length; i++)
            {
                numbers[i] = cells[i] == AkariCellKind.NumberedBlack
                    ? CountAdjacentBulbs(gridSize, solution, i)
                    : -1;
            }

            bool[] solvedBulbs;
            if (AkariPuzzleSolver.CountSolutions(
                CreatePuzzle(questionNumber, gridSize, difficulty, cells, numbers, solution, 0),
                SolverSolutionLimit,
                out solvedBulbs) != 1)
            {
                throw new InvalidOperationException("Unable to generate a valid Akari puzzle.");
            }

            return CreatePuzzle(questionNumber, gridSize, difficulty, cells, numbers, solvedBulbs, 0);
        }

        private static void MarkLightRay(
            AkariPuzzle puzzle,
            bool[] bulbs,
            AkariEvaluation evaluation,
            int originIndex,
            int offsetX,
            int offsetY,
            ref bool hasConflict)
        {
            var originX = originIndex % puzzle.GridSize;
            var originY = originIndex / puzzle.GridSize;
            var x = originX + offsetX;
            var y = originY + offsetY;
            while (IsInside(puzzle.GridSize, x, y))
            {
                var index = ToIndex(puzzle.GridSize, x, y);
                if (puzzle.Cells[index] != AkariCellKind.White)
                {
                    return;
                }

                evaluation.LitCells[index] = true;
                if (bulbs[index])
                {
                    evaluation.BulbConflicts[originIndex] = true;
                    evaluation.BulbConflicts[index] = true;
                    hasConflict = true;
                }

                x += offsetX;
                y += offsetY;
            }
        }

        private static int CountAdjacentBulbs(int gridSize, bool[] bulbs, int index)
        {
            var x = index % gridSize;
            var y = index / gridSize;
            var count = 0;
            if (IsBulbAt(gridSize, bulbs, x - 1, y))
            {
                count++;
            }

            if (IsBulbAt(gridSize, bulbs, x + 1, y))
            {
                count++;
            }

            if (IsBulbAt(gridSize, bulbs, x, y - 1))
            {
                count++;
            }

            if (IsBulbAt(gridSize, bulbs, x, y + 1))
            {
                count++;
            }

            return count;
        }

        private static bool IsBulbAt(int gridSize, bool[] bulbs, int x, int y)
        {
            return IsInside(gridSize, x, y) && bulbs[ToIndex(gridSize, x, y)];
        }

        private static bool IsInside(int gridSize, int x, int y)
        {
            return x >= 0 && x < gridSize && y >= 0 && y < gridSize;
        }

        private static int ToIndex(int gridSize, int x, int y)
        {
            return (y * gridSize) + x;
        }

        private static int CountBulbs(bool[] bulbs)
        {
            var count = 0;
            if (bulbs == null)
            {
                return count;
            }

            for (var i = 0; i < bulbs.Length; i++)
            {
                if (bulbs[i])
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountWhiteCells(AkariCellKind[] cells)
        {
            var count = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] == AkariCellKind.White)
                {
                    count++;
                }
            }

            return count;
        }

        private static AkariCellKind[] CopyCells(AkariCellKind[] source)
        {
            var copy = new AkariCellKind[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }

        private static int[] CopyNumbers(int[] source)
        {
            var copy = new int[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }

        private static bool[] CopyBulbs(bool[] source)
        {
            var copy = new bool[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swap = random.Next(0, i + 1);
                var value = values[i];
                values[i] = values[swap];
                values[swap] = value;
            }
        }
    }

    internal static class AkariPuzzleSolver
    {
        internal static bool TryCreateLightingSolution(AkariCellKind[] cells, int gridSize, Random random, out bool[] solution)
        {
            var puzzle = new AkariPuzzle
            {
                GridSize = gridSize,
                Cells = CopyCells(cells),
                Numbers = CreateEmptyNumbers(cells),
                SolutionBulbs = new bool[cells.Length]
            };

            return CountSolutions(puzzle, 1, out solution, random) == 1;
        }

        internal static int CountSolutions(AkariPuzzle puzzle, int solutionLimit, out bool[] firstSolution)
        {
            return CountSolutions(puzzle, solutionLimit, out firstSolution, null);
        }

        private static int CountSolutions(AkariPuzzle puzzle, int solutionLimit, out bool[] firstSolution, Random random)
        {
            firstSolution = null;
            if (puzzle == null || puzzle.Cells == null || puzzle.Numbers == null)
            {
                return 0;
            }

            var context = new SolverContext(puzzle);
            var state = new SolverState(puzzle.Cells.Length);
            var count = 0;
            Search(context, state, Mathf.Max(1, solutionLimit), random, ref count, ref firstSolution);
            return count;
        }

        private static void Search(
            SolverContext context,
            SolverState state,
            int solutionLimit,
            Random random,
            ref int count,
            ref bool[] firstSolution)
        {
            if (count >= solutionLimit)
            {
                return;
            }

            if (!CanSatisfyNumberClues(context, state))
            {
                return;
            }

            var candidates = new List<int>();
            if (!TryFindUnlitCellCandidates(context, state, candidates))
            {
                if (!AreNumberCluesExact(context, state))
                {
                    return;
                }

                count++;
                if (firstSolution == null)
                {
                    firstSolution = state.CopyBulbs();
                }

                return;
            }

            if (random != null)
            {
                Shuffle(candidates, random);
            }

            var forbiddenBeforeBranch = new List<int>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var branch = state.Clone();
                for (var j = 0; j < forbiddenBeforeBranch.Count; j++)
                {
                    branch.Forbidden[forbiddenBeforeBranch[j]] = true;
                }

                if (CanPlaceBulb(context, branch, candidate))
                {
                    PlaceBulb(context, branch, candidate);
                    Search(context, branch, solutionLimit, random, ref count, ref firstSolution);
                }

                forbiddenBeforeBranch.Add(candidate);
            }
        }

        private static bool TryFindUnlitCellCandidates(SolverContext context, SolverState state, List<int> bestCandidates)
        {
            var bestCount = int.MaxValue;
            for (var i = 0; i < context.WhiteCells.Count; i++)
            {
                var cell = context.WhiteCells[i];
                if (state.LitCounts[cell] > 0)
                {
                    continue;
                }

                var candidateSource = context.IlluminationCandidatesByCell[cell];
                var count = 0;
                var localCandidates = new List<int>();
                for (var j = 0; j < candidateSource.Count; j++)
                {
                    var candidate = candidateSource[j];
                    if (CanPlaceBulb(context, state, candidate))
                    {
                        localCandidates.Add(candidate);
                        count++;
                    }
                }

                if (count == 0)
                {
                    bestCandidates.Clear();
                    return true;
                }

                if (count < bestCount)
                {
                    bestCount = count;
                    bestCandidates.Clear();
                    bestCandidates.AddRange(localCandidates);
                    if (bestCount == 1)
                    {
                        return true;
                    }
                }
            }

            return bestCount != int.MaxValue;
        }

        private static bool CanPlaceBulb(SolverContext context, SolverState state, int index)
        {
            if (!context.IsWhite[index] || state.Bulbs[index] || state.Forbidden[index])
            {
                return false;
            }

            var horizontal = context.HorizontalSegmentByCell[index];
            var vertical = context.VerticalSegmentByCell[index];
            if ((horizontal >= 0 && state.HorizontalSegmentHasBulb[horizontal]) ||
                (vertical >= 0 && state.VerticalSegmentHasBulb[vertical]))
            {
                return false;
            }

            var adjacentNumbers = context.AdjacentNumberedBlackCellsByCell[index];
            for (var i = 0; i < adjacentNumbers.Count; i++)
            {
                var numberIndex = adjacentNumbers[i];
                if (state.AdjacentBulbCounts[numberIndex] >= context.Numbers[numberIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private static void PlaceBulb(SolverContext context, SolverState state, int index)
        {
            state.Bulbs[index] = true;

            var horizontal = context.HorizontalSegmentByCell[index];
            var vertical = context.VerticalSegmentByCell[index];
            if (horizontal >= 0)
            {
                state.HorizontalSegmentHasBulb[horizontal] = true;
            }

            if (vertical >= 0)
            {
                state.VerticalSegmentHasBulb[vertical] = true;
            }

            var litCells = context.LitCellsByBulb[index];
            for (var i = 0; i < litCells.Count; i++)
            {
                state.LitCounts[litCells[i]]++;
            }

            var adjacentNumbers = context.AdjacentNumberedBlackCellsByCell[index];
            for (var i = 0; i < adjacentNumbers.Count; i++)
            {
                state.AdjacentBulbCounts[adjacentNumbers[i]]++;
            }
        }

        private static bool CanSatisfyNumberClues(SolverContext context, SolverState state)
        {
            for (var i = 0; i < context.NumberedBlackCells.Count; i++)
            {
                var numberIndex = context.NumberedBlackCells[i];
                var current = state.AdjacentBulbCounts[numberIndex];
                var target = context.Numbers[numberIndex];
                if (current > target)
                {
                    return false;
                }

                var available = 0;
                var adjacentWhites = context.AdjacentWhiteCellsByNumberedBlackCell[numberIndex];
                for (var j = 0; j < adjacentWhites.Count; j++)
                {
                    if (CanPlaceBulb(context, state, adjacentWhites[j]))
                    {
                        available++;
                    }
                }

                if (current + available < target)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreNumberCluesExact(SolverContext context, SolverState state)
        {
            for (var i = 0; i < context.NumberedBlackCells.Count; i++)
            {
                var numberIndex = context.NumberedBlackCells[i];
                if (state.AdjacentBulbCounts[numberIndex] != context.Numbers[numberIndex])
                {
                    return false;
                }
            }

            return true;
        }

        private static int[] CreateEmptyNumbers(AkariCellKind[] cells)
        {
            var numbers = new int[cells.Length];
            for (var i = 0; i < numbers.Length; i++)
            {
                numbers[i] = -1;
            }

            return numbers;
        }

        private static AkariCellKind[] CopyCells(AkariCellKind[] source)
        {
            var copy = new AkariCellKind[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swap = random.Next(0, i + 1);
                var value = values[i];
                values[i] = values[swap];
                values[swap] = value;
            }
        }

        private sealed class SolverContext
        {
            public readonly int GridSize;
            public readonly AkariCellKind[] Cells;
            public readonly int[] Numbers;
            public readonly bool[] IsWhite;
            public readonly int[] HorizontalSegmentByCell;
            public readonly int[] VerticalSegmentByCell;
            public readonly List<int> WhiteCells = new List<int>();
            public readonly List<int> NumberedBlackCells = new List<int>();
            public readonly List<int>[] LitCellsByBulb;
            public readonly List<int>[] IlluminationCandidatesByCell;
            public readonly List<int>[] AdjacentNumberedBlackCellsByCell;
            public readonly List<int>[] AdjacentWhiteCellsByNumberedBlackCell;
            public int HorizontalSegmentCount;
            public int VerticalSegmentCount;

            public SolverContext(AkariPuzzle puzzle)
            {
                GridSize = puzzle.GridSize;
                Cells = puzzle.Cells;
                Numbers = puzzle.Numbers;
                IsWhite = new bool[Cells.Length];
                HorizontalSegmentByCell = CreateFilledArray(Cells.Length, -1);
                VerticalSegmentByCell = CreateFilledArray(Cells.Length, -1);
                LitCellsByBulb = CreateListArray(Cells.Length);
                IlluminationCandidatesByCell = CreateListArray(Cells.Length);
                AdjacentNumberedBlackCellsByCell = CreateListArray(Cells.Length);
                AdjacentWhiteCellsByNumberedBlackCell = CreateListArray(Cells.Length);

                BuildCellLists();
                BuildSegments(true);
                BuildSegments(false);
                BuildLightingMaps();
                BuildNumberAdjacency();
            }

            private void BuildCellLists()
            {
                for (var i = 0; i < Cells.Length; i++)
                {
                    IsWhite[i] = Cells[i] == AkariCellKind.White;
                    if (IsWhite[i])
                    {
                        WhiteCells.Add(i);
                    }
                    else if (Cells[i] == AkariCellKind.NumberedBlack)
                    {
                        NumberedBlackCells.Add(i);
                    }
                }
            }

            private void BuildSegments(bool horizontal)
            {
                var segmentId = 0;
                for (var outer = 0; outer < GridSize; outer++)
                {
                    var insideSegment = false;
                    for (var inner = 0; inner < GridSize; inner++)
                    {
                        var x = horizontal ? inner : outer;
                        var y = horizontal ? outer : inner;
                        var index = ToIndex(x, y);
                        if (!IsWhite[index])
                        {
                            insideSegment = false;
                            continue;
                        }

                        if (!insideSegment)
                        {
                            insideSegment = true;
                            segmentId++;
                        }

                        if (horizontal)
                        {
                            HorizontalSegmentByCell[index] = segmentId - 1;
                        }
                        else
                        {
                            VerticalSegmentByCell[index] = segmentId - 1;
                        }
                    }
                }

                if (horizontal)
                {
                    HorizontalSegmentCount = segmentId;
                }
                else
                {
                    VerticalSegmentCount = segmentId;
                }
            }

            private void BuildLightingMaps()
            {
                for (var i = 0; i < WhiteCells.Count; i++)
                {
                    var cell = WhiteCells[i];
                    AddVisibleCells(cell, LitCellsByBulb[cell]);
                    for (var j = 0; j < LitCellsByBulb[cell].Count; j++)
                    {
                        var lit = LitCellsByBulb[cell][j];
                        if (!IlluminationCandidatesByCell[lit].Contains(cell))
                        {
                            IlluminationCandidatesByCell[lit].Add(cell);
                        }
                    }
                }
            }

            private void BuildNumberAdjacency()
            {
                for (var i = 0; i < NumberedBlackCells.Count; i++)
                {
                    var numberIndex = NumberedBlackCells[i];
                    var x = numberIndex % GridSize;
                    var y = numberIndex / GridSize;
                    AddAdjacentWhite(numberIndex, x - 1, y);
                    AddAdjacentWhite(numberIndex, x + 1, y);
                    AddAdjacentWhite(numberIndex, x, y - 1);
                    AddAdjacentWhite(numberIndex, x, y + 1);
                }
            }

            private void AddAdjacentWhite(int numberIndex, int x, int y)
            {
                if (!IsInside(x, y))
                {
                    return;
                }

                var whiteIndex = ToIndex(x, y);
                if (!IsWhite[whiteIndex])
                {
                    return;
                }

                AdjacentWhiteCellsByNumberedBlackCell[numberIndex].Add(whiteIndex);
                AdjacentNumberedBlackCellsByCell[whiteIndex].Add(numberIndex);
            }

            private void AddVisibleCells(int origin, List<int> results)
            {
                results.Add(origin);
                AddRay(origin, -1, 0, results);
                AddRay(origin, 1, 0, results);
                AddRay(origin, 0, -1, results);
                AddRay(origin, 0, 1, results);
            }

            private void AddRay(int origin, int offsetX, int offsetY, List<int> results)
            {
                var x = (origin % GridSize) + offsetX;
                var y = (origin / GridSize) + offsetY;
                while (IsInside(x, y))
                {
                    var index = ToIndex(x, y);
                    if (!IsWhite[index])
                    {
                        return;
                    }

                    results.Add(index);
                    x += offsetX;
                    y += offsetY;
                }
            }

            private bool IsInside(int x, int y)
            {
                return x >= 0 && x < GridSize && y >= 0 && y < GridSize;
            }

            private int ToIndex(int x, int y)
            {
                return (y * GridSize) + x;
            }

            private static int[] CreateFilledArray(int length, int value)
            {
                var result = new int[length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = value;
                }

                return result;
            }

            private static List<int>[] CreateListArray(int length)
            {
                var result = new List<int>[length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = new List<int>();
                }

                return result;
            }
        }

        private sealed class SolverState
        {
            public readonly bool[] Bulbs;
            public readonly bool[] Forbidden;
            public readonly int[] LitCounts;
            public readonly bool[] HorizontalSegmentHasBulb;
            public readonly bool[] VerticalSegmentHasBulb;
            public readonly int[] AdjacentBulbCounts;

            public SolverState(int cellCount)
            {
                Bulbs = new bool[cellCount];
                Forbidden = new bool[cellCount];
                LitCounts = new int[cellCount];
                HorizontalSegmentHasBulb = new bool[cellCount];
                VerticalSegmentHasBulb = new bool[cellCount];
                AdjacentBulbCounts = new int[cellCount];
            }

            private SolverState(SolverState source)
            {
                Bulbs = Copy(source.Bulbs);
                Forbidden = Copy(source.Forbidden);
                LitCounts = Copy(source.LitCounts);
                HorizontalSegmentHasBulb = Copy(source.HorizontalSegmentHasBulb);
                VerticalSegmentHasBulb = Copy(source.VerticalSegmentHasBulb);
                AdjacentBulbCounts = Copy(source.AdjacentBulbCounts);
            }

            public SolverState Clone()
            {
                return new SolverState(this);
            }

            public bool[] CopyBulbs()
            {
                return Copy(Bulbs);
            }

            private static bool[] Copy(bool[] source)
            {
                var copy = new bool[source.Length];
                Array.Copy(source, copy, copy.Length);
                return copy;
            }

            private static int[] Copy(int[] source)
            {
                var copy = new int[source.Length];
                Array.Copy(source, copy, copy.Length);
                return copy;
            }
        }
    }
}
