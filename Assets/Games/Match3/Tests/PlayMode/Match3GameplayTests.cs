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
    public class Match3GameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float MinimumVisibleBusySeconds = 0.8f;

        [UnityTest]
        public IEnumerator CanEnterMatch3AndResolveOneSwap()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            Assert.AreEqual(Match3GameView.GameIdConstant, controller.ActiveGameId);

            var runtime = GetActiveGame(controller);
            var before = SnapshotBoard(runtime);

            Vector2Int first;
            Vector2Int second;
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(before, 7, 7, out first, out second), "The board should have at least one valid swap.");

            ClickTile(first.y, first.x);
            ClickTile(second.y, second.x);

            yield return WaitForBusy(runtime);
            yield return new WaitForSecondsRealtime(MinimumVisibleBusySeconds);
            Assert.IsTrue(IsBusy(runtime), "A valid swap animation should remain visible for a noticeable duration.");

            yield return WaitForGameReady(runtime);

            var after = SnapshotBoard(runtime);
            Assert.IsFalse(AreBoardsEqual(before, after), "A successful swap should change the board.");
            Assert.IsTrue(Match3BoardUtility.IsBoardFilled(after, 7, 7));
            Assert.IsFalse(Match3BoardUtility.HasAnyMatch(after, 7, 7));
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(after, 7, 7, out _, out _));
            Assert.Greater(GetScore(runtime), 0, "A successful swap should increase the score.");
        }

        [UnityTest]
        public IEnumerator CanResolveOneSwapBySwipe()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var before = SnapshotBoard(runtime);

            Vector2Int first;
            Vector2Int second;
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(before, 7, 7, out first, out second), "The board should have at least one valid swipe swap.");

            SwipeTile(first, second);

            yield return WaitForBusy(runtime);
            yield return WaitForGameReady(runtime);

            var after = SnapshotBoard(runtime);
            Assert.IsFalse(AreBoardsEqual(before, after), "A swipe swap should change the board.");
            Assert.Greater(GetScore(runtime), 0, "A swipe swap should increase the score.");
        }

        [UnityTest]
        public IEnumerator CanSwapInAnotherIdleRegionWhileFirstRegionResolves()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var board = BuildBoardWithIndependentSwaps();
            SetBoard(runtime, board);

            Vector2Int firstA;
            Vector2Int secondA;
            Vector2Int firstB;
            Vector2Int secondB;
            Assert.IsTrue(TryFindIndependentSwapPair(board, out firstA, out secondA, out firstB, out secondB), "The configured board should have two independent swap regions.");

            ClickTile(firstA.y, firstA.x);
            ClickTile(secondA.y, secondA.x);

            yield return WaitForBusy(runtime);

            Assert.IsTrue(IsTileInteractable(firstB.y, firstB.x), "The second region should stay clickable while the first region resolves.");
            Assert.IsTrue(IsTileInteractable(secondB.y, secondB.x), "The second region should stay clickable while the first region resolves.");

            ClickTile(firstB.y, firstB.x);
            ClickTile(secondB.y, secondB.x);

            yield return WaitForGameReady(runtime);

            Assert.GreaterOrEqual(GetScore(runtime), 360, "Two independent successful swaps should both be counted.");
        }

        [UnityTest]
        public IEnumerator Match3SettlementAddsCoinsAndChestsToHallProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var scoreField = typeof(Match3GameView).GetField("score", InstancePrivate);
            var clearedTileCountField = typeof(Match3GameView).GetField("clearedTileCount", InstancePrivate);
            Assert.IsNotNull(scoreField, "Failed to access score field.");
            Assert.IsNotNull(clearedTileCountField, "Failed to access clearedTileCount field.");
            scoreField.SetValue(runtime, 1800);
            clearedTileCountField.SetValue(runtime, 240);

            var settleMethod = typeof(Match3GameView).GetMethod("SettleAndReturn", InstancePrivate);
            Assert.IsNotNull(settleMethod, "Failed to access SettleAndReturn method.");
            settleMethod.Invoke(runtime, null);
            yield return null;

            var popup = GameObject.Find("Match3SettlementPanel");
            Assert.IsNotNull(popup, "Settlement popup should exist.");
            var primaryButton = popup.transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(primaryButton, "Settlement primary back hall button should exist.");
            var secondaryButton = popup.transform.Find("Dialog/BackHallButton")?.gameObject;
            Assert.IsNotNull(secondaryButton, "Settlement secondary back hall button should exist.");
            Assert.IsFalse(secondaryButton.activeSelf, "Exit settlement should not show a duplicate back hall button.");
            primaryButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(Match3GameView.GameIdConstant);
            Assert.AreEqual(240, progress.TotalCoinCount);
            Assert.AreEqual(2, progress.TotalChestCount);
        }

        [UnityTest]
        public IEnumerator SuccessfulSwapCleansTransientEffectsAndGhosts()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var before = SnapshotBoard(runtime);

            Vector2Int first;
            Vector2Int second;
            Assert.IsTrue(Match3BoardUtility.TryFindPossibleSwap(before, 7, 7, out first, out second), "The board should have at least one valid swap.");

            ClickTile(first.y, first.x);
            ClickTile(second.y, second.x);

            yield return WaitForObjectNamed("ClearShard", 3f);
            Assert.Greater(GetTransientEffectCount(runtime), 0, "Transient effect list should contain active effects while the animation is visible.");

            yield return WaitForGameReady(runtime);
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.Zero(CountNamedObjects("ClearShard"), "Clear shard effects should be cleaned up after resolution.");
            Assert.Zero(CountNamedObjects("ClearFlash"), "Clear flash overlays should be cleaned up after resolution.");
            Assert.Zero(CountNamedObjects("GhostIcon"), "Ghost icons should be cleaned up after resolution.");
            Assert.Zero(GetTransientEffectCount(runtime), "Transient effect list should be empty after cleanup.");
        }

        [UnityTest]
        public IEnumerator ComboPopupCanAppearAndClear()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(Match3GameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var method = typeof(Match3GameView).GetMethod("ShowComboPopup", InstancePrivate);
            Assert.IsNotNull(method, "Failed to access ShowComboPopup.");
            method.Invoke(runtime, new object[] { 2 });

            yield return WaitForObjectNamed("ComboPopup", 4f);
            Assert.Greater(CountNamedObjects("ComboPopup"), 0, "A cascade swap should show a combo popup.");
            Assert.Greater(GetTransientEffectCount(runtime), 0, "Transient effect list should contain the combo popup while it is visible.");

            yield return new WaitForSecondsRealtime(0.70f);

            Assert.Zero(CountNamedObjects("ComboPopup"), "Combo popup should be cleaned up after resolution.");
            Assert.Zero(GetTransientEffectCount(runtime), "Transient effect list should be empty after combo popup cleanup.");
        }

        private static IEnumerator LoadController(System.Action<MiniGameAppController> onLoaded)
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

        private static Match3GameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");

            var runtime = field.GetValue(controller) as Match3GameView;
            Assert.IsNotNull(runtime, "Match3 runtime was not created.");
            return runtime;
        }

        private static int[,] SnapshotBoard(Match3GameView runtime)
        {
            var field = typeof(Match3GameView).GetField("board", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access board field.");

            var source = (int[,])field.GetValue(runtime);
            var copy = new int[source.GetLength(0), source.GetLength(1)];
            for (var row = 0; row < source.GetLength(0); row++)
            {
                for (var column = 0; column < source.GetLength(1); column++)
                {
                    copy[row, column] = source[row, column];
                }
            }

            return copy;
        }

        private static IEnumerator WaitForGameReady(Match3GameView runtime)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!IsBusy(runtime))
                {
                    yield return null;
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Match3 game did not leave busy state in time.");
        }

        private static IEnumerator WaitForBusy(Match3GameView runtime)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (IsBusy(runtime))
                {
                    yield return null;
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Match3 game did not enter busy state in time.");
        }

        private static bool IsBusy(Match3GameView runtime)
        {
            var busyField = typeof(Match3GameView).GetField("isBusy", InstancePrivate);
            Assert.IsNotNull(busyField, "Failed to access isBusy field.");
            return (bool)busyField.GetValue(runtime);
        }

        private static int GetScore(Match3GameView runtime)
        {
            var field = typeof(Match3GameView).GetField("score", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access score field.");
            return (int)field.GetValue(runtime);
        }

        private static void SetBoard(Match3GameView runtime, int[,] source)
        {
            var boardField = typeof(Match3GameView).GetField("board", InstancePrivate);
            Assert.IsNotNull(boardField, "Failed to access board field.");

            var target = (int[,])boardField.GetValue(runtime);
            for (var row = 0; row < source.GetLength(0); row++)
            {
                for (var column = 0; column < source.GetLength(1); column++)
                {
                    target[row, column] = source[row, column];
                }
            }

            var refreshMethod = typeof(Match3GameView).GetMethod("RefreshAllTiles", InstancePrivate);
            Assert.IsNotNull(refreshMethod, "Failed to access RefreshAllTiles method.");
            refreshMethod.Invoke(runtime, null);
        }

        private static void ClickTile(int row, int column)
        {
            var tileName = string.Format("MatchTile_{0}_{1}", row, column);
            var target = FindTileButton(tileName);

            Assert.IsNotNull(target, "Could not find tile button: " + tileName);
            Assert.IsTrue(target.interactable, "Tile button should be interactable before click: " + tileName);

            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void SwipeTile(Vector2Int from, Vector2Int to)
        {
            var tileName = string.Format("MatchTile_{0}_{1}", from.y, from.x);
            var target = FindTileButton(tileName);

            Assert.IsNotNull(target, "Could not find tile button: " + tileName);
            Assert.IsTrue(target.interactable, "Tile button should be interactable before swipe: " + tileName);

            var delta = new Vector2(to.x - from.x, from.y - to.y) * 80f;
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -10,
                position = new Vector2(100f, 100f)
            };

            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerDownHandler);

            eventData.position += delta;
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.endDragHandler);
        }

        private static bool IsTileInteractable(int row, int column)
        {
            var button = FindTileButton(string.Format("MatchTile_{0}_{1}", row, column));
            Assert.IsNotNull(button, "Could not find tile button.");
            return button.interactable;
        }

        private static Button FindTileButton(string tileName)
        {
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == tileName)
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static bool AreBoardsEqual(int[,] first, int[,] second)
        {
            if (first.GetLength(0) != second.GetLength(0) || first.GetLength(1) != second.GetLength(1))
            {
                return false;
            }

            for (var row = 0; row < first.GetLength(0); row++)
            {
                for (var column = 0; column < first.GetLength(1); column++)
                {
                    if (first[row, column] != second[row, column])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerator WaitForObjectNamed(string objectName, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (CountNamedObjects(objectName) > 0)
                {
                    yield return null;
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Timed out waiting for object: " + objectName);
        }

        private static int CountNamedObjects(string objectName)
        {
            var transforms = Object.FindObjectsOfType<Transform>();
            var count = 0;
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static int GetTransientEffectCount(Match3GameView runtime)
        {
            var field = typeof(Match3GameView).GetField("transientEffects", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access transientEffects field.");
            var list = field.GetValue(runtime) as IList;
            Assert.IsNotNull(list, "transientEffects should exist.");
            return list.Count;
        }

        private static int[,] BuildBoardWithIndependentSwaps()
        {
            var board = new int[7, 7];
            for (var attempt = 0; attempt < 200; attempt++)
            {
                Random.InitState(1000 + attempt);
                Match3BoardUtility.FillBoard(board, 7, 7, 6);
                if (TryFindIndependentSwapPair(board, out _, out _, out _, out _))
                {
                    return board;
                }
            }

            Assert.Fail("Failed to build a board with two independent swap regions.");
            return null;
        }

        private static bool TryFindIndependentSwapPair(int[,] board, out Vector2Int firstA, out Vector2Int secondA, out Vector2Int firstB, out Vector2Int secondB)
        {
            var swaps = new List<SwapCandidate>();
            for (var row = 0; row < 7; row++)
            {
                for (var column = 0; column < 7; column++)
                {
                    var origin = new Vector2Int(column, row);
                    if (column + 1 < 7)
                    {
                        AddSwapCandidate(swaps, board, origin, new Vector2Int(column + 1, row));
                    }

                    if (row + 1 < 7)
                    {
                        AddSwapCandidate(swaps, board, origin, new Vector2Int(column, row + 1));
                    }
                }
            }

            for (var i = 0; i < swaps.Count; i++)
            {
                for (var j = i + 1; j < swaps.Count; j++)
                {
                    if (DoCellsOverlap(swaps[i].LockedCells, swaps[j].LockedCells))
                    {
                        continue;
                    }

                    firstA = swaps[i].First;
                    secondA = swaps[i].Second;
                    firstB = swaps[j].First;
                    secondB = swaps[j].Second;
                    return true;
                }
            }

            firstA = default(Vector2Int);
            secondA = default(Vector2Int);
            firstB = default(Vector2Int);
            secondB = default(Vector2Int);
            return false;
        }

        private static void AddSwapCandidate(List<SwapCandidate> swaps, int[,] board, Vector2Int first, Vector2Int second)
        {
            var plan = Match3BoardUtility.BuildResolvePlan(board, 7, 7, 6, first, second);
            if (!plan.IsValidSwap || plan.WasReshuffled)
            {
                return;
            }

            swaps.Add(new SwapCandidate
            {
                First = first,
                Second = second,
                LockedCells = Match3BoardUtility.CollectPlanLockedCells(plan, 7, 7, first, second)
            });
        }

        private static bool DoCellsOverlap(List<Vector2Int> first, List<Vector2Int> second)
        {
            for (var i = 0; i < first.Count; i++)
            {
                for (var j = 0; j < second.Count; j++)
                {
                    if (first[i] == second[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private sealed class SwapCandidate
        {
            public Vector2Int First;
            public Vector2Int Second;
            public List<Vector2Int> LockedCells;
        }
    }
}


