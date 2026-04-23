using System;
using System.Collections.Generic;

namespace HuanYouYu.Nonogram
{
    public sealed class NonogramPuzzle
    {
        private readonly bool[] solution;

        public NonogramPuzzle(string title, IReadOnlyList<string> rows)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Puzzle title is required.", nameof(title));
            }

            if (rows == null || rows.Count == 0)
            {
                throw new ArgumentException("Puzzle rows are required.", nameof(rows));
            }

            var width = rows[0] == null ? 0 : rows[0].Trim().Length;
            if (width <= 0)
            {
                throw new ArgumentException("Puzzle rows must not be empty.", nameof(rows));
            }

            Title = title.Trim();
            Height = rows.Count;
            Width = width;
            solution = new bool[Width * Height];

            for (var row = 0; row < Height; row++)
            {
                var source = rows[row] == null ? string.Empty : rows[row].Trim();
                if (source.Length != Width)
                {
                    throw new ArgumentException("Puzzle rows must have equal width.", nameof(rows));
                }

                for (var column = 0; column < Width; column++)
                {
                    solution[ToIndex(row, column)] = source[column] == '#';
                }
            }

            RowHints = BuildRowHints();
            ColumnHints = BuildColumnHints();
        }

        public string Title { get; }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<int>[] RowHints { get; }

        public IReadOnlyList<int>[] ColumnHints { get; }

        public bool IsFilled(int row, int column)
        {
            ValidateCell(row, column);
            return solution[ToIndex(row, column)];
        }

        private IReadOnlyList<int>[] BuildRowHints()
        {
            var result = new IReadOnlyList<int>[Height];
            for (var row = 0; row < Height; row++)
            {
                var line = new bool[Width];
                for (var column = 0; column < Width; column++)
                {
                    line[column] = solution[ToIndex(row, column)];
                }

                result[row] = BuildLineHints(line);
            }

            return result;
        }

        private IReadOnlyList<int>[] BuildColumnHints()
        {
            var result = new IReadOnlyList<int>[Width];
            for (var column = 0; column < Width; column++)
            {
                var line = new bool[Height];
                for (var row = 0; row < Height; row++)
                {
                    line[row] = solution[ToIndex(row, column)];
                }

                result[column] = BuildLineHints(line);
            }

            return result;
        }

        private static IReadOnlyList<int> BuildLineHints(IReadOnlyList<bool> line)
        {
            var values = new List<int>();
            var streak = 0;
            for (var index = 0; index < line.Count; index++)
            {
                if (line[index])
                {
                    streak += 1;
                    continue;
                }

                if (streak > 0)
                {
                    values.Add(streak);
                    streak = 0;
                }
            }

            if (streak > 0)
            {
                values.Add(streak);
            }

            if (values.Count == 0)
            {
                values.Add(0);
            }

            return values;
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

        private int ToIndex(int row, int column)
        {
            return (row * Width) + column;
        }
    }
}
