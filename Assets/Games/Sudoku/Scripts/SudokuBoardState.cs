using System;

namespace HuanYouYu.MiniGameHall
{
    public sealed class SudokuBoardState
    {
        public const int Size = 9;
        public const int CellCount = Size * Size;

        private readonly int[] givens;
        private readonly int[] solution;
        private readonly int[] currentValues;
        private readonly int[] candidateMasks;

        public SudokuBoardState(SudokuPuzzle puzzle)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            givens = new int[CellCount];
            solution = new int[CellCount];
            currentValues = new int[CellCount];
            candidateMasks = new int[CellCount];

            Array.Copy(puzzle.Givens, givens, CellCount);
            Array.Copy(puzzle.Solution, solution, CellCount);
            Array.Copy(puzzle.Givens, currentValues, CellCount);
        }

        public int GetValue(int cellIndex)
        {
            return IsValidIndex(cellIndex) ? currentValues[cellIndex] : 0;
        }

        public bool IsGiven(int cellIndex)
        {
            return IsValidIndex(cellIndex) && givens[cellIndex] != 0;
        }

        public bool CanEdit(int cellIndex)
        {
            return IsValidIndex(cellIndex) && !IsGiven(cellIndex);
        }

        public void SetPlayerValue(int cellIndex, int value)
        {
            if (!CanEdit(cellIndex) || value < 1 || value > 9)
            {
                return;
            }

            currentValues[cellIndex] = value;
            candidateMasks[cellIndex] = 0;
            ClearRelatedCandidates(cellIndex, value);
        }

        public void ClearPlayerValue(int cellIndex)
        {
            if (CanEdit(cellIndex))
            {
                currentValues[cellIndex] = 0;
            }
        }

        public bool HasCandidate(int cellIndex, int candidate)
        {
            if (!CanEdit(cellIndex) || candidate < 1 || candidate > 9)
            {
                return false;
            }

            return (candidateMasks[cellIndex] & BuildCandidateMask(candidate)) != 0;
        }

        public int GetCandidateMask(int cellIndex)
        {
            return IsValidIndex(cellIndex) ? candidateMasks[cellIndex] : 0;
        }

        public bool HasAnyCandidates(int cellIndex)
        {
            return GetCandidateMask(cellIndex) != 0;
        }

        public void ToggleCandidate(int cellIndex, int candidate)
        {
            if (!CanEdit(cellIndex) || candidate < 1 || candidate > 9 || currentValues[cellIndex] != 0)
            {
                return;
            }

            candidateMasks[cellIndex] ^= BuildCandidateMask(candidate);
        }

        public void ClearCandidates(int cellIndex)
        {
            if (CanEdit(cellIndex))
            {
                candidateMasks[cellIndex] = 0;
            }
        }

        public void RebuildAllCandidates()
        {
            for (var i = 0; i < CellCount; i++)
            {
                if (!CanEdit(i))
                {
                    continue;
                }

                candidateMasks[i] = currentValues[i] == 0 ? BuildAvailableCandidateMask(i) : 0;
            }
        }

        public bool HasConflict(int cellIndex)
        {
            if (!IsValidIndex(cellIndex))
            {
                return false;
            }

            var value = currentValues[cellIndex];
            if (value == 0)
            {
                return false;
            }

            var row = cellIndex / Size;
            var column = cellIndex % Size;

            for (var i = 0; i < Size; i++)
            {
                var rowIndex = row * Size + i;
                if (rowIndex != cellIndex && currentValues[rowIndex] == value)
                {
                    return true;
                }

                var columnIndex = i * Size + column;
                if (columnIndex != cellIndex && currentValues[columnIndex] == value)
                {
                    return true;
                }
            }

            var boxRowStart = (row / 3) * 3;
            var boxColumnStart = (column / 3) * 3;
            for (var boxRow = boxRowStart; boxRow < boxRowStart + 3; boxRow++)
            {
                for (var boxColumn = boxColumnStart; boxColumn < boxColumnStart + 3; boxColumn++)
                {
                    var boxIndex = boxRow * Size + boxColumn;
                    if (boxIndex != cellIndex && currentValues[boxIndex] == value)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsRelated(int leftIndex, int rightIndex)
        {
            if (!IsValidIndex(leftIndex) || !IsValidIndex(rightIndex) || leftIndex == rightIndex)
            {
                return false;
            }

            return GetRow(leftIndex) == GetRow(rightIndex) ||
                   GetColumn(leftIndex) == GetColumn(rightIndex) ||
                   GetBox(leftIndex) == GetBox(rightIndex);
        }

        public bool HasSameValue(int leftIndex, int rightIndex)
        {
            if (!IsValidIndex(leftIndex) || !IsValidIndex(rightIndex) || leftIndex == rightIndex)
            {
                return false;
            }

            var value = currentValues[leftIndex];
            return value != 0 && value == currentValues[rightIndex];
        }

        public bool IsSolved()
        {
            for (var i = 0; i < CellCount; i++)
            {
                if (currentValues[i] != solution[i])
                {
                    return false;
                }
            }

            return true;
        }

        public int FindFirstEditableCell()
        {
            for (var i = 0; i < CellCount; i++)
            {
                if (!IsGiven(i))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int BuildCandidateMask(int candidate)
        {
            return 1 << (candidate - 1);
        }

        private int BuildAvailableCandidateMask(int cellIndex)
        {
            if (!CanEdit(cellIndex) || currentValues[cellIndex] != 0)
            {
                return 0;
            }

            var mask = 0;
            for (var candidate = 1; candidate <= Size; candidate++)
            {
                if (CanPlaceValue(cellIndex, candidate))
                {
                    mask |= BuildCandidateMask(candidate);
                }
            }

            return mask;
        }

        private bool CanPlaceValue(int cellIndex, int value)
        {
            if (!IsValidIndex(cellIndex) || value < 1 || value > Size)
            {
                return false;
            }

            var row = GetRow(cellIndex);
            var column = GetColumn(cellIndex);

            for (var i = 0; i < Size; i++)
            {
                var rowIndex = row * Size + i;
                if (rowIndex != cellIndex && currentValues[rowIndex] == value)
                {
                    return false;
                }

                var columnIndex = i * Size + column;
                if (columnIndex != cellIndex && currentValues[columnIndex] == value)
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
                    var boxIndex = boxRow * Size + boxColumn;
                    if (boxIndex != cellIndex && currentValues[boxIndex] == value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void ClearRelatedCandidates(int cellIndex, int candidate)
        {
            var mask = BuildCandidateMask(candidate);
            for (var i = 0; i < CellCount; i++)
            {
                if (i == cellIndex || !CanEdit(i) || currentValues[i] != 0 || !IsRelated(cellIndex, i))
                {
                    continue;
                }

                candidateMasks[i] &= ~mask;
            }
        }

        private static bool IsValidIndex(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < CellCount;
        }

        private static int GetRow(int cellIndex)
        {
            return cellIndex / Size;
        }

        private static int GetColumn(int cellIndex)
        {
            return cellIndex % Size;
        }

        private static int GetBox(int cellIndex)
        {
            return (GetRow(cellIndex) / 3) * 3 + (GetColumn(cellIndex) / 3);
        }
    }
}
