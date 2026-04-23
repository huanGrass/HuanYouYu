using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class Match3BoardUtilityTests
    {
        [Test]
        public void AnimationConfigAsset_IsAvailableForInspectorTuning()
        {
            var config = Resources.Load<Match3AnimationConfig>(Match3AnimationConfig.ResourcePath);

            Assert.IsNotNull(config);
            Assert.Greater(config.DurationScale, 0f);
        }

        [Test]
        public void FillBoard_CreatesStablePlayableBoard()
        {
            Random.InitState(13579);

            var board = new int[7, 7];
            Match3BoardUtility.FillBoard(board, 7, 7, 6);

            Assert.IsTrue(Match3BoardUtility.IsBoardFilled(board, 7, 7));
            Assert.IsFalse(Match3BoardUtility.HasAnyMatch(board, 7, 7));
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(board, 7, 7, out _, out _));
        }

        [Test]
        public void TrySwapAndResolve_InvalidSwapRevertsBoard()
        {
            var board =
                new[,]
                {
                    { 1, 1, 2, 4, 5 },
                    { 3, 4, 1, 5, 2 },
                    { 2, 5, 4, 2, 3 },
                    { 4, 2, 5, 3, 1 },
                    { 5, 3, 2, 1, 4 }
                };

            var snapshot = CloneBoard(board);
            var result = new Match3ResolveResult();
            var success = Match3BoardUtility.TrySwapAndResolve(
                board,
                5,
                5,
                5,
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                result);

            Assert.IsFalse(success);
            AssertBoardEquals(snapshot, board);
            Assert.AreEqual(0, result.ClearedCount);
        }

        [Test]
        public void TrySwapAndResolve_ValidSwapClearsAndLeavesStableBoard()
        {
            Random.InitState(24680);

            var board =
                new[,]
                {
                    { 1, 1, 2, 4, 5 },
                    { 3, 4, 1, 5, 2 },
                    { 2, 5, 4, 2, 3 },
                    { 4, 2, 5, 3, 1 },
                    { 5, 3, 2, 1, 4 }
                };

            var result = new Match3ResolveResult();
            var success = Match3BoardUtility.TrySwapAndResolve(
                board,
                5,
                5,
                5,
                new Vector2Int(2, 0),
                new Vector2Int(2, 1),
                result);

            Assert.IsTrue(success);
            Assert.GreaterOrEqual(result.ClearedCount, 3);
            Assert.GreaterOrEqual(result.CascadeCount, 1);
            Assert.IsTrue(Match3BoardUtility.IsBoardFilled(board, 5, 5));
            Assert.IsFalse(Match3BoardUtility.HasAnyMatch(board, 5, 5));
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(board, 5, 5, out _, out _));
        }

        private static int[,] CloneBoard(int[,] source)
        {
            var clone = new int[source.GetLength(0), source.GetLength(1)];
            for (var row = 0; row < source.GetLength(0); row++)
            {
                for (var column = 0; column < source.GetLength(1); column++)
                {
                    clone[row, column] = source[row, column];
                }
            }

            return clone;
        }

        private static void AssertBoardEquals(int[,] expected, int[,] actual)
        {
            Assert.AreEqual(expected.GetLength(0), actual.GetLength(0));
            Assert.AreEqual(expected.GetLength(1), actual.GetLength(1));

            for (var row = 0; row < expected.GetLength(0); row++)
            {
                for (var column = 0; column < expected.GetLength(1); column++)
                {
                    Assert.AreEqual(expected[row, column], actual[row, column], string.Format("Mismatch at ({0}, {1})", row, column));
                }
            }
        }
    }
}
