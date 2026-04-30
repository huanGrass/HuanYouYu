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
    public sealed class MemoryFlipGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void MemoryFlipTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/memory-flip.ui_texts.zh-CN"), "MemoryFlip text catalog should exist.");
        }

        [UnityTest]
        public IEnumerator CanEnterMemoryFlipAndShowBoard()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.IsTrue(controller.HasActiveGame, "MemoryFlip should become the active game.");
            Assert.IsNotNull(GameObject.Find("MemoryFlipView"), "MemoryFlip shell root should exist.");
            Assert.IsNotNull(GameObject.Find("MemoryFlipGrid"), "MemoryFlip grid should exist.");
            Assert.IsNotNull(FindButton("LevelSelectButton"), "Level select button should exist.");
            Assert.IsNull(FindButton("EasyDifficultyButton"), "Legacy easy difficulty button should be removed.");
            Assert.IsNull(FindButton("NormalDifficultyButton"), "Legacy normal difficulty button should be removed.");
            Assert.IsNull(FindButton("HardDifficultyButton"), "Legacy hard difficulty button should be removed.");
            Assert.AreEqual(12, GetCards(GetActiveGame(controller)).Count, "First level should create 12 cards.");
        }

        [UnityTest]
        public IEnumerator LevelSelectShowsUnlockedAndLockedLevels()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;

            ClickButton("LevelSelectButton");
            yield return null;

            var firstLevel = FindButton("MemoryFlipLevelButton_1");
            var secondLevel = FindButton("MemoryFlipLevelButton_2");
            Assert.IsNotNull(firstLevel, "First level button should exist.");
            Assert.IsNotNull(secondLevel, "Second level button should exist.");
            Assert.IsTrue(firstLevel.interactable, "First level should be unlocked.");
            Assert.IsFalse(secondLevel.interactable, "Second level should be locked before clearing level 1.");

            AssertDeckShape(GetActiveGame(controller), 12);
        }

        [UnityTest]
        public IEnumerator MatchingPairStaysOpenAndUpdatesCoins()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindPair(runtime, true);
            ClickCard(pair[0]);
            ClickCard(pair[1]);
            yield return null;

            Assert.IsTrue(GetBoolField(pair[0], "IsMatched"), "First matching card should be matched.");
            Assert.IsTrue(GetBoolField(pair[1], "IsMatched"), "Second matching card should be matched.");
            Assert.IsTrue(GetBoolField(pair[0], "IsFaceUp"), "First matching card should stay face up.");
            Assert.IsTrue(GetBoolField(pair[1], "IsFaceUp"), "Second matching card should stay face up.");
            Assert.AreEqual(1, GetIntField(runtime, "matchedPairCount"), "Matched pair count should increase.");

            var scoreLabel = GameObject.Find("MemoryFlipTop/Header/Score")?.GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(scoreLabel, "Score label should exist.");
            var scoreText = scoreLabel.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public)?.GetValue(scoreLabel) as string;
            Assert.IsNotNull(scoreText, "Score label text property should exist.");
            StringAssert.Contains("金币 5", scoreText);
        }

        [UnityTest]
        public IEnumerator MismatchedPairFlipsBackAfterDelay()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindPair(runtime, false);
            ClickCard(pair[0]);
            ClickCard(pair[1]);
            yield return null;

            Assert.IsTrue(GetBoolField(pair[0], "IsFaceUp"), "First mismatched card should be shown immediately.");
            Assert.IsTrue(GetBoolField(pair[1], "IsFaceUp"), "Second mismatched card should be shown immediately.");

            yield return new WaitForSecondsRealtime(0.7f);
            yield return null;

            Assert.IsFalse(GetBoolField(pair[0], "IsFaceUp"), "First mismatched card should flip back.");
            Assert.IsFalse(GetBoolField(pair[1], "IsFaceUp"), "Second mismatched card should flip back.");
            Assert.AreEqual(0, GetIntField(runtime, "matchedPairCount"), "Mismatched pair should not increase score.");
        }

        [UnityTest]
        public IEnumerator CompletingAllPairsSettlesAndReturnsToHall()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            CompleteAllPairs(runtime);
            yield return null;

            var gameRoot = GameObject.Find("MemoryFlipView");
            Assert.IsNotNull(gameRoot, "MemoryFlip shell root should exist.");
            var settlementPopup = gameRoot.transform.Find("PopupHost/MemoryFlipSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Completing all pairs should show win settlement popup.");

            var backHallButton = settlementPopup.Find("Dialog/BackHallButton")?.GetComponent<Button>();
            Assert.IsNotNull(backHallButton, "Settlement back hall button should exist.");
            backHallButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(MemoryFlipGameView.GameIdConstant);
            Assert.AreEqual(1, progress.PlayCount, "Completing a game should count one play.");
            Assert.AreEqual(6, progress.BestScore, "First level completion should score six pairs.");
            Assert.AreEqual(30, progress.TotalCoinCount, "First level completion should grant six pair rewards.");
            Assert.AreEqual(1, progress.TotalChestCount, "Completion should grant one chest.");
            Assert.AreEqual(2, progress.UnlockedLevelCount, "Completing level 1 should unlock level 2.");
            Assert.IsFalse(controller.HasActiveGame, "Game should be disposed after confirming settlement.");
            Assert.IsTrue(controller.IsHallVisible, "Hall should be visible after settlement.");
        }

        [UnityTest]
        public IEnumerator PauseExitSettlesWithoutChest()
        {
            ResetProgress();

            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(MemoryFlipGameView.GameIdConstant);
            yield return null;

            var runtime = GetActiveGame(controller);
            var pair = FindPair(runtime, true);
            ClickCard(pair[0]);
            ClickCard(pair[1]);
            yield return null;

            var gameRoot = GameObject.Find("MemoryFlipView");
            var pauseButton = gameRoot.transform.Find("PauseButton")?.GetComponent<Button>();
            Assert.IsNotNull(pauseButton, "Pause button should exist.");
            pauseButton.onClick.Invoke();
            yield return null;

            var pausePopup = gameRoot.transform.Find("PopupHost/MiniGamePausePopup");
            Assert.IsNotNull(pausePopup, "Pause popup should be visible.");

            var helpButton = pausePopup.Find("Dialog/HelpButton")?.GetComponent<Button>();
            Assert.IsNotNull(helpButton, "Help button should exist.");
            helpButton.onClick.Invoke();
            yield return null;

            var helpOverlay = pausePopup.Find("HelpOverlay");
            Assert.IsNotNull(helpOverlay, "Help overlay should exist.");
            Assert.IsTrue(helpOverlay.gameObject.activeSelf, "Help overlay should become visible.");

            var helpConfirmButton = helpOverlay.Find("Dialog/ConfirmButton")?.GetComponent<Button>();
            Assert.IsNotNull(helpConfirmButton, "Help confirm button should exist.");
            helpConfirmButton.onClick.Invoke();
            yield return null;

            var exitButton = pausePopup.Find("Dialog/MainButtons/ExitButton")?.GetComponent<Button>();
            Assert.IsNotNull(exitButton, "Exit button should exist.");
            exitButton.onClick.Invoke();
            yield return null;

            var settlementPopup = gameRoot.transform.Find("PopupHost/MemoryFlipSettlementPanel");
            Assert.IsNotNull(settlementPopup, "Pause exit should show settlement popup.");

            var confirmButton = settlementPopup.Find("Dialog/NextButton")?.GetComponent<Button>();
            Assert.IsNotNull(confirmButton, "Settlement confirm button should exist.");
            confirmButton.onClick.Invoke();
            yield return null;

            var progress = controller.GetProgress(MemoryFlipGameView.GameIdConstant);
            Assert.AreEqual(1, progress.PlayCount);
            Assert.AreEqual(1, progress.BestScore);
            Assert.AreEqual(5, progress.TotalCoinCount);
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

        private static MemoryFlipGameView GetActiveGame(MiniGameAppController controller)
        {
            var field = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access activeGame field.");
            var runtime = field.GetValue(controller) as MemoryFlipGameView;
            Assert.IsNotNull(runtime, "MemoryFlip runtime was not created.");
            return runtime;
        }

        private static IList GetCards(MemoryFlipGameView runtime)
        {
            var field = typeof(MemoryFlipGameView).GetField("cards", InstancePrivate);
            Assert.IsNotNull(field, "Failed to access cards field.");
            var cards = field.GetValue(runtime) as IList;
            Assert.IsNotNull(cards, "Cards list should exist.");
            return cards;
        }

        private static void AssertDeckShape(MemoryFlipGameView runtime, int expectedCardCount)
        {
            var cards = GetCards(runtime);
            Assert.AreEqual(expectedCardCount, cards.Count, "Unexpected card count.");

            var counts = new Dictionary<int, int>();
            for (var i = 0; i < cards.Count; i++)
            {
                var pairId = GetIntField(cards[i], "PairId");
                counts[pairId] = counts.ContainsKey(pairId) ? counts[pairId] + 1 : 1;
            }

            foreach (var pair in counts)
            {
                Assert.AreEqual(2, pair.Value, "Every icon should appear exactly twice.");
            }
        }

        private static List<object> FindPair(MemoryFlipGameView runtime, bool matching)
        {
            var cards = GetCards(runtime);
            for (var i = 0; i < cards.Count; i++)
            {
                var leftPairId = GetIntField(cards[i], "PairId");
                for (var j = i + 1; j < cards.Count; j++)
                {
                    var rightPairId = GetIntField(cards[j], "PairId");
                    if ((leftPairId == rightPairId) == matching)
                    {
                        return new List<object> { cards[i], cards[j] };
                    }
                }
            }

            Assert.Fail("Could not find requested card pair.");
            return null;
        }

        private static void CompleteAllPairs(MemoryFlipGameView runtime)
        {
            var cards = GetCards(runtime);
            var byPairId = new Dictionary<int, List<object>>();
            for (var i = 0; i < cards.Count; i++)
            {
                var pairId = GetIntField(cards[i], "PairId");
                List<object> group;
                if (!byPairId.TryGetValue(pairId, out group))
                {
                    group = new List<object>();
                    byPairId[pairId] = group;
                }

                group.Add(cards[i]);
            }

            foreach (var group in byPairId.Values)
            {
                Assert.AreEqual(2, group.Count, "Each pair should contain two cards.");
                ClickCard(group[0]);
                ClickCard(group[1]);
            }
        }

        private static void ClickCard(object card)
        {
            var button = GetField<Button>(card, "Button");
            Assert.IsNotNull(button, "Card button should exist.");
            button.onClick.Invoke();
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

        private static int GetIntField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (int)field.GetValue(target);
        }

        private static bool GetBoolField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return (bool)field.GetValue(target);
        }

        private static T GetField<T>(object target, string fieldName)
            where T : class
        {
            var field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.IsNotNull(field, "Failed to access field: " + fieldName);
            return field.GetValue(target) as T;
        }

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
