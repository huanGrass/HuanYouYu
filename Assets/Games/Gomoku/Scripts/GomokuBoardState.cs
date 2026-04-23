using System;
using System.Collections.Generic;

namespace HuanYouYu.MiniGameHall
{
    internal enum GomokuStone
    {
        None = 0,
        Black = 1,
        White = 2
    }

    internal enum GomokuRoundState
    {
        Ongoing = 0,
        BlackWin = 1,
        WhiteWin = 2,
        Draw = 3
    }

    internal readonly struct GomokuMove
    {
        public GomokuMove(int row, int column)
        {
            Row = row;
            Column = column;
            IsValid = row >= 0 && column >= 0;
        }

        public int Row { get; }

        public int Column { get; }

        public bool IsValid { get; }
    }

    internal sealed class GomokuBoardState
    {
        private static readonly int[] DirectionSteps = { 1, 0, 0, 1, 1, 1, 1, -1 };

        private readonly GomokuStone[,] stones;

        public GomokuBoardState(int size)
        {
            if (size < 5)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Board size must be at least 5.");
            }

            Size = size;
            stones = new GomokuStone[size, size];
            CurrentTurn = GomokuStone.Black;
        }

        public int Size { get; }

        public GomokuStone CurrentTurn { get; private set; }

        public void Reset()
        {
            Array.Clear(stones, 0, stones.Length);
            CurrentTurn = GomokuStone.Black;
        }

        public GomokuStone GetStone(int row, int column)
        {
            return IsInside(row, column) ? stones[row, column] : GomokuStone.None;
        }

        public bool TryPlaceStone(int row, int column, GomokuStone stone, out GomokuRoundState roundState)
        {
            roundState = GomokuRoundState.Ongoing;
            if (!IsInside(row, column) || stone == GomokuStone.None || CurrentTurn != stone || stones[row, column] != GomokuStone.None)
            {
                return false;
            }

            stones[row, column] = stone;
            if (HasFiveInARow(row, column, stone))
            {
                roundState = stone == GomokuStone.Black ? GomokuRoundState.BlackWin : GomokuRoundState.WhiteWin;
                return true;
            }

            if (IsBoardFull())
            {
                roundState = GomokuRoundState.Draw;
                return true;
            }

            CurrentTurn = GetOpponent(stone);
            return true;
        }

        public bool WouldWin(int row, int column, GomokuStone stone)
        {
            if (!IsInside(row, column) || stone == GomokuStone.None || stones[row, column] != GomokuStone.None)
            {
                return false;
            }

            stones[row, column] = stone;
            var willWin = HasFiveInARow(row, column, stone);
            stones[row, column] = GomokuStone.None;
            return willWin;
        }

        public bool HasAnyStone()
        {
            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    if (stones[row, column] != GomokuStone.None)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IEnumerable<GomokuMove> EnumerateCandidateMoves(int radius)
        {
            if (!HasAnyStone())
            {
                var center = Size / 2;
                yield return new GomokuMove(center, center);
                yield break;
            }

            var seen = new bool[Size, Size];
            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    if (stones[row, column] == GomokuStone.None)
                    {
                        continue;
                    }

                    var minRow = Math.Max(0, row - radius);
                    var maxRow = Math.Min(Size - 1, row + radius);
                    var minColumn = Math.Max(0, column - radius);
                    var maxColumn = Math.Min(Size - 1, column + radius);
                    for (var candidateRow = minRow; candidateRow <= maxRow; candidateRow++)
                    {
                        for (var candidateColumn = minColumn; candidateColumn <= maxColumn; candidateColumn++)
                        {
                            if (stones[candidateRow, candidateColumn] != GomokuStone.None || seen[candidateRow, candidateColumn])
                            {
                                continue;
                            }

                            seen[candidateRow, candidateColumn] = true;
                            yield return new GomokuMove(candidateRow, candidateColumn);
                        }
                    }
                }
            }
        }

        public int EvaluateMove(int row, int column, GomokuStone stone)
        {
            if (!IsInside(row, column) || stone == GomokuStone.None || stones[row, column] != GomokuStone.None)
            {
                return int.MinValue;
            }

            var score = 0;
            for (var i = 0; i < DirectionSteps.Length; i += 2)
            {
                score += EvaluateDirection(row, column, stone, DirectionSteps[i], DirectionSteps[i + 1]);
            }

            return score;
        }

        public int DistanceToCenter(int row, int column)
        {
            var center = Size / 2;
            return Math.Abs(row - center) + Math.Abs(column - center);
        }

        private int EvaluateDirection(int row, int column, GomokuStone stone, int deltaRow, int deltaColumn)
        {
            var total = 1;
            var openEnds = 0;

            CountLine(row, column, stone, deltaRow, deltaColumn, ref total, ref openEnds);
            CountLine(row, column, stone, -deltaRow, -deltaColumn, ref total, ref openEnds);

            if (total >= 5)
            {
                return 100000;
            }

            if (total == 4 && openEnds == 2)
            {
                return 16000;
            }

            if (total == 4 && openEnds == 1)
            {
                return 6000;
            }

            if (total == 3 && openEnds == 2)
            {
                return 2200;
            }

            if (total == 3 && openEnds == 1)
            {
                return 500;
            }

            if (total == 2 && openEnds == 2)
            {
                return 220;
            }

            if (total == 2 && openEnds == 1)
            {
                return 60;
            }

            if (total == 1 && openEnds == 2)
            {
                return 20;
            }

            return 5;
        }

        private void CountLine(int row, int column, GomokuStone stone, int deltaRow, int deltaColumn, ref int total, ref int openEnds)
        {
            var scanRow = row + deltaRow;
            var scanColumn = column + deltaColumn;
            while (IsInside(scanRow, scanColumn) && stones[scanRow, scanColumn] == stone)
            {
                total++;
                scanRow += deltaRow;
                scanColumn += deltaColumn;
            }

            if (IsInside(scanRow, scanColumn) && stones[scanRow, scanColumn] == GomokuStone.None)
            {
                openEnds++;
            }
        }

        private bool HasFiveInARow(int row, int column, GomokuStone stone)
        {
            for (var i = 0; i < DirectionSteps.Length; i += 2)
            {
                var total = 1;
                total += CountConnected(row, column, stone, DirectionSteps[i], DirectionSteps[i + 1]);
                total += CountConnected(row, column, stone, -DirectionSteps[i], -DirectionSteps[i + 1]);
                if (total >= 5)
                {
                    return true;
                }
            }

            return false;
        }

        private int CountConnected(int row, int column, GomokuStone stone, int deltaRow, int deltaColumn)
        {
            var count = 0;
            var scanRow = row + deltaRow;
            var scanColumn = column + deltaColumn;
            while (IsInside(scanRow, scanColumn) && stones[scanRow, scanColumn] == stone)
            {
                count++;
                scanRow += deltaRow;
                scanColumn += deltaColumn;
            }

            return count;
        }

        private bool IsBoardFull()
        {
            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    if (stones[row, column] == GomokuStone.None)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsInside(int row, int column)
        {
            return row >= 0 && row < Size && column >= 0 && column < Size;
        }

        private static GomokuStone GetOpponent(GomokuStone stone)
        {
            return stone == GomokuStone.Black ? GomokuStone.White : GomokuStone.Black;
        }
    }
}
