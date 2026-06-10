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
    public class ClassicLinkGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void LevelCatalogResourceContainsExpectedLevels()
        {
            var asset = Resources.Load<TextAsset>("Levels/classic-link.levels");
            Assert.IsNotNull(asset, "ClassicLink level catalog resource should exist.");

            var field = typeof(TapTreasureGameView).GetField("LevelDefinitions", StaticPrivate);
            Assert.IsNotNull(field, "ClassicLink level definitions field should exist.");

            var levels = field.GetValue(null) as Array;
            Assert.IsNotNull(levels, "ClassicLink level definitions should parse from the resource catalog.");
            Assert.AreEqual(100, levels.Length, "ClassicLink level catalog should contain generated 100 levels.");
            Assert.IsTrue(asset.text.Contains("\"cells\""), "ClassicLink catalog should define concrete board cells.");
            Assert.IsFalse(asset.text.Contains("\"offset\""), "ClassicLink catalog should not be an offset-only placeholder.");

            var cellsProperty = levels.GetValue(0).GetType().GetProperty("Cells", InstanceAny);
            Assert.IsNotNull(cellsProperty, "ClassicLink level definitions should expose parsed board cells.");

            var hasNonFullLevel = false;
            for (var i = 0; i < levels.Length; i++)
            {
                var cells = cellsProperty.GetValue(levels.GetValue(i), null) as int[,];
                Assert.IsNotNull(cells, "ClassicLink level cells should parse into a board matrix.");
                Assert.AreEqual(10, cells.GetLength(0));
                Assert.AreEqual(8, cells.GetLength(1));

                var board = new int[12, 10];
                var counts = new int[15];
                var nonEmptyCount = 0;
                for (var row = 0; row < 10; row++)
                {
                    for (var column = 0; column < 8; column++)
                    {
                        var value = cells[row, column];
                        Assert.That(value, Is.InRange(0, 14), "ClassicLink cell value should be a valid icon or empty.");
                        board[row + 1, column + 1] = value;
                        if (value == 0)
                        {
                            hasNonFullLevel = true;
                            continue;
                        }

                        counts[value] += 1;
                        nonEmptyCount += 1;
                    }
                }

                Assert.Greater(nonEmptyCount, 0, "ClassicLink level should contain playable tiles.");
                Assert.AreEqual(0, nonEmptyCount % 2, "ClassicLink level should contain an even tile count.");
                for (var value = 1; value < counts.Length; value++)
                {
                    Assert.AreEqual(0, counts[value] % 2, "ClassicLink icons should appear in pairs.");
                }

                Assert.Greater(ClassicLinkBoardUtility.CountAvailablePairs(board, 10, 8), 0, "ClassicLink level should start with a playable pair.");
            }

            Assert.IsTrue(hasNonFullLevel, "Generated ClassicLink levels should include shaped non-full boards.");
        }

        [UnityTest]
        public IEnumerator FirstEntryTutorialGuidesOnePlayableMatch()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;
            yield return null;

            var runtime = GetActiveGame(controller);
            var initialRemainingCount = CountRemainingTiles(runtime);
            Assert.IsNotNull(GameObject.Find("MiniGameTutorialOverlay"), "ClassicLink should show the tutorial overlay on first entry.");

            ClickButton("TargetClick");
            yield return null;

            Assert.IsTrue(GetNullableTileCoord(runtime, "selectedTile").HasValue, "First tutorial click should select the guided tile.");

            ClickButton("TargetClick");
            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.IsNull(GameObject.Find("MiniGameTutorialOverlay"), "ClassicLink tutorial should finish after the guided pair is clicked.");
            Assert.AreEqual(initialRemainingCount - 2, CountRemainingTiles(runtime), "Guided tutorial clicks should remove one playable pair.");
        }

        [UnityTest]
        public IEnumerator CanContinuePlayingAfterOneSuccessfulMatch()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var initialRemainingCount = CountRemainingTiles(runtime);
            var firstPair = FindConnectablePair(runtime);

            ClickTile(firstPair.firstRow, firstPair.firstColumn);
            ClickTile(firstPair.secondRow, firstPair.secondColumn);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.AreEqual(initialRemainingCount - 2, CountRemainingTiles(runtime), "After one successful match, exactly two tiles should be removed.");
            Assert.Greater(CountInteractableTileButtons(), 0, "Remaining tiles should still be clickable after the first match.");

            var secondPair = FindConnectablePair(runtime);
            ClickTile(secondPair.firstRow, secondPair.firstColumn);
            ClickTile(secondPair.secondRow, secondPair.secondColumn);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.AreEqual(initialRemainingCount - 4, CountRemainingTiles(runtime), "The board should continue accepting subsequent matches.");
            Assert.Greater(CountInteractableTileButtons(), 0, "The board should remain interactive after multiple matches.");
        }

        [UnityTest]
        public IEnumerator LevelSelectOpensAfterSuccessfulMatch()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var firstPair = FindConnectablePair(runtime);

            ClickTile(firstPair.firstRow, firstPair.firstColumn);
            ClickTile(firstPair.secondRow, firstPair.secondColumn);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            ClickButton("LevelSelectButton");
            yield return null;

            Assert.IsNotNull(GameObject.Find("ClassicLinkLevelSelectPanel"), "Level select panel should open after a successful match.");
        }

        [UnityTest]
        public IEnumerator RemovingTilesDoesNotCollapseGridPositions()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindStablePair(SnapshotBoard(runtime), runtime);
            var positionsBefore = SnapshotTilePositions();

            ClickTile(pair.firstRow, pair.firstColumn);
            ClickTile(pair.secondRow, pair.secondColumn);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            var positionsAfter = SnapshotTilePositions();
            foreach (var entry in positionsBefore)
            {
                Vector2 after;
                Assert.IsTrue(positionsAfter.TryGetValue(entry.Key, out after), "Missing tile after match: " + entry.Key);
                Assert.That(Vector2.Distance(entry.Value, after), Is.LessThan(0.01f), "Tile position changed unexpectedly: " + entry.Key);
            }
        }

        [UnityTest]
        public IEnumerator OuterBorderLinePointsStayAxisAligned()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);

            var topOuter = GetLinePoint(runtime, 0, 3);
            var topInner = GetLinePoint(runtime, 1, 3);
            var topOuterNext = GetLinePoint(runtime, 0, 4);
            var leftOuter = GetLinePoint(runtime, 4, 0);
            var leftInner = GetLinePoint(runtime, 4, 1);
            var bottomOuter = GetLinePoint(runtime, 11, 6);
            var bottomInner = GetLinePoint(runtime, 10, 6);

            Assert.That(Mathf.Abs(topOuter.x - topInner.x), Is.LessThan(0.01f), "Top border point should align vertically with the tile center.");
            Assert.That(Mathf.Abs(topOuter.y - topOuterNext.y), Is.LessThan(0.01f), "Top border points should share the same horizontal line.");
            Assert.That(Mathf.Abs(leftOuter.y - leftInner.y), Is.LessThan(0.01f), "Left border point should align horizontally with the tile center.");
            Assert.That(Mathf.Abs(bottomOuter.x - bottomInner.x), Is.LessThan(0.01f), "Bottom border point should align vertically with the tile center.");
        }

        [UnityTest]
        public IEnumerator SuccessfulMatchDoesNotReshuffleWhenBoardStillHasMoves()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var boardBefore = SnapshotBoard(runtime);
            var pair = FindStablePair(boardBefore, runtime);

            ClickTile(pair.firstRow, pair.firstColumn);
            ClickTile(pair.secondRow, pair.secondColumn);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            var boardAfter = SnapshotBoard(runtime);
            for (var row = 1; row <= 10; row++)
            {
                for (var column = 1; column <= 8; column++)
                {
                    var isRemovedTile =
                        (row == pair.firstRow && column == pair.firstColumn) ||
                        (row == pair.secondRow && column == pair.secondColumn);

                    if (isRemovedTile)
                    {
                        Assert.AreEqual(0, boardAfter[row, column], "Matched tiles should be cleared.");
                        continue;
                    }

                    Assert.AreEqual(
                        boardBefore[row, column],
                        boardAfter[row, column],
                        string.Format("Board should not reshuffle when another move still exists at ({0}, {1}).", row, column));
                }
            }
        }

        [UnityTest]
        public IEnumerator RestartingSameLevelKeepsInitialBoardLayout()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var boardBefore = SnapshotBoard(runtime);

            InvokePrivate(runtime, "ResetGame");
            yield return null;

            CollectionAssert.AreEqual(boardBefore, SnapshotBoard(runtime), "Restarting the same level should rebuild the same initial ClassicLink board.");
        }

        [UnityTest]
        public IEnumerator PauseButtonReturnsAfterContinuingToNextLevelFromWinSettlement()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var gameRoot = GameObject.Find("ClassicLinkView");
            Assert.IsNotNull(gameRoot, "ClassicLink root should exist.");

            var pauseButton = gameRoot.transform.Find("PauseButton")?.gameObject;
            Assert.IsNotNull(pauseButton, "PauseButton should exist.");
            Assert.IsTrue(pauseButton.activeSelf, "PauseButton should be visible during normal gameplay.");

            ClearPlayableBoard(runtime);
            InvokePrivate(runtime, "SettleAndReturn");
            yield return null;

            var settlementPanel = gameRoot.transform.Find("PopupHost/ClassicLinkSettlementPanel");
            Assert.IsNotNull(settlementPanel, "Win settlement panel should appear after clearing the board.");
            Assert.IsTrue(pauseButton.activeSelf, "PauseButton should remain visible on the win settlement panel.");
            var nextButton = settlementPanel.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next level button should exist on the win settlement panel.");

            nextButton.onClick.Invoke();
            yield return null;

            Assert.IsNull(gameRoot.transform.Find("PopupHost/ClassicLinkSettlementPanel"), "Win settlement panel should close after continuing.");
            Assert.IsTrue(pauseButton.activeSelf, "PauseButton should return after continuing to the next level.");
        }

        [UnityTest]
        public IEnumerator BoardBackgroundTracksActualGridSizeOnTallScreen()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;
            yield return null;

            var runtime = GetActiveGame(controller);
            var boardAreaField = typeof(TapTreasureGameView).GetField("boardArea", InstancePrivate);
            var boardGridRectField = typeof(TapTreasureGameView).GetField("boardGridRect", InstancePrivate);
            var boardShadowRectField = typeof(TapTreasureGameView).GetField("boardShadowRect", InstancePrivate);
            var boardCardRectField = typeof(TapTreasureGameView).GetField("boardCardRect", InstancePrivate);
            var boardGridLayoutField = typeof(TapTreasureGameView).GetField("boardGridLayout", InstancePrivate);
            var refreshMethod = typeof(TapTreasureGameView).GetMethod("RefreshBoardLayout", InstancePrivate);

            Assert.IsNotNull(boardAreaField, "Failed to access boardArea field.");
            Assert.IsNotNull(boardGridRectField, "Failed to access boardGridRect field.");
            Assert.IsNotNull(boardShadowRectField, "Failed to access boardShadowRect field.");
            Assert.IsNotNull(boardCardRectField, "Failed to access boardCardRect field.");
            Assert.IsNotNull(boardGridLayoutField, "Failed to access boardGridLayout field.");
            Assert.IsNotNull(refreshMethod, "Failed to access RefreshBoardLayout.");

            var boardArea = boardAreaField.GetValue(runtime) as RectTransform;
            var boardGridRect = boardGridRectField.GetValue(runtime) as RectTransform;
            var boardShadowRect = boardShadowRectField.GetValue(runtime) as RectTransform;
            var boardCardRect = boardCardRectField.GetValue(runtime) as RectTransform;
            var boardGridLayout = boardGridLayoutField.GetValue(runtime) as GridLayoutGroup;

            Assert.IsNotNull(boardArea, "boardArea should exist.");
            Assert.IsNotNull(boardGridRect, "boardGridRect should exist.");
            Assert.IsNotNull(boardShadowRect, "boardShadowRect should exist.");
            Assert.IsNotNull(boardCardRect, "boardCardRect should exist.");
            Assert.IsNotNull(boardGridLayout, "boardGridLayout should exist.");

            boardArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 560f);
            boardArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 980f);
            Canvas.ForceUpdateCanvases();
            refreshMethod.Invoke(runtime, null);
            Canvas.ForceUpdateCanvases();
            yield return null;

            var cellSize = boardGridLayout.cellSize.x;
            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var board = SnapshotBoard(runtime);
            var layoutColumns = CountOccupiedColumns(board);
            var layoutRows = CountOccupiedRows(board);
            var expectedBoardWidth = cellSize * layoutColumns + spacing.x * (layoutColumns - 1) + padding.left + padding.right;
            var expectedBoardHeight = cellSize * layoutRows + spacing.y * (layoutRows - 1) + padding.top + padding.bottom;
            var expectedCenter = boardArea.InverseTransformPoint(boardGridRect.TransformPoint(boardGridRect.rect.center));

            Assert.That(boardShadowRect.rect.width, Is.EqualTo(expectedBoardWidth + 28f).Within(0.1f), "BoardShadow width should follow the actual occupied grid width.");
            Assert.That(boardShadowRect.rect.height, Is.EqualTo(expectedBoardHeight + 28f).Within(0.1f), "BoardShadow height should follow the actual occupied grid height.");
            Assert.That(boardCardRect.rect.width, Is.EqualTo(expectedBoardWidth + 48f).Within(0.1f), "BoardCardFull width should follow the actual occupied grid width.");
            Assert.That(boardCardRect.rect.height, Is.EqualTo(expectedBoardHeight + 48f).Within(0.1f), "BoardCardFull height should follow the actual occupied grid height.");
            Assert.That(Vector2.Distance(boardCardRect.anchoredPosition, expectedCenter), Is.LessThan(0.1f), "BoardCardFull should stay centered on the actual grid area.");
            Assert.That(expectedBoardWidth, Is.LessThanOrEqualTo(boardGridRect.rect.width + 0.1f), "ClassicLink grid should not overflow its own layout rect on tall screens.");
        }

        [UnityTest]
        public IEnumerator NonFullLevelUsesLargerTilesThanFullBoardFit()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;
            yield return null;

            var runtime = GetActiveGame(controller);
            var boardAreaField = typeof(TapTreasureGameView).GetField("boardArea", InstancePrivate);
            var boardGridLayoutField = typeof(TapTreasureGameView).GetField("boardGridLayout", InstancePrivate);
            var refreshMethod = typeof(TapTreasureGameView).GetMethod("RefreshBoardLayout", InstancePrivate);
            Assert.IsNotNull(boardAreaField, "Failed to access boardArea field.");
            Assert.IsNotNull(boardGridLayoutField, "Failed to access boardGridLayout field.");
            Assert.IsNotNull(refreshMethod, "Failed to access RefreshBoardLayout.");

            var boardArea = boardAreaField.GetValue(runtime) as RectTransform;
            var boardGridLayout = boardGridLayoutField.GetValue(runtime) as GridLayoutGroup;
            Assert.IsNotNull(boardArea, "boardArea should exist.");
            Assert.IsNotNull(boardGridLayout, "boardGridLayout should exist.");

            boardArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 720f);
            boardArea.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1040f);
            Canvas.ForceUpdateCanvases();
            refreshMethod.Invoke(runtime, null);
            Canvas.ForceUpdateCanvases();
            yield return null;

            var spacing = boardGridLayout.spacing;
            var padding = boardGridLayout.padding;
            var fullBoardFit = Mathf.Floor(Mathf.Min(
                (boardGridLayout.GetComponent<RectTransform>().rect.width - padding.left - padding.right - spacing.x * 7) / 8,
                (boardGridLayout.GetComponent<RectTransform>().rect.height - padding.top - padding.bottom - spacing.y * 9) / 10));

            Assert.Greater(boardGridLayout.cellSize.x, fullBoardFit, "Non-full ClassicLink levels should enlarge tiles instead of keeping the full 10x8 fit size.");
            Assert.GreaterOrEqual(boardGridLayout.cellSize.x, 148f, "Compact ClassicLink levels should use most of the available board space on portrait screens.");
        }

        [UnityTest]
        public IEnumerator SelectedTileBounceReturnsToDefaultScale()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindConnectablePair(runtime);

            ClickTile(pair.firstRow, pair.firstColumn);
            yield return new WaitForSeconds(0.20f);

            var iconRect = GetTileIconRect(runtime, pair.firstRow, pair.firstColumn);
            Assert.That(iconRect.localScale.x, Is.EqualTo(1f).Within(0.01f), "Selected tile icon scale should return to 1 after bounce.");
            Assert.That(iconRect.localScale.y, Is.EqualTo(1f).Within(0.01f), "Selected tile icon scale should return to 1 after bounce.");
        }

        [UnityTest]
        public IEnumerator HintPulseStopsAfterAnyTileClick()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            InvokePrivate(runtime, "ShowHintByPlayer");
            yield return new WaitForSeconds(0.12f);

            var firstHint = GetNullableTileCoord(runtime, "hintedFirstTile");
            var secondHint = GetNullableTileCoord(runtime, "hintedSecondTile");
            Assert.IsTrue(firstHint.HasValue, "Hint should assign the first hinted tile.");
            Assert.IsTrue(secondHint.HasValue, "Hint should assign the second hinted tile.");

            var firstHintRoutine = GetTileRoutine(runtime, firstHint.Value.row, firstHint.Value.column, "HintPulseRoutine");
            var secondHintRoutine = GetTileRoutine(runtime, secondHint.Value.row, secondHint.Value.column, "HintPulseRoutine");
            Assert.IsNotNull(firstHintRoutine, "First hinted tile should have an active pulse routine.");
            Assert.IsNotNull(secondHintRoutine, "Second hinted tile should have an active pulse routine.");

            ClickTile(firstHint.Value.row, firstHint.Value.column);
            yield return null;

            Assert.IsFalse(GetNullableTileCoord(runtime, "hintedFirstTile").HasValue, "Hinted first tile should be cleared after click.");
            Assert.IsFalse(GetNullableTileCoord(runtime, "hintedSecondTile").HasValue, "Hinted second tile should be cleared after click.");
            Assert.IsNull(GetTileRoutine(runtime, firstHint.Value.row, firstHint.Value.column, "HintPulseRoutine"), "First hinted tile pulse routine should stop after click.");
            Assert.IsNull(GetTileRoutine(runtime, secondHint.Value.row, secondHint.Value.column, "HintPulseRoutine"), "Second hinted tile pulse routine should stop after click.");
        }

        [UnityTest]
        public IEnumerator SuccessfulMatchCleansTransientPathAndBurstEffects()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.Save();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(TapTreasureGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindStablePair(SnapshotBoard(runtime), runtime);

            ClickTile(pair.firstRow, pair.firstColumn);
            ClickTile(pair.secondRow, pair.secondColumn);

            yield return new WaitForSeconds(0.05f);
            Assert.Greater(CountNamedObjects("MatchShard") + CountNamedObjects("PathSweepDot") + CountNamedObjects("Line"), 0, "Match should spawn transient path or burst effects.");

            yield return new WaitForSeconds(0.45f);
            Assert.Zero(CountNamedObjects("MatchShard"), "Match shards should be cleaned up after the effect finishes.");
            Assert.Zero(CountNamedObjects("PathSweepDot"), "Path sweep dot should be cleaned up after the effect finishes.");
            Assert.Zero(CountNamedObjects("Line"), "Path line segments should be cleaned up after the fade finishes.");
            Assert.Zero(GetTransientEffectCount(runtime), "Transient effect registry should be empty after cleanup.");
            Assert.IsNull(GetPrivateField(runtime, "activePathSweepDot"), "Path sweep dot reference should be cleared after cleanup.");
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

        private static TapTreasureGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");

            var runtime = field.GetValue(controller) as TapTreasureGameView;
            Assert.IsNotNull(runtime, "Classic Link runtime was not created.");
            return runtime;
        }

        private static (int firstRow, int firstColumn, int secondRow, int secondColumn) FindConnectablePair(TapTreasureGameView runtime)
        {
            var boardField = typeof(TapTreasureGameView).GetField("board", InstancePrivate);
            var tryFindPathMethod = typeof(TapTreasureGameView).GetMethod("TryFindPath", InstancePrivate);
            var tileCoordType = typeof(TapTreasureGameView).GetNestedType("TileCoord", BindingFlags.NonPublic);

            Assert.IsNotNull(boardField, "Failed to access board field.");
            Assert.IsNotNull(tryFindPathMethod, "Failed to access TryFindPath.");
            Assert.IsNotNull(tileCoordType, "Failed to access TileCoord type.");

            var board = (int[,])boardField.GetValue(runtime);
            for (var firstRow = 1; firstRow <= 10; firstRow++)
            {
                for (var firstColumn = 1; firstColumn <= 8; firstColumn++)
                {
                    var value = board[firstRow, firstColumn];
                    if (value == 0)
                    {
                        continue;
                    }

                    for (var secondRow = firstRow; secondRow <= 10; secondRow++)
                    {
                        var secondColumnStart = secondRow == firstRow ? firstColumn + 1 : 1;
                        for (var secondColumn = secondColumnStart; secondColumn <= 8; secondColumn++)
                        {
                            if (board[secondRow, secondColumn] != value)
                            {
                                continue;
                            }

                            var firstCoord = Activator.CreateInstance(tileCoordType, new object[] { firstRow, firstColumn });
                            var secondCoord = Activator.CreateInstance(tileCoordType, new object[] { secondRow, secondColumn });
                            var parameters = new object[] { firstCoord, secondCoord, null };

                            var canConnect = (bool)tryFindPathMethod.Invoke(runtime, parameters);
                            if (canConnect)
                            {
                                return (firstRow, firstColumn, secondRow, secondColumn);
                            }
                        }
                    }
                }
            }

            Assert.Fail("Could not find a connectable pair on the board.");
            return default((int firstRow, int firstColumn, int secondRow, int secondColumn));
        }

        private static (int firstRow, int firstColumn, int secondRow, int secondColumn) FindStablePair(int[,] boardSnapshot, TapTreasureGameView runtime)
        {
            var pair = FindConnectablePair(runtime);
            var copy = CloneBoard(boardSnapshot);
            copy[pair.firstRow, pair.firstColumn] = 0;
            copy[pair.secondRow, pair.secondColumn] = 0;
            if (ClassicLinkBoardUtility.CountAvailablePairs(copy, 10, 8) > 0)
            {
                return pair;
            }

            for (var firstRow = 1; firstRow <= 10; firstRow++)
            {
                for (var firstColumn = 1; firstColumn <= 8; firstColumn++)
                {
                    var value = boardSnapshot[firstRow, firstColumn];
                    if (value == 0)
                    {
                        continue;
                    }

                    for (var secondRow = firstRow; secondRow <= 10; secondRow++)
                    {
                        var secondColumnStart = secondRow == firstRow ? firstColumn + 1 : 1;
                        for (var secondColumn = secondColumnStart; secondColumn <= 8; secondColumn++)
                        {
                            if (boardSnapshot[secondRow, secondColumn] != value)
                            {
                                continue;
                            }

                            List<Vector2Int> path;
                            if (!ClassicLinkBoardUtility.TryFindPath(
                                    boardSnapshot,
                                    10,
                                    8,
                                    new Vector2Int(firstColumn, firstRow),
                                    new Vector2Int(secondColumn, secondRow),
                                    out path))
                            {
                                continue;
                            }

                            copy = CloneBoard(boardSnapshot);
                            copy[firstRow, firstColumn] = 0;
                            copy[secondRow, secondColumn] = 0;
                            if (ClassicLinkBoardUtility.CountAvailablePairs(copy, 10, 8) > 0)
                            {
                                return (firstRow, firstColumn, secondRow, secondColumn);
                            }
                        }
                    }
                }
            }

            Assert.Fail("Could not find a stable pair that keeps the board solvable.");
            return default((int firstRow, int firstColumn, int secondRow, int secondColumn));
        }

        private static void ClickTile(int row, int column)
        {
            var tileName = string.Format("Tile_{0}_{1}", row, column);
            var buttons = Object.FindObjectsOfType<Button>();
            Button target = null;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == tileName)
                {
                    target = buttons[i];
                    break;
                }
            }

            Assert.IsNotNull(target, "Could not find tile button: " + tileName);
            Assert.IsTrue(target.interactable, "Tile button should be interactable before click: " + tileName);

            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            };
            ExecuteEvents.Execute(target.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void ClickButton(string buttonName)
        {
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == buttonName)
                {
                    buttons[i].onClick.Invoke();
                    return;
                }
            }

            Assert.Fail("Could not find button: " + buttonName);
        }

        private static int[,] SnapshotBoard(TapTreasureGameView runtime)
        {
            var boardField = typeof(TapTreasureGameView).GetField("board", InstancePrivate);
            Assert.IsNotNull(boardField, "Failed to access board field.");
            return CloneBoard((int[,])boardField.GetValue(runtime));
        }

        private static int[,] CloneBoard(int[,] source)
        {
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

        private static Vector2 GetLinePoint(TapTreasureGameView runtime, int row, int column)
        {
            var method = typeof(TapTreasureGameView).GetMethod("GetPointPosition", InstancePrivate);
            var tileCoordType = typeof(TapTreasureGameView).GetNestedType("TileCoord", BindingFlags.NonPublic);

            Assert.IsNotNull(method, "Failed to access GetPointPosition.");
            Assert.IsNotNull(tileCoordType, "Failed to access TileCoord type.");

            var tileCoord = Activator.CreateInstance(tileCoordType, new object[] { row, column });
            return (Vector2)method.Invoke(runtime, new[] { tileCoord });
        }

        private static int CountRemainingTiles(TapTreasureGameView runtime)
        {
            var board = SnapshotBoard(runtime);
            var count = 0;
            for (var row = 1; row <= 10; row++)
            {
                for (var column = 1; column <= 8; column++)
                {
                    if (board[row, column] != 0)
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }

        private static int CountOccupiedRows(int[,] board)
        {
            var min = 10;
            var max = 1;
            var hasTile = false;
            for (var row = 1; row <= 10; row++)
            {
                for (var column = 1; column <= 8; column++)
                {
                    if (board[row, column] == 0)
                    {
                        continue;
                    }

                    hasTile = true;
                    min = Mathf.Min(min, row);
                    max = Mathf.Max(max, row);
                }
            }

            return hasTile ? max - min + 1 : 10;
        }

        private static int CountOccupiedColumns(int[,] board)
        {
            var min = 8;
            var max = 1;
            var hasTile = false;
            for (var row = 1; row <= 10; row++)
            {
                for (var column = 1; column <= 8; column++)
                {
                    if (board[row, column] == 0)
                    {
                        continue;
                    }

                    hasTile = true;
                    min = Mathf.Min(min, column);
                    max = Mathf.Max(max, column);
                }
            }

            return hasTile ? max - min + 1 : 8;
        }

        private static Dictionary<string, Vector2> SnapshotTilePositions()
        {
            var buttons = Object.FindObjectsOfType<Button>();
            var positions = new Dictionary<string, Vector2>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith("Tile_", StringComparison.Ordinal))
                {
                    var rect = buttons[i].GetComponent<RectTransform>();
                    positions[buttons[i].name] = rect.anchoredPosition;
                }
            }

            return positions;
        }

        private static int CountInteractableTileButtons()
        {
            var buttons = Object.FindObjectsOfType<Button>();
            var count = 0;
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith("Tile_", StringComparison.Ordinal) && buttons[i].interactable)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static RectTransform GetTileIconRect(TapTreasureGameView runtime, int row, int column)
        {
            var tile = GetTileView(runtime, row, column);
            var field = tile.GetType().GetField("IconRect", InstanceAny);
            Assert.IsNotNull(field, "Failed to access IconRect field.");
            var rect = field.GetValue(tile) as RectTransform;
            Assert.IsNotNull(rect, "Tile IconRect should exist.");
            return rect;
        }

        private static Coroutine GetTileRoutine(TapTreasureGameView runtime, int row, int column, string fieldName)
        {
            var tile = GetTileView(runtime, row, column);
            var field = tile.GetType().GetField(fieldName, InstanceAny);
            Assert.IsNotNull(field, "Failed to access tile field: " + fieldName);
            return field.GetValue(tile) as Coroutine;
        }

        private static object GetTileView(TapTreasureGameView runtime, int row, int column)
        {
            var tileViewsField = typeof(TapTreasureGameView).GetField("tileViews", InstancePrivate);
            Assert.IsNotNull(tileViewsField, "Failed to access tileViews field.");
            var tileViews = tileViewsField.GetValue(runtime) as Array;
            Assert.IsNotNull(tileViews, "tileViews should exist.");
            var index = (row - 1) * 8 + (column - 1);
            var tile = tileViews.GetValue(index);
            Assert.IsNotNull(tile, "TileView should exist at index " + index);
            return tile;
        }

        private static (int row, int column)? GetNullableTileCoord(TapTreasureGameView runtime, string fieldName)
        {
            var field = typeof(TapTreasureGameView).GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            var value = field.GetValue(runtime);
            if (value == null)
            {
                return null;
            }

            var valueType = value.GetType();
            if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var hasValueProperty = valueType.GetProperty("HasValue");
                Assert.IsNotNull(hasValueProperty, "Nullable coord should expose HasValue.");
                var hasValue = (bool)hasValueProperty.GetValue(value, null);
                if (!hasValue)
                {
                    return null;
                }

                var valueProperty = valueType.GetProperty("Value");
                Assert.IsNotNull(valueProperty, "Nullable coord should expose Value.");
                value = valueProperty.GetValue(value, null);
                valueType = value.GetType();
            }

            var row = (int)valueType.GetProperty("Row").GetValue(value, null);
            var column = (int)valueType.GetProperty("Column").GetValue(value, null);
            return (row, column);
        }

        private static void InvokePrivate(TapTreasureGameView runtime, string methodName)
        {
            var method = typeof(TapTreasureGameView).GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            method.Invoke(runtime, null);
        }

        private static void ClearPlayableBoard(TapTreasureGameView runtime)
        {
            var boardField = typeof(TapTreasureGameView).GetField("board", InstancePrivate);
            Assert.IsNotNull(boardField, "Failed to access board field.");
            var board = (int[,])boardField.GetValue(runtime);
            for (var row = 1; row <= 10; row++)
            {
                for (var column = 1; column <= 8; column++)
                {
                    board[row, column] = 0;
                }
            }
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

        private static int GetTransientEffectCount(TapTreasureGameView runtime)
        {
            var list = GetPrivateField(runtime, "transientEffects") as IList;
            Assert.IsNotNull(list, "Failed to access transientEffects list.");
            return list.Count;
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return field.GetValue(target);
        }
    }
}


