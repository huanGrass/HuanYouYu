using NUnit.Framework;

namespace Tests
{
    public class GomokuBoardTests
    {
        [Test]
        public void HorizontalFiveWins()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            PlaceAlternatingRow(board, 7, 3, out roundState);

            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.BlackWin, roundState);
        }

        [Test]
        public void VerticalFiveWins()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            for (var offset = 0; offset < 4; offset++)
            {
                Assert.IsTrue(board.TryPlaceStone(offset + 2, 8, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
                Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing, roundState);
                Assert.IsTrue(board.TryPlaceStone(offset + 2, 9, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            }

            Assert.IsTrue(board.TryPlaceStone(6, 8, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.BlackWin, roundState);
        }

        [Test]
        public void DiagonalFiveWins()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            for (var offset = 0; offset < 4; offset++)
            {
                Assert.IsTrue(board.TryPlaceStone(offset + 3, offset + 3, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
                Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing, roundState);
                Assert.IsTrue(board.TryPlaceStone(offset + 3, offset + 4, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            }

            Assert.IsTrue(board.TryPlaceStone(7, 7, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.BlackWin, roundState);
        }

        [Test]
        public void AntiDiagonalFiveWins()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            for (var offset = 0; offset < 4; offset++)
            {
                Assert.IsTrue(board.TryPlaceStone(offset + 3, 9 - offset, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
                Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing, roundState);
                Assert.IsTrue(board.TryPlaceStone(offset + 3, 10 - offset, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            }

            Assert.IsTrue(board.TryPlaceStone(7, 5, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.BlackWin, roundState);
        }

        [Test]
        public void OccupiedCellCannotBePlacedAgain()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            Assert.IsTrue(board.TryPlaceStone(7, 7, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsFalse(board.TryPlaceStone(7, 7, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuStone.White, board.CurrentTurn);
        }

        [Test]
        public void FullBoardWithoutFiveIsDraw()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(5);
            board.Reset();

            var moves = new[]
            {
                new Placement(0, 0, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(0, 2, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(0, 1, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(0, 3, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(0, 4, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(1, 0, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(1, 2, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(1, 1, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(1, 3, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(1, 4, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(2, 0, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(2, 2, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(2, 1, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(2, 3, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(2, 4, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(3, 0, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(3, 2, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(3, 1, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(3, 3, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(3, 4, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(4, 0, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(4, 1, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(4, 2, HuanYouYu.MiniGameHall.GomokuStone.Black),
                new Placement(4, 3, HuanYouYu.MiniGameHall.GomokuStone.White),
                new Placement(4, 4, HuanYouYu.MiniGameHall.GomokuStone.Black)
            };

            HuanYouYu.MiniGameHall.GomokuRoundState roundState = HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing;
            for (var index = 0; index < moves.Length; index++)
            {
                Assert.IsTrue(board.TryPlaceStone(moves[index].Row, moves[index].Column, moves[index].Stone, out roundState));
            }

            Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.Draw, roundState);
        }

        [Test]
        public void AiPrefersWinningMove()
        {
            var board = CreateAiDecisionBoard();
            var move = HuanYouYu.MiniGameHall.GomokuAi.ChooseMove(
                board,
                HuanYouYu.MiniGameHall.GomokuStone.White,
                HuanYouYu.MiniGameHall.GomokuStone.Black);

            Assert.AreEqual(7, move.Row);
            Assert.AreEqual(11, move.Column);
        }

        [Test]
        public void AiBlocksImmediateThreat()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            Assert.IsTrue(board.TryPlaceStone(7, 7, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(6, 6, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 8, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(6, 7, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 9, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 6, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 10, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));

            var move = HuanYouYu.MiniGameHall.GomokuAi.ChooseMove(
                board,
                HuanYouYu.MiniGameHall.GomokuStone.White,
                HuanYouYu.MiniGameHall.GomokuStone.Black);

            Assert.AreEqual(7, move.Row);
            Assert.AreEqual(11, move.Column);
        }

        private static void PlaceAlternatingRow(
            HuanYouYu.MiniGameHall.GomokuBoardState board,
            int row,
            int startColumn,
            out HuanYouYu.MiniGameHall.GomokuRoundState roundState)
        {
            roundState = HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing;
            for (var offset = 0; offset < 4; offset++)
            {
                Assert.IsTrue(board.TryPlaceStone(row, startColumn + offset, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
                Assert.AreEqual(HuanYouYu.MiniGameHall.GomokuRoundState.Ongoing, roundState);
                Assert.IsTrue(board.TryPlaceStone(row + 1, startColumn + offset, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            }

            Assert.IsTrue(board.TryPlaceStone(row, startColumn + 4, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
        }

        private static HuanYouYu.MiniGameHall.GomokuBoardState CreateAiDecisionBoard()
        {
            var board = new HuanYouYu.MiniGameHall.GomokuBoardState(15);
            board.Reset();

            HuanYouYu.MiniGameHall.GomokuRoundState roundState;
            Assert.IsTrue(board.TryPlaceStone(3, 3, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 7, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(3, 4, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 8, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(4, 4, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 9, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 6, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));
            Assert.IsTrue(board.TryPlaceStone(7, 10, HuanYouYu.MiniGameHall.GomokuStone.White, out roundState));
            Assert.IsTrue(board.TryPlaceStone(2, 2, HuanYouYu.MiniGameHall.GomokuStone.Black, out roundState));

            return board;
        }

        private readonly struct Placement
        {
            public Placement(int row, int column, HuanYouYu.MiniGameHall.GomokuStone stone)
            {
                Row = row;
                Column = column;
                Stone = stone;
            }

            public int Row { get; }

            public int Column { get; }

            public HuanYouYu.MiniGameHall.GomokuStone Stone { get; }
        }
    }
}
