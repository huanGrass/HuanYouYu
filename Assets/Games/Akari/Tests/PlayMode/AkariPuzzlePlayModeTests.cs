using System;
using System.Collections;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Random = System.Random;

namespace Tests
{
    public class AkariPuzzlePlayModeTests
    {
        [Test]
        public void GeneratorCreatesUniqueValidPuzzlesForFixedSeeds()
        {
            for (var question = 1; question <= 15; question++)
            {
                var puzzle = AkariPuzzleGenerator.Generate(question, new Random(1009 + (question * 37)));
                Assert.IsNotNull(puzzle);
                Assert.That(puzzle.GridSize, Is.InRange(AkariPuzzleGenerator.MinGridSize, AkariPuzzleGenerator.MaxGridSize));
                Assert.Greater(puzzle.ReferenceSteps, 0);

                bool[] solvedBulbs;
                var solutionCount = AkariPuzzleGenerator.CountSolutions(puzzle, 2, out solvedBulbs);
                Assert.AreEqual(1, solutionCount, "Generated Akari puzzle should have exactly one solution.");
                Assert.IsNotNull(solvedBulbs);

                var evaluation = AkariPuzzleGenerator.Evaluate(puzzle, solvedBulbs);
                Assert.IsTrue(evaluation.IsSolved, "Solver solution should satisfy Akari rules.");
                Assert.AreEqual(0, evaluation.UnlitWhiteCount);
            }
        }

        [Test]
        public void RandomGridSizeStaysWithinConfiguredRange()
        {
            var random = new Random(20260430);
            for (var i = 0; i < 100; i++)
            {
                var gridSize = AkariPuzzleGenerator.ResolveRandomGridSize(random);
                Assert.That(gridSize, Is.InRange(AkariPuzzleGenerator.MinGridSize, AkariPuzzleGenerator.MaxGridSize));
            }
        }

        [Test]
        public void RandomDifficultyStaysWithinConfiguredRange()
        {
            var random = new Random(20260430);
            for (var i = 0; i < 100; i++)
            {
                var difficulty = AkariPuzzleGenerator.ResolveRandomDifficulty(random);
                Assert.IsTrue(
                    difficulty == AkariDifficulty.Easy ||
                    difficulty == AkariDifficulty.Normal ||
                    difficulty == AkariDifficulty.Hard);
            }
        }

        [Test]
        public void GeneratorCreatesLargestConfiguredPuzzle()
        {
            var puzzle = AkariPuzzleGenerator.Generate(1, AkariPuzzleGenerator.MaxGridSize, AkariDifficulty.Hard, new Random(1414));
            Assert.IsNotNull(puzzle);
            Assert.AreEqual(AkariPuzzleGenerator.MaxGridSize, puzzle.GridSize);
            Assert.AreEqual(AkariDifficulty.Hard, puzzle.Difficulty);
            Assert.Greater(puzzle.ReferenceSteps, 0);

            bool[] solvedBulbs;
            var solutionCount = AkariPuzzleGenerator.CountSolutions(puzzle, 2, out solvedBulbs);
            Assert.AreEqual(1, solutionCount, "Generated largest Akari puzzle should have exactly one solution.");
        }

        [Test]
        public void EvaluationMarksBulbAndNumberConflicts()
        {
            var puzzle = CreateConflictPuzzle();
            var bulbs = new bool[puzzle.Cells.Length];
            bulbs[0] = true;
            bulbs[2] = true;
            bulbs[4] = true;

            var evaluation = AkariPuzzleGenerator.Evaluate(puzzle, bulbs);

            Assert.IsFalse(evaluation.IsSolved);
            Assert.IsTrue(evaluation.BulbConflicts[0]);
            Assert.IsTrue(evaluation.BulbConflicts[2]);
            Assert.IsTrue(evaluation.NumberConflicts[4]);
        }

