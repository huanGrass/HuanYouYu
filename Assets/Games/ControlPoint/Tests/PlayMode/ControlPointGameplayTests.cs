using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HuanYouYu.MiniGameHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Tests
{
    public class ControlPointGameplayTests
    {
        private const float SimulatedDeltaTime = 0.1f;

        [SetUp]
        public void SetUp()
        {
            CleanupTestObjects();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupTestObjects();
        }

        [Test]
        public void ControlPointLoadsGeneratedLevelCatalog()
        {
            Assert.AreEqual(100, GameControlPointView.LevelCount, "Generated level catalog should contain 100 levels.");
            var levels = GetLevelDefinitions();
            Assert.AreEqual(100, levels.Length, "Level definitions should be loaded from resource json.");

            AssertLevelDefinitionPoint(levels[0], 0, "Neutral", 8, new Vector2(0f, 230f));
            AssertLevelDefinitionPoint(levels[0], 1, "Player", 12, new Vector2(-238f, -214f));
            AssertLevelDefinitionPoint(levels[0], 2, "Enemy", 12, new Vector2(238f, -214f));
            AssertLevelDefinitionPoint(levels[0], 5, "EnemyTwo", 12, new Vector2(0f, -282f));
            AssertLevelDefinitionPoint(levels[0], 6, "EnemyThree", 12, new Vector2(0f, -36f));
        }

        [Test]
        public void ControlPointGeneratedLevelsStayValidAndIncreaseByDecade()
        {
            var levels = GetLevelDefinitions();
            var scores = new float[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                var points = GetField<object[]>(levels[i], "Points");
                var positions = GetField<Vector2[]>(levels[i], "Positions");
                Assert.GreaterOrEqual(points.Length, 5, "Level should have enough points.");
                Assert.LessOrEqual(points.Length, 10, "Level should not have too many points.");
                Assert.AreEqual(points.Length, positions.Length, "Point positions should match point count.");

                var hasPlayer = false;
                var hasEnemy = false;
                for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    var owner = GetField<object>(points[pointIndex], "Owner").ToString();
                    var units = GetField<int>(points[pointIndex], "UnitCount");
                    Assert.GreaterOrEqual(units, 1, "Point units should be positive.");
                    hasPlayer |= owner == "Player";
                    hasEnemy |= IsEnemyOwnerName(owner);
                    Assert.GreaterOrEqual(positions[pointIndex].x, -285f, "Point x should be inside content range.");
                    Assert.LessOrEqual(positions[pointIndex].x, 285f, "Point x should be inside content range.");
                    Assert.GreaterOrEqual(positions[pointIndex].y, -305f, "Point y should be inside content range.");
                    Assert.LessOrEqual(positions[pointIndex].y, 245f, "Point y should be inside content range.");

                    for (var previousIndex = 0; previousIndex < pointIndex; previousIndex++)
                    {
                        Assert.GreaterOrEqual(
                            Vector2.Distance(positions[pointIndex], positions[previousIndex]),
                            128f,
                            "Point positions should not overlap.");
                    }
                }

                Assert.IsTrue(hasPlayer, "Level should contain a player point.");
                Assert.IsTrue(hasEnemy, "Level should contain an enemy point.");
                scores[i] = ScoreLevelDefinition(levels[i]);
            }

            for (var decadeStart = 10; decadeStart < scores.Length; decadeStart += 10)
            {
                var previousAverage = Average(scores, decadeStart - 10, 10);
                var currentAverage = Average(scores, decadeStart, Mathf.Min(10, scores.Length - decadeStart));
                Assert.Greater(currentAverage, previousAverage, "Difficulty average should increase by decade.");
            }
        }

        [UnityTest]
        public IEnumerator ControlPointBuildsInitialPointNumbers()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            Assert.IsNotNull(GameObject.Find("GameControlPointView"), "Game root should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPointContent"), "Content should exist.");
            Assert.IsNotNull(GameObject.Find("RestartButton"), "Restart button should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPointLevelSelectButton"), "Level select button should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPoint_0"), "Top neutral point should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPoint_1"), "Player point should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPoint_2"), "Enemy point should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPoint_6"), "Additional enemy point should exist.");
            Assert.AreEqual(7, GetPointCount(game), "First generated level should keep the baseline point count.");
            AssertPoint(game, 0, "Neutral", 8);
            AssertPoint(game, 1, "Player", 12);
            AssertPoint(game, 2, "Enemy", 12);
            AssertPoint(game, 3, "Neutral", 8);
            AssertPoint(game, 4, "Neutral", 8);
            AssertPoint(game, 5, "EnemyTwo", 12);
            AssertPoint(game, 6, "EnemyThree", 12);
            AssertPointPosition(0, new Vector2(0f, 230f));
            AssertPointPosition(1, new Vector2(-238f, -214f));
            AssertPointPosition(2, new Vector2(238f, -214f));

            for (var i = 0; i < GetPointCount(game); i++)
            {
                var point = GameObject.Find("ControlPoint_" + i);
                Assert.IsNotNull(point, "Point object should exist.");
                Assert.AreEqual(2, CountTextMeshProLabels(point), "Point should show unit number and level label.");
                AssertLevelLabel(i, "Lv1");
            }

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator LevelSelectOpensWithCurrentLevel()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            Click("ControlPointLevelSelectButton");
            yield return null;

            Assert.IsNotNull(GameObject.Find("ControlPointLevelSelectPanel"), "Level select panel should open.");
            Assert.IsNotNull(GameObject.Find("ControlPointLevelButton_1"), "First level button should exist.");
            Assert.IsNotNull(GameObject.Find("ControlPointLevelButton_" + GameControlPointView.LevelCount), "Last level button should exist.");
            Assert.IsFalse(GameObject.Find("ControlPointLevelButton_2").GetComponent<Button>().interactable, "Locked level should not be selectable at first.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator OwnedPointsProduceUnitsButNeutralDoesNot()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            TickGame(game, 11);

            AssertPoint(game, 0, "Neutral", 8);
            AssertPoint(game, 1, "Player", 13);
            AssertPoint(game, 2, "Enemy", 13);
            AssertPoint(game, 3, "Neutral", 8);
            AssertPoint(game, 4, "Neutral", 8);
            AssertPoint(game, 5, "EnemyTwo", 13);
            AssertPoint(game, 6, "EnemyThree", 13);

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator LevelOnePointKeepsExistingConnectionWhenFull()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DragBetweenPoints(game, 1, 0);
            Assert.AreEqual(1, GetListCount(GetPrivateObject<object>(game, "connections")), "Dragging should create one connection.");
            AssertConnection(game, 0, 1, 0, "Player");
            Assert.IsNotNull(GameObject.Find("Connection_1_0/Arrow"), "Connection should show an arrow.");

            DragBetweenPoints(game, 1, 2);
            Assert.AreEqual(1, GetListCount(GetPrivateObject<object>(game, "connections")), "Full level one point should keep one connection.");
            AssertConnection(game, 0, 1, 0, "Player");
            Assert.IsNotNull(GameObject.Find("Connection_1_0/Arrow"), "Existing connection should remain.");
            Assert.IsNull(GameObject.Find("Connection_1_2"), "Full source should reject a new connection until a line is cut.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator PointLevelThresholdsUpdateLevelLabel()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 19);
            game.Tick(0f);
            AssertLevelLabel(1, "Lv1");

            SetField(GetPoint(game, 1), "UnitCount", 20);
            game.Tick(0f);
            AssertLevelLabel(1, "Lv2");

            SetField(GetPoint(game, 1), "UnitCount", 39);
            game.Tick(0f);
            AssertLevelLabel(1, "Lv2");

            SetField(GetPoint(game, 1), "UnitCount", 40);
            game.Tick(0f);
            AssertLevelLabel(1, "Lv3");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator HigherLevelPointsUseFasterProduction()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 20);
            SetField(GetPoint(game, 1), "ProduceTimer", 0f);
            game.Tick(0.7f);
            AssertPoint(game, 1, "Player", 20);
            game.Tick(0.1f);
            AssertPoint(game, 1, "Player", 21);

            SetField(GetPoint(game, 1), "UnitCount", 40);
            SetField(GetPoint(game, 1), "ProduceTimer", 0f);
            game.Tick(0.5f);
            AssertPoint(game, 1, "Player", 40);
            game.Tick(0.1f);
            AssertPoint(game, 1, "Player", 41);

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator LevelTwoPointAllowsTwoConnectionsAndRejectsThird()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 20);
            EstablishConnection(game, 1, 0, "Player");
            EstablishConnection(game, 1, 2, "Player");
            EstablishConnection(game, 1, 3, "Player");

            Assert.AreEqual(2, GetListCount(GetPrivateObject<object>(game, "connections")), "Level two source should keep two outgoing connections.");
            AssertConnection(game, 0, 1, 0, "Player");
            AssertConnection(game, 1, 1, 2, "Player");
            Assert.IsNull(GameObject.Find("Connection_1_3"), "Third connection should be rejected.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator LevelThreePointAllowsThreeConnectionsAndRejectsFourth()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 40);
            EstablishConnection(game, 1, 0, "Player");
            EstablishConnection(game, 1, 2, "Player");
            EstablishConnection(game, 1, 3, "Player");
            EstablishConnection(game, 1, 4, "Player");

            Assert.AreEqual(3, GetListCount(GetPrivateObject<object>(game, "connections")), "Level three source should keep three outgoing connections.");
            AssertConnection(game, 0, 1, 0, "Player");
            AssertConnection(game, 1, 1, 2, "Player");
            AssertConnection(game, 2, 1, 3, "Player");
            Assert.IsNull(GameObject.Find("Connection_1_4"), "Fourth connection should be rejected.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator CuttingPlayerLineFreesConnectionCapacity()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 20);
            EstablishConnection(game, 1, 0, "Player");
            EstablishConnection(game, 1, 2, "Player");
            EstablishConnection(game, 1, 3, "Player");
            Assert.AreEqual(2, GetListCount(GetPrivateObject<object>(game, "connections")), "Source should start full.");

            BeginAndUpdateCutAcrossConnection(game, 1, 0);
            InvokePrivate(game, "EndCutGesture");
            yield return null;

            EstablishConnection(game, 1, 3, "Player");
            Assert.AreEqual(2, GetListCount(GetPrivateObject<object>(game, "connections")), "Cutting a line should free one connection slot.");
            Assert.IsNull(GameObject.Find("Connection_1_0"), "Cut connection should be gone.");
            Assert.IsNotNull(GameObject.Find("Connection_1_2"), "Uncut connection should remain.");
            Assert.IsNotNull(GameObject.Find("Connection_1_3"), "New connection should be created after cutting.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator DraggingAcrossPlayerLineCutsConnectionAndKeepsMovingUnits()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 3);
            SetField(GetPoint(game, 1), "ProduceTimer", -99f);
            EstablishConnection(game, 1, 0, "Player");
            game.Tick(0.6f);

            Assert.AreEqual(1, GetListCount(GetPrivateObject<object>(game, "connections")), "Player connection should exist before cutting.");
            Assert.IsNotNull(GameObject.Find("Soldier_1_0"), "Moving unit should exist before cutting.");

            BeginAndUpdateCutAcrossConnection(game, 1, 0);
            yield return null;

            Assert.IsNotNull(GameObject.Find("CutGestureLine"), "Swipe should show a visible line before releasing.");
            Assert.AreEqual(1, GetListCount(GetPrivateObject<object>(game, "connections")), "Route should not be cut until the swipe is released.");

            InvokePrivate(game, "EndCutGesture");
            yield return null;

            Assert.AreEqual(0, GetListCount(GetPrivateObject<object>(game, "connections")), "Dragging across a player line should cut the route.");
            Assert.IsNull(GameObject.Find("Connection_1_0"), "Cut connection visual should be removed.");
            Assert.IsNotNull(GameObject.Find("Soldier_1_0"), "Cutting a route should keep moving units travelling.");
            Assert.IsNull(GameObject.Find("CutGestureLine"), "Swipe line should disappear after releasing.");

            game.Tick(3.0f);
            AssertPoint(game, 0, "Neutral", 7);
            yield return null;
            Assert.IsNull(GameObject.Find("Soldier_1_0"), "Detached moving unit should be removed after reaching the original target.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator CuttingGestureShowsTemporarySwipeLine()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            var contentRect = GameObject.Find("ControlPointContent").GetComponent<RectTransform>();
            var start = RectTransformUtility.WorldToScreenPoint(null, contentRect.TransformPoint(new Vector2(-120f, 0f)));
            var end = RectTransformUtility.WorldToScreenPoint(null, contentRect.TransformPoint(new Vector2(120f, 0f)));

            InvokePrivate(game, "BeginCutGesture", start, null);
            InvokePrivate(game, "UpdateCutGesture", end, null);

            Assert.IsNotNull(GameObject.Find("CutGestureLine"), "Swipe should show a temporary line while dragging.");

            InvokePrivate(game, "EndCutGesture");
            yield return null;

            Assert.IsNull(GameObject.Find("CutGestureLine"), "Swipe line should disappear after releasing.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator DraggingAcrossEnemyLineDoesNotCutConnection()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            EstablishConnection(game, 2, 0, "Enemy");

            BeginAndUpdateCutAcrossConnection(game, 2, 0);
            InvokePrivate(game, "EndCutGesture");
            yield return null;

            Assert.AreEqual(1, GetListCount(GetPrivateObject<object>(game, "connections")), "Dragging should not cut enemy routes.");
            AssertConnection(game, 0, 2, 0, "Enemy");
            Assert.IsNotNull(GameObject.Find("Connection_2_0"), "Enemy connection visual should remain.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator ConnectionTransfersUnitsAndKeepsOneAtSource()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 2);
            SetField(GetPoint(game, 1), "ProduceTimer", -99f);
            EstablishConnection(game, 1, 0, "Player");
            game.Tick(0.6f);

            AssertPoint(game, 1, "Player", 1);
            AssertPoint(game, 0, "Neutral", 8);
            Assert.IsNotNull(GameObject.Find("Soldier_1_0"), "Transferred unit should be visible while travelling.");

            game.Tick(2.3f);
            AssertPoint(game, 0, "Neutral", 7);
            yield return null;
            Assert.IsNull(GameObject.Find("Soldier_1_0"), "Transferred unit should be removed after arrival.");

            SetField(GetPoint(game, 1), "UnitCount", 1);
            SetField(GetPoint(game, 1), "ProduceTimer", -1f);
            SetField(GetPoint(game, 0), "UnitCount", 8);
            game.Tick(0.6f);

            AssertPoint(game, 1, "Player", 1);
            AssertPoint(game, 0, "Neutral", 8);

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator PlayerConnectionCanCaptureNeutralOrEnemyPoint()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 20);
            SetField(GetPoint(game, 0), "UnitCount", 1);
            EstablishConnection(game, 1, 0, "Player");
            game.Tick(0.6f);
            game.Tick(2.3f);

            AssertPoint(game, 0, "Player", 1);

            SetField(GetPoint(game, 1), "UnitCount", 20);
            SetField(GetPoint(game, 2), "UnitCount", 1);
            SetField(GetPoint(game, 2), "ProduceTimer", -99f);
            EstablishConnection(game, 1, 2, "Player");
            game.Tick(0.6f);
            game.Tick(1.8f);

            AssertPoint(game, 2, "Player", 1);
            Assert.GreaterOrEqual(GetPrivateValue<int>(game, "defeatedEnemyUnits"), 1);

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator OpposingConnectionsMeetAtMiddleAndConsumeSoldiers()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 2);
            SetField(GetPoint(game, 1), "ProduceTimer", -99f);
            SetField(GetPoint(game, 2), "UnitCount", 2);
            SetField(GetPoint(game, 2), "ProduceTimer", -99f);
            EstablishConnection(game, 1, 2, "Player");
            EstablishConnection(game, 2, 1, "Enemy");

            var playerLine = GameObject.Find("Connection_1_2").GetComponent<RectTransform>();
            var enemyLine = GameObject.Find("Connection_2_1").GetComponent<RectTransform>();
            Assert.Less(playerLine.sizeDelta.x, 220f, "Player line should stop at the middle during opposing fire.");
            Assert.Less(enemyLine.sizeDelta.x, 220f, "Enemy line should stop at the middle during opposing fire.");

            game.Tick(0.6f);
            AssertPoint(game, 1, "Player", 1);
            AssertPoint(game, 2, "Enemy", 1);
            Assert.IsNotNull(GameObject.Find("Soldier_1_2"), "Player soldier should move toward the middle.");
            Assert.IsNotNull(GameObject.Find("Soldier_2_1"), "Enemy soldier should move toward the middle.");

            game.Tick(1.5f);
            AssertPoint(game, 1, "Player", 1);
            AssertPoint(game, 2, "Enemy", 1);
            yield return null;
            Assert.IsNull(GameObject.Find("Soldier_1_2"), "Player soldier should be consumed at the middle.");
            Assert.IsNull(GameObject.Find("Soldier_2_1"), "Enemy soldier should be consumed at the middle.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator InFlightSoldierRedirectsToMiddleWithoutDisappearing()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            DisableEnemyAi(game);
            SetField(GetPoint(game, 1), "UnitCount", 3);
            SetField(GetPoint(game, 1), "ProduceTimer", -99f);
            SetField(GetPoint(game, 2), "UnitCount", 3);
            SetField(GetPoint(game, 2), "ProduceTimer", -99f);
            EstablishConnection(game, 2, 1, "Enemy");

            game.Tick(0.6f);
            game.Tick(0.4f);
            Assert.IsNotNull(GameObject.Find("Soldier_2_1"), "Enemy soldier should be travelling before player counters.");
            var beforeRedirectDistance = Vector2.Distance(
                GameObject.Find("Soldier_2_1").GetComponent<RectTransform>().anchoredPosition,
                GetMiddlePosition(1, 2));

            EstablishConnection(game, 1, 2, "Player");
            game.Tick(0.2f);
            Assert.IsNotNull(GameObject.Find("Soldier_2_1"), "Enemy soldier should redirect to the middle instead of disappearing.");
            Assert.Less(
                Vector2.Distance(
                    GameObject.Find("Soldier_2_1").GetComponent<RectTransform>().anchoredPosition,
                    GetMiddlePosition(1, 2)),
                beforeRedirectDistance,
                "Redirected enemy soldier should keep moving toward the middle based on remaining distance.");

            game.Tick(0.6f);
            Assert.IsNotNull(GameObject.Find("Soldier_2_1"), "Enemy soldier should remain visible while moving toward the middle.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator EnemyAiCreatesAConnectionUsingSameRules()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            TickGame(game, 18);

            Assert.AreEqual(3, GetListCount(GetPrivateObject<object>(game, "connections")), "Each enemy AI should create one outgoing connection.");
            AssertConnection(game, 0, 2, 0, "Enemy");
            AssertConnection(game, 1, 5, 0, "EnemyTwo");
            AssertConnection(game, 2, 6, 0, "EnemyThree");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator EnemyAiKeepsExistingSoldiersWhenRetargetingSamePoint()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            TickGame(game, 26);

            Assert.AreEqual(3, GetListCount(GetPrivateObject<object>(game, "connections")), "Enemy AI should keep existing connections.");
            AssertConnection(game, 0, 2, 0, "Enemy");
            AssertConnection(game, 1, 5, 0, "EnemyTwo");
            AssertConnection(game, 2, 6, 0, "EnemyThree");
            Assert.IsNotNull(GameObject.Find("Soldier_2_0"), "Enemy soldier should still be travelling instead of being cleared by the next AI decision.");
            AssertPoint(game, 0, "Neutral", 8);

            TickGame(game, 10);

            Assert.AreEqual("Neutral", GetField<object>(GetPoint(game, 0), "Owner").ToString(), "Enemy soldiers should not capture the neutral point yet.");
            Assert.Less(GetField<int>(GetPoint(game, 0), "UnitCount"), 8, "Enemy soldiers should reduce the neutral point after arrival.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator PlayerAndEnemyFullControlStillSettle()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            ForceOwners(game, "Player", 9);
            game.Tick(SimulatedDeltaTime);
            yield return null;

            Assert.IsTrue(GetPrivateValue<bool>(game, "isSettled"), "Owning all points should settle the game.");
            Assert.IsNotNull(GameObject.Find("ControlPointSettlementPanel"), "Win settlement popup should be shown.");
            var settlement = InvokeBuildSettlement(game);
            Assert.AreEqual(1, settlement.ChestCount);

            game.Dispose();
            Object.Destroy(root);
            yield return null;

            root = CreateGameRoot(out game);
            yield return null;
            ForceOwners(game, "Enemy", 9);
            game.Tick(SimulatedDeltaTime);
            yield return null;

            settlement = InvokeBuildSettlement(game);
            Assert.AreEqual(0, settlement.ChestCount);

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator WinningUnlocksAndLoadsNextLevel()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            ForceOwners(game, "Player", 9);
            game.Tick(SimulatedDeltaTime);
            yield return null;

            Assert.IsNotNull(GameObject.Find("ControlPointSettlementPanel"), "Win settlement should show next level action.");
            Click("NextButton");
            yield return null;

            Assert.IsFalse(GetPrivateValue<bool>(game, "isSettled"), "Next level should start a fresh round.");
            Assert.AreEqual(1, GetPrivateValue<int>(game, "currentLevelIndex"), "Next level should become current.");
            Assert.AreEqual(6, GetPointCount(game), "Second level should rebuild the point list with its generated point count.");
            AssertPoint(game, 0, "Neutral", 11);
            AssertPoint(game, 1, "Neutral", 7);
            AssertPoint(game, 2, "Enemy", 13);
            AssertPoint(game, 3, "Player", 16);
            AssertPoint(game, 4, "Neutral", 8);
            AssertPoint(game, 5, "Neutral", 7);
            Assert.IsNull(GameObject.Find("ControlPoint_6"), "Point views from the previous level should be removed.");

            yield return DestroyGameRoot(game, root);
        }

        [UnityTest]
        public IEnumerator RestartRestoresInitialStateAndClearsConnections()
        {
            GameControlPointView game;
            var root = CreateGameRoot(out game);
            yield return null;

            EstablishConnection(game, 1, 0, "Player");
            ForceOwners(game, "Player", 9);
            game.Tick(SimulatedDeltaTime);
            yield return null;
            Assert.IsNotNull(GameObject.Find("ControlPointSettlementPanel"), "Settlement popup should exist before restart.");

            Click("RestartButton");
            yield return null;

            Assert.IsFalse(GetPrivateValue<bool>(game, "isSettled"), "Restart should return to unsettled state.");
            Assert.IsNull(GameObject.Find("ControlPointSettlementPanel"), "Restart should close settlement popup.");
            Assert.AreEqual(0, GetListCount(GetPrivateObject<object>(game, "connections")), "Restart should clear all connections.");
            AssertPoint(game, 0, "Neutral", 8);
            AssertPoint(game, 1, "Player", 12);
            AssertPoint(game, 2, "Enemy", 12);
            AssertPoint(game, 3, "Neutral", 8);
            AssertPoint(game, 4, "Neutral", 8);
            AssertPoint(game, 5, "EnemyTwo", 12);
            AssertPoint(game, 6, "EnemyThree", 12);

            yield return DestroyGameRoot(game, root);
        }

        private static GameObject CreateGameRoot(out GameControlPointView game)
        {
            var root = new GameObject("ControlPointTestRoot");
            var canvasObject = new GameObject("MiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(750f, 1334f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (Object.FindObjectOfType<AudioListener>() == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(root.transform, false);
            }

            root.AddComponent<MiniGameSfxPlayer>();
            EnsureEventSystem();

            var host = root.AddComponent<TestHostBehaviour>();
            game = new GameControlPointView(host, canvas.transform, _ => { }, () => { });
            return root;
        }

        private static IEnumerator DestroyGameRoot(GameControlPointView game, GameObject root)
        {
            game.Dispose();
            Object.Destroy(root);
            yield return null;
        }

        private static void CleanupTestObjects()
        {
            var roots = Object.FindObjectsOfType<Transform>();
            for (var i = roots.Length - 1; i >= 0; i--)
            {
                var transform = roots[i];
                if (transform == null || transform.parent != null)
                {
                    continue;
                }

                if (transform.name == "ControlPointTestRoot" || transform.name == "EventSystem")
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        private static void Click(string objectName)
        {
            var button = GameObject.Find(objectName)?.GetComponent<Button>();
            Assert.IsNotNull(button, "Missing button: " + objectName);
            button.onClick.Invoke();
        }

        private static void TickGame(GameControlPointView game, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                game.Tick(SimulatedDeltaTime);
            }
        }

        private static void DisableEnemyAi(GameControlPointView game)
        {
            var timers = GetPrivateObject<float[]>(game, "enemyThinkTimers");
            for (var i = 0; i < timers.Length; i++)
            {
                timers[i] = 99f;
            }
        }

        private static void DragBetweenPoints(GameControlPointView game, int sourceIndex, int targetIndex)
        {
            InvokePrivate(game, "BeginPlayerDrag", sourceIndex);
            var targetRect = GameObject.Find("ControlPoint_" + targetIndex).GetComponent<RectTransform>();
            var screenPosition = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
            InvokePrivate(game, "EndPlayerDrag", screenPosition, null);
        }

        private static Vector2 GetMiddlePosition(int firstPointIndex, int secondPointIndex)
        {
            var first = GameObject.Find("ControlPoint_" + firstPointIndex).GetComponent<RectTransform>().anchoredPosition;
            var second = GameObject.Find("ControlPoint_" + secondPointIndex).GetComponent<RectTransform>().anchoredPosition;
            return (first + second) * 0.5f;
        }

        private static void BeginAndUpdateCutAcrossConnection(GameControlPointView game, int sourceIndex, int targetIndex)
        {
            var source = GameObject.Find("ControlPoint_" + sourceIndex).GetComponent<RectTransform>().anchoredPosition;
            var target = GameObject.Find("ControlPoint_" + targetIndex).GetComponent<RectTransform>().anchoredPosition;
            var middle = (source + target) * 0.5f;
            var direction = (target - source).normalized;
            var perpendicular = new Vector2(-direction.y, direction.x);
            var contentRect = GameObject.Find("ControlPointContent").GetComponent<RectTransform>();
            var start = RectTransformUtility.WorldToScreenPoint(null, contentRect.TransformPoint(middle - (perpendicular * 140f)));
            var end = RectTransformUtility.WorldToScreenPoint(null, contentRect.TransformPoint(middle + (perpendicular * 140f)));
            InvokePrivate(game, "BeginCutGesture", start, null);
            InvokePrivate(game, "UpdateCutGesture", end, null);
        }

        private static void EstablishConnection(GameControlPointView game, int sourceIndex, int targetIndex, string ownerName)
        {
            var ownerType = typeof(GameControlPointView).GetNestedType("ControlPointOwner", BindingFlags.NonPublic);
            var ownerValue = System.Enum.Parse(ownerType, ownerName);
            InvokePrivate(game, "EstablishConnection", sourceIndex, targetIndex, ownerValue);
        }

        private static void ForceOwners(GameControlPointView game, string ownerName, int units)
        {
            var ownerType = typeof(GameControlPointView).GetNestedType("ControlPointOwner", BindingFlags.NonPublic);
            var ownerValue = System.Enum.Parse(ownerType, ownerName);
            var points = GetPrivateObject<object[]>(game, "points");
            for (var i = 0; i < points.Length; i++)
            {
                SetField(points[i], "Owner", ownerValue);
                SetField(points[i], "UnitCount", units);
            }
        }

        private static void AssertPoint(GameControlPointView game, int index, string ownerName, int units)
        {
            var point = GetPoint(game, index);
            Assert.AreEqual(ownerName, GetField<object>(point, "Owner").ToString(), "Unexpected owner at point " + index);
            Assert.AreEqual(units, GetField<int>(point, "UnitCount"), "Unexpected unit count at point " + index);
        }

        private static void AssertConnection(GameControlPointView game, int index, int source, int target, string side)
        {
            var connections = GetPrivateObject<System.Collections.IList>(game, "connections");
            Assert.Greater(connections.Count, index, "Connection should exist.");
            var connection = connections[index];
            Assert.AreEqual(source, GetField<int>(connection, "SourceIndex"));
            Assert.AreEqual(target, GetField<int>(connection, "TargetIndex"));
            Assert.AreEqual(side, GetField<object>(connection, "Side").ToString());
        }

        private static void AssertLevelLabel(int pointIndex, string expected)
        {
            var levelObject = GameObject.Find("ControlPoint_" + pointIndex + "/Level");
            Assert.IsNotNull(levelObject, "Level label should exist.");
            var levelLabel = FindTextMeshProComponent(levelObject);
            Assert.IsNotNull(levelLabel, "Level label should exist.");
            Assert.AreEqual(expected, GetProperty<string>(levelLabel, "text"), "Unexpected level label at point " + pointIndex);
        }

        private static void AssertPointPosition(int pointIndex, Vector2 expected)
        {
            var point = GameObject.Find("ControlPoint_" + pointIndex);
            Assert.IsNotNull(point, "Point object should exist.");
            var rect = point.GetComponent<RectTransform>();
            Assert.AreEqual(expected.x, rect.anchoredPosition.x, 0.01f, "Unexpected point x at " + pointIndex);
            Assert.AreEqual(expected.y, rect.anchoredPosition.y, 0.01f, "Unexpected point y at " + pointIndex);
        }

        private static void AssertLevelDefinitionPoint(object level, int pointIndex, string ownerName, int units, Vector2 position)
        {
            var points = GetField<object[]>(level, "Points");
            var positions = GetField<Vector2[]>(level, "Positions");
            Assert.Greater(points.Length, pointIndex, "Level point should exist.");
            Assert.AreEqual(ownerName, GetField<object>(points[pointIndex], "Owner").ToString(), "Unexpected generated owner.");
            Assert.AreEqual(units, GetField<int>(points[pointIndex], "UnitCount"), "Unexpected generated units.");
            Assert.AreEqual(position.x, positions[pointIndex].x, 0.01f, "Unexpected generated x.");
            Assert.AreEqual(position.y, positions[pointIndex].y, 0.01f, "Unexpected generated y.");
        }

        private static float ScoreLevelDefinition(object level)
        {
            var points = GetField<object[]>(level, "Points");
            var positions = GetField<Vector2[]>(level, "Positions");
            var pointCount = points.Length;
            var enemyPointCount = 0;
            var playerUnits = 0;
            var enemyUnits = 0;
            var neutralUnits = 0;
            var enemyOwners = new HashSet<string>();
            var playerPositions = new System.Collections.Generic.List<Vector2>();

            for (var i = 0; i < points.Length; i++)
            {
                var owner = GetField<object>(points[i], "Owner").ToString();
                var units = GetField<int>(points[i], "UnitCount");
                if (owner == "Player")
                {
                    playerUnits += units;
                    playerPositions.Add(positions[i]);
                }
                else if (owner == "Neutral")
                {
                    neutralUnits += units;
                }
                else if (IsEnemyOwnerName(owner))
                {
                    enemyPointCount++;
                    enemyUnits += units;
                    enemyOwners.Add(owner);
                }
            }

            var enemyPressure = 0f;
            if (playerPositions.Count > 0)
            {
                for (var i = 0; i < points.Length; i++)
                {
                    var owner = GetField<object>(points[i], "Owner").ToString();
                    if (!IsEnemyOwnerName(owner))
                    {
                        continue;
                    }

                    var nearest = float.MaxValue;
                    for (var playerIndex = 0; playerIndex < playerPositions.Count; playerIndex++)
                    {
                        nearest = Mathf.Min(nearest, Vector2.Distance(positions[i], playerPositions[playerIndex]));
                    }

                    enemyPressure += Mathf.Max(0f, 330f - nearest) / 24f;
                }
            }

            return (pointCount * 5f) +
                (enemyPointCount * 9.5f) +
                (enemyOwners.Count * 8f) +
                (enemyUnits * 1.15f) +
                (neutralUnits * 0.55f) +
                enemyPressure -
                (playerUnits * 0.75f);
        }

        private static float Average(float[] values, int start, int count)
        {
            var total = 0f;
            for (var i = start; i < start + count; i++)
            {
                total += values[i];
            }

            return total / count;
        }

        private static bool IsEnemyOwnerName(string owner)
        {
            return owner == "Enemy" || owner == "EnemyTwo" || owner == "EnemyThree";
        }

        private static object[] GetLevelDefinitions()
        {
            var field = typeof(GameControlPointView).GetField("LevelDefinitions", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing LevelDefinitions field.");
            return field.GetValue(null) as object[];
        }

        private static int GetPointCount(GameControlPointView game)
        {
            return GetPrivateObject<object[]>(game, "points").Length;
        }

        private static object GetPoint(GameControlPointView game, int index)
        {
            return GetPrivateObject<object[]>(game, "points")[index];
        }

        private static MiniGameSettlement InvokeBuildSettlement(GameControlPointView game)
        {
            var method = typeof(GameControlPointView).GetMethod("BuildSettlement", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "BuildSettlement should exist.");
            return (MiniGameSettlement)method.Invoke(game, null);
        }

        private static int CountTextMeshProLabels(GameObject root)
        {
            var count = 0;
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "TextMeshProUGUI")
                {
                    count++;
                }
            }

            return count;
        }

        private static Component FindTextMeshProComponent(GameObject root)
        {
            var components = root.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "TextMeshProUGUI")
                {
                    return components[i];
                }
            }

            return null;
        }

        private static int GetListCount(object list)
        {
            Assert.IsNotNull(list, "List should exist.");
            return ((System.Collections.ICollection)list).Count;
        }

        private static T GetPrivateValue<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateValue(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field: " + fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "Missing property: " + propertyName);
            return (T)property.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field: " + fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateObject<T>(object target, string fieldName)
            where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field: " + fieldName);
            return field.GetValue(target) as T;
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing private method: " + methodName);
            method.Invoke(target, args);
        }

        private sealed class TestHostBehaviour : MonoBehaviour
        {
        }
    }
}
