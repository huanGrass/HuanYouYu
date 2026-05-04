using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class StackMatchGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const float MoveToTrayDuration = 0.30f;
        private const int BoardGridColumns = 16;
        private const int BoardGridRows = 20;
        private const int CardGridSize = 2;
        private const float CardWidth = 82f;
        private const float CardHeight = 96f;
        private const float BlindBoxStepScale = 0.25f;

        [Test]
        public void StackMatchTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/stack-match.ui_texts.zh-CN"), "StackMatch text catalog should exist.");
        }

        [Test]
        public void StackMatchIconResourcesAllExist()
        {
            var field = typeof(StackMatchGameView).GetField("IconResourcePaths", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "StackMatch icon path list should exist.");

            var paths = field.GetValue(null) as string[];
            Assert.IsNotNull(paths, "StackMatch icon paths should be available.");
            Assert.AreEqual(18, paths.Length, "StackMatch should keep the shared icon catalog available.");

            for (var i = 0; i < paths.Length; i++)
            {
                Assert.IsNotNull(Resources.Load<Sprite>(paths[i]), "Missing StackMatch icon resource: " + paths[i]);
            }
        }

        [UnityTest]
        public IEnumerator CanEnterStackMatchAndShowFirstLevel()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "StackMatch should become the active game.");
            Assert.IsNotNull(GameObject.Find("StackMatchView"), "StackMatch shell root should exist.");
            Assert.IsNotNull(GameObject.Find("StackMatchBoard"), "StackMatch board should exist.");
            Assert.IsNotNull(GameObject.Find("StackMatchTray"), "StackMatch tray should exist.");
            Assert.IsNull(GameObject.Find("TraySlot_1"), "StackMatch tray should not pre-draw empty slots.");
            Assert.IsNull(FindButton("RestartButton"), "Restart button should be hidden for StackMatch.");
            AssertButtonLabel("MoveOutButton", "移出");
            AssertButtonLabel("ShuffleButton", "洗牌");
            AssertButtonLabel("UndoButton", "撤回");
            Assert.IsNotNull(FindButton("ShuffleButton"), "Shuffle button should exist.");
            Assert.IsNull(FindButton("LevelSelectButton"), "Level select button should be hidden for StackMatch.");
            Assert.IsNotNull(GameObject.Find("MovedOutCards"), "Moved-out card row should exist above the tray.");
            AssertCardFaceFillsRoot("StackMatchTile_0");
            Assert.AreEqual(9, GetCards(GetActiveGame(controller)).Count, "First level should be tiny.");
        }

        [UnityTest]
        public IEnumerator FirstLevelCompletesAndUnlocksHardSecondLevel()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            yield return CompleteFirstLevel();

            var gameRoot = GameObject.Find("StackMatchView");
            var settlementPopup = gameRoot.transform.Find("PopupHost/StackMatchSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Clearing the first level should show settlement.");

            var nextButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "First level should expose the next-level primary button.");
            nextButton.onClick.Invoke();
            yield return null;

            var runtime = GetActiveGame(controller);
            Assert.AreEqual(198, GetCards(runtime).Count, "Second level should be much larger than the first.");

            var progress = controller.GetProgress(StackMatchGameView.GameIdConstant);
            Assert.AreEqual(2, progress.UnlockedLevelCount, "Clearing level 1 should unlock level 2.");
            Assert.AreEqual(18, progress.TotalCoinCount, "First level should grant three matched sets.");
            Assert.AreEqual(1, progress.TotalChestCount, "First level win should grant one chest.");
        }

        [UnityTest]
        public IEnumerator ReenteringStackMatchStartsFromFirstLevel()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(198, GetCards(GetActiveGame(controller)).Count, "The next-level action should still enter the hard level.");

            controller.ExitCurrentGameToHall();
            yield return null;

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            Assert.AreEqual(9, GetCards(GetActiveGame(controller)).Count, "Re-entering StackMatch should always start from the first level.");
            Assert.AreEqual(2, controller.GetProgress(StackMatchGameView.GameIdConstant).UnlockedLevelCount, "Re-entering should not erase unlocked levels.");
        }

        [UnityTest]
        public IEnumerator HardLevelHasCoveredAndPlayableCards()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var cards = GetCards(GetActiveGame(controller));
            var interactableCount = 0;
            var blockedCount = 0;
            for (var i = 0; i < cards.Count; i++)
            {
                var button = GetField<Button>(cards[i], "Button");
                if (button.interactable)
                {
                    interactableCount += 1;
                }
                else
                {
                    blockedCount += 1;
                }
            }

            Assert.GreaterOrEqual(interactableCount, 20, "Hard level should open with enough playable exposed cards.");
            Assert.LessOrEqual(interactableCount, 40, "Hard level should still keep part of the stack covered.");
            Assert.Greater(blockedCount, 0, "Hard level should have covered blocked cards.");
            Assert.GreaterOrEqual(CountPlayableMatchGroups(cards), 4, "Hard level should open with several immediately visible match groups.");
            Assert.GreaterOrEqual(CountPartiallyCoveredNormalCards(cards), 80, "Hard level should use staggered partial cover instead of fully stacked cards.");
            Assert.LessOrEqual(CountFullyCoveredNormalCards(cards), 36, "Hard level should not mostly hide cards by placing another card at the same position.");
            AssertCoveredNormalCardsAreNotInteractable(cards);
        }

        [UnityTest]
        public IEnumerator HardLevelRepeatsEachCardTypeAcrossMultipleMatchSets()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var cards = GetCards(GetActiveGame(controller));
            var typeCounts = CountCardTypes(cards);

            Assert.AreEqual(18, typeCounts.Count, "Hard level should keep enough visual types for meaningful choices.");
            foreach (var entry in typeCounts)
            {
                Assert.GreaterOrEqual(entry.Value, 9, "Each hard-level card type should appear across several match sets.");
                Assert.LessOrEqual(entry.Value, 12, "Hard-level card types should stay varied instead of collapsing into a few icons.");
                Assert.AreEqual(0, entry.Value % 3, "Each card type count should remain divisible by the match size.");
            }
        }

        [UnityTest]
        public IEnumerator HardLevelUsesTwoByTwoGridAndBlindBoxes()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var cards = GetCards(GetActiveGame(controller));
            var blindCards = 0;
            var blindTopCards = 0;
            var blindGroups = new HashSet<int>();
            var blindGroupGridXs = new Dictionary<int, HashSet<int>>();
            var blindGroupGridYs = new Dictionary<int, HashSet<int>>();
            var blindGroupCards = new Dictionary<int, List<object>>();
            for (var i = 0; i < cards.Count; i++)
            {
                var gridX = GetField<int>(cards[i], "GridX");
                var gridY = GetField<int>(cards[i], "GridY");
                Assert.GreaterOrEqual(gridX, 0, "Cards should be placed on small grid columns.");
                Assert.GreaterOrEqual(gridY, 0, "Cards should be placed on small grid rows.");

                if (!GetField<bool>(cards[i], "IsBlindBox"))
                {
                    Assert.GreaterOrEqual(gridX, 2, "Normal hard-level cards should stay out of the blind-box side lanes.");
                    Assert.LessOrEqual(gridX, 12, "Normal hard-level cards should stay out of the blind-box side lanes.");
                    continue;
                }

                Assert.IsTrue(gridX == 0 || gridX == BoardGridColumns - CardGridSize, "Blind boxes should sit in side lanes away from normal cards.");
                blindCards += 1;
                var group = GetField<int>(cards[i], "BlindBoxGroup");
                blindGroups.Add(group);
                if (!blindGroupCards.TryGetValue(group, out var groupCards))
                {
                    groupCards = new List<object>();
                    blindGroupCards[group] = groupCards;
                }

                groupCards.Add(cards[i]);
                if (!blindGroupGridXs.TryGetValue(group, out var xs))
                {
                    xs = new HashSet<int>();
                    blindGroupGridXs[group] = xs;
                }

                if (!blindGroupGridYs.TryGetValue(group, out var ys))
                {
                    ys = new HashSet<int>();
                    blindGroupGridYs[group] = ys;
                }

                xs.Add(gridX);
                ys.Add(gridY);
                if (GetField<Button>(cards[i], "Button").interactable)
                {
                    blindTopCards += 1;
                }
            }

            Assert.AreEqual(12, blindCards, "Hard level should include two 6-card blind boxes.");
            Assert.AreEqual(2, blindGroups.Count, "Hard level should create two blind box groups.");
            Assert.AreEqual(2, blindTopCards, "Only the top card of each blind box should be playable.");
            AssertNonBlindCardsAreCenterMirrored(cards);
            Assert.AreEqual(1, blindGroupGridXs[0].Count, "The left blind box should keep one bottom grid column.");
            Assert.AreEqual(1, blindGroupGridYs[0].Count, "The left blind box should keep one bottom grid row.");
            Assert.AreEqual(1, blindGroupGridXs[1].Count, "The right blind box should keep one bottom grid column.");
            Assert.AreEqual(1, blindGroupGridYs[1].Count, "The right blind box should keep one bottom grid row.");
            AssertBlindBoxesAreHorizontallyMirrored(blindGroupCards[0], blindGroupCards[1]);
            AssertBlindBoxExtendsByQuarterGridStep(blindGroupCards[0]);
            AssertBlindBoxExtendsByQuarterGridStep(blindGroupCards[1]);
        }

        [UnityTest]
        public IEnumerator HardLevelCanFailWhenTrayFillsWithoutMatch()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var hardCards = GetCards(GetActiveGame(controller));
            var fillCards = FindPlayableCardsWithoutMatch(hardCards, 7);
            Assert.AreEqual(7, fillCards.Count, "Hard level should expose seven cards that can fill tray without matching.");
            for (var i = 0; i < fillCards.Count; i++)
            {
                ClickCard(fillCards[i]);
                yield return null;
            }

            yield return WaitForMoveToTray();

            var gameRoot = GameObject.Find("StackMatchView");
            var settlementPopup = gameRoot.transform.Find("PopupHost/StackMatchSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Filling the tray without a match should show a failure settlement.");
        }

        [UnityTest]
        public IEnumerator MoveOutMovesFourTrayCardsAboveTrayAndAllowsReturning()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var runtime = GetActiveGame(controller);
            var fillCards = FindPlayableCardsWithoutMatch(GetCards(runtime), 4);
            Assert.AreEqual(4, fillCards.Count, "Hard level should expose four cards for move-out.");
            for (var i = 0; i < fillCards.Count; i++)
            {
                ClickCard(fillCards[i]);
                yield return null;
            }

            yield return WaitForMoveToTray();
            Assert.AreEqual(4, GetTrayCards(runtime).Count, "Tray should contain four cards before move-out.");

            ClickButton("MoveOutButton");
            yield return null;

            Assert.AreEqual(0, GetTrayCards(runtime).Count, "Move-out should free the collection tray.");
            Assert.AreEqual(4, GetMovedOutCards(runtime).Count, "Move-out should park four cards above the tray.");
            Assert.IsFalse(FindButton("MoveOutButton").interactable, "Move-out should only be usable once per run.");

            var movedOutCard = GetMovedOutCards(runtime)[0];
            ClickCard(movedOutCard);
            yield return null;

            Assert.AreEqual(1, GetTrayCards(runtime).Count, "Clicking a moved-out card should return it to the tray.");
            Assert.AreEqual(3, GetMovedOutCards(runtime).Count, "Returned card should leave the moved-out row.");
        }

        [UnityTest]
        public IEnumerator FinalLevelWinShowsRetrySettlementAndRestartsCurrentLevel()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;
            yield return CompleteFirstLevel();

            var nextButton = GameObject.Find("StackMatchSettlementPanel").transform.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(nextButton, "Next button should exist.");
            nextButton.onClick.Invoke();
            yield return null;

            var runtime = GetActiveGame(controller);
            InvokeShowResult(runtime, true);
            yield return null;

            Assert.IsNotNull(GameObject.Find("StackMatchSettlementPanel"), "Final level win should show settlement.");
            AssertButtonLabel("NextButton", "再来一局");
            Assert.IsTrue(FindButton("BackHallButton").gameObject.activeSelf, "Retry settlement should still expose back-hall action.");

            FindButton("NextButton").onClick.Invoke();
            yield return null;

            runtime = GetActiveGame(controller);
            Assert.AreEqual(198, GetCards(runtime).Count, "Retry should restart the current hard level.");
            Assert.IsNull(GameObject.Find("StackMatchSettlementPanel"), "Retry should close the settlement panel.");
        }

        [UnityTest]
        public IEnumerator UndoRestoresLastUnmatchedTrayCard()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            ClickButton("StackMatchTile_0");
            yield return WaitForMoveToTray();
            Assert.AreEqual(1, GetTrayCards(GetActiveGame(controller)).Count, "Tray should contain the selected card.");

            ClickButton("UndoButton");
            yield return null;

            var runtime = GetActiveGame(controller);
            Assert.AreEqual(0, GetTrayCards(runtime).Count, "Undo should remove the last unmatched card from tray.");
            Assert.IsTrue(FindButton("StackMatchTile_0").interactable, "Restored card should become playable again.");
            Assert.AreEqual(GetField<Rect>(GetCards(runtime)[0], "BoardRect").center, GetField<RectTransform>(GetCards(runtime)[0], "Root").anchoredPosition, "Undo should restore the card to its original board position.");
            Assert.IsFalse(FindButton("UndoButton").interactable, "Undo should only be usable once per run.");
        }

        [UnityTest]
        public IEnumerator ShuffleCanOnlyBeUsedOnce()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            ClickButton("ShuffleButton");
            yield return null;

            Assert.IsFalse(FindButton("ShuffleButton").interactable, "Shuffle should only be usable once per run.");
        }

        [UnityTest]
        public IEnumerator MatchingTrayCardsStayGroupedFromLeftToRight()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            var cards = GetCards(GetActiveGame(controller));
            var pair = FindCardsWithSameType(cards, 2);
            var different = FindCardWithDifferentType(cards, GetField<int>(pair[0], "TypeId"));

            ClickCard(pair[0]);
            yield return null;
            ClickCard(different);
            yield return null;
            ClickCard(pair[1]);
            yield return null;

            var trayCards = GetTrayCards(GetActiveGame(controller));
            var pairType = GetField<int>(pair[0], "TypeId");
            var differentType = GetField<int>(different, "TypeId");
            Assert.AreEqual(3, trayCards.Count, "Tray should keep the three selected cards before a match is formed.");
            Assert.AreEqual(pairType, GetField<int>(trayCards[0], "TypeId"), "First card should remain at the left edge.");
            Assert.AreEqual(pairType, GetField<int>(trayCards[1], "TypeId"), "Second matching card should be inserted next to the first one.");
            Assert.AreEqual(differentType, GetField<int>(trayCards[2], "TypeId"), "Different card should shift right after matching cards.");
        }

        [UnityTest]
        public IEnumerator TrayCardsUseBoardCardSize()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var cards = GetCards(runtime);
            var boardCardRoot = GetField<RectTransform>(cards[1], "Root");

            ClickButton("StackMatchTile_0");
            yield return null;

            var trayCardRoot = GetField<RectTransform>(GetTrayCards(runtime)[0], "Root");
            Assert.AreEqual(boardCardRoot.sizeDelta.x, trayCardRoot.sizeDelta.x, 0.01f, "Tray card width should match board card width.");
            Assert.AreEqual(boardCardRoot.sizeDelta.y, trayCardRoot.sizeDelta.y, 0.01f, "Tray card height should match board card height.");
        }

        [UnityTest]
        public IEnumerator MatchResolvesAfterMoveToTrayAnimation()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(StackMatchGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var triple = FindCardsWithSameType(GetCards(runtime), 3);
            for (var i = 0; i < triple.Count; i++)
            {
                ClickCard(triple[i]);
                yield return null;
            }

            Assert.AreEqual(3, GetTrayCards(runtime).Count, "Three matching cards should remain in tray before the move animation finishes.");

            yield return WaitForMoveToTray();

            Assert.AreEqual(0, GetTrayCards(runtime).Count, "Three matching cards should clear after the move animation finishes.");
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

        private static StackMatchGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as StackMatchGameView;
            Assert.IsNotNull(runtime, "StackMatch runtime was not created.");
            return runtime;
        }

        private static IList GetCards(StackMatchGameView runtime)
        {
            var field = typeof(StackMatchGameView).GetField("cards", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access cards field.");
            var cards = field.GetValue(runtime) as IList;
            Assert.IsNotNull(cards, "Cards list should exist.");
            return cards;
        }

        private static IList GetTrayCards(StackMatchGameView runtime)
        {
            var field = typeof(StackMatchGameView).GetField("trayCards", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access trayCards field.");
            var cards = field.GetValue(runtime) as IList;
            Assert.IsNotNull(cards, "Tray cards list should exist.");
            return cards;
        }

        private static IList GetMovedOutCards(StackMatchGameView runtime)
        {
            var field = typeof(StackMatchGameView).GetField("movedOutCards", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access movedOutCards field.");
            var cards = field.GetValue(runtime) as IList;
            Assert.IsNotNull(cards, "Moved-out cards list should exist.");
            return cards;
        }

        private static List<object> FindCardsWithSameType(IList cards, int count)
        {
            var groups = new Dictionary<int, List<object>>();
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (!GetField<Button>(card, "Button").interactable)
                {
                    continue;
                }

                var typeId = GetField<int>(card, "TypeId");
                if (!groups.TryGetValue(typeId, out var group))
                {
                    group = new List<object>();
                    groups[typeId] = group;
                }

                group.Add(card);
                if (group.Count >= count)
                {
                    return group.GetRange(0, count);
                }
            }

            Assert.Fail("Could not find enough playable cards with the same type.");
            return null;
        }

        private static int CountPlayableMatchGroups(IList cards)
        {
            var typeCounts = new Dictionary<int, int>();
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (!GetField<Button>(card, "Button").interactable)
                {
                    continue;
                }

                var typeId = GetField<int>(card, "TypeId");
                typeCounts[typeId] = typeCounts.TryGetValue(typeId, out var existing) ? existing + 1 : 1;
            }

            var result = 0;
            foreach (var entry in typeCounts)
            {
                if (entry.Value >= 3)
                {
                    result += 1;
                }
            }

            return result;
        }

        private static Dictionary<int, int> CountCardTypes(IList cards)
        {
            var result = new Dictionary<int, int>();
            for (var i = 0; i < cards.Count; i++)
            {
                var typeId = GetField<int>(cards[i], "TypeId");
                result[typeId] = result.TryGetValue(typeId, out var existing) ? existing + 1 : 1;
            }

            return result;
        }

        private static object FindCardWithDifferentType(IList cards, int typeId)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (GetField<Button>(card, "Button").interactable && GetField<int>(card, "TypeId") != typeId)
                {
                    return card;
                }
            }

            Assert.Fail("Could not find a playable card with a different type.");
            return null;
        }

        private static List<object> FindPlayableCardsWithoutMatch(IList cards, int count)
        {
            var result = new List<object>();
            var typeCounts = new Dictionary<int, int>();
            for (var i = 0; i < cards.Count && result.Count < count; i++)
            {
                var card = cards[i];
                if (!GetField<Button>(card, "Button").interactable)
                {
                    continue;
                }

                var typeId = GetField<int>(card, "TypeId");
                var typeCount = typeCounts.TryGetValue(typeId, out var existing) ? existing : 0;
                if (typeCount >= 2)
                {
                    continue;
                }

                typeCounts[typeId] = typeCount + 1;
                result.Add(card);
            }

            return result;
        }

        private static void AssertNonBlindCardsAreCenterMirrored(IList cards)
        {
            var keys = new Dictionary<string, int>();
            for (var i = 0; i < cards.Count; i++)
            {
                if (GetField<bool>(cards[i], "IsBlindBox"))
                {
                    continue;
                }

                var key = GetGridKey(
                    GetField<int>(cards[i], "Layer"),
                    GetField<int>(cards[i], "GridX"),
                    GetField<int>(cards[i], "GridY"));
                keys[key] = keys.TryGetValue(key, out var existing) ? existing + 1 : 1;
            }

            foreach (var entry in keys)
            {
                var parts = entry.Key.Split(':');
                var layer = int.Parse(parts[0]);
                var gridX = int.Parse(parts[1]);
                var gridY = int.Parse(parts[2]);
                var mirroredKey = GetGridKey(
                    layer,
                    BoardGridColumns - gridX - CardGridSize,
                    BoardGridRows - gridY - CardGridSize);
                Assert.IsTrue(keys.TryGetValue(mirroredKey, out var mirroredCount), "Missing center-mirrored hard-level card for " + entry.Key);
                Assert.AreEqual(entry.Value, mirroredCount, "Center-mirrored hard-level cards should have matching counts.");
            }
        }

        private static string GetGridKey(int layer, int gridX, int gridY)
        {
            return layer + ":" + gridX + ":" + gridY;
        }

        private static void AssertCoveredNormalCardsAreNotInteractable(IList cards)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var target = cards[i];
                if (GetField<bool>(target, "IsBlindBox") || !HasDrawnAboveNormalCover(target, cards))
                {
                    continue;
                }

                Assert.IsFalse(GetField<Button>(target, "Button").interactable, "A visually covered normal card should not be clickable.");
            }
        }

        private static bool HasDrawnAboveNormalCover(object target, IList cards)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                var other = cards[i];
                if (ReferenceEquals(target, other) || GetField<bool>(other, "IsBlindBox") || !IsDrawnAbove(target, other))
                {
                    continue;
                }

                if (RectsOverlap(GetField<Rect>(target, "BoardRect"), GetField<Rect>(other, "BoardRect")))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountPartiallyCoveredNormalCards(IList cards)
        {
            var result = 0;
            for (var i = 0; i < cards.Count; i++)
            {
                var target = cards[i];
                if (!GetField<bool>(target, "IsBlindBox") && HasDrawnAboveNormalPartialCover(target, cards))
                {
                    result += 1;
                }
            }

            return result;
        }

        private static int CountFullyCoveredNormalCards(IList cards)
        {
            var result = 0;
            for (var i = 0; i < cards.Count; i++)
            {
                var target = cards[i];
                if (!GetField<bool>(target, "IsBlindBox") && HasDrawnAboveNormalFullCover(target, cards))
                {
                    result += 1;
                }
            }

            return result;
        }

        private static bool HasDrawnAboveNormalPartialCover(object target, IList cards)
        {
            var targetRect = GetField<Rect>(target, "BoardRect");
            for (var i = 0; i < cards.Count; i++)
            {
                var other = cards[i];
                if (ReferenceEquals(target, other) || GetField<bool>(other, "IsBlindBox") || !IsDrawnAbove(target, other))
                {
                    continue;
                }

                var otherRect = GetField<Rect>(other, "BoardRect");
                if (RectsOverlap(targetRect, otherRect) && !RectsEqual(targetRect, otherRect))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDrawnAboveNormalFullCover(object target, IList cards)
        {
            var targetRect = GetField<Rect>(target, "BoardRect");
            for (var i = 0; i < cards.Count; i++)
            {
                var other = cards[i];
                if (ReferenceEquals(target, other) || GetField<bool>(other, "IsBlindBox") || !IsDrawnAbove(target, other))
                {
                    continue;
                }

                if (RectsEqual(targetRect, GetField<Rect>(other, "BoardRect")))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDrawnAbove(object target, object other)
        {
            var targetLayer = GetField<int>(target, "Layer");
            var otherLayer = GetField<int>(other, "Layer");
            return otherLayer > targetLayer || (otherLayer == targetLayer && GetField<int>(other, "Index") > GetField<int>(target, "Index"));
        }

        private static bool RectsOverlap(Rect left, Rect right)
        {
            return left.xMin < right.xMax
                && left.xMax > right.xMin
                && left.yMin < right.yMax
                && left.yMax > right.yMin;
        }

        private static bool RectsEqual(Rect left, Rect right)
        {
            return Mathf.Abs(left.x - right.x) < 0.01f
                && Mathf.Abs(left.y - right.y) < 0.01f
                && Mathf.Abs(left.width - right.width) < 0.01f
                && Mathf.Abs(left.height - right.height) < 0.01f;
        }

        private static void AssertBlindBoxExtendsByQuarterGridStep(List<object> groupCards)
        {
            groupCards.Sort(delegate(object left, object right)
            {
                return GetField<int>(left, "BlindBoxOrder").CompareTo(GetField<int>(right, "BlindBoxOrder"));
            });

            var direction = GetField<object>(groupCards[0], "BlindBoxDirection").ToString();
            var baseRect = GetField<Rect>(groupCards[0], "BoardRect");
            var stepX = CardWidth * 0.5f * BlindBoxStepScale;
            var stepY = CardHeight * 0.5f * BlindBoxStepScale;
            for (var i = 0; i < groupCards.Count; i++)
            {
                var rect = GetField<Rect>(groupCards[i], "BoardRect");
                if (direction == "Horizontal")
                {
                    Assert.AreEqual(baseRect.center.x + stepX * i, rect.center.x, 0.01f, "Blind-box cards should extend by a quarter grid horizontally.");
                    Assert.AreEqual(baseRect.center.y, rect.center.y, 0.01f, "Blind-box cards should not drift vertically when extending horizontally.");
                }
                else
                {
                    Assert.AreEqual(baseRect.center.x, rect.center.x, 0.01f, "Blind-box cards should not drift horizontally when extending vertically.");
                    Assert.AreEqual(baseRect.center.y - stepY * i, rect.center.y, 0.01f, "Blind-box cards should extend by a quarter grid vertically.");
                }
            }
        }

        private static void AssertBlindBoxesAreHorizontallyMirrored(List<object> leftCards, List<object> rightCards)
        {
            leftCards.Sort(delegate(object left, object right)
            {
                return GetField<int>(left, "BlindBoxOrder").CompareTo(GetField<int>(right, "BlindBoxOrder"));
            });
            rightCards.Sort(delegate(object left, object right)
            {
                return GetField<int>(left, "BlindBoxOrder").CompareTo(GetField<int>(right, "BlindBoxOrder"));
            });

            Assert.AreEqual(leftCards.Count, rightCards.Count, "Blind boxes should have the same card count.");
            for (var i = 0; i < leftCards.Count; i++)
            {
                var leftGridX = GetField<int>(leftCards[i], "GridX");
                var rightGridX = GetField<int>(rightCards[i], "GridX");
                Assert.AreEqual(BoardGridColumns - leftGridX - CardGridSize, rightGridX, "Blind boxes should mirror their bottom grid columns.");
                Assert.AreEqual(GetField<int>(leftCards[i], "GridY"), GetField<int>(rightCards[i], "GridY"), "Blind boxes should keep mirrored rows aligned.");
                Assert.AreEqual(GetField<object>(leftCards[i], "BlindBoxDirection").ToString(), GetField<object>(rightCards[i], "BlindBoxDirection").ToString(), "Blind boxes should extend in matching directions.");

                var leftRect = GetField<Rect>(leftCards[i], "BoardRect");
                var rightRect = GetField<Rect>(rightCards[i], "BoardRect");
                Assert.AreEqual(-leftRect.center.x, rightRect.center.x, 0.01f, "Blind boxes should mirror each visual card horizontally.");
                Assert.AreEqual(leftRect.center.y, rightRect.center.y, 0.01f, "Blind boxes should mirror each visual card at the same height.");
            }
        }

        private static IEnumerator CompleteFirstLevel()
        {
            for (var i = 0; i < 9; i++)
            {
                ClickButton("StackMatchTile_" + i);
            }

            yield return WaitForMoveToTray();
        }

        private static IEnumerator WaitForMoveToTray()
        {
            yield return new WaitForSecondsRealtime(MoveToTrayDuration + 0.08f);
            yield return null;
        }

        private static void ClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            Assert.IsTrue(button.interactable, "Button should be interactable before click: " + buttonName);
            button.onClick.Invoke();
        }

        private static void ClickCard(object card)
        {
            var root = GetField<RectTransform>(card, "Root");
            ClickButton(root.name);
        }

        private static void AssertCardFaceFillsRoot(string cardName)
        {
            var cardObject = GameObject.Find(cardName);
            Assert.IsNotNull(cardObject, "Could not find card: " + cardName);
            var face = cardObject.transform.Find("Face")?.GetComponent<RectTransform>();
            Assert.IsNotNull(face, "Card face should exist.");
            Assert.AreEqual(Vector2.zero, face.offsetMin, "Card face should fill root to avoid visible seams.");
            Assert.AreEqual(Vector2.zero, face.offsetMax, "Card face should fill root to avoid visible seams.");

            var icon = cardObject.transform.Find("Icon")?.GetComponent<RectTransform>();
            Assert.IsNotNull(icon, "Card icon should exist.");
            Assert.AreEqual(new Vector2(10f, 10f), icon.offsetMin, "Card icon should keep a 10px inset from the face.");
            Assert.AreEqual(new Vector2(-10f, -10f), icon.offsetMax, "Card icon should keep a 10px inset from the face.");
        }

        private static void AssertButtonLabel(string buttonName, string expectedText)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            var label = button.transform.Find("Label")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(label, "Button label should exist: " + buttonName);
            var textProperty = label.GetType().GetProperty("text", InstancePrivate);
            Assert.IsNotNull(textProperty, "Button label should expose text: " + buttonName);
            Assert.AreEqual(expectedText, textProperty.GetValue(label) as string, "Button label text should match.");
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

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokeShowResult(StackMatchGameView runtime, bool won)
        {
            var method = typeof(StackMatchGameView).GetMethod("ShowResult", InstancePrivate);
            Assert.IsNotNull(method, "Failed to access ShowResult method.");
            method.Invoke(runtime, new object[] { won });
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