        [UnityTest]
        public IEnumerator ApplyingGeneratedSolutionScoresAndShowsSettlement()
        {
            var root = new GameObject("AkariTestRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(750f, 1334f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            var host = root.AddComponent<TestHost>();
            MiniGameAkariGameView view = null;
            try
            {
                view = new MiniGameAkariGameView(host, root.transform, null, null);
                yield return null;

                var firstQuestionNumber = view.CurrentPuzzleForTests.QuestionNumber;
                view.ApplyGeneratedSolutionForTests();

                Assert.Greater(view.ScoreForTests, 0);
                Assert.AreEqual(1, view.CompletedPuzzleCountForTests);
                Assert.IsTrue(view.CurrentEvaluationForTests.IsSolved);

                yield return new WaitForSeconds(1f);

                Assert.IsNotNull(view.CurrentPuzzleForTests);
                Assert.AreEqual(firstQuestionNumber, view.CurrentPuzzleForTests.QuestionNumber);
                Assert.IsNotNull(GameObject.Find("AkariSettlementPanel"));
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedGenerationOptionsCreateRequestedPuzzle()
        {
            var root = new GameObject("AkariOptionsTestRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(750f, 1334f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            var host = root.AddComponent<TestHost>();
            MiniGameAkariGameView view = null;
            try
            {
                view = new MiniGameAkariGameView(host, root.transform, null, null);
                yield return null;

                Assert.IsTrue(view.HasGenerationDropdownsForTests);
                view.SelectGenerationOptionsForTests(5, AkariDifficulty.Hard);
                view.StartPuzzleForTests(1);

                Assert.AreEqual(5, view.CurrentPuzzleForTests.GridSize);
                Assert.AreEqual(AkariDifficulty.Hard, view.CurrentPuzzleForTests.Difficulty);
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator NumberedBlackCellKeepsBlackBackgroundAndColorsDigitByState()
        {
            var root = new GameObject("AkariColorTestRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(750f, 1334f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            var host = root.AddComponent<TestHost>();
            MiniGameAkariGameView view = null;
            try
            {
                view = new MiniGameAkariGameView(host, root.transform, null, null);
                yield return null;

                var puzzle = CreateConflictPuzzle();
                view.LoadPuzzleForTests(puzzle, new bool[puzzle.Cells.Length]);

                AssertColorApproximately(new Color(0.20f, 0.23f, 0.22f, 1f), view.GetCellBackgroundColorForTests(4));
                AssertColorApproximately(new Color(1f, 0.36f, 0.30f, 1f), view.GetCellLabelColorForTests(4));

                var satisfiedBulbs = new bool[puzzle.Cells.Length];
                satisfiedBulbs[1] = true;
                view.LoadPuzzleForTests(puzzle, satisfiedBulbs);

                AssertColorApproximately(new Color(0.20f, 0.23f, 0.22f, 1f), view.GetCellBackgroundColorForTests(4));
                AssertColorApproximately(new Color(0.58f, 0.92f, 0.45f, 1f), view.GetCellLabelColorForTests(4));
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        private static AkariPuzzle CreateConflictPuzzle()
        {
            return new AkariPuzzle
            {
                QuestionNumber = 1,
                GridSize = 3,
                Difficulty = AkariDifficulty.Normal,
                Cells = new[]
                {
                    AkariCellKind.White,
                    AkariCellKind.White,
                    AkariCellKind.White,
                    AkariCellKind.White,
                    AkariCellKind.NumberedBlack,
                    AkariCellKind.White,
                    AkariCellKind.White,
                    AkariCellKind.White,
                    AkariCellKind.White
                },
                Numbers = new[]
                {
                    -1, -1, -1,
                    -1, 1, -1,
                    -1, -1, -1
                },
                SolutionBulbs = new bool[9],
                ReferenceSteps = 2
            };
        }

        private static void AssertColorApproximately(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.001f);
            Assert.AreEqual(expected.g, actual.g, 0.001f);
            Assert.AreEqual(expected.b, actual.b, 0.001f);
            Assert.AreEqual(expected.a, actual.a, 0.001f);
        }

        private sealed class TestHost : MonoBehaviour
        {
        }
    }
}
