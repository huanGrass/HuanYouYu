using System;
using System.Collections.Generic;

namespace HuanYouYu.Game2048
{
    public enum Game2048MoveDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public enum Game2048GameState
    {
        Playing,
        Won,
        Lost
    }

    public interface IGame2048Random
    {
        int Next(int maxExclusive);
    }

    public readonly struct Game2048TileMotion
    {
        public Game2048TileMotion(int value, int fromRow, int fromColumn, int toRow, int toColumn, bool merged)
        {
            Value = value;
            FromRow = fromRow;
            FromColumn = fromColumn;
            ToRow = toRow;
            ToColumn = toColumn;
            Merged = merged;
        }

        public int Value { get; }

        public int FromRow { get; }

        public int FromColumn { get; }

        public int ToRow { get; }

        public int ToColumn { get; }

        public bool Merged { get; }
    }

    public readonly struct Game2048MoveResult
    {
        public Game2048MoveResult(bool boardChanged, int scoreGained, Game2048GameState state, bool reachedGoal, Game2048TileMotion[] tileMotions = null)
        {
            BoardChanged = boardChanged;
            ScoreGained = scoreGained;
            State = state;
            ReachedGoal = reachedGoal;
            TileMotions = tileMotions ?? Array.Empty<Game2048TileMotion>();
        }

        public bool BoardChanged { get; }

        public int ScoreGained { get; }

        public Game2048GameState State { get; }

        public bool ReachedGoal { get; }

        public Game2048TileMotion[] TileMotions { get; }
    }

    public sealed class Game2048Board
    {
        private const int GoalValue = 2048;
        private readonly int[] cells;
        private readonly IGame2048Random random;
        private bool goalReached;

        public Game2048Board(int size = 4, IGame2048Random randomSource = null)
        {
            if (size < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Board size must be at least 2.");
            }

            Size = size;
            cells = new int[size * size];
            random = randomSource ?? new SystemGame2048Random();
            State = Game2048GameState.Playing;
        }

        public int Size { get; }

        public int Score { get; private set; }

        public Game2048GameState State { get; private set; }

        public void Reset()
        {
            Array.Clear(cells, 0, cells.Length);
            Score = 0;
            State = Game2048GameState.Playing;
            goalReached = false;

            SpawnRandomTile();
            SpawnRandomTile();
        }

        public int GetCell(int row, int column)
        {
            ValidatePosition(row, column);
            return cells[(row * Size) + column];
        }

        public int[] Snapshot()
        {
            var snapshot = new int[cells.Length];
            Array.Copy(cells, snapshot, cells.Length);
            return snapshot;
        }

        public void SetBoard(IReadOnlyList<int> values, int score = 0)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.Count != cells.Length)
            {
                throw new ArgumentException("Board state length does not match the board size.", nameof(values));
            }

