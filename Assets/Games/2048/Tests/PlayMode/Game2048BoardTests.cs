using System.Collections.Generic;
using NUnit.Framework;
using HuanYouYu.Game2048;

namespace HuanYouYu.Tests
{
    public sealed class Game2048BoardTests
    {
        [Test]
        public void Reset_SpawnsExactlyTwoTiles()
        {
            var board = new Game2048Board(4, new SequenceRandom(0, 1, 14, 1));

            board.Reset();

            var cells = board.Snapshot();
            Assert.That(CountNonZero(cells), Is.EqualTo(2));
            Assert.That(cells[0], Is.EqualTo(2));
            Assert.That(cells[15], Is.EqualTo(2));
            Assert.That(board.Score, Is.EqualTo(0));
            Assert.That(board.State, Is.EqualTo(Game2048GameState.Playing));
        }

        [Test]
        public void TryMove_LeftMergesTilesAndAddsScore()
        {
            var board = new Game2048Board(4, new SequenceRandom(0, 1));
            board.SetBoard(new[]
            {
                2, 0, 2, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            });

            var result = board.TryMove(Game2048MoveDirection.Left);

            Assert.That(result.BoardChanged, Is.True);
            Assert.That(result.ScoreGained, Is.EqualTo(4));
            Assert.That(result.TileMotions, Has.Length.EqualTo(2));
            Assert.That(result.TileMotions[0].FromRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[0].FromColumn, Is.EqualTo(0));
            Assert.That(result.TileMotions[0].ToRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[0].ToColumn, Is.EqualTo(0));
            Assert.That(result.TileMotions[0].Merged, Is.True);
            Assert.That(result.TileMotions[1].FromRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[1].FromColumn, Is.EqualTo(2));
            Assert.That(result.TileMotions[1].ToRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[1].ToColumn, Is.EqualTo(0));
            Assert.That(result.TileMotions[1].Merged, Is.True);
            Assert.That(board.Score, Is.EqualTo(4));
            Assert.That(board.Snapshot(), Is.EqualTo(new[]
            {
                4, 2, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            }));
        }

        [Test]
        public void TryMove_LeftDoesNotMergeSameTileTwice()
        {
            var board = new Game2048Board(4, new SequenceRandom(13, 1));
            board.SetBoard(new[]
            {
                2, 2, 2, 2,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            });

            var result = board.TryMove(Game2048MoveDirection.Left);

            Assert.That(result.BoardChanged, Is.True);
            Assert.That(result.ScoreGained, Is.EqualTo(8));
            Assert.That(result.TileMotions, Has.Length.EqualTo(4));
            Assert.That(result.TileMotions[3].FromRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[3].FromColumn, Is.EqualTo(3));
            Assert.That(result.TileMotions[3].ToRow, Is.EqualTo(0));
            Assert.That(result.TileMotions[3].ToColumn, Is.EqualTo(1));
            Assert.That(result.TileMotions[3].Merged, Is.True);
            Assert.That(board.Snapshot(), Is.EqualTo(new[]
            {
                4, 4, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 2
            }));
        }

        [Test]
        public void TryMove_InvalidMoveDoesNotSpawnNewTile()
        {
            var board = new Game2048Board(4, new SequenceRandom(0, 1));
            board.SetBoard(new[]
            {
                2, 4, 8, 16,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            });

            var before = board.Snapshot();
            var result = board.TryMove(Game2048MoveDirection.Left);

            Assert.That(result.BoardChanged, Is.False);
            Assert.That(result.ScoreGained, Is.EqualTo(0));
            Assert.That(result.TileMotions, Is.Empty);
            Assert.That(board.Score, Is.EqualTo(0));
            Assert.That(board.Snapshot(), Is.EqualTo(before));
        }

        [Test]
        public void TryMove_Reaching2048MarksWin()
        {
            var board = new Game2048Board(4, new SequenceRandom(13, 1));
            board.SetBoard(new[]
            {
                1024, 1024, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            });

            var result = board.TryMove(Game2048MoveDirection.Left);

            Assert.That(result.BoardChanged, Is.True);
            Assert.That(result.ReachedGoal, Is.True);
            Assert.That(result.State, Is.EqualTo(Game2048GameState.Won));
            Assert.That(board.GetCell(0, 0), Is.EqualTo(2048));
        }

        [Test]
        public void TryMove_LastValidMoveCanEndInLoss()
        {
            var board = new Game2048Board(4, new SequenceRandom(0, 1));
            board.SetBoard(new[]
            {
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 2,
                4, 2, 4, 0
            });

            var result = board.TryMove(Game2048MoveDirection.Right);

            Assert.That(result.BoardChanged, Is.True);
            Assert.That(result.State, Is.EqualTo(Game2048GameState.Lost));
            Assert.That(board.Snapshot(), Is.EqualTo(new[]
            {
                4, 2, 4, 2,
                2, 4, 2, 4,
                4, 2, 4, 2,
                2, 4, 2, 4
            }));
        }

        private static int CountNonZero(IReadOnlyList<int> values)
        {
            var count = 0;
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index] != 0)
                {
                    count += 1;
                }
            }

            return count;
        }

        private sealed class SequenceRandom : IGame2048Random
        {
            private readonly Queue<int> values;

            public SequenceRandom(params int[] sequence)
            {
                values = new Queue<int>(sequence ?? new int[0]);
            }

            public int Next(int maxExclusive)
            {
                Assert.That(maxExclusive, Is.GreaterThan(0));
                if (values.Count == 0)
                {
                    return 0;
                }

                var next = values.Dequeue();
                if (next < 0)
                {
                    next = -next;
                }

                return next % maxExclusive;
            }
        }
    }
}
