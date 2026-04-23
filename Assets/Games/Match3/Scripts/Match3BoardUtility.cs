using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 一次交换解析后的汇总结果（清除数量、连锁次数、是否洗牌）。
    /// </summary>
    public sealed class Match3ResolveResult
    {
        public int ClearedCount;
        public int CascadeCount;
        public bool WasReshuffled;
    }

    /// <summary>
    /// 单次连锁步骤快照：记录清除、下落、补齐前后棋盘状态。
    /// </summary>
    public sealed class Match3CascadeStep
    {
        public int[,] BoardBeforeClear;
        public int[,] BoardAfterClear;
        public int[,] BoardAfterCollapse;
        public int[,] BoardAfterRefill;
        public readonly List<Vector2Int> ClearedCells = new List<Vector2Int>();
    }

    /// <summary>
    /// 交换求解计划：包含有效性、完整步骤和最终棋盘。
    /// </summary>
    public sealed class Match3ResolvePlan
    {
        public bool IsValidSwap;
        public int ClearedCount;
        public int CascadeCount;
        public bool WasReshuffled;
        public int[,] BoardAfterSwap;
        public int[,] FinalBoard;
        public readonly List<Match3CascadeStep> Steps = new List<Match3CascadeStep>();
    }

    /// <summary>
    /// 三消棋盘算法工具：填盘、判定可交换、结算消除、坠落与补牌。
    /// </summary>
    public static class Match3BoardUtility
    {
        /// <summary>
        /// 初始化棋盘，尽量避免开局即三连且保证至少存在一步可交换。
        /// </summary>
        public static void FillBoard(int[,] board, int rows, int columns, int tileTypeCount)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        board[row, column] = CreateNonMatchingTile(board, row, column, tileTypeCount);
                    }
                }

                if (TryFindPossibleSwap(board, rows, columns, out _, out _))
                {
                    return;
                }
            }

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    board[row, column] = ((row * 2 + column) % tileTypeCount) + 1;
                }
            }
        }

        /// <summary>
        /// 尝试交换并就地应用解析结果；无效交换返回 false。
        /// </summary>
        public static bool TrySwapAndResolve(
            int[,] board,
            int rows,
            int columns,
            int tileTypeCount,
            Vector2Int first,
            Vector2Int second,
            Match3ResolveResult result)
        {
            var plan = BuildResolvePlan(board, rows, columns, tileTypeCount, first, second);
            result.ClearedCount = plan.ClearedCount;
            result.CascadeCount = plan.CascadeCount;
            result.WasReshuffled = plan.WasReshuffled;

            if (!plan.IsValidSwap)
            {
                return false;
            }

            ApplyBoard(board, plan.FinalBoard);
            return true;
        }

        /// <summary>
        /// 构建交换后的完整解析计划，不直接修改输入棋盘。
        /// </summary>
        public static Match3ResolvePlan BuildResolvePlan(
            int[,] sourceBoard,
            int rows,
            int columns,
            int tileTypeCount,
            Vector2Int first,
            Vector2Int second)
        {
            var plan = new Match3ResolvePlan();
            if (!AreInside(first, rows, columns) || !AreInside(second, rows, columns))
            {
                plan.FinalBoard = CloneBoard(sourceBoard);
                return plan;
            }

            if (!AreAdjacent(first, second))
            {
                plan.FinalBoard = CloneBoard(sourceBoard);
                return plan;
            }

            var working = CloneBoard(sourceBoard);
            Swap(working, first, second);
            if (!HasAnyMatch(working, rows, columns))
            {
                plan.FinalBoard = CloneBoard(sourceBoard);
                return plan;
            }

            plan.IsValidSwap = true;
            plan.BoardAfterSwap = CloneBoard(working);

            while (true)
            {
                var matched = CollectMatches(working, rows, columns);
                if (matched.Count == 0)
                {
                    break;
                }

                var step = new Match3CascadeStep();
                step.BoardBeforeClear = CloneBoard(working);
                step.ClearedCells.AddRange(matched);

                plan.CascadeCount += 1;
                plan.ClearedCount += matched.Count;

                for (var i = 0; i < matched.Count; i++)
                {
                    working[matched[i].y, matched[i].x] = 0;
                }

                step.BoardAfterClear = CloneBoard(working);
                Collapse(working, rows, columns);
                step.BoardAfterCollapse = CloneBoard(working);
                Refill(working, rows, columns, tileTypeCount);
                step.BoardAfterRefill = CloneBoard(working);
                plan.Steps.Add(step);
            }

            if (!TryFindPossibleSwap(working, rows, columns, out _, out _))
            {
                FillBoard(working, rows, columns, tileTypeCount);
                plan.WasReshuffled = true;
            }

            plan.FinalBoard = CloneBoard(working);
            return plan;
        }

        /// <summary>
        /// 在当前棋盘中搜索任意一组可形成消除的交换位置。
        /// </summary>
        public static bool TryFindPossibleSwap(int[,] board, int rows, int columns, out Vector2Int first, out Vector2Int second)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var origin = new Vector2Int(column, row);
                    if (column + 1 < columns)
                    {
                        var right = new Vector2Int(column + 1, row);
                        if (WouldSwapCreateMatch(board, rows, columns, origin, right))
                        {
                            first = origin;
                            second = right;
                            return true;
                        }
                    }

                    if (row + 1 < rows)
                    {
                        var down = new Vector2Int(column, row + 1);
                        if (WouldSwapCreateMatch(board, rows, columns, origin, down))
                        {
                            first = origin;
                            second = down;
                            return true;
                        }
                    }
                }
            }

            first = default(Vector2Int);
            second = default(Vector2Int);
            return false;
        }

        /// <summary>
        /// 判断棋盘当前是否存在三连匹配。
        /// </summary>
        public static bool HasAnyMatch(int[,] board, int rows, int columns)
        {
            return CollectMatches(board, rows, columns).Count > 0;
        }

        /// <summary>
        /// 判断棋盘范围内是否全部为有效图块（大于 0）。
        /// </summary>
        public static bool IsBoardFilled(int[,] board, int rows, int columns)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (board[row, column] == 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 深拷贝棋盘数组。
        /// </summary>
        public static int[,] CloneBoard(int[,] source)
        {
            var clone = new int[source.GetLength(0), source.GetLength(1)];
            ApplyBoard(clone, source);
            return clone;
        }

        /// <summary>
        /// 将 source 全量复制到 target。
        /// </summary>
        public static void ApplyBoard(int[,] target, int[,] source)
        {
            for (var row = 0; row < source.GetLength(0); row++)
            {
                for (var column = 0; column < source.GetLength(1); column++)
                {
                    target[row, column] = source[row, column];
                }
            }
        }

        /// <summary>
        /// 仅复制指定坐标集合对应的棋盘值。
        /// </summary>
        public static void ApplyBoard(int[,] target, int[,] source, IList<Vector2Int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                target[cell.y, cell.x] = source[cell.y, cell.x];
            }
        }

        /// <summary>
        /// 汇总计划中会被动画占用的格子集合，用于输入锁定。
        /// </summary>
        public static List<Vector2Int> CollectPlanLockedCells(
            Match3ResolvePlan plan,
            int rows,
            int columns,
            Vector2Int first,
            Vector2Int second)
        {
            var result = new List<Vector2Int>();
            var marked = new bool[rows, columns];

            if (!plan.IsValidSwap)
            {
                AddCellIfInside(marked, result, rows, columns, first);
                AddCellIfInside(marked, result, rows, columns, second);
                return result;
            }

            if (plan.WasReshuffled)
            {
                for (var row = 0; row < rows; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        marked[row, column] = true;
                        result.Add(new Vector2Int(column, row));
                    }
                }

                return result;
            }

            var affectedColumns = new bool[columns];
            if (AreInside(first, rows, columns))
            {
                affectedColumns[first.x] = true;
            }

            if (AreInside(second, rows, columns))
            {
                affectedColumns[second.x] = true;
            }

            for (var stepIndex = 0; stepIndex < plan.Steps.Count; stepIndex++)
            {
                var step = plan.Steps[stepIndex];
                for (var cellIndex = 0; cellIndex < step.ClearedCells.Count; cellIndex++)
                {
                    affectedColumns[step.ClearedCells[cellIndex].x] = true;
                }
            }

            for (var column = 0; column < columns; column++)
            {
                if (!affectedColumns[column])
                {
                    continue;
                }

                for (var row = 0; row < rows; row++)
                {
                    if (marked[row, column])
                    {
                        continue;
                    }

                    marked[row, column] = true;
                    result.Add(new Vector2Int(column, row));
                }
            }

            return result;
        }

        private static bool WouldSwapCreateMatch(int[,] board, int rows, int columns, Vector2Int first, Vector2Int second)
        {
            Swap(board, first, second);
            var hasMatch = HasAnyMatch(board, rows, columns);
            Swap(board, first, second);
            return hasMatch;
        }

        private static List<Vector2Int> CollectMatches(int[,] board, int rows, int columns)
        {
            var marked = new bool[rows, columns];
            var matches = new List<Vector2Int>();

            for (var row = 0; row < rows; row++)
            {
                var start = 0;
                while (start < columns)
                {
                    var value = board[row, start];
                    if (value == 0)
                    {
                        start += 1;
                        continue;
                    }

                    var end = start + 1;
                    while (end < columns && board[row, end] == value)
                    {
                        end += 1;
                    }

                    if (end - start >= 3)
                    {
                        for (var column = start; column < end; column++)
                        {
                            marked[row, column] = true;
                        }
                    }

                    start = end;
                }
            }

            for (var column = 0; column < columns; column++)
            {
                var start = 0;
                while (start < rows)
                {
                    var value = board[start, column];
                    if (value == 0)
                    {
                        start += 1;
                        continue;
                    }

                    var end = start + 1;
                    while (end < rows && board[end, column] == value)
                    {
                        end += 1;
                    }

                    if (end - start >= 3)
                    {
                        for (var row = start; row < end; row++)
                        {
                            marked[row, column] = true;
                        }
                    }

                    start = end;
                }
            }

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (marked[row, column])
                    {
                        matches.Add(new Vector2Int(column, row));
                    }
                }
            }

            return matches;
        }

        private static void Collapse(int[,] board, int rows, int columns)
        {
            for (var column = 0; column < columns; column++)
            {
                var writeRow = rows - 1;
                for (var row = rows - 1; row >= 0; row--)
                {
                    if (board[row, column] == 0)
                    {
                        continue;
                    }

                    board[writeRow, column] = board[row, column];
                    if (writeRow != row)
                    {
                        board[row, column] = 0;
                    }

                    writeRow -= 1;
                }

                while (writeRow >= 0)
                {
                    board[writeRow, column] = 0;
                    writeRow -= 1;
                }
            }
        }

        private static void Refill(int[,] board, int rows, int columns, int tileTypeCount)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    if (board[row, column] == 0)
                    {
                        board[row, column] = Random.Range(1, tileTypeCount + 1);
                    }
                }
            }
        }

        private static int CreateNonMatchingTile(int[,] board, int row, int column, int tileTypeCount)
        {
            for (var attempt = 0; attempt < 24; attempt++)
            {
                var candidate = Random.Range(1, tileTypeCount + 1);
                if (column >= 2 &&
                    board[row, column - 1] == candidate &&
                    board[row, column - 2] == candidate)
                {
                    continue;
                }

                if (row >= 2 &&
                    board[row - 1, column] == candidate &&
                    board[row - 2, column] == candidate)
                {
                    continue;
                }

                return candidate;
            }

            return ((row + column) % tileTypeCount) + 1;
        }

        private static bool AreAdjacent(Vector2Int first, Vector2Int second)
        {
            return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y) == 1;
        }

        private static bool AreInside(Vector2Int point, int rows, int columns)
        {
            return point.x >= 0 && point.x < columns && point.y >= 0 && point.y < rows;
        }

        private static void AddCellIfInside(bool[,] marked, List<Vector2Int> result, int rows, int columns, Vector2Int cell)
        {
            if (!AreInside(cell, rows, columns) || marked[cell.y, cell.x])
            {
                return;
            }

            marked[cell.y, cell.x] = true;
            result.Add(cell);
        }

        private static void Swap(int[,] board, Vector2Int first, Vector2Int second)
        {
            var temporary = board[first.y, first.x];
            board[first.y, first.x] = board[second.y, second.x];
            board[second.y, second.x] = temporary;
        }
    }
}

