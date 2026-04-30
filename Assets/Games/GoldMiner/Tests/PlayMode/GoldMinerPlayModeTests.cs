using System.Collections;
using System.Reflection;
using NUnit.Framework;
using HuanYouYu.MiniGameHall;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Tests
{
    public class GoldMinerPlayModeTests
    {
        private const float SimulatedDeltaTime = 1f / 60f;
        private const int MinTargetCount = 4;
        private const int MaxTargetCount = 8;

        [UnityTest]
        public IEnumerator GoldMinerBuildsExpectedUiAndInitialState()
        {
            GameGoldMinerView game;
            var root = CreateGameRoot(out game);
            yield return TickFrames(game, 3);

            var probe = Object.FindObjectOfType<GoldMinerRuntimeProbe>();
            Assert.IsNotNull(probe, "GoldMiner runtime probe should exist.");
            Assert.IsNotNull(GameObject.Find("GameGoldMinerView"), "Game root should exist.");
            Assert.IsNotNull(GameObject.Find("GoldMinerPlayfield"), "Playfield should exist.");
            Assert.AreEqual("ContentHost", GameObject.Find("GoldMinerPlayfield").transform.parent.name, "Playfield should be attached under ContentHost.");
            Assert.IsNotNull(GameObject.Find("RestartButton"), "Restart button should exist.");
            AssertTargetsUseNonBombSprites();
            Assert.AreEqual(0, probe.Score, "Initial score should be zero.");
            Assert.That(probe.RemainingCount, Is.InRange(MinTargetCount, MaxTargetCount), "Initial target count should be randomized within the expected range.");
            Assert.AreEqual("Swinging", probe.HookStateName, "Hook should start in swinging state.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator GoldMinerSupportsSwingingAndEmptyRetract()
        {
            GameGoldMinerView game;
            var root = CreateGameRoot(out game);
            yield return TickFrames(game, 3);

            var probe = Object.FindObjectOfType<GoldMinerRuntimeProbe>();
            Assert.IsNotNull(probe, "GoldMiner runtime probe should exist.");

            var initialAngle = probe.SwingAngle;
            yield return TickFrames(game, 12);
            Assert.AreNotEqual(initialAngle, probe.SwingAngle, "Swing angle should change over time.");

            var remainingBefore = probe.RemainingCount;
            Assert.IsTrue(probe.LaunchAtAngleForTest(0f), "Hook should launch in test mode.");
            yield return WaitUntilState(game, probe, "Swinging", 180);

            Assert.AreEqual(0, probe.Score, "Empty retract should not change score.");
            Assert.AreEqual(remainingBefore, probe.RemainingCount, "Empty retract should not remove targets.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator GoldMinerPlayfieldClickLaunchesHook()
        {
            GameGoldMinerView game;
            var root = CreateGameRoot(out game);
            yield return TickFrames(game, 3);

            var probe = Object.FindObjectOfType<GoldMinerRuntimeProbe>();
            Assert.IsNotNull(probe, "GoldMiner runtime probe should exist.");

            InvokePrivateVoid(game, "OnPlayfieldPressed");

            Assert.AreEqual("Firing", probe.HookStateName, "Clicking the playfield should launch the hook.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator GoldMinerCanCatchTargetAndRestart()
        {
            GameGoldMinerView game;
            var root = CreateGameRoot(out game);
            yield return TickFrames(game, 3);

            var probe = Object.FindObjectOfType<GoldMinerRuntimeProbe>();
            Assert.IsNotNull(probe, "GoldMiner runtime probe should exist.");

            var remainingBefore = probe.RemainingCount;
            Assert.That(remainingBefore, Is.InRange(MinTargetCount, MaxTargetCount), "Initial target count should be randomized within the expected range.");
            var angles = probe.GetSuggestedLaunchAnglesForTest();
            Assert.IsNotEmpty(angles, "Suggested target angles should be available.");

            Assert.IsTrue(probe.LaunchAtAngleForTest(angles[0]), "Hook should launch toward the first target.");
            yield return WaitUntilState(game, probe, "Swinging", 240);

            Assert.Greater(probe.Score, 0, "Catching a target should increase score.");
            Assert.AreEqual(remainingBefore - 1, probe.RemainingCount, "Catching a target should remove one target.");

            InvokePrivateVoid(game, "OnRestartClicked");
            yield return TickFrames(game, 2);

            Assert.AreEqual(0, probe.Score, "Restart should clear the score.");
            Assert.That(probe.RemainingCount, Is.InRange(MinTargetCount, MaxTargetCount), "Restart should rebuild a randomized target count.");
            Assert.AreEqual("Swinging", probe.HookStateName, "Restart should restore swinging state.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator GoldMinerShowsSettlementAfterClearingAllTargets()
        {
            GameGoldMinerView game;
            var root = CreateGameRoot(out game);
            yield return TickFrames(game, 3);

            var probe = Object.FindObjectOfType<GoldMinerRuntimeProbe>();
            Assert.IsNotNull(probe, "GoldMiner runtime probe should exist.");

            var angles = probe.GetSuggestedLaunchAnglesForTest();
            Assert.IsNotEmpty(angles, "Suggested target angles should be available.");
            var remainingBefore = probe.RemainingCount;
            Assert.That(remainingBefore, Is.InRange(MinTargetCount, MaxTargetCount), "Initial target count should be randomized within the expected range.");
            Assert.IsTrue(probe.LaunchAtAngleForTest(angles[0]), "Hook should launch toward an active target.");
            yield return WaitUntilState(game, probe, "Swinging", 240);

            Assert.Less(probe.RemainingCount, remainingBefore, "At least one target should be caught before settlement.");

            probe.ForceClearBoardForTest();
            yield return TickFrames(game, 2);

            Assert.IsTrue(probe.IsSettled, "Clearing all targets should enter settled state.");
            Assert.IsNotNull(GameObject.Find("GoldMinerSettlementPanel"), "Settlement popup should be shown.");
            Assert.Greater(probe.Score, 0, "Settlement should preserve earned score.");
            Assert.Greater(probe.CoinCount, 0, "Settlement should preserve earned coins.");
            Assert.GreaterOrEqual(probe.ChestCount, 1, "Settlement should preserve earned chests.");

            yield return DestroyGameRoot(game, root);
        }

        private static GameObject CreateGameRoot(out GameGoldMinerView game)
        {
            var root = new GameObject("GoldMinerTestRoot");
            var canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindObjectOfType<AudioListener>() == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(root.transform, false);
            }

            root.AddComponent<MiniGameSfxPlayer>();
            EnsureEventSystem();

            var host = root.AddComponent<TestHostBehaviour>();
            game = new GameGoldMinerView(host, canvas.transform, _ => { }, () => { });
            return root;
        }

        private static IEnumerator DestroyGameRoot(GameGoldMinerView game, GameObject root)
        {
            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void AssertTargetsUseNonBombSprites()
        {
            var targetLayer = GameObject.Find("TargetLayer");
            Assert.IsNotNull(targetLayer, "Target layer should exist.");

            var activeTargetCount = 0;
            for (var index = 0; index < targetLayer.transform.childCount; index++)
            {
                var child = targetLayer.transform.GetChild(index);
                var image = child.GetComponent<Image>();
                if (child.gameObject.activeSelf && image != null)
                {
                    Assert.IsNotNull(image.sprite, child.name + " should have a sprite assigned.");
                    Assert.AreNotEqual("bomb", image.sprite.name, child.name + " should not use the bomb sprite.");
                }

                if (child.gameObject.activeSelf)
                {
                    activeTargetCount++;
                }
            }

            Assert.That(activeTargetCount, Is.InRange(MinTargetCount, MaxTargetCount), "Active target count should be randomized within the expected range.");
        }

        private static void InvokePrivateVoid(GameGoldMinerView game, string methodName)
        {
            var method = typeof(GameGoldMinerView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName + " should exist.");
            method.Invoke(game, null);
        }

        private static IEnumerator WaitUntilState(GameGoldMinerView game, GoldMinerRuntimeProbe probe, string expectedState, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                if (probe != null && probe.HookStateName == expectedState)
                {
                    yield break;
                }

                yield return null;
                game.Tick(SimulatedDeltaTime);
            }

            Assert.Fail("State did not reach " + expectedState + " within " + maxFrames + " frames.");
        }

        private static IEnumerator TickFrames(GameGoldMinerView game, int frames)
        {
            for (var index = 0; index < frames; index++)
            {
                yield return null;
                game.Tick(SimulatedDeltaTime);
            }
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
