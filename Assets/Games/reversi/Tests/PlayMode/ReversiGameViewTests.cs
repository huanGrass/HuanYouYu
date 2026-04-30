using System;
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
    public class GameReversiViewTests
    {
        [UnityTest]
        public IEnumerator BootBuildsReversiBoardAndInitialState()
        {
            yield return LoadGameScene();

            Assert.IsNotNull(GameObject.Find("Status"), "Missing status label.");
            Assert.IsNotNull(GameObject.Find("Cell_0_0"), "Missing board cell.");
            Assert.IsNotNull(GameObject.Find("Cell_7_7"), "Missing board cell.");
            AssertInitialBoardState();
        }

        [UnityTest]
        public IEnumerator PlayerMoveFlipsPiecesAndAiResponds()
        {
            yield return LoadGameScene();

            ClickButton("Cell_2_3");
            yield return null;
            InvokePrivate("ExecuteAiTurn");
            yield return null;

            Assert.AreEqual("Black", GetBoardValue(2, 3));
            Assert.AreEqual(6, CountPieces("Black") + CountPieces("White"));
            Assert.AreEqual("Black", GetCurrentPlayer());
        }

        [UnityTest]
        public IEnumerator AutoPassAndRestartWork()
        {
            yield return LoadGameScene();

            FillBoard("Black");
            SetBoardValue(0, 0, "Empty");
            SetBoardValue(0, 1, "White");
            SetCurrentPlayer("White");
            SetGameOver(false);
            InvokePrivate("ResolveTurnState");
            yield return null;

            Assert.AreEqual("Black", GetCurrentPlayer());
            Assert.IsFalse(GetGameOver());
            StringAssert.Contains("无合法", GetLabelText("Status"));

            FillBoard("Black");
            SetCurrentPlayer("White");
            SetGameOver(false);
            InvokePrivate("ResolveTurnState");
            yield return null;

            Assert.IsTrue(GetGameOver());
            Assert.IsNotNull(GameObject.Find("ReversiSettlementPanel"), "Settlement popup should appear when both sides cannot move.");

            ClickButton("RestartButton");
            yield return null;
            AssertInitialBoardState();
        }

        private static IEnumerator LoadGameScene()
        {
            var load = SceneManager.LoadSceneAsync("SampleScene");
            while (!load.isDone)
            {
                yield return null;
            }

            MiniGameAppController controller = null;
            for (var i = 0; i < 30; i++)
            {
                controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
                if (controller != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.IsNotNull(controller, "Missing MiniGameAppController.");
            controller.EnterGame(GameReversiView.GameIdConstant);
            yield return WaitFrames(10);
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private static void AssertInitialBoardState()
        {
            Assert.AreEqual(2, CountPieces("Black"));
            Assert.AreEqual(2, CountPieces("White"));
            Assert.AreEqual("White", GetBoardValue(3, 3));
            Assert.AreEqual("Black", GetBoardValue(3, 4));
            Assert.AreEqual("Black", GetBoardValue(4, 3));
            Assert.AreEqual("White", GetBoardValue(4, 4));
            Assert.AreEqual("Black", GetCurrentPlayer());
        }

        private static int CountPieces(string pieceName)
        {
            var board = GetBoard();
            var count = 0;
            for (var row = 0; row < board.GetLength(0); row++)
            {
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    if (board.GetValue(row, column).ToString() == pieceName)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void FillBoard(string pieceName)
        {
            var board = GetBoard();
            for (var row = 0; row < board.GetLength(0); row++)
            {
                for (var column = 0; column < board.GetLength(1); column++)
                {
                    SetBoardValue(row, column, pieceName);
                }
            }
        }

        private static string GetBoardValue(int row, int column)
        {
            return GetBoard().GetValue(row, column).ToString();
        }

        private static void SetBoardValue(int row, int column, string pieceName)
        {
            var board = GetBoard();
            var elementType = board.GetType().GetElementType();
            board.SetValue(Enum.Parse(elementType, pieceName), row, column);
        }

        private static Array GetBoard()
        {
            return (Array)GetField("board").GetValue(GetActiveGame());
        }

        private static string GetCurrentPlayer()
        {
            return GetField("currentPlayer").GetValue(GetActiveGame()).ToString();
        }

        private static void SetCurrentPlayer(string pieceName)
        {
            var field = GetField("currentPlayer");
            field.SetValue(GetActiveGame(), Enum.Parse(field.FieldType, pieceName));
        }

        private static bool GetGameOver()
        {
            return (bool)GetField("isGameOver").GetValue(GetActiveGame());
        }

        private static void SetGameOver(bool value)
        {
            GetField("isGameOver").SetValue(GetActiveGame(), value);
        }

        private static void InvokePrivate(string methodName)
        {
            var method = GetActiveGame().GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method: " + methodName);
            method.Invoke(GetActiveGame(), null);
        }

        private static object GetActiveGame()
        {
            var controller = UnityEngine.Object.FindObjectOfType<MiniGameAppController>();
            Assert.IsNotNull(controller, "Missing MiniGameAppController.");

            var field = controller.GetType().GetField("activeGame", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing activeGame field.");

            var value = field.GetValue(controller);
            Assert.IsNotNull(value, "Active game not created.");
            return value;
        }

        private static string GetLabelText(string gameObjectName)
        {
            var labelObject = GameObject.Find(gameObjectName);
            Assert.IsNotNull(labelObject, "Missing label: " + gameObjectName);

            var label = labelObject.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(label, "Missing TextMeshProUGUI component: " + gameObjectName);

            var property = label.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing text property: " + gameObjectName);
            return property.GetValue(label, null).ToString();
        }

        private static FieldInfo GetField(string fieldName)
        {
            var field = GetActiveGame().GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            return field;
        }

        private static void ClickButton(string gameObjectName)
        {
            var button = GameObject.Find(gameObjectName)?.GetComponent<Button>();
            Assert.IsNotNull(button, "Missing button: " + gameObjectName);
            button.onClick.Invoke();
        }
    }
}




