using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public class SlidingPuzzleGameViewTests
    {
        [UnityTest]
        public IEnumerator GameBootsWithSolvableFourByFourBoard()
        {
            yield return LoadGameScene();

            var view = FindActiveSlidingPuzzleView();
            var boardRoot = GameObject.Find("SlidingPuzzleBoard");
            Assert.IsNotNull(boardRoot, "SlidingPuzzleBoard should be created.");

            var buttons = GetTileButtons(view);
            var activeTiles = 0;
            foreach (var button in buttons)
            {
                if (button.gameObject.activeSelf)
                {
                    activeTiles++;
                }
            }

            Assert.AreEqual(15, activeTiles, "A 4x4 puzzle should show 15 numbered tiles.");
            Assert.AreEqual(16, boardRoot.transform.Find("BoardGrid").childCount, "Board should contain 16 fixed cells.");

            var board = GetBoard(view);
            Assert.AreEqual(16, board.Length);
            Assert.AreEqual(1, CountValue(board, 0), "Board should contain exactly one empty cell.");
            Assert.IsFalse(IsSolved(view), "Initial board should not start solved.");
            Assert.IsTrue(IsSolvable(board), "Generated board should be solvable.");
        }

        [UnityTest]
        public IEnumerator AdjacentClickMovesTileAndNonAdjacentClickDoesNot()
        {
            yield return LoadGameScene();

            var view = FindActiveSlidingPuzzleView();
            ApplyBoardState(view, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 0, 14, 15 }, 0);
            yield return null;

            var buttons = GetTileButtons(view);
            Click(buttons[14]);
            yield return null;

            Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 15 }, GetBoard(view));
            Assert.AreEqual(1, GetMoves(view), "Moving an adjacent tile should increment moves.");

            Click(buttons[0]);
            yield return null;

            Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 15 }, GetBoard(view));
            Assert.AreEqual(1, GetMoves(view), "Clicking a non-adjacent tile should not move.");
        }

        [UnityTest]
        public IEnumerator CompletingBoardShowsSettlementAndRetryRestarts()
        {
            yield return LoadGameScene();

            var view = FindActiveSlidingPuzzleView();
            ApplyBoardState(view, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 15 }, 20);
            yield return null;

            var buttons = GetTileButtons(view);
            Click(buttons[15]);
            yield return null;

            Assert.IsTrue(IsSolved(view), "Final legal move should solve the board.");
            var panel = GameObject.Find("SlidingPuzzleSettlementPanel");
            Assert.IsNotNull(panel, "Solving the puzzle should show settlement panel.");

            for (var i = 0; i < 40; i++)
            {
                yield return null;
            }

            var retryButton = panel.transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(retryButton, "Settlement should expose retry as the primary action.");
            Click(retryButton);
            yield return null;

            Assert.IsNull(GameObject.Find("SlidingPuzzleSettlementPanel"), "Retry should close settlement panel.");
            Assert.IsFalse(IsSolved(view), "Retry should generate a new unsolved board.");
            Assert.AreEqual(0, GetMoves(view), "Retry should reset moves.");
        }

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            var controller = Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            controller.EnterGame(MiniGameSlidingPuzzleGameView.GameIdConstant);

            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static MiniGameSlidingPuzzleGameView FindActiveSlidingPuzzleView()
        {
            var controller = Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");

            var field = typeof(MiniGameAppController).GetField("activeGame", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "activeGame field should exist.");

            var view = field.GetValue(controller) as MiniGameSlidingPuzzleGameView;
            Assert.IsNotNull(view, "Active game should be SlidingPuzzle.");
            return view;
        }

        private static int[] GetBoard(MiniGameSlidingPuzzleGameView view)
        {
            var field = typeof(MiniGameSlidingPuzzleGameView).GetField("board", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "board field should exist.");

            var board = (int[])field.GetValue(view);
            return (int[])board.Clone();
        }

        private static Button[] GetTileButtons(MiniGameSlidingPuzzleGameView view)
        {
            var field = typeof(MiniGameSlidingPuzzleGameView).GetField("tileButtons", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "tileButtons field should exist.");
            return (Button[])field.GetValue(view);
        }

        private static int GetMoves(MiniGameSlidingPuzzleGameView view)
        {
            var field = typeof(MiniGameSlidingPuzzleGameView).GetField("moves", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "moves field should exist.");
            return (int)field.GetValue(view);
        }

        private static bool IsSolved(MiniGameSlidingPuzzleGameView view)
        {
            var method = typeof(MiniGameSlidingPuzzleGameView).GetMethod("IsSolved", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "IsSolved method should exist.");
            return (bool)method.Invoke(view, null);
        }

        private static bool IsSolvable(int[] board)
        {
            var method = typeof(MiniGameSlidingPuzzleGameView).GetMethod("IsSolvable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "IsSolvable method should exist.");
            return (bool)method.Invoke(null, new object[] { board });
        }

        private static void ApplyBoardState(MiniGameSlidingPuzzleGameView view, int[] board, int moves)
        {
            var method = typeof(MiniGameSlidingPuzzleGameView).GetMethod("ApplyBoardState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "ApplyBoardState method should exist.");
            method.Invoke(view, new object[] { board, moves });
        }

        private static int CountValue(int[] board, int value)
        {
            var count = 0;
            foreach (var item in board)
            {
                if (item == value)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "Button should exist.");
            ExecuteEvents.Execute(
                button.gameObject,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
        }
    }
}