            for (var index = 0; index < values.Count; index++)
            {
                var value = values[index];
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(values), "Board values must not be negative.");
                }

                cells[index] = value;
            }

            Score = Math.Max(0, score);
            goalReached = ContainsValue(GoalValue);
            State = goalReached ? Game2048GameState.Won : (CanMove() ? Game2048GameState.Playing : Game2048GameState.Lost);
        }

        public bool CanMove()
        {
            for (var row = 0; row < Size; row++)
            {
                for (var column = 0; column < Size; column++)
                {
                    var value = GetCell(row, column);
                    if (value == 0)
                    {
                        return true;
                    }

                    if (column + 1 < Size && value == GetCell(row, column + 1))
                    {
                        return true;
                    }

                    if (row + 1 < Size && value == GetCell(row + 1, column))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public Game2048MoveResult TryMove(Game2048MoveDirection direction)
        {
            if (State != Game2048GameState.Playing)
            {
                return new Game2048MoveResult(false, 0, State, goalReached);
            }

            var lineBuffer = new int[Size];
            var lineOutput = new int[Size];
            var boardChanged = false;
            var scoreGained = 0;
            var reachedGoalThisMove = false;
            var tileMotions = new List<Game2048TileMotion>(cells.Length);

            for (var line = 0; line < Size; line++)
            {
                for (var offset = 0; offset < Size; offset++)
                {
                    var position = ResolvePosition(direction, line, offset);
                    lineBuffer[offset] = cells[position];
                }

                var lineChanged = ProcessLine(direction, line, lineBuffer, lineOutput, tileMotions, out var lineScore, out var lineReachedGoal);
                boardChanged |= lineChanged;
                scoreGained += lineScore;
                reachedGoalThisMove |= lineReachedGoal;

                for (var offset = 0; offset < Size; offset++)
                {
                    var position = ResolvePosition(direction, line, offset);
                    cells[position] = lineOutput[offset];
                }
            }

            if (!boardChanged)
            {
                return new Game2048MoveResult(false, 0, State, false);
            }

            Score += scoreGained;
            SpawnRandomTile();

            if (!goalReached && reachedGoalThisMove)
            {
                goalReached = true;
                State = Game2048GameState.Won;
            }
            else if (!CanMove())
            {
                State = Game2048GameState.Lost;
            }

            return new Game2048MoveResult(true, scoreGained, State, reachedGoalThisMove, tileMotions.ToArray());
        }

        private bool ContainsValue(int target)
        {
            for (var index = 0; index < cells.Length; index++)
            {
                if (cells[index] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private int ResolvePosition(Game2048MoveDirection direction, int line, int offset)
        {
            switch (direction)
            {
                case Game2048MoveDirection.Left:
                    return (line * Size) + offset;
                case Game2048MoveDirection.Right:
                    return (line * Size) + (Size - 1 - offset);
                case Game2048MoveDirection.Up:
                    return (offset * Size) + line;
                default:
                    return ((Size - 1 - offset) * Size) + line;
            }
        }

        private bool ProcessLine(Game2048MoveDirection direction, int line, int[] input, int[] output, List<Game2048TileMotion> tileMotions, out int scoreGained, out bool reachedGoal)
        {
            Array.Clear(output, 0, output.Length);
            scoreGained = 0;
            reachedGoal = false;

            var targetIndex = 0;
            var pending = 0;
            var pendingSourceOffset = -1;
            for (var index = 0; index < input.Length; index++)
            {
                var value = input[index];
                if (value == 0)
                {
                    continue;
                }

                if (pending == 0)
                {
                    pending = value;
                    pendingSourceOffset = index;
                    continue;
                }

                if (pending == value)
                {
                    var mergedValue = pending * 2;
                    output[targetIndex++] = mergedValue;
                    tileMotions.Add(CreateMotion(direction, line, pendingSourceOffset, targetIndex - 1, pending, true));
                    tileMotions.Add(CreateMotion(direction, line, index, targetIndex - 1, value, true));
                    scoreGained += mergedValue;
                    if (mergedValue >= GoalValue)
                    {
                        reachedGoal = true;
                    }

                    pending = 0;
                    pendingSourceOffset = -1;
                    continue;
                }

                output[targetIndex++] = pending;
                if (pendingSourceOffset != targetIndex - 1)
                {
                    tileMotions.Add(CreateMotion(direction, line, pendingSourceOffset, targetIndex - 1, pending, false));
                }

                pending = value;
                pendingSourceOffset = index;
            }

            if (pending != 0)
            {
                output[targetIndex] = pending;
                if (pendingSourceOffset != targetIndex)
                {
                    tileMotions.Add(CreateMotion(direction, line, pendingSourceOffset, targetIndex, pending, false));
                }
            }

            for (var index = 0; index < input.Length; index++)
            {
                if (input[index] != output[index])
                {
                    return true;
                }
            }

            return false;
        }

        private Game2048TileMotion CreateMotion(Game2048MoveDirection direction, int line, int sourceOffset, int targetOffset, int value, bool merged)
        {
            var sourceIndex = ResolvePosition(direction, line, sourceOffset);
            var targetIndex = ResolvePosition(direction, line, targetOffset);
            return new Game2048TileMotion(
                value,
                sourceIndex / Size,
                sourceIndex % Size,
                targetIndex / Size,
                targetIndex % Size,
                merged);
        }

        private void SpawnRandomTile()
        {
            var emptyIndices = new List<int>(cells.Length);
            for (var index = 0; index < cells.Length; index++)
            {
                if (cells[index] == 0)
                {
                    emptyIndices.Add(index);
                }
            }

            if (emptyIndices.Count == 0)
            {
                return;
            }

            var spawnIndex = emptyIndices[random.Next(emptyIndices.Count)];
            cells[spawnIndex] = random.Next(10) == 0 ? 4 : 2;
        }

        private void ValidatePosition(int row, int column)
        {
            if (row < 0 || row >= Size)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (column < 0 || column >= Size)
            {
                throw new ArgumentOutOfRangeException(nameof(column));
            }
        }

        private sealed class SystemGame2048Random : IGame2048Random
        {
            private readonly Random random = new Random();

            public int Next(int maxExclusive)
            {
                return random.Next(maxExclusive);
            }
        }
    }
}
