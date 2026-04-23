using System;

namespace HuanYouYu.Nonogram
{
    public enum NonogramCellMark
    {
        Unknown = 0,
        Filled = 1,
        Crossed = 2
    }

    public enum NonogramInputMode
    {
        Fill = 0,
        Cross = 1
    }

    public sealed class NonogramBoardState
    {
        private readonly NonogramCellMark[] marks;

        public NonogramBoardState(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            marks = new NonogramCellMark[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public NonogramCellMark GetMark(int row, int column)
        {
            ValidateCell(row, column);
            return marks[ToIndex(row, column)];
        }

        public void Toggle(int row, int column, NonogramInputMode mode)
        {
            ValidateCell(row, column);

            var index = ToIndex(row, column);
            var currentMark = marks[index];
            if (mode == NonogramInputMode.Fill)
            {
                marks[index] = currentMark == NonogramCellMark.Filled ? NonogramCellMark.Unknown : NonogramCellMark.Filled;
                return;
            }

            marks[index] = currentMark == NonogramCellMark.Crossed ? NonogramCellMark.Unknown : NonogramCellMark.Crossed;
        }

        public void SetMark(int row, int column, NonogramCellMark mark)
        {
            ValidateCell(row, column);
            marks[ToIndex(row, column)] = mark;
        }

        public void ClearMark(int row, int column)
        {
            SetMark(row, column, NonogramCellMark.Unknown);
        }

        public bool AutoCrossSatisfiedLines(NonogramPuzzle puzzle)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            if (puzzle.Width != Width || puzzle.Height != Height)
            {
                return false;
            }

            var changedAny = false;
            var changed = false;
            do
            {
                changed = false;
                for (var row = 0; row < Height; row++)
                {
                    if (!IsRowSatisfied(puzzle, row))
                    {
                        continue;
                    }

                    changed |= CrossUnknownsInRow(row);
                }

                for (var column = 0; column < Width; column++)
                {
                    if (!IsColumnSatisfied(puzzle, column))
                    {
                        continue;
                    }

                    changed |= CrossUnknownsInColumn(column);
                }

                changedAny |= changed;
            }
            while (changed);

            return changedAny;
        }

        public bool[] GetRowHintCompletion(NonogramPuzzle puzzle, int row)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            if (puzzle.Width != Width || puzzle.Height != Height)
            {
                return new bool[0];
            }

            ValidateRow(row);
            return BuildLineHintCompletionMask(puzzle.RowHints[row], row, true);
        }

        public bool[] GetColumnHintCompletion(NonogramPuzzle puzzle, int column)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            if (puzzle.Width != Width || puzzle.Height != Height)
            {
                return new bool[0];
            }

            ValidateColumn(column);
            return BuildLineHintCompletionMask(puzzle.ColumnHints[column], column, false);
        }

        public void Clear()
        {
            Array.Clear(marks, 0, marks.Length);
        }

        public bool IsSolved(NonogramPuzzle puzzle)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            if (puzzle.Width != Width || puzzle.Height != Height)
            {
                return false;
            }

            for (var row = 0; row < Height; row++)
            {
                if (!IsLineSolved(row, true, puzzle.RowHints[row]))
                {
                    return false;
                }
            }

