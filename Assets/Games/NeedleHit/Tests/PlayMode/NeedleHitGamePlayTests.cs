using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public class NeedleHitGamePlayTests
    {
        private const string RootName = "GameNeedleHitView";

        [SetUp]
        public void SetUp()
        {
            PlayModeGlobalLogMonitor.Clear();
        }

        [UnityTest]
        public IEnumerator NeedleHitBootsAndFirstShotScores()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            var gameView = GetPrivateField(controller, "activeGame");
            Assert.IsNotNull(gameView, "Active game runtime should exist.");

            var scoreLabel = RequireTextComponent(root, "TopHost/NeedleHitTop/Header/Score");
            Assert.AreEqual("\u63d2\u9488 0/3", ReadText(scoreLabel));

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            Assert.AreEqual("\u63d2\u9488 1/3", ReadText(scoreLabel));
            AssertNoUnexpectedLogs();
        }

        [UnityTest]
        public IEnumerator NeedleHitLevelSelectOpensAfterScoring()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            var gameView = GetPrivateField(controller, "activeGame");
            Assert.IsNotNull(gameView, "Active game runtime should exist.");

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            Click(root, "BottomHost/NeedleHitBottom/ActionBar/LevelSelectButton");
            yield return null;

            Assert.IsNotNull(root.Find("PopupHost/NeedleHitLevelSelectPanel"), "Level select panel should open after scoring.");
            AssertNoUnexpectedLogs();
        }

        [UnityTest]
        public IEnumerator NeedleHitCanFailAndRestart()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            var gameView = GetPrivateField(controller, "activeGame");
            Assert.IsNotNull(gameView, "Active game runtime should exist.");

            var scoreLabel = RequireTextComponent(root, "TopHost/NeedleHitTop/Header/Score");
            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;
            Assert.AreEqual("\u63d2\u9488 1/3", ReadText(scoreLabel));
            ForceNextShotFailure(gameView);

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            var popup = root.Find("PopupHost/NeedleHitFailureSettlementPanel");
            Assert.IsNotNull(popup, "Settlement popup should exist after failure.");
            Assert.IsNotNull(popup.Find("Dialog/NextButton")?.GetComponent<Button>(), "Retry button should exist.");
            Assert.IsNotNull(popup.Find("Dialog/BackHallButton")?.GetComponent<Button>(), "Back hall button should exist.");
            Assert.IsNotNull(popup.Find("InputBlocker"), "Settlement input blocker should exist.");

            Click(root, "BottomHost/NeedleHitBottom/ActionBar/RestartButton");
            yield return null;

            Assert.AreEqual("\u63d2\u9488 0/3", ReadText(scoreLabel));
            Assert.AreEqual(3, GetStuckNeedleCount(gameView));
            Assert.IsNull(root.Find("PopupHost/NeedleHitFailureSettlementPanel"), "Restart should close settlement popup.");
            AssertNoUnexpectedLogs();
        }

        [UnityTest]
        public IEnumerator NeedleHitAllowsNarrowNearMisses()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            var gameView = GetPrivateField(controller, "activeGame");
            Assert.IsNotNull(gameView, "Active game runtime should exist.");

            var scoreLabel = RequireTextComponent(root, "TopHost/NeedleHitTop/Header/Score");

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            Assert.AreEqual("\u63d2\u9488 1/3", ReadText(scoreLabel));

            ForceNextShotNearMiss(gameView);

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            Assert.AreEqual("\u63d2\u9488 2/3", ReadText(scoreLabel));
            AssertNoUnexpectedLogs();
        }

        [UnityTest]
        public IEnumerator NeedleHitPreLaunchNeedleShowsHeadAtBottom()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            var needle = RequireRectTransform(root, "ContentHost/NeedleHitContent/BoardRoot/NeedleLayer/CurrentNeedle");
            var shaft = RequireRectTransform(needle, "Shaft");
            var head = RequireRectTransform(needle, "Head");

            Assert.Less(head.anchoredPosition.y, shaft.anchoredPosition.y, "Head should sit below the shaft before firing.");
            Assert.AreEqual(11f, head.anchoredPosition.y, 0.01f);
            Assert.AreEqual(22f, shaft.anchoredPosition.y, 0.01f);
            AssertNoUnexpectedLogs();
        }

        [UnityTest]
        public IEnumerator NeedleHitFailureSettlementAwardsCoinsOnly()
        {
            var controller = default(MiniGameAppController);
            yield return LoadGameSceneAndEnterGame(result => controller = result);

            var root = RequireRuntimeRoot();
            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            var gameView = GetPrivateField(controller, "activeGame");
            Assert.IsNotNull(gameView, "Active game runtime should exist.");

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;
            ForceNextShotFailure(gameView);

            Click(root, "ContentHost/NeedleHitContent/BoardRoot/TapZone");
            ResolveCurrentShot(gameView);
            yield return null;

            var popup = root.Find("PopupHost/NeedleHitFailureSettlementPanel");
            Assert.IsNotNull(popup, "Settlement popup should exist after failure.");
            var backHallButton = popup.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button should exist.");
            backHallButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(GameNeedleHitView.GameIdConstant);
            Assert.AreEqual(3, progress.TotalCoinCount);
            Assert.AreEqual(0, progress.TotalChestCount);
            AssertNoUnexpectedLogs();
        }

        private static IEnumerator LoadGameSceneAndEnterGame(System.Action<MiniGameAppController> onLoaded)
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 30; i++)
            {
                controller = Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(controller, "MiniGameAppController should exist.");
            controller.EnterGame(GameNeedleHitView.GameIdConstant);
            onLoaded?.Invoke(controller);

            for (var i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static Transform RequireRuntimeRoot()
        {
            var root = GameObject.Find(RootName);
            Assert.IsNotNull(root, "NeedleHit runtime root should exist.");
            return root.transform;
        }

        private static Component RequireTextComponent(Transform root, string relativePath)
        {
            var text = root.Find(relativePath)?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(text, "Missing TMP text at " + relativePath);
            return text;
        }

        private static RectTransform RequireRectTransform(Transform root, string relativePath)
        {
            var transform = root.Find(relativePath);
            Assert.IsNotNull(transform, "Missing transform at " + relativePath);

            var rectTransform = transform.GetComponent<RectTransform>();
            Assert.IsNotNull(rectTransform, "Missing RectTransform at " + relativePath);
            return rectTransform;
        }

        private static void Click(Transform root, string relativePath)
        {
            var button = root.Find(relativePath)?.GetComponent<Button>();
            Assert.IsNotNull(button, "Missing Button at " + relativePath);
            button.onClick.Invoke();
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + fieldName);
            return field.GetValue(target);
        }

        private static int GetStuckNeedleCount(object gameView)
        {
            var stuckNeedles = GetPrivateField(gameView, "stuckNeedles") as System.Collections.IList;
            Assert.IsNotNull(stuckNeedles, "stuckNeedles should be accessible.");
            return stuckNeedles.Count;
        }

        private static void ForceNextShotFailure(object gameView)
        {
            var viewType = gameView.GetType();
            var stuckNeedles = GetPrivateField(gameView, "stuckNeedles") as System.Collections.IList;
            Assert.IsNotNull(stuckNeedles, "stuckNeedles should be accessible.");
            Assert.Greater(stuckNeedles.Count, 0, "There should be at least one stuck needle.");

            var lastNeedle = stuckNeedles[stuckNeedles.Count - 1];
            var localAngleField = lastNeedle.GetType().GetField("LocalAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(localAngleField, "LocalAngle should be accessible.");
            var localAngle = (float)localAngleField.GetValue(lastNeedle);

            var rotationDirectionField = viewType.GetField("rotationDirection", BindingFlags.Instance | BindingFlags.NonPublic);
            var discRotationDegreesField = viewType.GetField("discRotationDegrees", BindingFlags.Instance | BindingFlags.NonPublic);
            var applyDiscRotationMethod = viewType.GetMethod("ApplyDiscRotation", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(rotationDirectionField, "rotationDirection should exist.");
            Assert.IsNotNull(discRotationDegreesField, "discRotationDegrees should exist.");
            Assert.IsNotNull(applyDiscRotationMethod, "ApplyDiscRotation should exist.");

            rotationDirectionField.SetValue(gameView, 0);
            discRotationDegreesField.SetValue(gameView, 180f - localAngle);
            applyDiscRotationMethod.Invoke(gameView, null);
        }

        private static void ForceNextShotNearMiss(object gameView)
        {
            var viewType = gameView.GetType();
            var stuckNeedles = GetPrivateField(gameView, "stuckNeedles") as System.Collections.IList;
            Assert.IsNotNull(stuckNeedles, "stuckNeedles should be accessible.");
            Assert.Greater(stuckNeedles.Count, 0, "There should be at least one stuck needle.");

            var lastNeedle = stuckNeedles[stuckNeedles.Count - 1];
            var localAngleField = lastNeedle.GetType().GetField("LocalAngle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(localAngleField, "LocalAngle should be accessible.");
            var localAngle = (float)localAngleField.GetValue(lastNeedle);

            var rotationDirectionField = viewType.GetField("rotationDirection", BindingFlags.Instance | BindingFlags.NonPublic);
            var discRotationDegreesField = viewType.GetField("discRotationDegrees", BindingFlags.Instance | BindingFlags.NonPublic);
            var applyDiscRotationMethod = viewType.GetMethod("ApplyDiscRotation", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(rotationDirectionField, "rotationDirection should exist.");
            Assert.IsNotNull(discRotationDegreesField, "discRotationDegrees should exist.");
            Assert.IsNotNull(applyDiscRotationMethod, "ApplyDiscRotation should exist.");

            rotationDirectionField.SetValue(gameView, 0);
            discRotationDegreesField.SetValue(gameView, 190f - localAngle);
            applyDiscRotationMethod.Invoke(gameView, null);
        }

        private static void TickGame(object gameView, float deltaTime, int iterations)
        {
            var tickMethod = gameView.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(tickMethod, "Tick should exist.");

            for (var i = 0; i < iterations; i++)
            {
                tickMethod.Invoke(gameView, new object[] { deltaTime });
            }
        }

        private static void ResolveCurrentShot(object gameView)
        {
            var resolveMethod = gameView.GetType().GetMethod("ResolveFlyingNeedleImpact", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resolveMethod, "ResolveFlyingNeedleImpact should exist.");
            resolveMethod.Invoke(gameView, null);
        }

        private static void AssertNoUnexpectedLogs()
        {
            var report = PlayModeGlobalLogMonitor.BuildFailureReport();
            if (!string.IsNullOrEmpty(report))
            {
                Assert.Fail("Unexpected Error/Exception logs:\n" + report);
            }
        }

        private static string ReadText(Component textComponent)
        {
            Assert.IsNotNull(textComponent, "Text component should exist.");
            var property = textComponent.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "text property should exist.");
            return property.GetValue(textComponent, null) as string;
        }
    }
}
