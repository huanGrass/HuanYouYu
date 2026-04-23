using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 连连看棋盘算法工具：生成、洗牌、可解对数统计与三折线路径搜索。
    /// </summary>
    public static class ClassicLinkBoardUtility
    {
        private static readonly int[] RowOffsets = { -1, 1, 0, 0 };
        private static readonly int[] ColumnOffsets = { 0, 0, -1, 1 };

        /// <summary>
        /// 填充棋盘并尽量保证初始可消除对数达到阈值。
        /// </summary>
        public static void FillBoardWithRandomPairs(int[,] board, int rows, int columns, int typeCount, int copiesPerType, int minimumAvailablePairs)
        {
            var values = BuildRandomPairedValues(rows * columns, typeCount, copiesPerType);

            for (var attempt = 0; attempt < 200; attempt++)
            {
                Shuffle(values);
                WriteValues(board, rows, columns, values);
                if (CountAvailablePairs(board, rows, columns) >= minimumAvailablePairs)
                {
                    return;
                }
            }

            WriteValues(board, rows, columns, values);
        }

        private static List<int> BuildRandomPairedValues(int totalCellCount, int typeCount, int copiesPerType)
        {
            var values = new List<int>(totalCellCount);
            if (totalCellCount <= 0 || typeCount <= 0)
            {
                return values;
            }

            // Prefer evenly-distributed copies when it can exactly fill the board.
            if (copiesPerType > 0 && typeCount * copiesPerType == totalCellCount && copiesPerType % 2 == 0)
            {
                for (var value = 1; value <= typeCount; value++)
                {
                    for (var count = 0; count < copiesPerType; count++)
                    {
                        values.Add(value);
                    }
                }

                return values;
            }

            // Fallback: always fill with strict pairs so every icon remains removable.
            var pairCount = totalCellCount / 2;
            for (var pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                var value = (pairIndex % typeCount) + 1;
                values.Add(value);
                values.Add(value);
            }

            if (values.Count < totalCellCount)
            {
                // Defensive fallback for odd grids.
                values.Add(Random.Range(1, typeCount + 1));
            }

            return values;
        }

        /// <summary>
        /// 对剩余未消除方块重新洗牌，尽量恢复到有足够可消除对数的状态。
        /// </summary>
        public static void ReshuffleRemainingTiles(int[,] board, int rows, int columns, int minimumAvailablePairs)
        {
            var values = new List<int>(rows * columns);
            for (var row = 1; row <= rows; row++)
            {
                for (var column = 1; column <= columns; column++)
                {
                    if (board[row, column] != 0)
                    {
                        values.Add(board[row, column]);
                    }
                }
            }

            if (values.Count <= 1)
            {
                return;
            }

            for (var attempt = 0; attempt < 200; attempt++)
            {
                Shuffle(values);
                WriteRemainingValues(board, rows, columns, values);
                if (CountAvailablePairs(board, rows, columns) >= minimumAvailablePairs)
                {
                    return;
                }
            }

            WriteRemainingValues(board, rows, columns, values);
        }

        /// <summary>
        /// 统计当前棋盘可直接连通并消除的配对数量。
        /// </summary>
        public static int CountAvailablePairs(int[,] board, int rows, int columns)
        {
            var count = 0;
            for (var firstRow = 1; firstRow <= rows; firstRow++)
            {
                for (var firstColumn = 1; firstColumn <= columns; firstColumn++)
                {
                    var value = board[firstRow, firstColumn];
                    if (value == 0)
                    {
                        continue;
                    }

                    for (var secondRow = firstRow; secondRow <= rows; secondRow++)
                    {
                        var secondColumnStart = secondRow == firstRow ? firstColumn + 1 : 1;
                        for (var secondColumn = secondColumnStart; secondColumn <= columns; secondColumn++)
                        {
                            if (board[secondRow, secondColumn] != value)
                            {
                                continue;
                            }

                            List<Vector2Int> path;
                            if (TryFindPath(board, rows, columns, new Vector2Int(firstColumn, firstRow), new Vector2Int(secondColumn, secondRow), out path))
                            {
                                count += 1;
                            }
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// 搜索两点间是否存在不超过两次拐弯的有效路径。
        /// </summary>
        public static bool TryFindPath(int[,] board, int rows, int columns, Vector2Int start, Vector2Int target, out List<Vector2Int> path)
        {
            path = null;
            if (start == target)
            {
                return false;
            }

            var maxRow = rows + 2;
            var maxColumn = columns + 2;
            var bestTurns = new int[maxRow, maxColumn, 4];
            var parentIds = new int[maxRow, maxColumn, 4];
            var queue = new Queue<State>();

            for (var row = 0; row < maxRow; row++)
            {
                for (var column = 0; column < maxColumn; column++)
                {
                    for (var direction = 0; direction < 4; direction++)
                    {
                        bestTurns[row, column, direction] = int.MaxValue;
                        parentIds[row, column, direction] = -1;
                    }
                }
            }

            for (var direction = 0; direction < 4; direction++)
            {
                ExpandDirection(board, rows, columns, start.y, start.x, direction, 0, -1, target, queue, bestTurns, parentIds);
            }

            State endState = default(State);
            var found = false;
            while (queue.Count > 0 && !found)
            {
                var current = queue.Dequeue();
                if (current.Row == target.y && current.Column == target.x)
                {
                    endState = current;
                    found = true;
                    break;
                }

                for (var direction = 0; direction < 4; direction++)
                {
                    var nextTurns = current.Direction == direction ? current.Turns : current.Turns + 1;
                    if (nextTurns > 2)
                    {
                        continue;
                    }

                    ExpandDirection(board, rows, columns, current.Row, current.Column, direction, nextTurns, current.Id, target, queue, bestTurns, parentIds);
                }
            }

            if (!found)
            {
                return false;
            }

            path = BuildPath(endState, start, columns, parentIds);
            return true;
        }

        private static void ExpandDirection(
            int[,] board,
            int rows,
            int columns,
            int row,
            int column,
            int direction,
            int turns,
            int parentId,
            Vector2Int target,
            Queue<State> queue,
            int[,,] bestTurns,
            int[,,] parentIds)
        {
            var nextRow = row + RowOffsets[direction];
            var nextColumn = column + ColumnOffsets[direction];

            while (IsInsideBoard(nextRow, nextColumn, rows, columns) && CanPass(board, nextRow, nextColumn, target))
            {
                if (turns < bestTurns[nextRow, nextColumn, direction])
                {
                    bestTurns[nextRow, nextColumn, direction] = turns;
                    parentIds[nextRow, nextColumn, direction] = parentId;
                    var state = new State(nextRow, nextColumn, direction, columns, turns);
                    queue.Enqueue(state);

                    if (nextRow == target.y && nextColumn == target.x)
                    {
                        return;
                    }
                }

                nextRow += RowOffsets[direction];
                nextColumn += ColumnOffsets[direction];
            }
        }

        private static List<Vector2Int> BuildPath(State endState, Vector2Int start, int columns, int[,,] parentIds)
        {
            var rawPath = new List<Vector2Int>();
            var currentId = endState.Id;

            while (currentId >= 0)
            {
                var decoded = DecodeStateId(currentId, columns);
                rawPath.Add(new Vector2Int(decoded.Column, decoded.Row));
                currentId = parentIds[decoded.Row, decoded.Column, decoded.Direction];
            }

            rawPath.Add(start);
            rawPath.Reverse();

            var corners = new List<Vector2Int>();
            for (var i = 0; i < rawPath.Count; i++)
            {
                if (i == 0 || i == rawPath.Count - 1)
                {
                    corners.Add(rawPath[i]);
                    continue;
                }

                var previous = rawPath[i - 1];
                var current = rawPath[i];
                var next = rawPath[i + 1];
                var horizontalBefore = previous.y == current.y;
                var horizontalAfter = current.y == next.y;
                if (horizontalBefore != horizontalAfter)
                {
                    corners.Add(current);
                }
            }

            return corners;
        }

        private static bool CanPass(int[,] board, int row, int column, Vector2Int target)
        {
            if (row == target.y && column == target.x)
            {
                return true;
            }

            return board[row, column] == 0;
        }

        private static bool IsInsideBoard(int row, int column, int rows, int columns)
        {
            return row >= 0 && row <= rows + 1 && column >= 0 && column <= columns + 1;
        }

        private static void Shuffle(List<int> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                var temporary = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temporary;
            }
        }

        private static void WriteValues(int[,] board, int rows, int columns, List<int> values)
        {
            ClearBoard(board, rows, columns);

            var index = 0;
            for (var row = 1; row <= rows; row++)
            {
                for (var column = 1; column <= columns; column++)
                {
                    board[row, column] = values[index];
                    index += 1;
                }
            }
        }

        private static void WriteRemainingValues(int[,] board, int rows, int columns, List<int> values)
        {
            var index = 0;
            for (var row = 1; row <= rows; row++)
            {
                for (var column = 1; column <= columns; column++)
                {
                    if (board[row, column] == 0)
                    {
                        continue;
                    }

                    board[row, column] = values[index];
                    index += 1;
                }
            }
        }

        private static void ClearBoard(int[,] board, int rows, int columns)
        {
            for (var row = 0; row <= rows + 1; row++)
            {
                for (var column = 0; column <= columns + 1; column++)
                {
                    board[row, column] = 0;
                }
            }
        }

        private static DecodedState DecodeStateId(int id, int columns)
        {
            var direction = id % 4;
            var cell = id / 4;
            var column = cell % (columns + 2);
            var row = cell / (columns + 2);
            return new DecodedState(row, column, direction);
        }

        private readonly struct State
        {
            public State(int row, int column, int direction, int columns, int turns)
            {
                Row = row;
                Column = column;
                Direction = direction;
                Turns = turns;
                Id = ((row * (columns + 2)) + column) * 4 + direction;
            }

            public int Row { get; }
            public int Column { get; }
            public int Direction { get; }
            public int Turns { get; }
            public int Id { get; }
        }

        private readonly struct DecodedState
        {
            public DecodedState(int row, int column, int direction)
            {
                Row = row;
                Column = column;
                Direction = direction;
            }

            public int Row { get; }
            public int Column { get; }
            public int Direction { get; }
        }
    }
}

