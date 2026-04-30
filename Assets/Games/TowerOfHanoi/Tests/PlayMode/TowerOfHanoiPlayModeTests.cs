using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests
{
    public sealed class TowerOfHanoiPlayModeTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private GameObject hostObject;
        private MiniGameAppController controller;
        private MiniGameTowerOfHanoiGameView game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Cleanup();
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();
            hostObject = new GameObject("TowerOfHanoiTestHost");
            controller = hostObject.AddComponent<MiniGameAppController>();
            yield return null;
            controller.EnterGame(MiniGameTowerOfHanoiGameView.GameIdConstant);
            yield return null;
            game = GetActiveTowerOfHanoiGame(controller);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Cleanup();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialLevelCreatesThreeColumnsAndThreeDisks()
        {
            yield return null;

            Assert.IsNotNull(GameObject.Find("TowerOfHanoiBoard"));
            Assert.IsNotNull(GameObject.Find("TowerColumn_0"));
            Assert.IsNotNull(GameObject.Find("TowerColumn_1"));
            Assert.IsNotNull(GameObject.Find("TowerColumn_2"));
            Assert.IsNotNull(GameObject.Find("Disk_1"));
            Assert.IsNotNull(GameObject.Find("Disk_2"));
            Assert.IsNotNull(GameObject.Find("Disk_3"));

            var columns = GetColumns(game);
            Assert.AreEqual(3, columns[0].Count);
            Assert.AreEqual(0, columns[1].Count);
            Assert.AreEqual(0, columns[2].Count);

            var disk1Rect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            var disk2Rect = GameObject.Find("Disk_2").GetComponent<RectTransform>();
            Assert.Less(disk1Rect.anchoredPosition.y - disk2Rect.anchoredPosition.y, disk1Rect.sizeDelta.y);
        }

        [UnityTest]
        public IEnumerator LegalMoveIncrementsMoveCountAndUpdatesColumns()
        {
            InvokeColumnClick(game, 0);
            InvokeColumnClick(game, 2);
            yield return null;

            var columns = GetColumns(game);
            Assert.AreEqual(1, GetIntField(game, "moveCount"));
            Assert.AreEqual(2, columns[0].Count);
            Assert.AreEqual(1, columns[2].Count);
            Assert.AreEqual(1, columns[2][columns[2].Count - 1]);
        }

        [UnityTest]
        public IEnumerator SelectingColumnLiftsTopDiskWithoutColumnSelectionBackground()
        {
            var diskRect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            var originalPosition = diskRect.anchoredPosition;
            var highlight = FindChildComponent<RoundedRectGraphic>(GameObject.Find("TowerColumn_0").transform, "Highlight");
            Assert.IsNotNull(highlight);

            InvokeColumnClick(game, 0);
            yield return null;

            Assert.Greater(diskRect.anchoredPosition.y, originalPosition.y);
            Assert.AreEqual(Color.clear, highlight.color);

            InvokeColumnClick(game, 0);
            yield return null;

            Assert.AreEqual(originalPosition.x, diskRect.anchoredPosition.x, 0.01f);
            Assert.AreEqual(originalPosition.y, diskRect.anchoredPosition.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator DragMoveSnapsDiskToDestinationStack()
        {
            var diskRect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            diskRect.anchoredPosition = new Vector2(220f, 80f);
            InvokeMove(game, 0, 2, false);
            yield return null;

            Assert.AreEqual(220f, diskRect.anchoredPosition.x, 0.01f);
            Assert.Less(diskRect.anchoredPosition.y, -190f);
        }

        [UnityTest]
        public IEnumerator DragEndSnapsDiskToDestinationStackNextFrame()
        {
            var diskRect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            InvokeBeginDrag(game, 1);
            diskRect.anchoredPosition = new Vector2(220f, 520f);
            InvokeEndDrag(game, 1);
            yield return null;

            Assert.AreEqual(220f, diskRect.anchoredPosition.x, 0.01f);
            Assert.Less(diskRect.anchoredPosition.y, -190f);
        }

        [UnityTest]
        public IEnumerator TickFallbackCompletesDragWhenEndDragIsMissing()
        {
            var diskRect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            InvokeBeginDrag(game, 1);
            diskRect.anchoredPosition = new Vector2(220f, 520f);
            game.Tick(0.016f);
            game.Tick(0.016f);
            yield return null;

            Assert.AreEqual(220f, diskRect.anchoredPosition.x, 0.01f);
            Assert.Less(diskRect.anchoredPosition.y, -190f);
        }

        [UnityTest]
        public IEnumerator ColumnAreaDragMovesTopDisk()
        {
            var diskRect = GameObject.Find("Disk_1").GetComponent<RectTransform>();
            var columnTarget = FindTowerInputTarget("TowerColumn_0");
            InvokeInputTargetDragMethod(columnTarget, "OnBeginDrag");
            diskRect.anchoredPosition = new Vector2(220f, 520f);
            InvokeInputTargetDragMethod(columnTarget, "OnEndDrag");
            yield return null;

            var columns = GetColumns(game);
            Assert.AreEqual(2, columns[0].Count);
            Assert.AreEqual(1, columns[2].Count);
            Assert.AreEqual(1, columns[2][columns[2].Count - 1]);
            Assert.AreEqual(220f, diskRect.anchoredPosition.x, 0.01f);
            Assert.Less(diskRect.anchoredPosition.y, -190f);
        }

        [UnityTest]
        public IEnumerator IllegalLargerDiskOntoSmallerDiskDoesNotIncrementMoveCount()
        {
            InvokeColumnClick(game, 0);
            InvokeColumnClick(game, 2);
            InvokeColumnClick(game, 0);
            InvokeColumnClick(game, 2);
            yield return null;

            var columns = GetColumns(game);
            Assert.AreEqual(1, GetIntField(game, "moveCount"));
            Assert.AreEqual(2, columns[0].Count);
            Assert.AreEqual(1, columns[2].Count);
        }

        [UnityTest]
        public IEnumerator CompletingFirstLevelShowsSettlementAfterMoveAnimationAndUnlocksNextLevel()
        {
            Move(0, 2);
            Move(0, 1);
            Move(2, 1);
            Move(0, 2);
            Move(1, 0);
            Move(1, 2);
            Move(0, 2);
            yield return null;

            var progress = controller.GetProgress(MiniGameTowerOfHanoiGameView.GameIdConstant);
            Assert.AreEqual(7, GetIntField(game, "moveCount"));
            Assert.AreEqual(2, progress.UnlockedLevelCount);
            Assert.IsNull(GameObject.Find("TowerOfHanoiWinSettlementPanel"));

            yield return new WaitForSeconds(0.35f);

            Assert.IsNotNull(GameObject.Find("TowerOfHanoiWinSettlementPanel"));
        }

        [UnityTest]
        public IEnumerator RestartButtonResetsCurrentLevelState()
        {
            Move(0, 2);
            yield return null;

            var restartButton = FindChildComponent<Button>(hostObject.transform, "RestartButton");
            Assert.IsNotNull(restartButton);
            restartButton.onClick.Invoke();
            yield return null;

            var columns = GetColumns(game);
            Assert.AreEqual(0, GetIntField(game, "moveCount"));
            Assert.AreEqual(3, columns[0].Count);
            Assert.AreEqual(0, columns[1].Count);
            Assert.AreEqual(0, columns[2].Count);
        }

        [Test]
        public void DropColumnUsesColumnHitArea()
        {
            var method = typeof(MiniGameTowerOfHanoiGameView).GetMethod("FindDropColumn", StaticPrivate, null, new[] { typeof(Vector2) }, null);
            Assert.IsNotNull(method);

            Assert.AreEqual(0, method.Invoke(null, new object[] { new Vector2(-220f, 0f) }));
            Assert.AreEqual(1, method.Invoke(null, new object[] { Vector2.zero }));
            Assert.AreEqual(2, method.Invoke(null, new object[] { new Vector2(220f, 0f) }));
            Assert.AreEqual(2, method.Invoke(null, new object[] { new Vector2(220f, 520f) }));
            Assert.AreEqual(-1, method.Invoke(null, new object[] { new Vector2(-110f, 0f) }));
        }

        [Test]
        public void LargestDiskFitsInsideBoardOnSideColumns()
        {
            var getDiskWidthMethod = typeof(MiniGameTowerOfHanoiGameView).GetMethod("GetDiskWidth", StaticPrivate);
            var getColumnXMethod = typeof(MiniGameTowerOfHanoiGameView).GetMethod("GetColumnX", StaticPrivate);
            var maxDiskCountField = typeof(MiniGameTowerOfHanoiGameView).GetField("MaxDiskCount", StaticPrivate);
            Assert.IsNotNull(getDiskWidthMethod);
            Assert.IsNotNull(getColumnXMethod);
            Assert.IsNotNull(maxDiskCountField);

            var largestDiskWidth = (float)getDiskWidthMethod.Invoke(null, new[] { maxDiskCountField.GetValue(null) });
            var leftColumnX = (float)getColumnXMethod.Invoke(null, new object[] { 0 });
            var rightColumnX = (float)getColumnXMethod.Invoke(null, new object[] { 2 });

            Assert.GreaterOrEqual(leftColumnX - largestDiskWidth * 0.5f, -340f);
            Assert.LessOrEqual(rightColumnX + largestDiskWidth * 0.5f, 340f);
        }

        private void Move(int fromColumn, int toColumn)
        {
            InvokeColumnClick(game, fromColumn);
            InvokeColumnClick(game, toColumn);
        }

        private static MiniGameTowerOfHanoiGameView GetActiveTowerOfHanoiGame(MiniGameAppController appController)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field);
            var value = field.GetValue(appController) as MiniGameTowerOfHanoiGameView;
            Assert.IsNotNull(value);
            return value;
        }

        private static void InvokeColumnClick(MiniGameTowerOfHanoiGameView view, int columnIndex)
        {
            var method = typeof(MiniGameTowerOfHanoiGameView).GetMethod("HandleColumnClicked", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(view, new object[] { columnIndex });
        }

        private static void InvokeMove(MiniGameTowerOfHanoiGameView view, int fromColumn, int toColumn, bool animateMovedDisk)
        {
            var method = typeof(MiniGameTowerOfHanoiGameView).GetMethod("TryMove", InstancePrivate);
            Assert.IsNotNull(method);
            Assert.AreEqual(true, method.Invoke(view, new object[] { fromColumn, toColumn, animateMovedDisk }));
        }

        private static void InvokeBeginDrag(MiniGameTowerOfHanoiGameView view, int diskSize)
        {
            var method = typeof(MiniGameTowerOfHanoiGameView).GetMethod("HandleBeginDrag", InstancePrivate);
            Assert.IsNotNull(method);
            Assert.AreEqual(true, method.Invoke(view, new object[] { diskSize, null }));
        }

        private static void InvokeEndDrag(MiniGameTowerOfHanoiGameView view, int diskSize)
        {
            var method = typeof(MiniGameTowerOfHanoiGameView).GetMethod("HandleEndDrag", InstancePrivate);
            Assert.IsNotNull(method);
            method.Invoke(view, new object[] { diskSize, null });
        }

        private static Component FindTowerInputTarget(string objectName)
        {
            var targetObject = GameObject.Find(objectName);
            Assert.IsNotNull(targetObject);
            var components = targetObject.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "TowerOfHanoiInputTarget")
                {
                    return components[i];
                }
            }

            Assert.Fail("Tower input target not found: " + objectName);
            return null;
        }

        private static void InvokeInputTargetDragMethod(Component target, string methodName)
        {
            Assert.IsNotNull(target);
            var method = target.GetType().GetMethod(methodName, InstancePrivate | BindingFlags.Public);
            Assert.IsNotNull(method);
            method.Invoke(target, new object[] { null });
        }

        private static List<int>[] GetColumns(MiniGameTowerOfHanoiGameView view)
        {
            var field = typeof(MiniGameTowerOfHanoiGameView).GetField("columns", InstancePrivate);
            Assert.IsNotNull(field);
            return (List<int>[])field.GetValue(view);
        }

        private static int GetIntField(MiniGameTowerOfHanoiGameView view, string fieldName)
        {
            var field = typeof(MiniGameTowerOfHanoiGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field);
            return (int)field.GetValue(view);
        }

        private static TComponent FindChildComponent<TComponent>(Transform root, string childName)
            where TComponent : Component
        {
            if (root == null)
            {
                return null;
            }

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName)
                {
                    return children[i].GetComponent<TComponent>();
                }
            }

            return null;
        }

        private static void Cleanup()
        {
            DestroyIfExists("TowerOfHanoiTestHost");
            DestroyIfExists("EventSystem");
        }

        private static void DestroyIfExists(string objectName)
        {
            var target = GameObject.Find(objectName);
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
