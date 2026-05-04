using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests
{
    public sealed class WaterSortGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const int BottleCapacity = 4;
        private const int MaxWaterColorCount = 12;

        [Test]
        public void WaterSortTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/water-sort.ui_texts.zh-CN"), "WaterSort text catalog should exist.");
            Assert.IsNotNull(Resources.Load<TextAsset>("Levels/water-sort.levels"), "WaterSort level catalog should exist.");
        }

        [Test]
        public void SaveStoreNormalizesAndPersistsLevelProgress()
        {
            PlayerPrefs.SetString(
                MiniGameSaveStore.PlayerPrefsKey,
                "{\"Entries\":[{\"GameId\":\"water-sort\",\"PlayCount\":2,\"BestScore\":8,\"TotalChestCount\":1,\"TotalCoinCount\":24}],\"FavoriteGameIds\":[]}");

            var definitions = new[]
            {
                new MiniGameDefinition
                {
                    Id = WaterSortGameView.GameIdConstant,
                    Name = "水排序",
                    IsPlayable = true
                }
            };
            var store = new MiniGameSaveStore();
            var loaded = store.Load(definitions);
            var progress = loaded.ProgressLookup[WaterSortGameView.GameIdConstant];
            Assert.AreEqual(0, progress.CurrentLevelIndex);
            Assert.AreEqual(1, progress.UnlockedLevelCount, "Old saves should unlock at least the first level.");

            progress.CurrentLevelIndex = 3;
            progress.UnlockedLevelCount = 4;
            store.Save(loaded.ProgressLookup, loaded.FavoriteGameIds);

            loaded = store.Load(definitions);
            progress = loaded.ProgressLookup[WaterSortGameView.GameIdConstant];
            Assert.AreEqual(3, progress.CurrentLevelIndex);
            Assert.AreEqual(4, progress.UnlockedLevelCount);
        }

        [Test]
        public void WaterSortLevelTableLoadsBalancedLayouts()
        {
            var definitions = GetLevelDefinitions();
            Assert.AreEqual(100, definitions.Length, "WaterSort should expose the generated 100-level progression.");

            for (var levelIndex = 0; levelIndex < definitions.Length; levelIndex++)
            {
                var definition = definitions[levelIndex];
                var colorCount = GetIntProperty(definition, "ColorCount");
                var emptyBottleCount = GetIntProperty(definition, "EmptyBottleCount");
                var bottleCount = GetIntProperty(definition, "BottleCount");
                var layout = InvokeLayout(definition);

                Assert.AreEqual(layout.Length, bottleCount, "Bottle count should come from the parsed layout.");
                Assert.AreEqual(bottleCount, layout.Length, "Generated layout should match the level bottle count.");

                var colorTotals = new int[colorCount];
                var emptyCount = 0;
                for (var bottleIndex = 0; bottleIndex < layout.Length; bottleIndex++)
                {
                    var bottle = layout[bottleIndex];
                    Assert.LessOrEqual(bottle.Length, BottleCapacity, "A generated bottle should never exceed capacity.");
                    Assert.IsFalse(
                        IsCompletedBottleLayout(bottle),
                        "Generated layout should not start with already completed bottles: " + (levelIndex + 1));
                    if (bottle.Length == 0)
                    {
                        emptyCount += 1;
                    }

                    for (var layerIndex = 0; layerIndex < bottle.Length; layerIndex++)
                    {
                        Assert.GreaterOrEqual(bottle[layerIndex], 0, "Color indices should be non-negative.");
                        Assert.Less(bottle[layerIndex], MaxWaterColorCount, "Color indices should stay inside the expanded water palette.");
                        Assert.Less(bottle[layerIndex], colorCount, "Color indices should stay inside the level palette.");
                        colorTotals[bottle[layerIndex]] += 1;
                    }
                }

                Assert.AreEqual(emptyBottleCount, emptyCount, "Generated layout should include the configured empty bottles.");
                Assert.LessOrEqual(emptyBottleCount, 3, "Generated layouts may use zero to three empty bottles.");
                Assert.LessOrEqual(colorCount, MaxWaterColorCount, "Generated layouts should not exceed twelve colors.");
                for (var colorIndex = 0; colorIndex < colorTotals.Length; colorIndex++)
                {
                    Assert.AreEqual(BottleCapacity, colorTotals[colorIndex], "Each color should appear exactly one full bottle worth of layers.");
                }

                Assert.IsTrue(CanSolveLayout(layout), "Generated WaterSort level should be solvable: " + (levelIndex + 1));
            }
        }

        [UnityTest]
        public IEnumerator CanEnterWaterSortAndShowBottles()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "WaterSort should become the active game.");
            Assert.IsNotNull(GameObject.Find("WaterSortView"), "WaterSort shell root should exist.");
            Assert.IsNotNull(GameObject.Find("WaterSortBottleGrid"), "Bottle grid should exist.");
            var levelSelectButton = FindButton("LevelSelectButton");
            Assert.IsNotNull(levelSelectButton, "Level select button should exist.");
            var levelSelectGraphic = levelSelectButton.targetGraphic;
            Assert.IsNotNull(levelSelectGraphic, "Level select button should have a graphic background.");
            Assert.AreNotEqual(Color.white, levelSelectGraphic.color, "Level select button should not be white.");
            var levelSelectLabel = levelSelectButton.transform.Find("Label")?.GetComponent<Graphic>();
            Assert.IsNotNull(levelSelectLabel, "Level select button should have a label.");
            Assert.AreEqual(Color.white, levelSelectLabel.color, "Level select button text should stay white.");
            Assert.IsNotNull(FindButton("RestartButton"), "Restart button should exist.");
            Assert.IsNull(FindButton("NextLevelButton"), "Next level button should be removed from the bottom bar.");
            Assert.IsNull(FindButton("WaterSortEasyButton"), "Difficulty buttons should be replaced by level controls.");
            Assert.AreEqual(GetExpectedBottleCount(0), GetBottles(GetActiveGame(controller)).Count, "First level should create its generated bottle layout.");
        }

        [UnityTest]
        public IEnumerator LevelSelectShowsUnlockedAndLockedLevels()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            ClickButton("LevelSelectButton");
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.IsNotNull(GameObject.Find("WaterSortLevelSelectPanel"), "Level select panel should open.");
            Assert.IsNotNull(FindButton("WaterSortLevelButton_1"), "First level button should exist.");
            Assert.IsNotNull(FindButton("WaterSortLevelButton_100"), "Last level button should exist.");
            Assert.IsTrue(FindButton("WaterSortLevelButton_1").interactable, "First level should be unlocked.");
            Assert.IsFalse(FindButton("WaterSortLevelButton_2").interactable, "Second level should start locked.");
            Assert.IsFalse(FindButton("WaterSortLevelButton_100").interactable, "Last level should start locked.");
        }

        [UnityTest]
        public IEnumerator LoadsSavedCurrentLevelOnEntry()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.SetLevelProgress(WaterSortGameView.GameIdConstant, 3, 4);
            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            Assert.AreEqual(GetExpectedBottleCount(3), GetBottles(GetActiveGame(controller)).Count, "Saved level 4 should create its generated bottle layout.");
            Assert.AreEqual(3, GetIntField(GetActiveGame(controller), "currentLevelIndex"), "Saved level index should be restored.");
        }

        [UnityTest]
        public IEnumerator ValidPourMovesMatchingTopGroup()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2, 2, 2 },
                    new[] { 0, 0, 0, 0 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return null;

            var bottles = GetBottles(runtime);
            Assert.AreEqual(1, ((IList)bottles[0]).Count, "Source should commit the move immediately for parallel previews.");
            Assert.AreEqual(3, ((IList)bottles[1]).Count, "Target should commit the move immediately for parallel previews.");
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Valid pour should not count as complete before the animation finishes.");

            yield return new WaitForSeconds(8.2f);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(1, ((IList)bottles[0]).Count, "Source should pour the contiguous top group.");
            Assert.AreEqual(3, ((IList)bottles[1]).Count, "Target should receive two matching layers.");
            Assert.AreEqual(1, GetIntField(runtime, "moveCount"), "Valid pour should count one move after the animation finishes.");
        }

        [UnityTest]
        public IEnumerator LevelSelectOpensDuringPourAnimation()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2, 2, 2 },
                    new[] { 0, 0, 0, 0 },
                    new int[0]
                });

            var sourceBottle = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            Assert.IsNotNull(sourceBottle, "Source bottle should exist.");
            var sourceStartPosition = sourceBottle.anchoredPosition;
            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return null;

            Assert.Greater(GetCollectionCount(runtime, "activePourAnimations"), 0, "Pour animation should be active before opening level select.");
            yield return WaitForCondition(
                () => Vector2.Distance(sourceBottle.anchoredPosition, sourceStartPosition) > 12f,
                2f,
                "Source bottle should move before opening level select.");

            ClickButton("LevelSelectButton");
            yield return null;

            Assert.IsNotNull(GameObject.Find("WaterSortLevelSelectPanel"), "Level select panel should open during a pour animation.");
            Assert.Greater(GetCollectionCount(runtime, "activePourAnimations"), 0, "Opening level select should not stop active pour animations.");
            Assert.IsNotNull(GameObject.Find("WaterSortPourStream"), "Opening level select should keep the active pour stream until a level is selected.");
            Assert.Greater(Vector2.Distance(sourceBottle.anchoredPosition, sourceStartPosition), 12f, "Opening level select should not restore the source bottle before selecting a level.");
        }

        [UnityTest]
        public IEnumerator SelectingDifferentLevelDuringPourAnimationStopsPour()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.SetLevelProgress(WaterSortGameView.GameIdConstant, 0, 2);
            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2, 2, 2 },
                    new[] { 0, 0, 0, 0 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return null;

            Assert.Greater(GetCollectionCount(runtime, "activePourAnimations"), 0, "Pour animation should be active before selecting another level.");

            ClickButton("LevelSelectButton");
            yield return null;
            ClickButton("WaterSortLevelButton_2");
            yield return null;

            Assert.AreEqual(1, GetIntField(runtime, "currentLevelIndex"), "Selecting another level should load that level.");
            Assert.AreEqual(0, GetCollectionCount(runtime, "activePourAnimations"), "Selecting another level should stop active pour animations.");
            Assert.IsNull(GameObject.Find("WaterSortPourStream"), "Selecting another level should remove the pour stream.");
        }

        [UnityTest]
        public IEnumerator MatchingPoursCanShareOneTargetBeforeAnimationsFinish()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0 },
                    new[] { 0 },
                    new int[0],
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 }
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_2");
            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_2");
            yield return null;

            var bottles = GetBottles(runtime);
            Assert.AreEqual(0, ((IList)bottles[0]).Count);
            Assert.AreEqual(0, ((IList)bottles[1]).Count);
            Assert.AreEqual(2, ((IList)bottles[2]).Count, "Shared target should accept both committed pours immediately.");
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Shared-target pours should still wait for animations to finish.");

            yield return new WaitForSeconds(8.2f);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(2, GetIntField(runtime, "moveCount"), "Both shared-target pours should finish independently.");
        }

        [UnityTest]
        public IEnumerator DisjointPoursCanStartBeforePreviousAnimationFinishes()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2 },
                    new[] { 2 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_2");
            ClickButton("WaterSortBottle_3");
            yield return null;

            var bottles = GetBottles(runtime);
            Assert.AreEqual(1, ((IList)bottles[0]).Count);
            Assert.AreEqual(3, ((IList)bottles[1]).Count);
            Assert.AreEqual(0, ((IList)bottles[2]).Count);
            Assert.AreEqual(3, ((IList)bottles[3]).Count);
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Both pours should still be animating immediately after they start.");

            yield return new WaitForSeconds(8.2f);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(2, GetIntField(runtime, "moveCount"), "Both disjoint pours should finish independently.");
        }

        [UnityTest]
        public IEnumerator CompletedBottleCapCanAppearBeforeOtherPoursFinish()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0 },
                    new[] { 1, 1, 1 },
                    new[] { 0, 0, 0 },
                    new[] { 1 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_2");
            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_3");
            yield return WaitForCondition(
                () => GameObject.Find("WaterSortBottle_2")?.transform.Find("BottleCap")?.gameObject.activeSelf == true,
                3f,
                "Completed bottle cap should appear before all pours finish.");
            Canvas.ForceUpdateCanvases();

            var activeAnimations = GetCollectionCount(runtime, "activePourAnimations");
            Assert.Greater(activeAnimations, 0, "Another pour animation should still be running when the cap appears.");
        }

        [UnityTest]
        public IEnumerator SelectingBottleKeepsGridLayoutStable()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var grid = GameObject.Find("WaterSortBottleGrid")?.GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(grid, "Bottle grid layout should exist.");
            Assert.Greater(grid.cellSize.x, 20f, "Bottle grid cells should have a usable width.");
            Assert.Greater(grid.cellSize.y, 20f, "Bottle grid cells should have a usable height.");
            AssertBottlePositionsAreDistinct();

            var firstBottle = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            Assert.IsNotNull(firstBottle, "First bottle should exist.");
            var originalPosition = firstBottle.anchoredPosition;
            var originalScale = firstBottle.localScale;

            ClickButton("WaterSortBottle_0");
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.Greater(grid.cellSize.x, 20f, "Selecting a bottle should not collapse cell width.");
            Assert.Greater(grid.cellSize.y, 20f, "Selecting a bottle should not collapse cell height.");
            AssertBottlePositionsAreDistinct();
            Assert.AreEqual(originalPosition.x, firstBottle.anchoredPosition.x, 0.1f, "Selecting a bottle should not move it horizontally.");
            Assert.Greater(firstBottle.anchoredPosition.y, originalPosition.y + 8f, "Selecting a bottle should lift it upward.");
            Assert.AreEqual(originalScale, firstBottle.localScale, "Selecting a bottle should not scale it.");

            var background = firstBottle.GetComponent<Graphic>();
            Assert.IsNotNull(background, "Bottle button background graphic should exist.");
            Assert.AreEqual(0f, background.color.a, 0.01f, "Selecting a bottle should not show a highlight background.");
        }

        [UnityTest]
        public IEnumerator WaterFillAreaMatchesBottleInterior()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            var bottle = GameObject.Find("WaterSortBottle_0")?.transform;
            Assert.IsNotNull(bottle, "Bottle should exist.");

            var bottleShape = bottle.Find("BottleShape")?.GetComponent<RectTransform>();
            var liquidMask = bottle.Find("LiquidMask")?.GetComponent<RectTransform>();
            var fillArea = bottle.Find("LiquidMask/FillArea")?.GetComponent<RectTransform>();
            Assert.IsNotNull(bottleShape, "Bottle shape should exist.");
            Assert.IsNotNull(liquidMask, "Liquid mask should exist.");
            Assert.IsNotNull(fillArea, "Fill area should exist.");

            var expectedInnerWidth = bottleShape.rect.width * 0.56f - 4f;
            Assert.LessOrEqual(
                liquidMask.rect.width,
                expectedInnerWidth + 0.5f,
                "Water mask should stay inside the bottle side walls.");

            var bottleBottom = bottleShape.TransformPoint(new Vector3(0f, bottleShape.rect.yMin + 3f, 0f)).y;
            var fillBottom = fillArea.TransformPoint(new Vector3(0f, fillArea.rect.yMin, 0f)).y;
            Assert.LessOrEqual(
                Mathf.Abs(fillBottom - bottleBottom),
                1.5f,
                "Bottom water should align with the bottle bottom curve.");

            Assert.IsNull(bottle.Find("Label"), "Bottle bottom number label should not be shown.");

            var topSegment = fillArea.Find("Segment_3")?.GetComponent<RectTransform>();
            Assert.IsNotNull(topSegment, "Top water segment should exist.");
            var fillTop = liquidMask.TransformPoint(new Vector3(0f, liquidMask.rect.yMax, 0f)).y;
            var waterTop = topSegment.TransformPoint(new Vector3(0f, topSegment.rect.yMax, 0f)).y;
            Assert.GreaterOrEqual(
                fillTop - waterTop,
                2f,
                "A full bottle should still leave a small gap above the water.");

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0 },
                    new int[0],
                    new int[0],
                    new int[0],
                    new int[0],
                    new int[0]
                });
            Canvas.ForceUpdateCanvases();

            Assert.Greater(
                fillArea.anchorMax.y,
                0.25f,
                "A single bottom layer should compensate for the bottle bottom curve instead of using a linear height.");
            Assert.Less(
                fillArea.anchorMax.y,
                0.32f,
                "A single bottom layer should not overfill into the second layer area.");
        }

        [UnityTest]
        public IEnumerator PourAnimationShowsTiltedBottleStreamAndRisingTargetWater()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2, 2, 2 },
                    new[] { 0, 0, 0, 0 },
                    new int[0]
                });
            Canvas.ForceUpdateCanvases();

            var source = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            var target = GameObject.Find("WaterSortBottle_1")?.transform;
            var sourceFillArea = GameObject.Find("WaterSortBottle_0")?.transform.Find("LiquidMask/FillArea")?.GetComponent<RectTransform>();
            Assert.IsNotNull(source, "Source bottle should exist.");
            Assert.IsNotNull(target, "Target bottle should exist.");
            Assert.IsNotNull(sourceFillArea, "Source bottle fill area should exist.");

            var sourceStart = source.anchoredPosition;
            var sourceStartRotation = source.localRotation;
            var sourceBottomSegment = sourceFillArea.Find("Segment_0")?.GetComponent<RectTransform>();
            var sourceMiddleSegment = sourceFillArea.Find("Segment_1")?.GetComponent<RectTransform>();
            var sourceTopSegment = sourceFillArea.Find("Segment_2")?.GetComponent<RectTransform>();
            Assert.IsNotNull(sourceBottomSegment, "Source bottom segment should exist.");
            Assert.IsNotNull(sourceMiddleSegment, "Source middle segment should exist.");
            Assert.IsNotNull(sourceTopSegment, "Source top segment should exist.");

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var stream = GameObject.Find("WaterSortPourStream")?.GetComponent<Graphic>();
            Assert.IsNotNull(stream, "Pour stream should exist.");
            Assert.Greater(Vector2.Distance(source.anchoredPosition, sourceStart), 12f, "Pouring source bottle should move toward the target.");
            Assert.IsFalse(stream.enabled, "Pour stream should stay hidden while the source bottle is still moving into pour position.");
            var earlyReceivingSegment = target.Find("LiquidMask/FillArea/Segment_1")?.GetComponent<RectTransform>();
            Assert.IsNotNull(earlyReceivingSegment, "Receiving top segment should exist.");
            Assert.IsFalse(earlyReceivingSegment.gameObject.activeSelf, "Target bottle should not show received water before the stream starts.");

            yield return WaitForCondition(() => stream != null && stream.enabled, 2f, "Pour stream should become visible during pouring.");
            Canvas.ForceUpdateCanvases();

            Assert.Greater(Vector2.Distance(source.anchoredPosition, sourceStart), 12f, "Pouring source bottle should move toward the target.");
            Assert.Greater(Mathf.Abs(NormalizeAngle(source.localEulerAngles.z)), 20f, "Pouring source bottle should rotate.");
            Assert.Less(
                Mathf.Abs(NormalizeAngle(source.localEulerAngles.z) + NormalizeAngle(sourceFillArea.localEulerAngles.z)),
                0.2f,
                "Liquid layer should counter-rotate instead of sharing the bottle rotation.");
            Assert.AreEqual(0f, sourceFillArea.anchoredPosition.y, 0.01f, "Whole liquid block should not move upward and leave the bottle bottom empty.");
            Assert.AreEqual(0f, sourceFillArea.anchorMin.y, 0.01f, "Whole liquid block should stay anchored to the bottle bottom while tilted.");
            Assert.LessOrEqual(sourceBottomSegment.anchorMin.y, 0.01f, "Bottom water should keep covering the bottle bottom while tilted.");
            Assert.LessOrEqual(sourceBottomSegment.offsetMin.y, -70f, "Bottom water should extend below the fill area by a fixed pixel distance and rely on the mask to clip it.");
            for (var segmentIndex = 0; segmentIndex < 3; segmentIndex++)
            {
                var sourceSegment = sourceFillArea.Find("Segment_" + segmentIndex)?.GetComponent<RectTransform>();
                Assert.IsNotNull(sourceSegment, "Source water segment should exist: " + segmentIndex);
                Assert.IsTrue(sourceSegment.gameObject.activeSelf, "Source water segment should remain visible while tilted: " + segmentIndex);
                if (segmentIndex > 0)
                {
                    Assert.GreaterOrEqual(sourceSegment.anchorMin.y, 0f, "Source water segment should stay inside the bottle bottom: " + segmentIndex);
                    Assert.AreEqual(0f, sourceSegment.offsetMin.y, 0.01f, "Only the bottom water segment should extend below the fill area: " + segmentIndex);
                }
                Assert.LessOrEqual(sourceSegment.anchorMax.y, 1f, "Source water segment should stay inside the whole liquid block: " + segmentIndex);
            }

            Assert.IsTrue(stream.enabled, "Pour stream should be visible during pouring.");
            var streamStart = GetVector2Field(stream, "startPoint");
            var streamEnd = GetVector2Field(stream, "endPoint");
            Assert.AreEqual(streamStart.x, streamEnd.x, 0.1f, "Pour stream should be a vertical line.");
            Assert.Less(streamEnd.y, streamStart.y, "Pour stream should extend downward into the target bottle.");
            var streamLayer = stream.transform.parent;
            Assert.IsNotNull(streamLayer, "Pour stream layer should exist.");
            var sourceSurfacePoint = sourceTopSegment.TransformPoint(new Vector3(0f, sourceTopSegment.rect.yMax, 0f));
            var sourceSurfaceInStream = streamLayer.InverseTransformPoint(sourceSurfacePoint);
            Assert.AreEqual(
                streamStart.y,
                sourceSurfaceInStream.y,
                2f,
                "Source liquid surface should reach the pour mouth instead of using a fake connector.");
            yield return new WaitForSeconds(0.5f);
            Canvas.ForceUpdateCanvases();

            var receivingSegment = target.Find("LiquidMask/FillArea/Segment_1")?.GetComponent<RectTransform>();
            Assert.IsNotNull(receivingSegment, "Receiving top segment should exist.");
            Assert.IsTrue(receivingSegment.gameObject.activeSelf, "Target bottle should show the newly received water during pouring.");
            Assert.Greater(receivingSegment.anchorMax.y - receivingSegment.anchorMin.y, 0.02f, "Receiving water layer should rise during pouring.");

            yield return WaitForCondition(() => GameObject.Find("WaterSortPourStream") == null, 4f, "Pour animation should finish.");
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(sourceStart.x, source.anchoredPosition.x, 0.2f, "Source bottle should restore its horizontal position after pouring.");
            Assert.AreEqual(sourceStart.y, source.anchoredPosition.y, 0.2f, "Source bottle should restore its vertical position after pouring.");
            Assert.AreEqual(sourceStartRotation.eulerAngles.z, source.localRotation.eulerAngles.z, 0.2f, "Source bottle should restore its rotation after pouring.");
            Assert.IsNull(GameObject.Find("WaterSortPourStream"), "Pour stream should be removed after pouring.");
        }

        [UnityTest]
        public IEnumerator SingleLayerPourKeepsBottleBottomCovered()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0 },
                    new int[0],
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 },
                    new int[0]
                });
            Canvas.ForceUpdateCanvases();

            var source = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            var fillArea = GameObject.Find("WaterSortBottle_0")?.transform.Find("LiquidMask/FillArea")?.GetComponent<RectTransform>();
            var bottomSegment = GameObject.Find("WaterSortBottle_0")?.transform.Find("LiquidMask/FillArea/Segment_0")?.GetComponent<RectTransform>();
            Assert.IsNotNull(source, "Source bottle should exist.");
            Assert.IsNotNull(fillArea, "Source bottle fill area should exist.");
            Assert.IsNotNull(bottomSegment, "Source bottle bottom segment should exist.");
            Assert.IsNull(GameObject.Find("WaterSortBottle_0")?.transform.Find("LiquidMask/BottomSeal"), "Bottom seal should not be used as a separate filler.");

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return WaitForCondition(
                () => Mathf.Abs(NormalizeAngle(source.localEulerAngles.z)) > 20f,
                2f,
                "Single layer source bottle should rotate while pouring.");
            Canvas.ForceUpdateCanvases();

            Assert.Greater(Mathf.Abs(NormalizeAngle(source.localEulerAngles.z)), 20f, "Single layer source bottle should rotate while pouring.");
            Assert.Less(
                Mathf.Abs(NormalizeAngle(source.localEulerAngles.z) + NormalizeAngle(fillArea.localEulerAngles.z)),
                0.2f,
                "Single layer liquid should counter-rotate while pouring.");
            Assert.IsTrue(bottomSegment.gameObject.activeSelf, "Single layer bottom segment should remain visible while pouring.");
            Assert.LessOrEqual(bottomSegment.offsetMin.y, -70f, "Single layer bottom segment should extend downward by a fixed pixel distance.");
        }

        [UnityTest]
        public IEnumerator FullUnfinishedSourceBottleStartsWithSmallTiltAndTiltsMoreAsItEmpties()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1, 1, 1 },
                    new[] { 1 },
                    new[] { 2, 2, 2, 2 },
                    new[] { 0, 0, 0, 0 },
                    new int[0]
                });
            Canvas.ForceUpdateCanvases();

            var source = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            Assert.IsNotNull(source, "Source bottle should exist.");

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return new WaitForSeconds(0.1f);
            Canvas.ForceUpdateCanvases();

            var earlyTilt = Mathf.Abs(NormalizeAngle(source.localEulerAngles.z));
            Assert.Less(earlyTilt, 24f, "A full source bottle should start pouring with a small tilt.");

            yield return WaitForCondition(
                () => Mathf.Abs(NormalizeAngle(source.localEulerAngles.z)) > 60f,
                3f,
                "Source bottle should keep tilting more as its matching top group is poured out.");
            Canvas.ForceUpdateCanvases();

            var laterTilt = Mathf.Abs(NormalizeAngle(source.localEulerAngles.z));
            Assert.Greater(laterTilt, earlyTilt + 20f, "Source bottle tilt should increase as its water is poured out.");
            Assert.Greater(laterTilt, 60f, "Source bottle should keep tilting more as its matching top group is poured out.");

            yield return WaitForCondition(
                () => Mathf.Abs(NormalizeAngle(source.localEulerAngles.z)) < 1f,
                4f,
                "Source bottle should restore its rotation after pouring.");
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator InvalidPourDoesNotMoveWater()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 1 },
                    new[] { 2 },
                    new int[0],
                    new[] { 3, 3, 3, 3 },
                    new[] { 4, 4, 4, 4 }
                });

            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return null;

            var bottles = GetBottles(runtime);
            Assert.AreEqual(2, ((IList)bottles[0]).Count, "Source should remain unchanged after invalid pour.");
            Assert.AreEqual(1, ((IList)bottles[1]).Count, "Target should remain unchanged after invalid pour.");
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Invalid pour should not count a move.");
        }

        [UnityTest]
        public IEnumerator CompletedBottleShowsCapAndCannotMove()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0, 0 },
                    new int[0],
                    new[] { 1, 2 },
                    new int[0],
                    new int[0],
                    new int[0]
                });
            Canvas.ForceUpdateCanvases();

            var completedBottle = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            Assert.IsNotNull(completedBottle, "Completed bottle should exist.");
            var cap = completedBottle.transform.Find("BottleCap");
            Assert.IsNotNull(cap, "Completed bottle should show a cap.");
            Assert.IsTrue(cap.gameObject.activeSelf, "Completed bottle cap should be active.");
            var capRect = cap.GetComponent<RectTransform>();
            var capStartY = capRect.anchoredPosition.y;

            yield return new WaitForSeconds(0.35f);
            Canvas.ForceUpdateCanvases();

            Assert.Less(capRect.anchoredPosition.y, capStartY - 10f, "Completed bottle cap should drop into place.");
            var bottleShape = completedBottle.transform.Find("BottleShape")?.GetComponent<RectTransform>();
            Assert.IsNotNull(bottleShape, "Completed bottle shape should exist.");
            var bottleMouthY = bottleShape.TransformPoint(new Vector3(0f, bottleShape.rect.yMax - 3f, 0f)).y;
            var capBottomY = capRect.TransformPoint(new Vector3(0f, capRect.rect.yMin, 0f)).y;
            Assert.AreEqual(bottleMouthY, capBottomY, 2f, "Completed bottle cap should touch the bottle mouth.");

            var originalPosition = completedBottle.anchoredPosition;
            ClickButton("WaterSortBottle_0");
            ClickButton("WaterSortBottle_1");
            yield return null;
            Canvas.ForceUpdateCanvases();

            var bottles = GetBottles(runtime);
            Assert.AreEqual(4, ((IList)bottles[0]).Count, "Completed source should remain unchanged.");
            Assert.AreEqual(0, ((IList)bottles[1]).Count, "Completed source should not pour into an empty bottle.");
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Clicking a completed bottle should not count a move.");
            Assert.AreEqual(originalPosition.x, completedBottle.anchoredPosition.x, 0.1f, "Completed bottle should not move horizontally.");
            Assert.AreEqual(originalPosition.y, completedBottle.anchoredPosition.y, 0.1f, "Completed bottle should not lift as selected.");
        }

        [UnityTest]
        public IEnumerator CompletingPuzzleSettlesAndReturnsToHall()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0 },
                    new[] { 0 },
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_0");
            yield return null;

            var gameRoot = GameObject.Find("WaterSortView");
            Assert.IsNotNull(gameRoot, "WaterSort shell root should exist.");
            var settlementPopup = gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel");
            Assert.IsNull(settlementPopup, "Completing pour should wait for animation before showing settlement popup.");

            yield return new WaitForSeconds(6f);
            Canvas.ForceUpdateCanvases();

            settlementPopup = gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Completing the puzzle should show settlement popup.");
            var dialogImage = settlementPopup.Find("Dialog")?.GetComponent<Image>();
            Assert.IsNotNull(dialogImage, "Settlement dialog should use an image background.");
            Assert.AreEqual(Image.Type.Sliced, dialogImage.type, "Settlement dialog background should use sliced rendering.");

            var nextButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Settlement next button should exist.");
            var backHallButton = settlementPopup.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button should exist.");
            var confirmImage = nextButton.GetComponent<Image>();
            Assert.IsNotNull(confirmImage, "Settlement next button should use an image background.");
            Assert.AreSame(Resources.Load<Sprite>("HallTheme/hall_tab_unselected"), confirmImage.sprite, "Settlement next button should reuse hall tab background.");
            Assert.AreEqual(new Color32(255, 183, 31, 255), (Color32)confirmImage.color, "Settlement next button should be golden orange.");
            var confirmRect = nextButton.GetComponent<RectTransform>();
            var backHallRect = backHallButton.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(confirmRect.anchoredPosition.y - (confirmRect.sizeDelta.y * 0.5f) - (backHallRect.anchoredPosition.y + (backHallRect.sizeDelta.y * 0.5f)), 8f, "Settlement buttons should not overlap vertically.");
            backHallButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(WaterSortGameView.GameIdConstant);
            Assert.AreEqual(1, progress.PlayCount);
            Assert.AreEqual(99, progress.BestScore);
            Assert.AreEqual(36, progress.TotalCoinCount);
            Assert.AreEqual(1, progress.TotalChestCount);
            Assert.AreEqual(2, progress.UnlockedLevelCount);
            Assert.IsFalse(controller.HasActiveGame);
            Assert.IsTrue(controller.IsHallVisible);
        }

        [UnityTest]
        public IEnumerator LevelSelectDuringFinalPourCompletesPuzzle()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0 },
                    new[] { 0 },
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_0");
            yield return null;

            Assert.Greater(GetCollectionCount(runtime, "activePourAnimations"), 0, "Final pour animation should be active before opening level select.");

            ClickButton("LevelSelectButton");
            yield return null;

            Assert.IsNotNull(GameObject.Find("WaterSortLevelSelectPanel"), "Level select panel should open while the final pour is still animating.");
            Assert.Greater(GetCollectionCount(runtime, "activePourAnimations"), 0, "Final pour should continue after opening level select.");

            yield return new WaitForSeconds(6f);
            Canvas.ForceUpdateCanvases();

            var gameRoot = GameObject.Find("WaterSortView");
            Assert.IsNotNull(gameRoot, "WaterSort shell root should exist.");
            Assert.IsNotNull(gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel"), "Final pour should complete naturally while level select is open.");
            Assert.IsNull(GameObject.Find("WaterSortLevelSelectPanel"), "Settlement should close level select after the final pour completes.");
            Assert.AreEqual(1, GetIntField(runtime, "moveCount"), "Final pour should count as a completed move.");
        }

        [UnityTest]
        public IEnumerator CompletingPuzzleUnlocksAndNextButtonLoadsNextLevel()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0 },
                    new[] { 0 },
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_0");
            yield return new WaitForSeconds(6f);
            Canvas.ForceUpdateCanvases();

            var progress = controller.GetProgress(WaterSortGameView.GameIdConstant);
            Assert.AreEqual(2, progress.UnlockedLevelCount, "Completing level 1 should unlock level 2.");
            Assert.AreEqual(0, progress.CurrentLevelIndex, "Winning should save the completed current level until next is selected.");

            var gameRoot = GameObject.Find("WaterSortView");
            var nextButton = gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel/Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Settlement next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            runtime = GetActiveGame(controller);
            Assert.IsTrue(controller.HasActiveGame, "Next level should keep WaterSort active.");
            Assert.AreEqual(1, GetIntField(runtime, "currentLevelIndex"), "Next level should advance the current level.");
            Assert.AreEqual(0, GetIntField(runtime, "moveCount"), "Next level should reset move count.");
            Assert.AreEqual(GetExpectedBottleCount(1), GetBottles(runtime).Count, "Level 2 should load its own bottle layout.");
            progress = controller.GetProgress(WaterSortGameView.GameIdConstant);
            Assert.AreEqual(1, progress.CurrentLevelIndex, "Next level selection should be saved.");
            Assert.AreEqual(2, progress.UnlockedLevelCount);
        }

        [UnityTest]
        public IEnumerator CompletingPuzzleAndReturningHallSavesNextLevelForReentry()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0 },
                    new[] { 0 },
                    new[] { 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2 },
                    new int[0]
                });

            ClickButton("WaterSortBottle_1");
            ClickButton("WaterSortBottle_0");
            yield return new WaitForSeconds(6f);
            Canvas.ForceUpdateCanvases();

            var gameRoot = GameObject.Find("WaterSortView");
            var backHallButton = gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel/Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button should exist.");
            backHallButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(controller.IsHallVisible, "Back hall should return to hall.");
            var progress = controller.GetProgress(WaterSortGameView.GameIdConstant);
            Assert.AreEqual(1, progress.CurrentLevelIndex, "Returning hall after winning should save next level for reentry.");
            Assert.AreEqual(2, progress.UnlockedLevelCount);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            runtime = GetActiveGame(controller);
            Assert.AreEqual(1, GetIntField(runtime, "currentLevelIndex"), "Reentry should load level 2.");
            Assert.AreEqual(GetExpectedBottleCount(1), GetBottles(runtime).Count, "Reentry should create level 2 layout.");
        }

        [UnityTest]
        public IEnumerator PauseExitSettlesCompletedBottlesWithoutChest()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(WaterSortGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            SetBottleState(
                runtime,
                new[]
                {
                    new[] { 0, 0, 0, 0 },
                    new[] { 1, 2 },
                    new int[0],
                    new int[0],
                    new int[0]
                });

            var gameRoot = GameObject.Find("WaterSortView");
            var pauseButton = gameRoot.transform.Find("PauseButton")?.GetComponent<Button>();
            Assert.IsNotNull(pauseButton, "Pause button should exist.");
            pauseButton.onClick.Invoke();
            yield return null;

            var pausePopup = gameRoot.transform.Find("PopupHost/MiniGamePausePopup");
            Assert.IsNotNull(pausePopup, "Pause popup should be visible.");

            var exitButton = pausePopup.Find("Dialog/MainButtons/ExitButton")?.GetComponent<Button>();
            Assert.IsNotNull(exitButton, "Exit button should exist.");
            exitButton.onClick.Invoke();
            yield return null;

            var settlementPopup = gameRoot.transform.Find("PopupHost/WaterSortSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Pause exit should show settlement popup.");

            var confirmButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Settlement confirm button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(WaterSortGameView.GameIdConstant);
            Assert.AreEqual(1, progress.PlayCount);
            Assert.AreEqual(1, progress.BestScore);
            Assert.AreEqual(12, progress.TotalCoinCount);
            Assert.AreEqual(0, progress.TotalChestCount);
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

        private static WaterSortGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as WaterSortGameView;
            Assert.IsNotNull(runtime, "WaterSort runtime was not created.");
            return runtime;
        }

        private static IList GetBottles(WaterSortGameView runtime)
        {
            var field = typeof(WaterSortGameView).GetField("bottles", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access bottles field.");
            var bottles = field.GetValue(runtime) as IList;
            Assert.IsNotNull(bottles, "Bottles list should exist.");
            return bottles;
        }

        private static void SetBottleState(WaterSortGameView runtime, int[][] state)
        {
            var bottles = GetBottles(runtime);
            bottles.Clear();
            for (var i = 0; i < state.Length; i++)
            {
                bottles.Add(new List<int>(state[i]));
            }

            Invoke(runtime, "RefreshAll");
        }

        private static void ClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            button.onClick.Invoke();
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

        private static void AssertBottlePositionsAreDistinct()
        {
            var first = GameObject.Find("WaterSortBottle_0")?.GetComponent<RectTransform>();
            var second = GameObject.Find("WaterSortBottle_1")?.GetComponent<RectTransform>();
            Assert.IsNotNull(first, "First bottle should exist.");
            Assert.IsNotNull(second, "Second bottle should exist.");
            Assert.Greater(
                Vector2.Distance(first.anchoredPosition, second.anchoredPosition),
                10f,
                "Bottle positions should not collapse into a single point.");
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, float timeout, string failureMessage)
        {
            var elapsed = 0f;
            while (elapsed < timeout)
            {
                if (condition())
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(condition(), failureMessage);
        }

        private static int GetIntField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (int)field.GetValue(target);
        }

        private static object[] GetLevelDefinitions()
        {
            var field = typeof(WaterSortGameView).GetField("LevelDefinitions", StaticPrivate);
            Assert.IsNotNull(field, "Failed to access LevelDefinitions field.");
            var definitions = field.GetValue(null) as object[];
            Assert.IsNotNull(definitions, "LevelDefinitions should be an object array.");
            return definitions;
        }

        private static int GetExpectedBottleCount(int levelIndex)
        {
            var definitions = GetLevelDefinitions();
            Assert.GreaterOrEqual(levelIndex, 0);
            Assert.Less(levelIndex, definitions.Length);
            return GetIntProperty(definitions[levelIndex], "BottleCount");
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, InstancePrivate);
            Assert.IsNotNull(property, "Failed to access property: " + propertyName);
            return (int)property.GetValue(target);
        }

        private static int[][] InvokeLayout(object target)
        {
            var method = target.GetType().GetMethod("CreateLayout", InstancePrivate);
            Assert.IsNotNull(method, "Failed to access CreateLayout method.");
            var layout = method.Invoke(target, null) as int[][];
            Assert.IsNotNull(layout, "CreateLayout should return a layout.");
            return layout;
        }

        private static bool CanSolveLayout(int[][] layout)
        {
            return CountSolverSolutionLength(layout) >= 0;
        }

        private static int CountSolverSolutionLength(int[][] layout)
        {
            var state = CopyState(layout);
            var seen = new Dictionary<string, int>();
            return TryFindSolutionLength(state, seen, 0, 96, 70000);
        }

        private static int TryFindSolutionLength(List<int>[] state, Dictionary<string, int> seen, int depth, int maxDepth, int maxStates)
        {
            if (IsSolvedState(state))
            {
                return depth;
            }

            if (depth >= maxDepth || seen.Count > maxStates)
            {
                return -1;
            }

            var key = CreateStateKey(state);
            int previousDepth;
            if (seen.TryGetValue(key, out previousDepth) && previousDepth <= depth)
            {
                return -1;
            }

            seen[key] = depth;
            var moves = ListSolverMoves(state);
            moves.Sort(CompareSolverMoves);
            for (var i = 0; i < moves.Count; i++)
            {
                var next = ApplySolverMove(state, moves[i]);
                var solutionLength = TryFindSolutionLength(next, seen, depth + 1, maxDepth, maxStates);
                if (solutionLength >= 0)
                {
                    return solutionLength;
                }
            }

            return -1;
        }

        private static bool TrySolveState(List<int>[] state, Dictionary<string, int> seen, int depth, int maxDepth, int maxStates)
        {
            if (IsSolvedState(state))
            {
                return true;
            }

            if (depth >= maxDepth || seen.Count > maxStates)
            {
                return false;
            }

            var key = CreateStateKey(state);
            int previousDepth;
            if (seen.TryGetValue(key, out previousDepth) && previousDepth <= depth)
            {
                return false;
            }

            seen[key] = depth;
            var moves = ListSolverMoves(state);
            moves.Sort(CompareSolverMoves);
            for (var i = 0; i < moves.Count; i++)
            {
                var next = ApplySolverMove(state, moves[i]);
                if (TrySolveState(next, seen, depth + 1, maxDepth, maxStates))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareSolverMoves(SolverMove left, SolverMove right)
        {
            return ScoreSolverMove(right).CompareTo(ScoreSolverMove(left));
        }

        private static int ScoreSolverMove(SolverMove move)
        {
            return move.Amount * 4 + (move.TargetHadWater ? 8 : 0) + (move.CompletesBottle ? 16 : 0) + (move.EmptiesSource ? 3 : 0);
        }

        private static List<SolverMove> ListSolverMoves(List<int>[] state)
        {
            var moves = new List<SolverMove>();
            for (var sourceIndex = 0; sourceIndex < state.Length; sourceIndex++)
            {
                var source = state[sourceIndex];
                if (source.Count == 0 || IsCompletedBottleState(source))
                {
                    continue;
                }

                var color = source[source.Count - 1];
                var amount = CountTopGroup(source);
                for (var targetIndex = 0; targetIndex < state.Length; targetIndex++)
                {
                    if (sourceIndex == targetIndex)
                    {
                        continue;
                    }

                    var target = state[targetIndex];
                    if (target.Count >= BottleCapacity || IsCompletedBottleState(target))
                    {
                        continue;
                    }

                    if (target.Count > 0 && target[target.Count - 1] != color)
                    {
                        continue;
                    }

                    var pourAmount = Mathf.Min(amount, BottleCapacity - target.Count);
                    if (pourAmount <= 0 || (target.Count == 0 && source.Count == pourAmount))
                    {
                        continue;
                    }

                    moves.Add(new SolverMove
                    {
                        SourceIndex = sourceIndex,
                        TargetIndex = targetIndex,
                        Amount = pourAmount,
                        TargetHadWater = target.Count > 0,
                        CompletesBottle = target.Count + pourAmount == BottleCapacity,
                        EmptiesSource = source.Count == pourAmount
                    });
                }
            }

            return moves;
        }

        private static List<int>[] ApplySolverMove(List<int>[] state, SolverMove move)
        {
            var next = CopyState(state);
            var source = next[move.SourceIndex];
            var target = next[move.TargetIndex];
            var color = source[source.Count - 1];
            for (var i = 0; i < move.Amount; i++)
            {
                target.Add(color);
                source.RemoveAt(source.Count - 1);
            }

            return next;
        }

        private static bool IsSolvedState(List<int>[] state)
        {
            for (var i = 0; i < state.Length; i++)
            {
                if (state[i].Count != 0 && !IsCompletedBottleState(state[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCompletedBottleState(List<int> bottle)
        {
            if (bottle == null || bottle.Count != BottleCapacity)
            {
                return false;
            }

            var color = bottle[0];
            for (var i = 1; i < bottle.Count; i++)
            {
                if (bottle[i] != color)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCompletedBottleLayout(int[] bottle)
        {
            if (bottle == null || bottle.Length != BottleCapacity)
            {
                return false;
            }

            var color = bottle[0];
            for (var i = 1; i < bottle.Length; i++)
            {
                if (bottle[i] != color)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountTopGroup(List<int> bottle)
        {
            var color = bottle[bottle.Count - 1];
            var count = 0;
            for (var i = bottle.Count - 1; i >= 0; i--)
            {
                if (bottle[i] != color)
                {
                    break;
                }

                count += 1;
            }

            return count;
        }

        private static string CreateStateKey(List<int>[] state)
        {
            var builder = new StringBuilder();
            for (var bottleIndex = 0; bottleIndex < state.Length; bottleIndex++)
            {
                if (bottleIndex > 0)
                {
                    builder.Append(';');
                }

                var bottle = state[bottleIndex];
                for (var layerIndex = 0; layerIndex < bottle.Count; layerIndex++)
                {
                    if (layerIndex > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(bottle[layerIndex]);
                }
            }

            return builder.ToString();
        }

        private static List<int>[] CopyState(int[][] source)
        {
            var copy = new List<int>[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = new List<int>(source[i]);
            }

            return copy;
        }

        private static List<int>[] CopyState(List<int>[] source)
        {
            var copy = new List<int>[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = new List<int>(source[i]);
            }

            return copy;
        }

        private struct SolverMove
        {
            public int SourceIndex;
            public int TargetIndex;
            public int Amount;
            public bool TargetHadWater;
            public bool CompletesBottle;
            public bool EmptiesSource;
        }

        private static int GetCollectionCount(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            var collection = field.GetValue(target) as ICollection;
            Assert.IsNotNull(collection, "Field is not a collection: " + fieldName);
            return collection.Count;
        }

        private static Vector2 GetVector2Field(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (Vector2)field.GetValue(target);
        }

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }

            return degrees;
        }

        private static void Invoke(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, InstancePrivate);
            Assert.IsNotNull(method, "Failed to access method: " + methodName);
            method.Invoke(target, null);
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
