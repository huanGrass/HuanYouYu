using System;
using System.Collections.Generic;

namespace HuanYouYu.MiniGameHall
{
    public enum SudokuDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static class SudokuPuzzleGenerator
    {
        private const int EasyTargetClueCount = 42;
        private const int NormalTargetClueCount = 36;
        private const int HardTargetClueCount = 30;

        public static SudokuPuzzle GenerateNormalPuzzle()
        {
            return GeneratePuzzle(SudokuDifficulty.Normal);
        }

        public static SudokuPuzzle GeneratePuzzle(SudokuDifficulty difficulty)
        {
            var random = new Random(unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
            var solution = CreateSolvedBoard(random);
            var givens = CreatePuzzle(solution, random, GetTargetClueCount(difficulty));
            return new SudokuPuzzle(givens, solution);
        }

        private static int GetTargetClueCount(SudokuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case SudokuDifficulty.Easy:
                    return EasyTargetClueCount;
                case SudokuDifficulty.Hard:
                    return HardTargetClueCount;
                default:
                    return NormalTargetClueCount;
            }
        }

        private static int[] CreateSolvedBoard(Random random)
        {
            var board = new int[SudokuBoardState.CellCount];
            if (!FillBoard(board, 0, random))
            {
                throw new InvalidOperationException("Failed to create a solved Sudoku board.");
            }

            return board;
        }

        private static bool FillBoard(int[] board, int index, Random random)
        {
            if (index >= SudokuBoardState.CellCount)
            {
                return true;
            }

            if (board[index] != 0)
            {
                return FillBoard(board, index + 1, random);
            }

            var candidates = BuildShuffledDigits(random);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!CanPlace(board, index, candidate))
                {
                    continue;
                }

                board[index] = candidate;
                if (FillBoard(board, index + 1, random))
                {
                    return true;
                }

                board[index] = 0;
            }

            return false;
        }

        private static int[] CreatePuzzle(int[] solution, Random random, int targetClueCount)
        {
            var givens = new int[SudokuBoardState.CellCount];
            Array.Copy(solution, givens, SudokuBoardState.CellCount);

            var cells = new List<int>(SudokuBoardState.CellCount);
            for (var i = 0; i < SudokuBoardState.CellCount; i++)
            {
                cells.Add(i);
            }

            Shuffle(cells, random);

            for (var i = 0; i < cells.Count; i++)
            {
                if (CountFilledCells(givens) <= targetClueCount)
                {
                    break;
                }

                var index = cells[i];
                var backup = givens[index];
                givens[index] = 0;

                if (CountSolutions(givens, 2) != 1)
                {
                    givens[index] = backup;
                }
            }

            return givens;
        }

        private static int CountSolutions(int[] puzzle, int solutionLimit)
        {
            var scratch = new int[SudokuBoardState.CellCount];
            Array.Copy(puzzle, scratch, SudokuBoardState.CellCount);
            return SolveAndCount(scratch, solutionLimit);
        }

        private static int SolveAndCount(int[] board, int solutionLimit)
        {
            if (solutionLimit <= 0)
            {
                return 0;
            }

            var nextIndex = FindBestEmptyCell(board);
            if (nextIndex < 0)
            {
                return 1;
            }

            var count = 0;
            for (var value = 1; value <= 9; value++)
            {
                if (!CanPlace(board, nextIndex, value))
                {
                    continue;
                }

                board[nextIndex] = value;
                count += SolveAndCount(board, solutionLimit - count);
                board[nextIndex] = 0;

                if (count >= solutionLimit)
                {
                    break;
                }
            }

            return count;
        }

        private static int FindBestEmptyCell(int[] board)
        {
            var bestIndex = -1;
            var bestCandidateCount = int.MaxValue;

            for (var i = 0; i < SudokuBoardState.CellCount; i++)
            {
                if (board[i] != 0)
                {
                    continue;
                }

                var candidateCount = 0;
                for (var value = 1; value <= 9; value++)
                {
                    if (CanPlace(board, i, value))
                    {
                        candidateCount++;
                    }
                }

                if (candidateCount < bestCandidateCount)
                {
                    bestCandidateCount = candidateCount;
                    bestIndex = i;
                    if (candidateCount <= 1)
                    {
                        break;
                    }
                }
            }

            return bestIndex;
        }

        private static bool CanPlace(int[] board, int index, int value)
        {
            var row = index / SudokuBoardState.Size;
            var column = index % SudokuBoardState.Size;

            for (var i = 0; i < SudokuBoardState.Size; i++)
            {
                if (board[row * SudokuBoardState.Size + i] == value)
                {
                    return false;
                }

                if (board[i * SudokuBoardState.Size + column] == value)
                {
                    return false;
                }
            }

            var boxRowStart = (row / 3) * 3;
            var boxColumnStart = (column / 3) * 3;
            for (var boxRow = boxRowStart; boxRow < boxRowStart + 3; boxRow++)
            {
                for (var boxColumn = boxColumnStart; boxColumn < boxColumnStart + 3; boxColumn++)
                {
                    if (board[boxRow * SudokuBoardState.Size + boxColumn] == value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int[] BuildShuffledDigits(Random random)
        {
            var digits = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            for (var i = digits.Length - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = digits[i];
                digits[i] = digits[swapIndex];
                digits[swapIndex] = temp;
            }

            return digits;
        }

        private static int CountFilledCells(int[] board)
        {
            var count = 0;
            for (var i = 0; i < board.Length; i++)
            {
                if (board[i] != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Shuffle(List<int> values, Random random)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }
    }
}
