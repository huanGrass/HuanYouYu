using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace Tests
{
    public class GomokuViewSmokeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator GomokuViewCanBootWithoutErrors()
        {
            PlayModeGlobalLogMonitor.Clear();

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            var hostObject = new GameObject("GomokuViewHost");
            var host = hostObject.AddComponent<TestHostBehaviour>();
            hostObject.AddComponent<HuanYouYu.MiniGameHall.MiniGameSfxPlayer>();
            hostObject.AddComponent<AudioListener>();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(hostObject.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            HuanYouYu.MiniGameHall.GameGomokuView view = null;
            try
            {
                view = new HuanYouYu.MiniGameHall.GameGomokuView(
                    host,
                    canvas.transform,
                    delegate(HuanYouYu.MiniGameHall.MiniGameSettlement _) { },
                    delegate { });
                yield return null;

                Assert.IsNotNull(GameObject.Find("GomokuTop"));
                Assert.IsNotNull(GameObject.Find("GomokuContent"));
                Assert.IsNotNull(GameObject.Find("GomokuBottom"));
                Assert.IsNotNull(GameObject.Find("Cell_7_7"));
                Assert.IsNotNull(GameObject.Find("HorizontalLine_7"));
                Assert.IsNotNull(GameObject.Find("VerticalLine_7"));
                Assert.IsNotNull(GameObject.Find("StarPoint_7_7"));

                var report = PlayModeGlobalLogMonitor.BuildFailureReport();
                Assert.IsTrue(string.IsNullOrEmpty(report), report);
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [UnityTest]
        public IEnumerator PlayerMoveRequiresPreviewAndSecondClick()
        {
            PlayModeGlobalLogMonitor.Clear();
            var hostObject = new GameObject("GomokuPreviewHost");
            var host = hostObject.AddComponent<TestHostBehaviour>();
            hostObject.AddComponent<MiniGameSfxPlayer>();
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(hostObject.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            GameGomokuView view = null;
            try
            {
                view = new GameGomokuView(host, canvasObject.transform, delegate(MiniGameSettlement _) { }, delegate { });
                yield return null;

                var runtimeType = view.GetType();
                var boardField = runtimeType.GetField("boardState", InstancePrivate);
                var playerStoneField = runtimeType.GetField("playerStone", InstancePrivate);
                var aiStoneField = runtimeType.GetField("aiStone", InstancePrivate);
                Assert.IsNotNull(boardField);
                Assert.IsNotNull(playerStoneField);
                Assert.IsNotNull(aiStoneField);

                var board = new GomokuBoardState(15);
                board.Reset();
                boardField.SetValue(view, board);
                playerStoneField.SetValue(view, GomokuStone.Black);
                aiStoneField.SetValue(view, GomokuStone.White);
                InvokePrivate(view, "RefreshBoardUi");

                var cellButton = GameObject.Find("Cell_7_7").GetComponent<Button>();
                var preview = GameObject.Find("Preview_7_7").GetComponent<GomokuCircleGraphic>();
                cellButton.onClick.Invoke();
                Assert.AreEqual(GomokuStone.None, board.GetStone(7, 7));
                Assert.IsTrue(preview.enabled, "First click should only show the dashed preview.");

                cellButton.onClick.Invoke();
                Assert.AreEqual(GomokuStone.Black, board.GetStone(7, 7));
                Assert.IsFalse(preview.enabled, "Preview should disappear after confirming the move.");
                Assert.IsTrue(HasVisibleLastMoveMarker(), "Confirmed moves should leave a visible last-move marker.");
                Assert.AreEqual(0, CountStones(board, GomokuStone.White), "AI should not move in the same frame.");

                yield return new WaitForSeconds(1.1f);
                Assert.AreEqual(1, CountStones(board, GomokuStone.White), "AI should move after the one-second delay.");
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [UnityTest]
        public IEnumerator GomokuWinShowsSettlementAndAwardsRewards()
        {
            PlayModeGlobalLogMonitor.Clear();
            ResetProgress();
            yield return LoadGameScene();

            var controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller, "Missing MiniGameAppController.");

            var runtime = GetActiveGame(controller);
            ForceRoundState(runtime, "Black", "White", "BlackWin");

            InvokePrivate(runtime, "EndRound");
            yield return null;

            var popup = GameObject.Find("GomokuSettlementPanel");
            Assert.IsNotNull(popup, "Winning the round should show a settlement popup.");

            var backHallButton = popup.transform.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button was not found.");
            backHallButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(GameGomokuView.GameIdConstant);
            Assert.AreEqual(60, progress.TotalCoinCount);
            Assert.AreEqual(1, progress.TotalChestCount);
        }

        [UnityTest]
        public IEnumerator WinningLineAnimatesBeforeSettlementAppears()
        {
            PlayModeGlobalLogMonitor.Clear();
            ResetProgress();
            yield return LoadGameScene();

            var controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
            var runtime = GetActiveGame(controller);
            var runtimeType = runtime.GetType();
            var boardField = runtimeType.GetField("boardState", InstancePrivate);
            var lastMoveRowField = runtimeType.GetField("lastMoveRow", InstancePrivate);
            var lastMoveColumnField = runtimeType.GetField("lastMoveColumn", InstancePrivate);
            Assert.IsNotNull(boardField);
            Assert.IsNotNull(lastMoveRowField);
            Assert.IsNotNull(lastMoveColumnField);

            var board = new GomokuBoardState(15);
            board.Reset();
            GomokuRoundState roundState;
            for (var offset = 0; offset < 4; offset++)
            {
                Assert.IsTrue(board.TryPlaceStone(7, 4 + offset, GomokuStone.Black, out roundState));
                Assert.IsTrue(board.TryPlaceStone(8, 4 + offset, GomokuStone.White, out roundState));
            }

            Assert.IsTrue(board.TryPlaceStone(7, 8, GomokuStone.Black, out roundState));
            boardField.SetValue(runtime, board);
            lastMoveRowField.SetValue(runtime, 7);
            lastMoveColumnField.SetValue(runtime, 8);
            ForceRoundState(runtime, "Black", "White", "BlackWin");

            InvokePrivate(runtime, "EndRound");
            yield return null;
            Assert.IsNull(GameObject.Find("GomokuSettlementPanel"), "Settlement should wait for the winning animation.");

            var animatedStone = GameObject.Find("Cell_7_4").transform.Find("Stone").GetComponent<GomokuCircleGraphic>();
            Assert.AreNotEqual(Color.black, animatedStone.color, "The winning line should be highlighted during animation.");

            yield return new WaitForSeconds(1.1f);
            Assert.IsNull(GameObject.Find("GomokuSettlementPanel"), "Settlement should remain hidden during the one-second post-animation pause.");

            yield return new WaitForSeconds(1f);
            Assert.IsNotNull(GameObject.Find("GomokuSettlementPanel"), "Settlement should appear after the winning animation.");
        }

        [UnityTest]
        public IEnumerator ExitingRoundAwardsExitCoinsOnly()
        {
            PlayModeGlobalLogMonitor.Clear();
            ResetProgress();
            yield return LoadGameScene();

            var controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller, "Missing MiniGameAppController.");

            var runtime = GetActiveGame(controller);
            InvokePrivate(runtime, "ConfirmExitToHall");
            yield return null;

            var popup = GameObject.Find("GomokuSettlementPanel");
            Assert.IsNotNull(popup, "Exiting should show a settlement popup.");

            var duplicateBackHallButton = popup.transform.Find("Dialog/BackHallButton")?.gameObject;
            Assert.IsNotNull(duplicateBackHallButton, "Settlement secondary back hall button should exist.");
            Assert.IsFalse(duplicateBackHallButton.activeSelf, "Exit settlement should not show a duplicate back hall button.");

            var confirmButton = popup.transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Settlement confirm button was not found.");
            confirmButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(GameGomokuView.GameIdConstant);
            Assert.AreEqual(10, progress.TotalCoinCount);
            Assert.AreEqual(0, progress.TotalChestCount);
        }

        private static IEnumerator LoadGameScene()
        {
            var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 60; i++)
            {
                controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(controller, "MiniGameAppController was not created.");
            controller.EnterGame(GameGomokuView.GameIdConstant);
            yield return null;
        }

        private static object GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller);
            Assert.IsNotNull(runtime, "Gomoku runtime was not created.");
            return runtime;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Missing method: " + methodName);
            method.Invoke(target, null);
        }

        private static void ForceRoundState(object runtime, string playerStoneName, string aiStoneName, string roundStateName)
        {
            var runtimeType = runtime.GetType();
            var playerStoneField = runtimeType.GetField("playerStone", InstancePrivate);
            var aiStoneField = runtimeType.GetField("aiStone", InstancePrivate);
            var roundStateField = runtimeType.GetField("roundState", InstancePrivate);
            Assert.IsNotNull(playerStoneField, "Missing playerStone field.");
            Assert.IsNotNull(aiStoneField, "Missing aiStone field.");
            Assert.IsNotNull(roundStateField, "Missing roundState field.");

            playerStoneField.SetValue(runtime, Enum.Parse(playerStoneField.FieldType, playerStoneName));
            aiStoneField.SetValue(runtime, Enum.Parse(aiStoneField.FieldType, aiStoneName));
            roundStateField.SetValue(runtime, Enum.Parse(roundStateField.FieldType, roundStateName));
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static bool HasVisibleLastMoveMarker()
        {
            var markers = UnityEngine.Object.FindObjectsOfType<GomokuCircleGraphic>();
            foreach (var marker in markers)
            {
                if (marker.gameObject.name.StartsWith("LastMoveMarker_", StringComparison.Ordinal) && marker.enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountStones(GomokuBoardState board, GomokuStone stone)
        {
            var count = 0;
            for (var row = 0; row < board.Size; row++)
            {
                for (var column = 0; column < board.Size; column++)
                {
                    if (board.GetStone(row, column) == stone)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
