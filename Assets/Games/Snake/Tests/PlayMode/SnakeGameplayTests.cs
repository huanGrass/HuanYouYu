using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public class SnakeGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void PauseHelpTextCanComposeGameplayAndCreditsSections()
        {
            var composeMethod = typeof(MiniGameBase).GetMethod("BuildPauseHelpText", StaticPrivate);
            Assert.IsNotNull(composeMethod, "BuildPauseHelpText was not found.");

            var helpText = composeMethod.Invoke(null, new object[] { "详细玩法", "参与制作：测试成员" }) as string;
            Assert.IsNotNull(helpText, "Composed pause help text should not be null.");
            StringAssert.Contains("玩法说明", helpText);
            StringAssert.Contains("详细玩法", helpText);
            StringAssert.Contains("参与制作：测试成员", helpText);
        }

        [UnityTest]
        public IEnumerator CanTurnAndAdvanceOneStepByButton()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var before = SnapshotSnake(runtime);
            SetFood(runtime, new Vector2Int(0, 0));

            ClickButton("UpButton");
            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;

            var after = SnapshotSnake(runtime);
            Assert.AreEqual(before.Count, after.Count, "Snake length should remain unchanged after a simple move.");
            Assert.AreEqual(new Vector2Int(before[before.Count - 1].x, before[before.Count - 1].y - 1), after[after.Count - 1], "Snake head should move upward after clicking Up.");
        }

        [UnityTest]
        public IEnumerator CanEatFoodAndIncreaseScore()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var snake = SnapshotSnake(runtime);
            var head = snake[snake.Count - 1];
            Assert.Less(head.x, 9, "Test setup requires room on the right side.");

            SetFood(runtime, new Vector2Int(head.x + 1, head.y));
            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;

            var afterSnake = SnapshotSnake(runtime);
            Assert.AreEqual(snake.Count + 1, afterSnake.Count, "Eating food should increase snake length by one.");
            Assert.AreEqual(10, GetIntField(runtime, "score"), "Eating one food should increase score by 10.");
        }

        [UnityTest]
        public IEnumerator CanAdjustSpeedByButtons()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var before = GetFloatField(runtime, "currentMoveInterval");

            ClickButton("FasterButton");
            yield return null;
            var faster = GetFloatField(runtime, "currentMoveInterval");
            Assert.Less(faster, before, "FasterButton should reduce move interval.");

            ClickButton("SlowerButton");
            yield return null;
            var slower = GetFloatField(runtime, "currentMoveInterval");
            Assert.Greater(slower, faster, "SlowerButton should increase move interval.");
        }

        [UnityTest]
        public IEnumerator BoardVisualBoundsMatchPlayableGridAndButtonsAreLargeEnough()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            var boardPlayfield = GameObject.Find("BoardPlayfield")?.GetComponent<RectTransform>();
            var boardGrid = GameObject.Find("BoardGrid")?.GetComponent<RectTransform>();
            var upButton = FindButton("UpButton");
            var fasterButton = FindButton("FasterButton");

            Assert.IsNotNull(boardPlayfield, "BoardPlayfield should exist as the real wall boundary.");
            Assert.IsNotNull(boardGrid, "BoardGrid should exist.");
            Assert.IsNotNull(upButton, "UpButton was not found.");
            Assert.IsNotNull(fasterButton, "FasterButton was not found.");

            Assert.AreEqual(boardPlayfield.rect.width, boardGrid.rect.width, 0.5f, "Playable width should match the visible playfield.");
            Assert.AreEqual(boardPlayfield.rect.height, boardGrid.rect.height, 0.5f, "Playable height should match the visible playfield.");
            Assert.GreaterOrEqual(upButton.GetComponent<RectTransform>().rect.width, 92f, "Direction buttons should remain large enough for touch.");
            Assert.GreaterOrEqual(fasterButton.GetComponent<RectTransform>().rect.width, 72f, "Speed buttons should remain large enough for touch.");
        }

        [UnityTest]
        public IEnumerator EatingFoodDoesNotChangeSpeed()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var snake = SnapshotSnake(runtime);
            var head = snake[snake.Count - 1];
            var before = GetFloatField(runtime, "currentMoveInterval");

            SetFood(runtime, new Vector2Int(head.x + 1, head.y));
            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;

            var after = GetFloatField(runtime, "currentMoveInterval");
            Assert.AreEqual(before, after, 0.0001f, "Eating food should no longer change speed.");
        }

        [UnityTest]
        public IEnumerator EatingFoodCreatesAndCleansTransientEffects()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var snake = SnapshotSnake(runtime);
            var head = snake[snake.Count - 1];
            SetFood(runtime, new Vector2Int(head.x + 1, head.y));

            yield return new WaitForSecondsRealtime(0.46f);
            yield return null;

            Assert.Greater(CountTransientEffects(runtime, "SnakeEat"), 0, "Eating food should create transient eat effects.");

            yield return new WaitForSecondsRealtime(0.45f);
            yield return null;

            Assert.AreEqual(0, CountTransientEffects(runtime, "SnakeEat"), "Transient eat effects should clean themselves up.");
        }

        [UnityTest]
        public IEnumerator WrapAroundCreatesAndCleansEdgeFlash()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            ForceWrapAroundState(runtime);

            yield return new WaitForSecondsRealtime(0.46f);
            yield return null;

            Assert.Greater(CountTransientEffects(runtime, "SnakeEdgeFlash"), 0, "Wrap-around should create edge flash effects.");

            yield return new WaitForSecondsRealtime(0.30f);
            yield return null;

            Assert.AreEqual(0, CountTransientEffects(runtime, "SnakeEdgeFlash"), "Edge flash effects should clean themselves up.");
            Assert.IsTrue(controller.HasActiveGame, "Wrap-around effects should not interrupt gameplay.");
        }

        [UnityTest]
        public IEnumerator SelfCollisionSettlementAppearsAfterShortEffectDelay()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            ForceSelfCollisionState(runtime);

            yield return new WaitForSecondsRealtime(0.47f);
            yield return null;

            var gameRoot = GameObject.Find("SnakeView");
            Assert.IsNotNull(gameRoot, "Snake shell root was not found.");
            Assert.Greater(CountTransientEffects(runtime, "SnakeCollisionFlash"), 0, "Self collision should briefly play collision flash effects.");
            Assert.IsNull(gameRoot.transform.Find("PopupHost/SnakeSettlementPanel"), "Settlement popup should wait until the short collision effect has played.");

            yield return new WaitForSecondsRealtime(0.12f);
            yield return null;

            Assert.IsNotNull(gameRoot.transform.Find("PopupHost/SnakeSettlementPanel"), "Settlement popup should appear after the collision effect delay.");
        }

        [UnityTest]
        public IEnumerator CanPauseResumeAndExitToHall()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var gameRoot = GameObject.Find("SnakeView");
            Assert.IsNotNull(gameRoot, "Snake shell root was not created.");

            var pauseButton = gameRoot.transform.Find("PauseButton")?.GetComponent<Button>();
            Assert.IsNotNull(pauseButton, "PauseButton was not found.");

            pauseButton.onClick.Invoke();
            yield return null;

            var pausePopup = gameRoot.transform.Find("PopupHost/MiniGamePausePopup");
            Assert.IsNotNull(pausePopup, "Pause popup should be visible after clicking pause.");

            var helpButton = pausePopup.Find("Dialog/HelpButton")?.GetComponent<Button>();
            Assert.IsNotNull(helpButton, "Pause popup help button was not found.");
            helpButton.onClick.Invoke();
            yield return null;

            var helpOverlay = pausePopup.Find("HelpOverlay");
            Assert.IsNotNull(helpOverlay, "Pause popup help overlay was not found.");
            Assert.IsTrue(helpOverlay.gameObject.activeSelf, "Help overlay should become visible after clicking help.");

            var helpDialogRect = helpOverlay.Find("Dialog") as RectTransform;
            Assert.IsNotNull(helpDialogRect, "Pause popup help dialog was not found.");
            Assert.AreEqual(660f, helpDialogRect.sizeDelta.x, 0.1f, "Pause popup help dialog width should stay fixed.");
            Assert.GreaterOrEqual(helpDialogRect.sizeDelta.y, 380f, "Pause popup help dialog height should preserve the minimum readable size.");
            Assert.LessOrEqual(
                helpDialogRect.sizeDelta.y,
                pausePopup.GetComponent<RectTransform>().rect.height * 0.68f + 0.1f,
                "Pause popup help dialog height should stay within the configured screen ratio limit.");

            var helpMessage = helpOverlay.Find("Dialog/Message")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(helpMessage, "Pause popup help message was not found.");
            var helpText = helpMessage.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public)?.GetValue(helpMessage) as string;
            Assert.IsNotNull(helpText, "Pause popup help message text property was not found.");
            StringAssert.Contains("控制小蛇吃掉食物", helpText);
            StringAssert.Contains("参与制作：幻之小草", helpText);

            var helpConfirmButton = helpOverlay.Find("Dialog/ConfirmButton")?.GetComponent<Button>();
            Assert.IsNotNull(helpConfirmButton, "Pause popup help confirm button was not found.");
            helpConfirmButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(helpOverlay.gameObject.activeSelf, "Help overlay should close after confirming.");

            AssertToggleState(pausePopup, "Dialog/Settings/MusicRow/ToggleButton/Label", "开");
            ClickPopupButton(pausePopup, "Dialog/Settings/MusicRow/ToggleButton");
            AssertToggleState(pausePopup, "Dialog/Settings/MusicRow/ToggleButton/Label", "关");
            Assert.AreEqual(0f, controller.GetComponent<AudioSource>().volume, 0.001f, "Music toggle should mute background music immediately.");

            AssertToggleState(pausePopup, "Dialog/Settings/SfxRow/ToggleButton/Label", "开");
            ClickPopupButton(pausePopup, "Dialog/Settings/SfxRow/ToggleButton");
            AssertToggleState(pausePopup, "Dialog/Settings/SfxRow/ToggleButton/Label", "关");

            AssertToggleState(pausePopup, "Dialog/Settings/VibrationRow/ToggleButton/Label", "关");
            ClickPopupButton(pausePopup, "Dialog/Settings/VibrationRow/ToggleButton");
            AssertToggleState(pausePopup, "Dialog/Settings/VibrationRow/ToggleButton/Label", "开");

            var continueButton = pausePopup.Find("Dialog/MainButtons/ContinueButton")?.GetComponent<Button>();
            Assert.IsNotNull(continueButton, "Pause popup continue button was not found.");
            continueButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(gameRoot.transform.Find("PopupHost/MiniGamePausePopup"), "Pause popup should close after resuming.");
            Assert.IsTrue(controller.HasActiveGame, "Game should still be active after resuming.");

            pauseButton.onClick.Invoke();
            yield return null;

            pausePopup = gameRoot.transform.Find("PopupHost/MiniGamePausePopup");
            Assert.IsNotNull(pausePopup, "Pause popup should re-open.");
            AssertToggleState(pausePopup, "Dialog/Settings/MusicRow/ToggleButton/Label", "关");
            AssertToggleState(pausePopup, "Dialog/Settings/SfxRow/ToggleButton/Label", "关");
            AssertToggleState(pausePopup, "Dialog/Settings/VibrationRow/ToggleButton/Label", "开");

            var confirmExitButton = pausePopup.Find("Dialog/MainButtons/ExitButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmExitButton, "Pause popup exit button was not found.");
            confirmExitButton.onClick.Invoke();
            yield return null;

            var settlementPopup = gameRoot.transform.Find("PopupHost/SnakeSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Exit should open a reward settlement popup before returning to hall.");

            var duplicateBackHallButton = settlementPopup.Find("Dialog/BackHallButton")?.gameObject;
            Assert.IsNotNull(duplicateBackHallButton, "Settlement secondary back hall button should exist.");
            Assert.IsFalse(duplicateBackHallButton.activeSelf, "Exit settlement should not show a duplicate back hall button.");
            var settlementConfirmButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(settlementConfirmButton, "Settlement back hall button was not found after exiting.");
            settlementConfirmButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(controller.HasActiveGame, "Game should be disposed after confirming settlement.");
            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after leaving the snake game.");
        }

        [UnityTest]
        public IEnumerator CanWrapAroundWalls()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            ForceWrapAroundState(runtime);

            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;

            var snake = SnapshotSnake(runtime);
            var head = snake[snake.Count - 1];
            Assert.AreEqual(new Vector2Int(0, 9), head, "Snake should wrap from the right wall to the left side.");
            Assert.IsTrue(controller.HasActiveGame, "Wrapping through the wall should not end the game.");
        }

        [UnityTest]
        public IEnumerator CanSettleAfterSelfCollision()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            ForceSelfCollisionState(runtime);

            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;

            var gameRoot = GameObject.Find("SnakeView");
            Assert.IsNotNull(gameRoot, "Snake shell root was not found.");
            var settlementPopup = gameRoot.transform.Find("PopupHost/SnakeSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Self collision should show the settlement popup.");

            var confirmButton = settlementPopup.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Settlement confirm button was not found.");
            confirmButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(controller.HasActiveGame, "Game should be disposed after confirming settlement.");
            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after settlement.");
        }

        [UnityTest]
        public IEnumerator PauseButtonRemainsVisibleAfterWin()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(SnakeGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var gameRoot = GameObject.Find("SnakeView");
            Assert.IsNotNull(gameRoot, "Snake shell root was not found.");
            var pauseButton = gameRoot.transform.Find("PauseButton")?.gameObject;
            Assert.IsNotNull(pauseButton, "PauseButton was not found.");
            Assert.IsTrue(pauseButton.activeSelf, "PauseButton should be visible during normal gameplay.");

            InvokePrivateVoid(runtime, "HandleWin");
            yield return null;

            Assert.IsTrue(pauseButton.activeSelf, "PauseButton should remain visible after winning.");
        }

        private static IEnumerator LoadController(Action<MiniGameAppController> onLoaded)
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
            onLoaded(controller);
        }

        private static SnakeGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as SnakeGameView;
            Assert.IsNotNull(runtime, "Snake runtime was not created.");
            return runtime;
        }

        private static List<Vector2Int> SnapshotSnake(SnakeGameView runtime)
        {
            var field = typeof(SnakeGameView).GetField("snakeSegments", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access snakeSegments field.");
            var source = field.GetValue(runtime) as List<Vector2Int>;
            Assert.IsNotNull(source, "Snake list should exist.");
            return new List<Vector2Int>(source);
        }

        private static void SetFood(SnakeGameView runtime, Vector2Int cell)
        {
            var boardField = typeof(SnakeGameView).GetField("board", InstancePrivate);
            var foodField = typeof(SnakeGameView).GetField("foodCell", InstancePrivate);
            Assert.IsNotNull(boardField, "Failed to access board field.");
            Assert.IsNotNull(foodField, "Failed to access foodCell field.");

            var board = (int[,])boardField.GetValue(runtime);
            for (var row = 0; row < board.GetLength(0); row++)
            {
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    if (board[row, column] == 2)
                    {
                        board[row, column] = 0;
                    }
                }
            }

            board[cell.y, cell.x] = 2;
            foodField.SetValue(runtime, cell);
        }

        private static void ForceWrapAroundState(SnakeGameView runtime)
        {
            var boardField = typeof(SnakeGameView).GetField("board", InstancePrivate);
            var snakeField = typeof(SnakeGameView).GetField("snakeSegments", InstancePrivate);
            var directionField = typeof(SnakeGameView).GetField("currentDirection", InstancePrivate);
            var foodField = typeof(SnakeGameView).GetField("foodCell", InstancePrivate);

            Assert.IsNotNull(boardField, "Failed to access board field.");
            Assert.IsNotNull(snakeField, "Failed to access snakeSegments field.");
            Assert.IsNotNull(directionField, "Failed to access currentDirection field.");
            Assert.IsNotNull(foodField, "Failed to access foodCell field.");

            var board = (int[,])boardField.GetValue(runtime);
            for (var row = 0; row < board.GetLength(0); row++)
            {
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    board[row, column] = 0;
                }
            }

            var snake = snakeField.GetValue(runtime) as List<Vector2Int>;
            Assert.IsNotNull(snake, "Snake list should exist.");
            snake.Clear();

            var targetRow = Mathf.Max(0, board.GetLength(0) / 2);
            var headX = board.GetLength(1) - 1;
            var bodyX = Mathf.Max(0, headX - 1);

            snake.Add(new Vector2Int(bodyX, targetRow));
            snake.Add(new Vector2Int(headX, targetRow));

            board[targetRow, bodyX] = 1;
            board[targetRow, headX] = 1;
            board[0, 0] = 2;
            foodField.SetValue(runtime, new Vector2Int(0, 0));
            directionField.SetValue(runtime, SnakeDirection.Right);
        }

        private static void ForceSelfCollisionState(SnakeGameView runtime)
        {
            var boardField = typeof(SnakeGameView).GetField("board", InstancePrivate);
            var snakeField = typeof(SnakeGameView).GetField("snakeSegments", InstancePrivate);
            var directionField = typeof(SnakeGameView).GetField("currentDirection", InstancePrivate);
            var foodField = typeof(SnakeGameView).GetField("foodCell", InstancePrivate);

            Assert.IsNotNull(boardField, "Failed to access board field.");
            Assert.IsNotNull(snakeField, "Failed to access snakeSegments field.");
            Assert.IsNotNull(directionField, "Failed to access currentDirection field.");
            Assert.IsNotNull(foodField, "Failed to access foodCell field.");

            var board = (int[,])boardField.GetValue(runtime);
            for (var row = 0; row < board.GetLength(0); row++)
            {
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    board[row, column] = 0;
                }
            }

            var snake = snakeField.GetValue(runtime) as List<Vector2Int>;
            Assert.IsNotNull(snake, "Snake list should exist.");
            snake.Clear();

            snake.Add(new Vector2Int(6, 9));
            snake.Add(new Vector2Int(7, 9));
            snake.Add(new Vector2Int(8, 9));
            snake.Add(new Vector2Int(8, 10));
            snake.Add(new Vector2Int(7, 10));

            for (var i = 0; i < snake.Count; i++)
            {
                board[snake[i].y, snake[i].x] = 1;
            }

            board[0, 0] = 2;
            foodField.SetValue(runtime, new Vector2Int(0, 0));
            directionField.SetValue(runtime, SnakeDirection.Up);
        }

        private static void ClickButton(string buttonName)
        {
            var target = FindButton(buttonName);
            Assert.IsNotNull(target, "Could not find button: " + buttonName);

            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void ClickPopupButton(Transform popup, string path)
        {
            var target = popup.Find(path)?.GetComponent<Button>();
            Assert.IsNotNull(target, "Could not find popup button: " + path);
            target.onClick.Invoke();
        }

        private static void AssertToggleState(Transform popup, string path, string expected)
        {
            var label = popup.Find(path)?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(label, "Could not find toggle label: " + path);
            var text = label.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public)?.GetValue(label) as string;
            Assert.IsNotNull(text, "Toggle label text property was not found: " + path);
            Assert.AreEqual(expected, text, "Unexpected toggle state at path: " + path);
        }

        private static Button FindButton(string buttonName)
        {
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == buttonName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static int GetIntField(SnakeGameView runtime, string fieldName)
        {
            var field = typeof(SnakeGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (int)field.GetValue(runtime);
        }

        private static float GetFloatField(SnakeGameView runtime, string fieldName)
        {
            var field = typeof(SnakeGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (float)field.GetValue(runtime);
        }

        private static int CountTransientEffects(SnakeGameView runtime, string namePrefix)
        {
            var field = typeof(SnakeGameView).GetField("transientEffects", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access transientEffects field.");
            var list = field.GetValue(runtime) as List<GameObject>;
            Assert.IsNotNull(list, "Transient effects list should exist.");

            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void InvokePrivateVoid(SnakeGameView runtime, string methodName)
        {
            var method = typeof(SnakeGameView).GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            method.Invoke(runtime, null);
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
