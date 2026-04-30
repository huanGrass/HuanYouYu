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
    public class JumpJumpGamePlayModeTests
    {
        private GameObject eventSystemObject;
        private GameObject canvasObject;
        private TestHostBehaviour hostBehaviour;
        private MiniGameJumpJumpGameView view;
        private MiniGameSettlement completedSettlement;
        private bool exitedToHall;

        [SetUp]
        public void SetUp()
        {
            PlayModeGlobalLogMonitor.Clear();

            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);

            hostBehaviour = new GameObject("TestHost").AddComponent<TestHostBehaviour>();
            view = new MiniGameJumpJumpGameView(
                hostBehaviour,
                canvasObject.transform,
                settlement => completedSettlement = settlement,
                () => exitedToHall = true);
        }

        [TearDown]
        public void TearDown()
        {
            if (view != null)
            {
                view.Dispose();
                view = null;
            }

            if (hostBehaviour != null)
            {
                UnityEngine.Object.DestroyImmediate(hostBehaviour.gameObject);
            }

            if (canvasObject != null)
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }

            if (eventSystemObject != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }

            var report = PlayModeGlobalLogMonitor.BuildFailureReport();
            if (!string.IsNullOrEmpty(report))
            {
                Assert.Fail("Unexpected Error/Exception logs:\n" + report);
            }
        }

        [UnityTest]
        public IEnumerator CreatesInitialPlatformsPlayerAndHud()
        {
            yield return null;

            Assert.IsNotNull(canvasObject.transform.Find("MiniGameJumpJumpView"), "Should create JumpJump root.");
            Assert.IsNotNull(GameObject.Find("JumpJumpWorldRoot"), "Should create world root.");
            Assert.IsNotNull(GameObject.Find("CurrentPlatform"), "Should create current platform.");
            Assert.IsNotNull(GameObject.Find("TargetPlatform"), "Should create target platform.");
            Assert.IsNull(GameObject.Find("PreviewPlatform1"), "Should not create future preview platforms.");
            Assert.IsNull(GameObject.Find("PreviewPlatform2"), "Should not create future preview platforms.");
            Assert.AreEqual(0, CountObjectsNamedWithPrefix("PastPlatform"), "Should not create past platforms before the first jump.");
            Assert.IsNotNull(GameObject.Find("Player"), "Should create player.");
            Assert.IsNotNull(Camera.main, "Should have a world camera.");

            var scoreText = canvasObject.transform.Find("MiniGameJumpJumpView/TopHost/JumpJumpHeader/Header/Score");
            Assert.IsNotNull(scoreText, "Score label should exist.");
            Assert.AreEqual(0, InvokePrivate<int>(view, "DebugGetScore"));

            var player = GameObject.Find("Player");
            var currentPlatform = GameObject.Find("CurrentPlatform");
            Assert.Less(player.transform.localScale.x, currentPlatform.transform.GetChild(0).localScale.x);
            Assert.Less(player.transform.localScale.z, currentPlatform.transform.GetChild(0).localScale.z);
            Assert.IsNull(player.transform.Find("PlayerBase"));
            Assert.IsNotNull(player.transform.Find("PlayerBody"));
            Assert.IsNotNull(player.transform.Find("PlayerHead"));
            Assert.IsNotNull(player.transform.Find("PlayerMark"));

            var playerShadow = GameObject.Find("PlayerShadow");
            Assert.IsNotNull(playerShadow, "Should create player shadow.");
            Assert.IsNull(playerShadow.GetComponent<Collider>(), "Player shadow should not have a collider.");
            Assert.Less(playerShadow.GetComponent<Renderer>().bounds.size.y, 0.01f);
            Assert.Less(playerShadow.transform.localScale.x, player.transform.localScale.x);
            Assert.Less(playerShadow.transform.localScale.z, player.transform.localScale.z);
        }

        [UnityTest]
        public IEnumerator SuccessfulJumpIncreasesScoreAndGeneratesNextTarget()
        {
            yield return null;

            var beforeTarget = GameObject.Find("TargetPlatform").transform.position;
            var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
            InvokePrivate(view, "DebugSimulateJump", idealCharge);

            yield return AdvanceGame(1.0f);

            Assert.AreEqual(1, InvokePrivate<int>(view, "DebugGetScore"));
            var afterTarget = GameObject.Find("TargetPlatform").transform.position;
            Assert.AreNotEqual(beforeTarget, afterTarget);
            Assert.GreaterOrEqual(CountObjectsNamedWithPrefix("PastPlatform"), 1, "Jumped platforms should remain after landing.");
        }

        [UnityTest]
        public IEnumerator PlayerRollsForwardDuringJumpAndLandsUpright()
        {
            yield return null;

            var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
            InvokePrivate(view, "DebugSimulateJump", idealCharge);

            var rollDegrees = InvokePrivate<float>(view, "DebugGetPlayerJumpRollDegrees");
            Assert.AreEqual(0f, rollDegrees % 360f, 0.001f, "Jump roll should end upright without snapping on landing.");

            yield return AdvanceGame(0.1f);

            var player = GameObject.Find("Player");
            var playerForward = InvokePrivate<Vector3>(view, "DebugGetPlayerForward");
            playerForward.y = 0f;
            var facingRotation = Quaternion.LookRotation(playerForward.normalized, Vector3.up);
            var rollAngle = Quaternion.Angle(player.transform.rotation, facingRotation);
            Assert.Greater(rollAngle, 80f, "Player should roll quickly while jumping.");

            var expectedForwardRoll = facingRotation * Quaternion.AngleAxis(rollAngle, Vector3.right);
            var reversedRoll = facingRotation * Quaternion.AngleAxis(-rollAngle, Vector3.right);
            Assert.Less(
                Quaternion.Angle(player.transform.rotation, expectedForwardRoll),
                Quaternion.Angle(player.transform.rotation, reversedRoll),
                "Player should roll in the forward direction while jumping.");

            yield return AdvanceGame(0.8f);

            Assert.AreEqual(1, InvokePrivate<int>(view, "DebugGetScore"));
            playerForward = InvokePrivate<Vector3>(view, "DebugGetPlayerForward");
            playerForward.y = 0f;
            facingRotation = Quaternion.LookRotation(playerForward.normalized, Vector3.up);
            Assert.Less(
                Quaternion.Angle(player.transform.rotation, facingRotation),
                1f,
                "Player should return upright after landing.");
        }

        [UnityTest]
        public IEnumerator SuccessfulOffCenterLandingKeepsPlayerAtLandingPosition()
        {
            yield return null;

            var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
            InvokePrivate(view, "DebugSimulateJump", Mathf.Clamp01(idealCharge + 0.04f));
            yield return AdvanceGame(1.0f);

            Assert.AreEqual(1, InvokePrivate<int>(view, "DebugGetScore"));
            var playerPosition = GameObject.Find("Player").transform.position;
            var currentPlatformPosition = GameObject.Find("CurrentPlatform").transform.position;
            var horizontalOffset = new Vector2(
                playerPosition.x - currentPlatformPosition.x,
                playerPosition.z - currentPlatformPosition.z).magnitude;
            Assert.Greater(horizontalOffset, 0.08f, "Player should keep the actual off-center landing position.");
        }

        [UnityTest]
        public IEnumerator RetainedPlatformsDoNotOverlapAfterSeveralJumps()
        {
            yield return null;

            AssertRetainedPlatformsDoNotOverlap();
            for (var i = 0; i < 6; i++)
            {
                var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
                InvokePrivate(view, "DebugSimulateJump", idealCharge);
                yield return AdvanceGame(1.0f);

                Assert.AreEqual(i + 1, InvokePrivate<int>(view, "DebugGetScore"));
                AssertRetainedPlatformsDoNotOverlap();
            }
        }

        [UnityTest]
        public IEnumerator InitialTargetUsesComfortableChargeWindow()
        {
            yield return null;

            var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
            Assert.Greater(idealCharge, 0.25f);
            Assert.Less(idealCharge, 0.65f);
        }

        [UnityTest]
        public IEnumerator InitialCameraKeepsPlayerAndPlatformsInComfortableView()
        {
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Should have a world camera.");
            Assert.GreaterOrEqual(camera.fieldOfView, 42f);

            AssertViewportInComfortableRange(camera, GameObject.Find("Player").transform.position, "Player");
            AssertViewportInComfortableRange(camera, GameObject.Find("CurrentPlatform").transform.position, "CurrentPlatform");
            AssertViewportInComfortableRange(camera, GameObject.Find("TargetPlatform").transform.position, "TargetPlatform");
        }

        [UnityTest]
        public IEnumerator FailedJumpShowsSettlementAndCompletesOnExit()
        {
            yield return null;

            InvokePrivate(view, "BeginFailure");
            yield return AdvanceGame(1.1f);

            Assert.IsNotNull(canvasObject.transform.Find("MiniGameJumpJumpView/PopupHost/JumpJumpSettlementPanel"), "Failure popup should exist.");

            var exitButton = canvasObject.transform.Find("MiniGameJumpJumpView/PopupHost/JumpJumpSettlementPanel/Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(exitButton, "Exit button should exist.");
            exitButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(completedSettlement, "Completing from settlement should report a settlement.");
            Assert.AreEqual(0, completedSettlement.Score);
            Assert.AreEqual(0, completedSettlement.CoinCount);
            Assert.AreEqual(0, completedSettlement.ChestCount);
        }

        [UnityTest]
        public IEnumerator FailedJumpAfterScoringGrantsPlatformRewards()
        {
            yield return null;

            for (var i = 0; i < 4; i++)
            {
                var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
                InvokePrivate(view, "DebugSimulateJump", idealCharge);
                yield return AdvanceGame(1.0f);
            }

            InvokePrivate(view, "BeginFailure");
            yield return AdvanceGame(1.1f);

            var exitButton = canvasObject.transform.Find("MiniGameJumpJumpView/PopupHost/JumpJumpSettlementPanel/Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(exitButton, "Exit button should exist.");
            exitButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(completedSettlement, "Completing from settlement should report a settlement.");
            Assert.AreEqual(4, completedSettlement.Score);
            Assert.AreEqual(20, completedSettlement.CoinCount);
            Assert.AreEqual(0, completedSettlement.ChestCount);
        }

        [UnityTest]
        public IEnumerator RestartResetsScoreAndPausePopupCanOpen()
        {
            yield return null;

            var idealCharge = InvokePrivate<float>(view, "DebugGetIdealChargeNormalized");
            InvokePrivate(view, "DebugSimulateJump", idealCharge);
            yield return AdvanceGame(1.0f);
            Assert.AreEqual(1, InvokePrivate<int>(view, "DebugGetScore"));

            InvokePrivate(view, "OnRestartClicked");
            yield return null;
            Assert.AreEqual(0, InvokePrivate<int>(view, "DebugGetScore"));

            InvokePrivate(view, "OnPauseRequested");
            yield return null;
            Assert.IsNotNull(canvasObject.transform.Find("MiniGameJumpJumpView/PopupHost/MiniGamePausePopup"), "Pause popup should open.");

            InvokePrivate(view, "ConfirmExitToHall");
            yield return null;
            Assert.IsFalse(exitedToHall, "Exit should wait for reward settlement confirmation.");
            var confirmButton = canvasObject.transform.Find("MiniGameJumpJumpView/PopupHost/JumpJumpSettlementPanel/Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Exit settlement back hall button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(completedSettlement, "Exit should complete with current settlement.");
        }

        private IEnumerator AdvanceGame(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                view.Tick(0.05f);
                elapsed += 0.05f;
                yield return null;
            }
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method: " + methodName);
            method.Invoke(target, args);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method: " + methodName);
            return (T)method.Invoke(target, args);
        }

        private static void AssertViewportInComfortableRange(Camera camera, Vector3 worldPosition, string objectName)
        {
            var viewportPosition = camera.WorldToViewportPoint(worldPosition);
            Assert.Greater(viewportPosition.z, 0f, objectName + " should be in front of camera.");
            Assert.That(viewportPosition.x, Is.InRange(0.08f, 0.92f), objectName + " should not be clipped horizontally.");
            Assert.That(viewportPosition.y, Is.InRange(0.14f, 0.82f), objectName + " should not be clipped vertically.");
        }

        private static void AssertRetainedPlatformsDoNotOverlap()
        {
            var platforms = FindRetainedPlatformObjects();

            for (var i = 0; i < platforms.Length; i++)
            {
                Assert.IsNotNull(platforms[i], "Visible platform should exist.");
                for (var j = i + 1; j < platforms.Length; j++)
                {
                    var delta = platforms[i].transform.position - platforms[j].transform.position;
                    delta.y = 0f;
                    Assert.GreaterOrEqual(
                        delta.magnitude,
                        3.15f,
                        platforms[i].name + " should not overlap " + platforms[j].name + ".");
                }
            }
        }

        private static GameObject[] FindRetainedPlatformObjects()
        {
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            var platforms = new System.Collections.Generic.List<GameObject>();
            for (var i = 0; i < transforms.Length; i++)
            {
                var name = transforms[i].gameObject.name;
                if (name == "CurrentPlatform" || name == "TargetPlatform" || name.StartsWith("PastPlatform", StringComparison.Ordinal))
                {
                    platforms.Add(transforms[i].gameObject);
                }
            }

            return platforms.ToArray();
        }

        private static int CountObjectsNamedWithPrefix(string prefix)
        {
            var count = 0;
            var transforms = UnityEngine.Object.FindObjectsOfType<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].gameObject.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count += 1;
                }
            }

            return count;
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
