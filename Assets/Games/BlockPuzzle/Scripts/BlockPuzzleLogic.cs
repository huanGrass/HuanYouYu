using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuanYouYu.MiniGameHall
{
    internal struct BlockPuzzleCell
    {
        public BlockPuzzleCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    internal sealed class BlockPuzzlePiece
    {
        private readonly BlockPuzzleCell[] cells;

        public BlockPuzzlePiece(string id, IEnumerable<BlockPuzzleCell> sourceCells, int colorIndex)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Piece id is required.", nameof(id));
            }

            Id = id;
            cells = NormalizeCells(sourceCells, out var width, out var height);
            Width = width;
            Height = height;
            ColorIndex = Mathf.Max(1, colorIndex);
        }

        public string Id { get; }

        public int Width { get; }

        public int Height { get; }

        public int ColorIndex { get; }

        public int CellCount
        {
            get { return cells.Length; }
        }

        public BlockPuzzlePiece WithColorIndex(int colorIndex)
        {
            return new BlockPuzzlePiece(Id, cells, colorIndex);
        }

        public BlockPuzzleCell GetCell(int index)
        {
            return cells[index];
        }

        public BlockPuzzleCell[] GetCells()
        {
            var copy = new BlockPuzzleCell[cells.Length];
            Array.Copy(cells, copy, cells.Length);
            return copy;
        }

        private static BlockPuzzleCell[] NormalizeCells(IEnumerable<BlockPuzzleCell> sourceCells, out int width, out int height)
        {
            if (sourceCells == null)
            {
                throw new ArgumentNullException(nameof(sourceCells));
            }

            var rawCells = new List<BlockPuzzleCell>();
            foreach (var cell in sourceCells)
            {
                rawCells.Add(cell);
            }

            if (rawCells.Count == 0)
            {
                throw new ArgumentException("Piece must contain at least one cell.", nameof(sourceCells));
            }

            var minX = rawCells[0].X;
            var minY = rawCells[0].Y;
            for (var i = 1; i < rawCells.Count; i++)
            {
                minX = Mathf.Min(minX, rawCells[i].X);
                minY = Mathf.Min(minY, rawCells[i].Y);
            }

            var normalized = new BlockPuzzleCell[rawCells.Count];
            var used = new HashSet<string>(StringComparer.Ordinal);
            var maxX = 0;
            var maxY = 0;
            for (var i = 0; i < rawCells.Count; i++)
            {
                var normalizedCell = new BlockPuzzleCell(rawCells[i].X - minX, rawCells[i].Y - minY);
                var key = normalizedCell.X + ":" + normalizedCell.Y;
                if (!used.Add(key))
                {
                    throw new ArgumentException("Piece cells must not overlap.", nameof(sourceCells));
                }

                normalized[i] = normalizedCell;
                maxX = Mathf.Max(maxX, normalizedCell.X);
                maxY = Mathf.Max(maxY, normalizedCell.Y);
            }

            Array.Sort(normalized, CompareCells);
            width = maxX + 1;
            height = maxY + 1;
            return normalized;
        }

        private static int CompareCells(BlockPuzzleCell left, BlockPuzzleCell right)
        {
            var yCompare = left.Y.CompareTo(right.Y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return left.X.CompareTo(right.X);
        }
    }

    internal static class BlockPuzzlePieceLibrary
    {
        private static readonly BlockPuzzlePiece[] ShapeTemplates =
        {
            CreateFromCells("single", 1, new BlockPuzzleCell(0, 0)),
            CreateLine("h2", 2, true),
            CreateLine("h3", 3, true),
            CreateLine("h4", 4, true),
            CreateLine("h5", 5, true),
            CreateLine("v2", 2, false),
            CreateLine("v3", 3, false),
            CreateLine("v4", 4, false),
            CreateLine("v5", 5, false),
            CreateSquare("square2", 2),
            CreateSquare("square3", 3),
            CreateFromCells("corner3", 1, new BlockPuzzleCell(0, 0), new BlockPuzzleCell(1, 0), new BlockPuzzleCell(0, 1)),
            CreateFromCells("corner4", 1, new BlockPuzzleCell(0, 0), new BlockPuzzleCell(0, 1), new BlockPuzzleCell(0, 2), new BlockPuzzleCell(1, 0)),
            CreateFromCells("corner4_mirror", 1, new BlockPuzzleCell(1, 0), new BlockPuzzleCell(1, 1), new BlockPuzzleCell(1, 2), new BlockPuzzleCell(0, 0)),
            CreateFromCells("t4", 1, new BlockPuzzleCell(0, 1), new BlockPuzzleCell(1, 1), new BlockPuzzleCell(2, 1), new BlockPuzzleCell(1, 0)),
            CreateFromCells("z4", 1, new BlockPuzzleCell(0, 1), new BlockPuzzleCell(1, 1), new BlockPuzzleCell(1, 0), new BlockPuzzleCell(2, 0)),
            CreateFromCells("s4", 1, new BlockPuzzleCell(0, 0), new BlockPuzzleCell(1, 0), new BlockPuzzleCell(1, 1), new BlockPuzzleCell(2, 1)),
            CreateFromCells("plus5", 1, new BlockPuzzleCell(1, 0), new BlockPuzzleCell(0, 1), new BlockPuzzleCell(1, 1), new BlockPuzzleCell(2, 1), new BlockPuzzleCell(1, 2))
        };

        public static BlockPuzzlePiece CreateFromCells(string id, params BlockPuzzleCell[] cells)
        {
            return CreateFromCells(id, 1, cells);
        }

        public static BlockPuzzlePiece CreateFromCells(string id, int colorIndex, params BlockPuzzleCell[] cells)
        {
            return new BlockPuzzlePiece(id, cells, colorIndex);
        }

        public static BlockPuzzlePiece CreateRandom(System.Random random)
        {
            if (random == null)
            {
                random = new System.Random();
            }

            var template = ShapeTemplates[random.Next(ShapeTemplates.Length)];
            return template.WithColorIndex(random.Next(1, 8));
        }

        private static BlockPuzzlePiece CreateLine(string id, int length, bool horizontal)
        {
            var cells = new BlockPuzzleCell[length];
            for (var i = 0; i < length; i++)
            {
                cells[i] = horizontal ? new BlockPuzzleCell(i, 0) : new BlockPuzzleCell(0, i);
            }

            return CreateFromCells(id, 1, cells);
        }

        private static BlockPuzzlePiece CreateSquare(string id, int size)
        {
            var cells = new List<BlockPuzzleCell>(size * size);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    cells.Add(new BlockPuzzleCell(x, y));
                }
            }

            return new BlockPuzzlePiece(id, cells, 1);
        }
    }

    internal struct BlockPuzzlePlacementResult
    {
        public BlockPuzzlePlacementResult(bool success, int cellsPlaced, int linesCleared, int scoreEarned)
        {
            Success = success;
            CellsPlaced = cellsPlaced;
            LinesCleared = linesCleared;
            ScoreEarned = scoreEarned;
        }

        public bool Success { get; }

        public int CellsPlaced { get; }

        public int LinesCleared { get; }

        public int ScoreEarned { get; }
    }

    internal struct BlockPuzzleMoveResult
    {
        public BlockPuzzleMoveResult(bool success, int scoreEarned, int linesCleared, bool trayRefreshed, bool gameOver)
        {
            Success = success;
            ScoreEarned = scoreEarned;
            LinesCleared = linesCleared;
            TrayRefreshed = trayRefreshed;
            GameOver = gameOver;
        }

        public bool Success { get; }

        public int ScoreEarned { get; }

        public int LinesCleared { get; }

        public bool TrayRefreshed { get; }

        public bool GameOver { get; }
    }

    internal sealed class BlockPuzzleBoard
    {
        public const int Size = 10;

        private readonly int[,] cells = new int[Size, Size];

        public int GetCellValue(int x, int y)
        {
            return IsInside(x, y) ? cells[x, y] : 0;
        }

        public bool IsOccupied(int x, int y)
        {
            return GetCellValue(x, y) > 0;
        }

        public void Clear()
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    cells[x, y] = 0;
                }
            }
        }

        public bool CanPlace(BlockPuzzlePiece piece, int anchorX, int anchorY)
        {
            if (piece == null)
            {
                return false;
            }

            for (var i = 0; i < piece.CellCount; i++)
            {
                var cell = piece.GetCell(i);
                var x = anchorX + cell.X;
                var y = anchorY + cell.Y;
                if (!IsInside(x, y) || cells[x, y] > 0)
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanAnyPieceFit(IEnumerable<BlockPuzzlePiece> pieces)
        {
            if (pieces == null)
            {
                return false;
            }

            foreach (var piece in pieces)
            {
                if (CanPieceFit(piece))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanPieceFit(BlockPuzzlePiece piece)
        {
            if (piece == null)
            {
                return false;
            }

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (CanPlace(piece, x, y))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public BlockPuzzlePlacementResult PlacePiece(BlockPuzzlePiece piece, int anchorX, int anchorY)
        {
            if (!CanPlace(piece, anchorX, anchorY))
            {
                return new BlockPuzzlePlacementResult(false, 0, 0, 0);
            }

            var colorIndex = Mathf.Max(1, piece.ColorIndex);
            for (var i = 0; i < piece.CellCount; i++)
            {
                var cell = piece.GetCell(i);
                cells[anchorX + cell.X, anchorY + cell.Y] = colorIndex;
            }

            var linesCleared = ClearCompletedLines();
            var score = piece.CellCount + (linesCleared * 100) + (Mathf.Max(0, linesCleared - 1) * 50);
            return new BlockPuzzlePlacementResult(true, piece.CellCount, linesCleared, score);
        }

        public int CountOccupiedCells()
        {
            var count = 0;
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (cells[x, y] > 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        internal void SetCell(int x, int y, int colorIndex)
        {
            if (!IsInside(x, y))
            {
                throw new ArgumentOutOfRangeException("Cell is outside the board.");
            }

            cells[x, y] = Mathf.Max(0, colorIndex);
        }

        private int ClearCompletedLines()
        {
            var fullRows = new List<int>();
            var fullColumns = new List<int>();

            for (var y = 0; y < Size; y++)
            {
                var rowFull = true;
                for (var x = 0; x < Size; x++)
                {
                    if (cells[x, y] == 0)
                    {
                        rowFull = false;
                        break;
                    }
                }

                if (rowFull)
                {
                    fullRows.Add(y);
                }
            }

            for (var x = 0; x < Size; x++)
            {
                var columnFull = true;
                for (var y = 0; y < Size; y++)
                {
                    if (cells[x, y] == 0)
                    {
                        columnFull = false;
                        break;
                    }
                }

                if (columnFull)
                {
                    fullColumns.Add(x);
                }
            }

            for (var i = 0; i < fullRows.Count; i++)
            {
                var y = fullRows[i];
                for (var x = 0; x < Size; x++)
                {
                    cells[x, y] = 0;
                }
            }

            for (var i = 0; i < fullColumns.Count; i++)
            {
                var x = fullColumns[i];
                for (var y = 0; y < Size; y++)
                {
                    cells[x, y] = 0;
                }
            }

            return fullRows.Count + fullColumns.Count;
        }

        private static bool IsInside(int x, int y)
        {
            return x >= 0 && x < Size && y >= 0 && y < Size;
        }
    }

    internal sealed class BlockPuzzleGameState
    {
        public const int TraySlotCount = 3;

        private readonly Func<BlockPuzzlePiece> nextPieceFactory;
        private readonly BlockPuzzlePiece[] trayPieces = new BlockPuzzlePiece[TraySlotCount];

        public BlockPuzzleGameState()
            : this(CreateDefaultPieceFactory())
        {
        }

        public BlockPuzzleGameState(Func<BlockPuzzlePiece> nextPieceFactory)
        {
            if (nextPieceFactory == null)
            {
                throw new ArgumentNullException(nameof(nextPieceFactory));
            }

            this.nextPieceFactory = nextPieceFactory;
            Board = new BlockPuzzleBoard();
        }

        public BlockPuzzleBoard Board { get; }

        public int Score { get; private set; }

        public bool IsGameOver { get; private set; }

        public void Reset()
        {
            Board.Clear();
            Score = 0;
            for (var i = 0; i < trayPieces.Length; i++)
            {
                trayPieces[i] = CreateNextPiece();
            }

            UpdateGameOverStatus();
        }

        public BlockPuzzlePiece GetTrayPiece(int index)
        {
            return IsValidTrayIndex(index) ? trayPieces[index] : null;
        }

        public void SetTrayPieces(params BlockPuzzlePiece[] pieces)
        {
            for (var i = 0; i < trayPieces.Length; i++)
            {
                trayPieces[i] = pieces != null && i < pieces.Length ? pieces[i] : null;
            }

            UpdateGameOverStatus();
        }

        public BlockPuzzleMoveResult TryPlaceTrayPiece(int trayIndex, int anchorX, int anchorY)
        {
            if (!IsValidTrayIndex(trayIndex) || trayPieces[trayIndex] == null || IsGameOver)
            {
                return new BlockPuzzleMoveResult(false, 0, 0, false, IsGameOver);
            }

            var placement = Board.PlacePiece(trayPieces[trayIndex], anchorX, anchorY);
            if (!placement.Success)
            {
                UpdateGameOverStatus();
                return new BlockPuzzleMoveResult(false, 0, 0, false, IsGameOver);
            }

            Score += placement.ScoreEarned;
            trayPieces[trayIndex] = null;

            var trayRefreshed = false;
            if (AreAllTraySlotsEmpty())
            {
                for (var i = 0; i < trayPieces.Length; i++)
                {
                    trayPieces[i] = CreateNextPiece();
                }

                trayRefreshed = true;
            }

            UpdateGameOverStatus();
            return new BlockPuzzleMoveResult(true, placement.ScoreEarned, placement.LinesCleared, trayRefreshed, IsGameOver);
        }

        private static Func<BlockPuzzlePiece> CreateDefaultPieceFactory()
        {
            var random = new System.Random(Environment.TickCount);
            return delegate { return BlockPuzzlePieceLibrary.CreateRandom(random); };
        }

        private BlockPuzzlePiece CreateNextPiece()
        {
            var piece = nextPieceFactory();
            if (piece == null)
            {
                throw new InvalidOperationException("BlockPuzzle piece factory returned null.");
            }

            return piece;
        }

        private bool AreAllTraySlotsEmpty()
        {
            for (var i = 0; i < trayPieces.Length; i++)
            {
                if (trayPieces[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateGameOverStatus()
        {
            var hasPiece = false;
            for (var i = 0; i < trayPieces.Length; i++)
            {
                if (trayPieces[i] != null)
                {
                    hasPiece = true;
                    break;
                }
            }

            IsGameOver = hasPiece && !Board.CanAnyPieceFit(trayPieces);
        }

        private static bool IsValidTrayIndex(int index)
        {
            return index >= 0 && index < TraySlotCount;
        }
    }
}
