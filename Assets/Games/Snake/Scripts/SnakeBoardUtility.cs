using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    /// <summary>
    /// 贪吃蛇方向定义。
    /// </summary>
    public enum SnakeDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// 贪吃蛇棋盘辅助工具。
    /// </summary>
    public static class SnakeBoardUtility
    {
        public static Vector2Int ToOffset(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.Up:
                    return new Vector2Int(0, -1);
                case SnakeDirection.Down:
                    return new Vector2Int(0, 1);
                case SnakeDirection.Left:
                    return new Vector2Int(-1, 0);
                default:
                    return new Vector2Int(1, 0);
            }
        }

        public static SnakeDirection OppositeOf(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.Up:
                    return SnakeDirection.Down;
                case SnakeDirection.Down:
                    return SnakeDirection.Up;
                case SnakeDirection.Left:
                    return SnakeDirection.Right;
                default:
                    return SnakeDirection.Left;
            }
        }

        public static bool IsOpposite(SnakeDirection first, SnakeDirection second)
        {
            return OppositeOf(first) == second;
        }

        public static bool IsInside(int rows, int columns, Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
        }

        public static Vector2Int Step(Vector2Int cell, SnakeDirection direction)
        {
            return cell + ToOffset(direction);
        }
    }
}
