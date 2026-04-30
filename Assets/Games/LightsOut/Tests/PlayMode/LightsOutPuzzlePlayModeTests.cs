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
    public class LightsOutPuzzlePlayModeTests
    {
        [Test]
        public void GeneratorCreatesSolvablePuzzlesForFixedSeeds()
        {
            for (var seed = 1; seed <= 40; seed++)
            {
                var questionNumber = (seed % 18) + 1;
                var puzzle = LightsOutPuzzleGenerator.Generate(questionNumber, new Random(seed * 97));
                Assert.IsNotNull(puzzle);
                Assert.AreEqual(LightsOutPuzzleGenerator.ResolveGridSize(questionNumber), puzzle.GridSize);
                Assert.GreaterOrEqual(puzzle.TargetAnswer, 1);
                Assert.LessOrEqual(puzzle.TargetAnswer, 20);
                Assert.AreNotEqual(
                    puzzle.TargetAnswer,
                    LightsOutPuzzleGenerator.SumLit(puzzle.Numbers, puzzle.InitialLights),
                    "Initial state should not already solve the puzzle.");

                var lights = LightsOutPuzzleGenerator.CopyLights(puzzle.InitialLights);
                for (var i = 0; i < puzzle.SolutionClickIndices.Length; i++)
                {
                    LightsOutPuzzleGenerator.ToggleCross(lights, puzzle.GridSize, puzzle.SolutionClickIndices[i]);
                    var currentSum = LightsOutPuzzleGenerator.SumLit(puzzle.Numbers, lights);
                    if (i < puzzle.SolutionClickIndices.Length - 1)
                    {
                        Assert.AreNotEqual(puzzle.TargetAnswer, currentSum, "Generated solution should not solve early.");
                    }
                    else
                    {
                        Assert.AreEqual(puzzle.TargetAnswer, currentSum, "Generated solution should solve the puzzle.");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator ApplyingGeneratedSolutionShowsSettlementAndContinueAdvancesQuestion()
        {
            var root = new GameObject("LightsOutTestRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(750f, 1334f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            var host = root.AddComponent<TestHost>();
            MiniGameLightsOutGameView view = null;
            try
            {
                view = new MiniGameLightsOutGameView(host, root.transform, null, null);
                yield return null;

                view.ApplyGeneratedSolutionForTests();

                Assert.Greater(view.ScoreForTests, 0);
                Assert.AreEqual(1, view.CompletedQuestionCountForTests);

                yield return new WaitForSeconds(1f);

                Assert.IsNotNull(GameObject.Find("LightsOutSettlementPanel"), "Completing a LightsOut puzzle should show settlement.");
                var continueButton = GameObject.Find("NextButton")?.GetComponent<Button>();
                Assert.IsNotNull(continueButton, "LightsOut settlement should provide a continue button.");
                continueButton.onClick.Invoke();
                yield return null;

                Assert.IsNotNull(view.CurrentPuzzleForTests);
                Assert.GreaterOrEqual(view.CurrentPuzzleForTests.GridSize, 3);
                Assert.LessOrEqual(view.CurrentPuzzleForTests.GridSize, 5);
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
        public IEnumerator HudShowsScoreOnly()
        {
            var root = new GameObject("LightsOutHudTestRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(750f, 1334f);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            var host = root.AddComponent<TestHost>();
            MiniGameLightsOutGameView view = null;
            try
            {
                view = new MiniGameLightsOutGameView(host, root.transform, null, null);
                yield return null;

                Assert.IsFalse(view.HudScoreTextForTests.Contains("题"), "LightsOut HUD should not show question count.");
                Assert.IsFalse(view.HudScoreTextForTests.Contains("*"), "LightsOut HUD should not show grid size.");
                Assert.AreEqual("分数 0", view.HudScoreTextForTests);
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

        private sealed class TestHost : MonoBehaviour
        {
        }
    }
}
