using System;

namespace HuanYouYu.MiniGameHall
{
    public sealed class SudokuPuzzle
    {
        public SudokuPuzzle(int[] givens, int[] solution)
        {
            if (givens == null || solution == null)
            {
                throw new ArgumentNullException(givens == null ? nameof(givens) : nameof(solution));
            }

            if (givens.Length != SudokuBoardState.CellCount || solution.Length != SudokuBoardState.CellCount)
            {
                throw new ArgumentException("Sudoku puzzle data must contain exactly 81 cells.");
            }

            Givens = new int[SudokuBoardState.CellCount];
            Solution = new int[SudokuBoardState.CellCount];
            Array.Copy(givens, Givens, SudokuBoardState.CellCount);
            Array.Copy(solution, Solution, SudokuBoardState.CellCount);
        }

        public int[] Givens { get; }

        public int[] Solution { get; }
    }
}
