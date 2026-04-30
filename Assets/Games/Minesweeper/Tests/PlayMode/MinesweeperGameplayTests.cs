using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace HuanYouYu.MiniGameHall.Tests
{
    public class MinesweeperGameplayTests
    {
        private GameObject rootObject;
        private GameObject hostObject;
        private GameMinesweeperView gameView;
        private MiniGameSettlement completedSettlement;

        [SetUp]
        public void SetUp()
        {
            completedSettlement = null;
            rootObject = new GameObject("RootCanvas", typeof(RectTransform), typeof(Canvas));
            hostObject = new GameObject("Host", typeof(TestHostBehaviour));
            var host = hostObject.GetComponent<TestHostBehaviour>();
            gameView = new GameMinesweeperView(
                host,
                rootObject.transform,
                settlement => completedSettlement = settlement,
                null);
        }

        [TearDown]
        public void TearDown()
        {
            if (gameView != null)
            {
                gameView.Dispose();
                gameView = null;
            }

            if (hostObject != null)
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }

            if (rootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void InitializesHudButtonsAndBoard()
        {
            AssertTextContains("Title", "扫雷");
            AssertTextContains("Score", "10");
            Assert.IsNotNull(FindPath<Button>("BottomHost/MinesweeperBottom/ActionBar/RestartButton"));
            Assert.IsNotNull(FindPath<Image>("BottomHost/MinesweeperBottom/ActionBar/RestartButton/Icon"));
            Assert.IsNotNull(FindPath<Button>("BottomHost/MinesweeperBottom/ActionBar/ModeButtonHost/ModeButton"));
            Assert.AreEqual(160f, FindPath<RectTransform>("BottomHost/MinesweeperBottom/ActionBar/ModeButtonHost").rect.width, 0.01f);
            Assert.AreEqual(81, rootObject.transform.Find("GameMinesweeperView/ContentHost/BoardRoot/BoardPanel/BoardGrid").childCount);
        }

        [Test]
        public void FirstRevealBuildsBoardAndKeepsFirstCellSafe()
        {
            ConfigureBoardForTests(new[]
            {
                new Vector2Int(1, 1), new Vector2Int(8, 0), new Vector2Int(8, 1), new Vector2Int(8, 2), new Vector2Int(8, 3),
                new Vector2Int(8, 4), new Vector2Int(8, 5), new Vector2Int(8, 6), new Vector2Int(8, 7), new Vector2Int(8, 8)
            });

            ClickCell(0, 0);

            Assert.IsTrue(GetField<bool>("isBoardGenerated"));
            Assert.IsTrue(GetCellBool(0, 0, "IsRevealed"));
            Assert.IsFalse(GetCellBool(0, 0, "HasMine"));
            Assert.IsNull(completedSettlement);
        }

        [Test]
        public void FlagModeTogglesFlagAndHudCount()
        {
            ClickModeButton();
            ClickCell(2, 3);
            Assert.IsTrue(GetCellBool(2, 3, "IsFlagged"));
            AssertTextContains("Score", "9");

            ClickCell(2, 3);
            Assert.IsFalse(GetCellBool(2, 3, "IsFlagged"));
            AssertTextContains("Score", "10");
        }

        [Test]
        public void EmptyCellRevealExpandsNeighbors()
        {
            ConfigureBoardForTests(new[]
            {
                new Vector2Int(6, 6), new Vector2Int(7, 6), new Vector2Int(8, 6), new Vector2Int(6, 7), new Vector2Int(7, 7),
                new Vector2Int(8, 7), new Vector2Int(6, 8), new Vector2Int(7, 8), new Vector2Int(8, 8), new Vector2Int(5, 8)
            });

            ClickCell(0, 0);

            Assert.Greater(GetField<int>("revealedSafeCellCount"), 1);
            Assert.IsTrue(GetCellBool(0, 0, "IsRevealed"));
            Assert.IsTrue(GetCellBool(4, 4, "IsRevealed"));
        }

        [Test]
        public void ExitSettlementAccumulatesRewardsAcrossRetries()
        {
            ConfigureBoardForTests(new[]
            {
                new Vector2Int(1, 1), new Vector2Int(8, 0), new Vector2Int(8, 1), new Vector2Int(8, 2), new Vector2Int(8, 3),
                new Vector2Int(8, 4), new Vector2Int(8, 5), new Vector2Int(8, 6), new Vector2Int(8, 7), new Vector2Int(7, 8)
            });

            ClickCell(0, 0);
            ClickCell(1, 1);

            var firstSettlement = InvokeSettlementBuilder();
            Assert.AreEqual(1, firstSettlement.Score);
            Assert.AreEqual(2, firstSettlement.CoinCount);
            Assert.AreEqual(0, firstSettlement.ChestCount);

            ClickPath("PopupHost/MinesweeperSettlementPanel/Dialog/NextButton");

            ConfigureBoardForTests(new[]
            {
                new Vector2Int(1, 1), new Vector2Int(8, 0), new Vector2Int(8, 1), new Vector2Int(8, 2), new Vector2Int(8, 3),
                new Vector2Int(8, 4), new Vector2Int(8, 5), new Vector2Int(8, 6), new Vector2Int(8, 7), new Vector2Int(7, 8)
            });

            ClickCell(0, 0);
            ClickCell(1, 1);

            var finalSettlement = InvokeSettlementBuilder();
            Assert.AreEqual(3, finalSettlement.Score);
            Assert.AreEqual(6, finalSettlement.CoinCount);
            Assert.AreEqual(0, finalSettlement.ChestCount);
            StringAssert.Contains("6", finalSettlement.Summary);
        }

        [Test]
        public void RestartResetsGeneratedBoardFlagsAndRevealState()
        {
            ConfigureBoardForTests(new[]
            {
                new Vector2Int(8, 0), new Vector2Int(8, 1), new Vector2Int(8, 2), new Vector2Int(8, 3), new Vector2Int(8, 4),
                new Vector2Int(8, 5), new Vector2Int(8, 6), new Vector2Int(8, 7), new Vector2Int(8, 8), new Vector2Int(7, 8)
            });

            ClickModeButton();
            ClickCell(0, 0);
            ClickModeButton();
            ClickCell(1, 0);
            ClickPath("BottomHost/MinesweeperBottom/ActionBar/RestartButton");

            Assert.IsFalse(GetField<bool>("isBoardGenerated"));
            Assert.AreEqual(0, GetField<int>("revealedSafeCellCount"));
            Assert.AreEqual(0, GetField<int>("flaggedCellCount"));
            Assert.IsFalse(GetCellBool(0, 0, "IsFlagged"));
            Assert.IsFalse(GetCellBool(1, 0, "IsRevealed"));
            AssertTextContains("Score", "10");
        }

        [Test]
        public void PauseButtonOpensPausePopup()
        {
            ClickPath("PauseButton");

            Assert.IsNotNull(FindPath<Button>("PopupHost/MiniGamePausePopup/Dialog/HelpButton"), "Pause popup should appear after clicking pause.");
        }

        private void ConfigureBoardForTests(Vector2Int[] mines)
        {
            var cells = (Array)GetFieldObject("cells");
            for (var y = 0; y < 9; y++)
            {
                for (var x = 0; x < 9; x++)
                {
                    var cell = cells.GetValue(x, y);
                    SetCellField(cell, "HasMine", false);
                    SetCellField(cell, "IsRevealed", false);
                    SetCellField(cell, "IsFlagged", false);
                    SetCellField(cell, "AdjacentMineCount", 0);
                }
            }

            foreach (var mine in mines)
            {
                SetCellBool(mine.x, mine.y, "HasMine", true);
            }

            SetField("isBoardGenerated", true);
            SetField("isGameOver", false);
            SetField("revealedSafeCellCount", 0);
            SetField("flaggedCellCount", 0);
            SetField("score", 0);
            SetField("explodedMineX", -1);
            SetField("explodedMineY", -1);
            SetField("interactionMode", Enum.Parse(GetFieldInfo("interactionMode").FieldType, "Reveal"));
            InvokePrivate("RecalculateAdjacentMineCounts");
            InvokePrivate("RefreshBoard");
            InvokePrivate("RefreshHud");
            InvokePrivate("RefreshModeButton");
        }

        private void AssertTextContains(string name, string expected)
        {
            var component = GameObject.Find(name)?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(component, "Missing TMP object: " + name);
            var property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "TMP text property missing: " + name);
            var value = property.GetValue(component) as string;
            StringAssert.Contains(expected, value ?? string.Empty);
        }

        private T FindPath<T>(string path) where T : Component
        {
            var transform = rootObject.transform.Find("GameMinesweeperView/" + path);
            return transform != null ? transform.GetComponent<T>() : null;
        }

        private void ClickModeButton()
        {
            ClickPath("BottomHost/MinesweeperBottom/ActionBar/ModeButtonHost/ModeButton");
        }

        private void ClickCell(int x, int y)
        {
            ClickPath("ContentHost/BoardRoot/BoardPanel/BoardGrid/Cell_" + x + "_" + y);
        }

        private void ClickPath(string path)
        {
            var button = FindPath<UnityEngine.UI.Button>(path);
            Assert.IsNotNull(button, "Missing button at path: " + path);
            button.onClick.Invoke();
        }

        private bool GetCellBool(int x, int y, string fieldName)
        {
            var cell = ((Array)GetFieldObject("cells")).GetValue(x, y);
            return (bool)GetCellField(cell, fieldName);
        }

        private void SetCellBool(int x, int y, string fieldName, bool value)
        {
            var cell = ((Array)GetFieldObject("cells")).GetValue(x, y);
            SetCellField(cell, fieldName, value);
        }

        private object GetCellField(object cell, string fieldName)
        {
            return cell.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(cell);
        }

        private void SetCellField(object cell, string fieldName, object value)
        {
            cell.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(cell, value);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)GetFieldInfo(fieldName).GetValue(gameView);
        }

        private object GetFieldObject(string fieldName)
        {
            return GetFieldInfo(fieldName).GetValue(gameView);
        }

        private void SetField(string fieldName, object value)
        {
            GetFieldInfo(fieldName).SetValue(gameView, value);
        }

        private FieldInfo GetFieldInfo(string fieldName)
        {
            return typeof(GameMinesweeperView).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void InvokePrivate(string methodName)
        {
            typeof(GameMinesweeperView).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gameView, null);
        }

        private MiniGameSettlement InvokeSettlementBuilder()
        {
            return typeof(GameMinesweeperView)
                .GetMethod("BuildSessionSettlementForExit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(gameView, null) as MiniGameSettlement;
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
