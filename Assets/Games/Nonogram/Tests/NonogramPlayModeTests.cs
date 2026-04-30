using HuanYouYu.MiniGameHall;
using HuanYouYu.Nonogram;
using NUnit.Framework;

namespace HuanYouYu.Tests
{
    public sealed class NonogramPlayModeTests
    {
        [Test]
        public void PuzzleBuildsExpectedHints()
        {
            var puzzle = new NonogramPuzzle(
                "Test",
                new[]
                {
                    "#.#",
                    "###",
                    "..#"
                });

            CollectionAssert.AreEqual(new[] { 1, 1 }, puzzle.RowHints[0]);
            CollectionAssert.AreEqual(new[] { 3 }, puzzle.ColumnHints[2]);
            CollectionAssert.AreEqual(new[] { 0 }, new NonogramPuzzle("EmptyRow", new[] { "..." }).RowHints[0]);
        }

        [Test]
        public void BoardRequiresFilledAndCrossedCellsToSolve()
        {
            var puzzle = new NonogramPuzzle(
                "Test",
                new[]
                {
                    "#.",
                    ".#"
                });
            var board = new NonogramBoardState(2, 2);

            board.Toggle(0, 0, NonogramInputMode.Fill);
            board.Toggle(1, 1, NonogramInputMode.Fill);
            Assert.False(board.IsSolved(puzzle));

            board.Toggle(0, 1, NonogramInputMode.Cross);
            board.Toggle(1, 0, NonogramInputMode.Cross);
            Assert.True(board.IsSolved(puzzle));
        }

        [Test]
        public void BoardAcceptsAnyBoardMatchingTheHints()
        {
            var puzzle = new NonogramPuzzle(
                "Test",
                new[]
                {
                    "#.",
                    ".#"
                });
            var board = new NonogramBoardState(2, 2);

            board.SetMark(0, 0, NonogramCellMark.Crossed);
            board.SetMark(0, 1, NonogramCellMark.Filled);
            board.SetMark(1, 0, NonogramCellMark.Filled);
            board.SetMark(1, 1, NonogramCellMark.Crossed);

            Assert.True(board.IsSolved(puzzle));
        }

        [Test]
        public void UiTextCatalogLoadsNonogramTexts()
        {
            Assert.AreEqual("数织", UiTextCatalog.Get("game.nonogram.name"));
            Assert.AreEqual("涂黑", UiTextCatalog.Get("nonogram.button.fill"));
            Assert.AreEqual("小船", UiTextCatalog.Get("nonogram.puzzle.boat"));
            Assert.AreEqual("苹果", UiTextCatalog.Get("nonogram.puzzle.apple"));
            Assert.AreEqual("笑脸", UiTextCatalog.Get("nonogram.puzzle.smile"));
            Assert.AreEqual("爱心", UiTextCatalog.Get("nonogram.puzzle.heart"));
            Assert.AreEqual("大笑脸", UiTextCatalog.Get("nonogram.puzzle.smile_big"));
            Assert.AreEqual("爱心 · 15x15", UiTextCatalog.Format("nonogram.progress", "爱心", 15, 15));
            Assert.AreEqual("取消", UiTextCatalog.Get("common.action.cancel"));
            Assert.AreEqual("确认重置", UiTextCatalog.Get("nonogram.confirm.reset.title"));
            Assert.AreEqual("确认换题", UiTextCatalog.Get("nonogram.confirm.next.title"));
        }

        [Test]
        public void AutoCrossSatisfiedLinesCleansRemainingUnknownCells()
        {
            var puzzle = new NonogramPuzzle("Test", new[] { "#.." });
            var board = new NonogramBoardState(3, 1);

            board.SetMark(0, 0, NonogramCellMark.Filled);

            Assert.True(board.AutoCrossSatisfiedLines(puzzle));
            Assert.AreEqual(NonogramCellMark.Filled, board.GetMark(0, 0));
            Assert.AreEqual(NonogramCellMark.Crossed, board.GetMark(0, 1));
            Assert.AreEqual(NonogramCellMark.Crossed, board.GetMark(0, 2));
        }

        [Test]
        public void BoardReportsCompletedHintNumbersPerRun()
        {
            var puzzle = new NonogramPuzzle("Test", new[] { "###.#" });
            var board = new NonogramBoardState(5, 1);

            board.SetMark(0, 0, NonogramCellMark.Filled);
            board.SetMark(0, 1, NonogramCellMark.Filled);
            board.SetMark(0, 2, NonogramCellMark.Filled);
            board.SetMark(0, 3, NonogramCellMark.Crossed);

            CollectionAssert.AreEqual(new[] { true, false }, board.GetRowHintCompletion(puzzle, 0));
        }

        [Test]
        public void PuzzleLibraryContainsMixedBoardSizes()
        {
            var hasFive = false;
            var hasTen = false;
            var hasFifteen = false;

            for (var i = 0; i < NonogramPuzzleLibrary.Count; i++)
            {
                var puzzle = NonogramPuzzleLibrary.GetByIndex(i);
                if (puzzle.Width == 5 && puzzle.Height == 5)
                {
                    hasFive = true;
                }
                else if (puzzle.Width == 10 && puzzle.Height == 10)
                {
                    hasTen = true;
                }
                else if (puzzle.Width == 15 && puzzle.Height == 15)
                {
                    hasFifteen = true;
                }
            }

            Assert.True(hasFive);
            Assert.True(hasTen);
            Assert.True(hasFifteen);
        }

        [Test]
        public void PuzzleLibraryContainsTwentyFiveLevels()
        {
            Assert.AreEqual(25, NonogramPuzzleLibrary.Count);
        }
    }
}
