using System;
using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests
{
    public class BreakoutGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator HudKeepsCoreStatsButOmitsHintText()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameBreakoutView.GameIdConstant);
            yield return null;

            var gameRoot = GameObject.Find("GameBreakoutView");
            Assert.IsNotNull(gameRoot, "Breakout shell root was not created.");
            Assert.IsNull(gameRoot.transform.Find("TopHost/BreakoutTop/Hint"), "Breakout HUD should no longer create the hint text node.");
            Assert.IsNotNull(gameRoot.transform.Find("TopHost/BreakoutTop/Header/Title"), "Breakout title should remain visible.");
            Assert.IsNotNull(gameRoot.transform.Find("TopHost/BreakoutTop/Header/Score"), "Breakout score should remain visible.");
            Assert.IsNotNull(gameRoot.transform.Find("TopHost/BreakoutTop/Lives"), "Breakout lives should remain visible.");
        }

        [UnityTest]
        public IEnumerator LaunchLoseAndWinFlowStillWorksWithoutHintUpdates()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameBreakoutView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            InvokePrivate(runtime, "LaunchBall");
            yield return null;

            Assert.AreEqual("Playing", GetPrivateField(runtime, "state").ToString(), "LaunchBall should switch breakout into the playing state.");

            InvokePrivate(runtime, "OnBrickBroken");
            yield return null;
            Assert.AreEqual(100, GetPrivateField<int>(runtime, "score"), "Breaking one brick should still update score after removing hint logic.");

            InvokePrivate(runtime, "OnBallLost");
            yield return null;
            Assert.AreEqual("ReadyToLaunch", GetPrivateField(runtime, "state").ToString(), "Losing one ball should still return breakout to ready-to-launch state.");

            InvokePrivate(runtime, "LaunchBall");
            yield return null;
            InvokePrivate(runtime, "OnBoardCleared");
            yield return null;

            var gameRoot = GameObject.Find("GameBreakoutView");
            Assert.IsNotNull(gameRoot, "Breakout shell root should still exist.");
            Assert.IsNotNull(gameRoot.transform.Find("PopupHost/MiniGamePopup"), "Winning breakout should still show the settlement popup.");
        }

        [UnityTest]
        public IEnumerator BrickCollisionCreatesTransientEffectsAndCleansUp()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameBreakoutView.GameIdConstant);
            yield return null;

            var board = GetBoard(GetActiveGame(controller));
            var bricks = GetPrivateField(board, "bricks") as System.Collections.IList;
            Assert.IsNotNull(bricks, "Breakout bricks list should exist.");

            object firstActiveBrick = null;
            for (var i = 0; i < bricks.Count; i++)
            {
                var brick = bricks[i];
                if (GetProperty<bool>(brick, "Active"))
                {
                    firstActiveBrick = brick;
                    break;
                }
            }

            Assert.IsNotNull(firstActiveBrick, "Expected at least one active brick in the current breakout layout.");

            var brickRect = GetProperty<RectTransform>(firstActiveBrick, "Rect");
            Assert.IsNotNull(brickRect, "Brick rect should exist.");
            SetPrivateField(board, "ballVelocity", new Vector2(120f, -240f));

            var args = new object[] { brickRect.anchoredPosition };
            var collided = (bool)GetMethod(board, "CheckBrickCollision").Invoke(board, args);
            Assert.IsTrue(collided, "Direct brick collision invocation should report a hit.");
            Assert.IsFalse(GetProperty<bool>(firstActiveBrick, "Active"), "Hit brick should become inactive immediately.");
            Assert.Greater(GetProperty<int>(board, "ActiveTransientEffectCount"), 0, "Breaking a brick should create transient visual effects.");

            InvokeAny(board, "TickVisualEffects", 0.5f);
            yield return null;

            Assert.AreEqual(0, GetProperty<int>(board, "ActiveTransientEffectCount"), "Transient brick effects should clean themselves up after a short delay.");
        }

        [UnityTest]
        public IEnumerator PaddleCollisionTriggersPulseAndKeepsUpwardBounce()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameBreakoutView.GameIdConstant);
            yield return null;

            var board = GetBoard(GetActiveGame(controller));
            InvokeAny(board, "SetPaddlePosition", 0f);

            var paddleRect = GetPrivateField(board, "paddleRect") as RectTransform;
            var ballRect = GetPrivateField(board, "ballRect") as RectTransform;
            Assert.IsNotNull(paddleRect, "Paddle rect should exist.");
            Assert.IsNotNull(ballRect, "Ball rect should exist.");

            SetPrivateField(board, "ballVelocity", new Vector2(0f, -320f));
            var probePosition = new Vector2(
                paddleRect.anchoredPosition.x,
                paddleRect.anchoredPosition.y + (paddleRect.sizeDelta.y * 0.5f) + (ballRect.sizeDelta.y * 0.5f) - 1f);

            var args = new object[] { probePosition };
            var collided = (bool)GetMethod(board, "CheckPaddleCollision").Invoke(board, args);
            Assert.IsTrue(collided, "Direct paddle collision invocation should report a hit.");

            var ballVelocity = GetPrivateField<Vector2>(board, "ballVelocity");
            Assert.Greater(ballVelocity.y, 0f, "Paddle bounce should keep the ball moving upward.");
            Assert.Greater(ballVelocity.x, 0f, "Center hit should still honor the minimum horizontal bounce.");
            Assert.IsTrue(GetProperty<bool>(board, "IsPaddlePulseActive"), "Paddle hit should start the paddle pulse effect.");
            Assert.IsTrue(GetProperty<bool>(board, "IsBallPulseActive"), "Paddle hit should start the ball pulse effect.");

            InvokeAny(board, "TickVisualEffects", 0.4f);
            yield return null;

            Assert.IsFalse(GetProperty<bool>(board, "IsPaddlePulseActive"), "Paddle pulse should stop after its short animation window.");
            Assert.IsFalse(GetProperty<bool>(board, "IsBallPulseActive"), "Ball pulse should stop after its short animation window.");
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static IEnumerator LoadController(Action<MiniGameAppController> assign)
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 60; i++)
            {
                controller = Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(controller, "MiniGameAppController was not created.");
            assign(controller);
        }

        private static GameBreakoutView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as GameBreakoutView;
            Assert.IsNotNull(runtime, "Breakout runtime was not created.");
            return runtime;
        }

        private static object GetBoard(GameBreakoutView runtime)
        {
            return GetPrivateField(runtime, "board");
        }

        private static MethodInfo GetMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            return method;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            GetMethod(target, methodName).Invoke(target, null);
        }

        private static void InvokeAny(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, InstanceAny);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            method.Invoke(target, args);
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return field.GetValue(target);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)GetPrivateField(target, fieldName);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            field.SetValue(target, value);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, InstanceAny);
            Assert.IsNotNull(property, "Failed to access property: " + propertyName);
            return (T)property.GetValue(target, null);
        }
    }
}
