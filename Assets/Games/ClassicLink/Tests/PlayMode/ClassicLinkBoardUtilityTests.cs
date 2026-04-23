using System.Collections.Generic;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class ClassicLinkBoardUtilityTests
    {
        [Test]
        public void FillBoardWithRandomPairs_CreatesMultipleAvailablePairs()
        {
            Random.InitState(12345);

            var board = new int[12, 10];
            ClassicLinkBoardUtility.FillBoardWithRandomPairs(board, 10, 8, 20, 4, 4);

            Assert.GreaterOrEqual(ClassicLinkBoardUtility.CountAvailablePairs(board, 10, 8), 4);
        }

        [Test]
        public void TryFindPath_CanUseOuterBorderWithinTwoTurns()
        {
            var board = new int[6, 6];
            board[2, 2] = 1;
            board[2, 4] = 1;
            board[2, 3] = 9;

            List<Vector2Int> path;
            var found = ClassicLinkBoardUtility.TryFindPath(
                board,
                4,
                4,
                new Vector2Int(2, 2),
                new Vector2Int(4, 2),
                out path);

            Assert.IsTrue(found, "A path through the outer border should be valid within two turns.");
            Assert.GreaterOrEqual(path.Count, 3);
        }

        [Test]
        public void ReshuffleRemainingTiles_ProducesConfiguredMinimumPairs()
        {
            Random.InitState(6789);

            var board = new int[12, 10];
            ClassicLinkBoardUtility.FillBoardWithRandomPairs(board, 10, 8, 20, 4, 1);

            board[1, 1] = 0;
            board[1, 2] = 0;

            ClassicLinkBoardUtility.ReshuffleRemainingTiles(board, 10, 8, 4);

            Assert.GreaterOrEqual(ClassicLinkBoardUtility.CountAvailablePairs(board, 10, 8), 4);
        }
    }
}