            for (var column = 0; column < Width; column++)
            {
                if (!IsLineSolved(column, false, puzzle.ColumnHints[column]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLineSolved(int index, bool rowLine, System.Collections.Generic.IReadOnlyList<int> expectedHints)
        {
            if (expectedHints == null)
            {
                return false;
            }

            var actualRuns = new System.Collections.Generic.List<int>();
            var streak = 0;
            var length = rowLine ? Width : Height;

            for (var offset = 0; offset < length; offset++)
            {
                var mark = rowLine ? marks[ToIndex(index, offset)] : marks[ToIndex(offset, index)];
                if (mark == NonogramCellMark.Unknown)
                {
                    return false;
                }

                if (mark == NonogramCellMark.Filled)
                {
                    streak += 1;
                    continue;
                }

                if (streak > 0)
                {
                    actualRuns.Add(streak);
                    streak = 0;
                }
            }

            if (streak > 0)
            {
                actualRuns.Add(streak);
            }

            return MatchesLineHints(expectedHints, actualRuns);
        }

        private static bool MatchesLineHints(System.Collections.Generic.IReadOnlyList<int> expectedHints, System.Collections.Generic.IReadOnlyList<int> actualRuns)
        {
            if (expectedHints == null || actualRuns == null)
            {
                return false;
            }

            if (expectedHints.Count == 1 && expectedHints[0] == 0)
            {
                return actualRuns.Count == 0;
            }

            if (expectedHints.Count != actualRuns.Count)
            {
                return false;
            }

            for (var i = 0; i < expectedHints.Count; i++)
            {
                if (expectedHints[i] != actualRuns[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ValidateCell(int row, int column)
        {
            if (row < 0 || row >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (column < 0 || column >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        private void ValidateRow(int row)
        {
            if (row < 0 || row >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
        }

        private void ValidateColumn(int column)
        {
            if (column < 0 || column >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        private int ToIndex(int row, int column)
        {
            return (row * Width) + column;
        }

        private bool IsRowSatisfied(NonogramPuzzle puzzle, int row)
        {
            var expectedHints = puzzle.RowHints[row];
            return MatchesLineHints(expectedHints, GetLineFillRuns(row, true));
        }

        private bool IsColumnSatisfied(NonogramPuzzle puzzle, int column)
        {
            var expectedHints = puzzle.ColumnHints[column];
            return MatchesLineHints(expectedHints, GetLineFillRuns(column, false));
        }

        private bool CrossUnknownsInRow(int row)
        {
            var changed = false;
            for (var column = 0; column < Width; column++)
            {
                var index = ToIndex(row, column);
                if (marks[index] != NonogramCellMark.Unknown)
                {
                    continue;
                }

                marks[index] = NonogramCellMark.Crossed;
                changed = true;
            }

            return changed;
        }

        private bool CrossUnknownsInColumn(int column)
        {
            var changed = false;
            for (var row = 0; row < Height; row++)
            {
                var index = ToIndex(row, column);
                if (marks[index] != NonogramCellMark.Unknown)
                {
                    continue;
                }

                marks[index] = NonogramCellMark.Crossed;
                changed = true;
            }

            return changed;
        }

        private System.Collections.Generic.List<int> GetLineFillRuns(int index, bool rowLine)
        {
            var runs = new System.Collections.Generic.List<int>();
            var streak = 0;
            var length = rowLine ? Width : Height;

            for (var offset = 0; offset < length; offset++)
            {
                var mark = rowLine ? marks[ToIndex(index, offset)] : marks[ToIndex(offset, index)];
                if (mark == NonogramCellMark.Filled)
                {
                    streak += 1;
                    continue;
                }

                if (streak > 0)
                {
                    runs.Add(streak);
                    streak = 0;
                }
            }

            if (streak > 0)
            {
                runs.Add(streak);
            }

            return runs;
        }

        private bool[] BuildLineHintCompletionMask(System.Collections.Generic.IReadOnlyList<int> expectedHints, int lineIndex, bool rowLine)
        {
            if (expectedHints == null)
            {
                return new bool[0];
            }

            var completed = new bool[expectedHints.Count];
            if (expectedHints.Count == 0)
            {
                return completed;
            }

            if (expectedHints.Count == 1 && expectedHints[0] == 0)
            {
                completed[0] = IsLineFullyCrossed(lineIndex, rowLine);
                return completed;
            }

            var length = rowLine ? Width : Height;
            var clueIndex = 0;
            var cell = 0;

            while (cell < length && clueIndex < expectedHints.Count)
            {
                while (cell < length && GetLineMark(lineIndex, cell, rowLine) == NonogramCellMark.Crossed)
                {
                    cell += 1;
                }

                if (cell >= length)
                {
                    break;
                }

                if (GetLineMark(lineIndex, cell, rowLine) != NonogramCellMark.Filled)
                {
                    cell += 1;
                    continue;
                }

                var runStart = cell;
                while (cell < length && GetLineMark(lineIndex, cell, rowLine) == NonogramCellMark.Filled)
                {
                    cell += 1;
                }

                var runLength = cell - runStart;
                var leftBlocked = runStart == 0 || GetLineMark(lineIndex, runStart - 1, rowLine) == NonogramCellMark.Crossed;
                var rightBlocked = cell == length || GetLineMark(lineIndex, cell, rowLine) == NonogramCellMark.Crossed;

                if (runLength == expectedHints[clueIndex] && leftBlocked && rightBlocked)
                {
                    completed[clueIndex] = true;
                    clueIndex += 1;
                }
            }

            return completed;
        }

        private bool IsLineFullyCrossed(int lineIndex, bool rowLine)
        {
            var length = rowLine ? Width : Height;
            for (var cell = 0; cell < length; cell++)
            {
                if (GetLineMark(lineIndex, cell, rowLine) != NonogramCellMark.Crossed)
                {
                    return false;
                }
            }

            return true;
        }

        private NonogramCellMark GetLineMark(int lineIndex, int offset, bool rowLine)
        {
            return rowLine ? marks[ToIndex(lineIndex, offset)] : marks[ToIndex(offset, lineIndex)];
        }
    }
}
