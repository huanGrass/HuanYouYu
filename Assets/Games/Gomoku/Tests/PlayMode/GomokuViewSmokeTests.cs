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

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
