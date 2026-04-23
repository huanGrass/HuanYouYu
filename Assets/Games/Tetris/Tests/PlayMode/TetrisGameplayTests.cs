using System;
using System.Collections;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public sealed class TetrisGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator TetrisCanEnterFromControllerAndBuildsCoreUi()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameTetrisView.GameIdConstant);
            yield return null;

            var root = GameObject.Find("GameTetrisView");
            Assert.IsNotNull(root, "Tetris shell root should be created.");
            var boardHost = root.transform.Find("ContentHost/TetrisBoardHost");
            Assert.IsNotNull(boardHost, "Tetris board host should be created.");
            Assert.IsNull(boardHost.GetComponent<RoundedRectGraphic>(), "Tetris board host should use square corners.");
            var boardGrid = root.transform.Find("ContentHost/TetrisBoardHost/Grid") as RectTransform;
            Assert.IsNotNull(boardGrid, "Tetris board grid should be created.");
            Assert.AreEqual(Vector2.zero, boardGrid.offsetMin, "Tetris board grid should touch the board bottom and left edge.");
            Assert.AreEqual(Vector2.zero, boardGrid.offsetMax, "Tetris board grid should touch the board top and right edge.");
            Assert.IsNotNull(root.transform.Find("ContentHost/TetrisSidePanel/PreviewGrid"), "Tetris next-piece preview should be created.");
            var rotateButton = root.transform.Find("BottomHost/TetrisBottom/RotateButton") as RectTransform;
            var hardDropButton = root.transform.Find("BottomHost/TetrisBottom/HardDropButton") as RectTransform;
            Assert.IsNotNull(rotateButton, "Tetris controls should include a standalone rotate button.");
            Assert.IsNotNull(hardDropButton, "Tetris controls should include a standalone hard-drop button.");
            Assert.Greater(rotateButton.anchoredPosition.x, 0f, "Standalone rotate button should sit on the right side.");
            Assert.Greater(hardDropButton.anchoredPosition.x, 0f, "Standalone hard-drop button should sit on the right side.");
            Assert.IsNotNull(root.transform.Find("BottomHost/TetrisBottom/DirectionPad/DownButton"), "Tetris controls should include soft drop.");

            var bottomHost = root.transform.Find("BottomHost") as RectTransform;
            var directionPad = root.transform.Find("BottomHost/TetrisBottom/DirectionPad") as RectTransform;
            Assert.IsNotNull(bottomHost, "Tetris bottom host should exist.");
            Assert.IsNotNull(directionPad, "Tetris direction pad should exist.");
            Assert.Less(directionPad.anchoredPosition.x, 0f, "Tetris direction pad should sit on the left side.");
            Assert.Less(Mathf.Abs(directionPad.anchoredPosition.x), 160f, "Tetris direction pad should stay near the center-left area.");
            Assert.Less(rotateButton.anchoredPosition.x, 180f, "Standalone rotate button should stay near the center-right area.");
            Assert.Less(hardDropButton.anchoredPosition.x, 180f, "Standalone hard-drop button should stay near the center-right area.");
            Assert.GreaterOrEqual(bottomHost.rect.height, 244f, "Tetris bottom area should fit the full direction pad.");
        }

        [UnityTest]
        public IEnumerator TetrisDirectionPadUpButtonRotatesPiece()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameTetrisView.GameIdConstant);
            yield return null;

            var root = GameObject.Find("GameTetrisView");
            var upButton = root.transform.Find("BottomHost/TetrisBottom/DirectionPad/UpButton").GetComponent<Button>();
            Assert.IsNotNull(upButton, "Tetris direction pad up button should exist.");

            var runtime = GetActiveGame(controller);
            SetPrivateField(runtime, "currentPiece", 0);
            SetPrivateField(runtime, "rotation", 0);
            SetPrivateField(runtime, "piecePosition", new Vector2Int(3, 10));

            upButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, GetPrivateField<int>(runtime, "rotation"), "Direction pad up button should rotate the current piece.");
        }

        [UnityTest]
        public IEnumerator TetrisHardDropButtonLocksPieceInsteadOfRotating()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameTetrisView.GameIdConstant);
            yield return null;

            var root = GameObject.Find("GameTetrisView");
            var hardDropButton = root.transform.Find("BottomHost/TetrisBottom/HardDropButton").GetComponent<Button>();
            Assert.IsNotNull(hardDropButton, "Tetris hard-drop button should exist.");

            var runtime = GetActiveGame(controller);
            var lockedCells = GetPrivateField<int[,]>(runtime, "lockedCells");
            Array.Clear(lockedCells, 0, lockedCells.Length);
            SetPrivateField(runtime, "currentPiece", 0);
            SetPrivateField(runtime, "nextPiece", 0);
            SetPrivateField(runtime, "rotation", 0);
            SetPrivateField(runtime, "piecePosition", new Vector2Int(3, 10));

            hardDropButton.onClick.Invoke();
            yield return null;

            Assert.Greater(CountLockedCells(lockedCells), 0, "Hard-drop button should drop and lock the current piece.");
        }

        [UnityTest]
        public IEnumerator TetrisClearsCompletedLineAndUpdatesScore()
        {
            ResetProgress();

            MiniGameAppController controller = null;
            yield return LoadController(value => controller = value);

            controller.EnterGame(GameTetrisView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var lockedCells = GetPrivateField<int[,]>(runtime, "lockedCells");
            for (var x = 0; x < lockedCells.GetLength(0); x++)
            {
                lockedCells[x, 0] = 1;
            }

            var cleared = (int)GetPrivateMethod(runtime, "ClearCompletedLines").Invoke(runtime, null);
            Assert.AreEqual(1, cleared, "A filled Tetris row should be cleared.");
            for (var x = 0; x < lockedCells.GetLength(0); x++)
            {
                Assert.AreEqual(0, lockedCells[x, 0], "Cleared Tetris row should become empty.");
            }
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

        private static GameTetrisView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as GameTetrisView;
            Assert.IsNotNull(runtime, "Tetris runtime was not created.");
            return runtime;
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            field.SetValue(target, value);
        }

        private static int CountLockedCells(int[,] lockedCells)
        {
            var count = 0;
            for (var x = 0; x < lockedCells.GetLength(0); x++)
            {
                for (var y = 0; y < lockedCells.GetLength(1); y++)
                {
                    if (lockedCells[x, y] != 0)
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }

        private static MethodInfo GetPrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            return method;
        }
    }
}
