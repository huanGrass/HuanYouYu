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
    public sealed class BullsCowsGameplayTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [Test]
        public void BullsCowsTextResourceExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>("Text/bulls-cows.ui_texts.zh-CN"), "BullsCows text catalog should exist.");
        }

        [Test]
        public void BullsCowsRulesEvaluateBullsAndCows()
        {
            Assert.IsTrue(BullsCowsGameView.IsValidGuess("0123"));
            Assert.IsFalse(BullsCowsGameView.IsValidGuess("0012"));
            BullsCowsGameView.EvaluateGuess("1234", "1243", out var bulls, out var cows);
            Assert.AreEqual(2, bulls);
            Assert.AreEqual(2, cows);
            BullsCowsGameView.EvaluateGuess("5678", "1234", out bulls, out cows);
            Assert.AreEqual(0, bulls);
            Assert.AreEqual(0, cows);
        }

        [UnityTest]
        public IEnumerator CanEnterInputAndReceiveFeedback()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "1234");

            Assert.IsTrue(controller.HasActiveGame, "BullsCows should become active.");
            Assert.IsNotNull(GameObject.Find("BullsCowsView"), "BullsCows root should exist.");
            Assert.AreEqual(10, CountButtonsWithPrefix("DigitButton_"), "BullsCows should render ten digit buttons.");
            Assert.IsNull(FindButton("SubmitButton"), "BullsCows should auto submit without a submit button.");
            Assert.IsNull(GameObject.Find("StatusLabel"), "BullsCows should not render a status label.");

            ClickButton("DigitButton_1");
            ClickButton("DigitButton_2");
            ClickButton("DigitButton_4");
            ClickButton("DigitButton_3");
            yield return null;

            var row = GameObject.Find("HistoryRow_0").GetComponent("TextMeshProUGUI");
            Assert.IsNotNull(row, "History row should have TMP text.");
            var textProperty = row.GetType().GetProperty("text", InstancePrivate);
            Assert.IsNotNull(textProperty, "TMP text should expose text property.");
            var historyText = textProperty.GetValue(row) as string;
            Assert.IsTrue(historyText.Contains("位置正确 2 个"), "History should describe exact matches in Chinese.");
            Assert.IsTrue(historyText.Contains("数字正确但位置不对 2 个"), "History should describe misplaced matches in Chinese.");
        }

        [UnityTest]
        public IEnumerator WinningGuessShowsSettlement()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "1234");

            ClickButton("DigitButton_1");
            ClickButton("DigitButton_2");
            ClickButton("DigitButton_3");
            ClickButton("DigitButton_4");
            yield return null;

            Assert.IsNotNull(GameObject.Find("BullsCowsSettlementPanel"), "Correct answer should show settlement.");
        }

        [UnityTest]
        public IEnumerator CanContinueAfterRewardTargetAttemptsAndKeepsScrollableHistory()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            SetAnswer(controller, "9876");

            var wrongGuesses = new[]
            {
                "0123",
                "0124",
                "0125",
                "0134",
                "0135",
                "0145",
                "0234",
                "0235",
                "0245"
            };

            for (var i = 0; i < wrongGuesses.Length; i++)
            {
                SubmitGuess(wrongGuesses[i]);
                yield return null;
            }

            Assert.IsNull(GameObject.Find("BullsCowsSettlementPanel"), "Running past the reward target should not fail the game.");
            Assert.IsNotNull(GameObject.Find("HistoryRow_8"), "Scrollable history should keep entries beyond the reward target.");
            Assert.IsTrue(FindButton("DigitButton_0").interactable, "Digits should remain interactable after the reward target.");
        }

        [UnityTest]
        public IEnumerator PauseButtonOpensPausePopup()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var pauseButton = FindButton("PauseButton");
            Assert.IsNotNull(pauseButton, "Pause button should exist in BullsCows.");
            Assert.IsTrue(pauseButton.interactable, "Pause button should be interactable in BullsCows.");
            pauseButton.onClick.Invoke();
            yield return null;

            Assert.IsNotNull(GameObject.Find("MiniGamePausePopup"), "Pause button should open the pause popup in BullsCows.");
        }

        [UnityTest]
        public IEnumerator BullsCowsScreenshotHasNonBlankPlayableLayout()
        {
            ResetProgress();
            var controller = default(MiniGameAppController);
            yield return LoadController(result => controller = result);

            controller.EnterGame(BullsCowsGameView.GameIdConstant);
            yield return null;
            Canvas.ForceUpdateCanvases();

            AssertChildrenStayInside("BullsCowsContent", "GuessSlot_", 4);
            AssertChildrenStayInside("BullsCowsControls", "DigitButton_", 10);
            AssertChildStaysInside("BullsCowsControls", "Keypad");
            AssertChildStaysInside("BullsCowsControls", "BullsCowsActionRow");
            AssertChildSizeAtLeast("GuessSlot_0", 120f, 120f);
            AssertChildSizeAtLeast("DigitButton_0", 145f, 58f);
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

        private static void SetAnswer(MiniGameAppController controller, string answer)
        {
            var activeGameField = typeof(MiniGameAppController).GetField("activeGame", InstancePrivate);
            Assert.IsNotNull(activeGameField, "activeGame field should be accessible.");
            var runtime = activeGameField.GetValue(controller) as BullsCowsGameView;
            Assert.IsNotNull(runtime, "BullsCows runtime should be active.");
            var answerField = typeof(BullsCowsGameView).GetField("answer", InstancePrivate);
            Assert.IsNotNull(answerField, "answer field should be accessible for deterministic tests.");
            answerField.SetValue(runtime, answer);
        }

        private static void AssertChildStaysInside(string parentName, string childName)
        {
            var parent = GameObject.Find(parentName)?.GetComponent<RectTransform>();
            var child = GameObject.Find(childName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(parent, "Missing parent rect: " + parentName);
            Assert.IsNotNull(child, "Missing child rect: " + childName);
            var parentRect = ToScreenRect(parent);
            var childRect = ToScreenRect(child);
            Assert.GreaterOrEqual(childRect.xMin, parentRect.xMin - 1f, childName + " should stay inside parent horizontally.");
            Assert.LessOrEqual(childRect.xMax, parentRect.xMax + 1f, childName + " should stay inside parent horizontally.");
            Assert.GreaterOrEqual(childRect.yMin, parentRect.yMin - 1f, childName + " should stay inside parent vertically.");
            Assert.LessOrEqual(childRect.yMax, parentRect.yMax + 1f, childName + " should stay inside parent vertically.");
        }

        private static void AssertChildrenStayInside(string parentName, string childPrefix, int count)
        {
            var parent = GameObject.Find(parentName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(parent, "Missing parent rect: " + parentName);
            var parentRect = ToScreenRect(parent);
            for (var i = 0; i < count; i++)
            {
                var child = GameObject.Find(childPrefix + i)?.GetComponent<RectTransform>();
                Assert.IsNotNull(child, "Missing child rect: " + childPrefix + i);
                var childRect = ToScreenRect(child);
                Assert.GreaterOrEqual(childRect.xMin, parentRect.xMin - 1f, childPrefix + i + " should stay inside content horizontally.");
                Assert.LessOrEqual(childRect.xMax, parentRect.xMax + 1f, childPrefix + i + " should stay inside content horizontally.");
                Assert.GreaterOrEqual(childRect.yMin, parentRect.yMin - 1f, childPrefix + i + " should stay inside content vertically.");
                Assert.LessOrEqual(childRect.yMax, parentRect.yMax + 1f, childPrefix + i + " should stay inside content vertically.");
            }
        }

        private static void AssertChildSizeAtLeast(string childName, float minWidth, float minHeight)
        {
            var child = GameObject.Find(childName)?.GetComponent<RectTransform>();
            Assert.IsNotNull(child, "Missing child rect: " + childName);
            Assert.GreaterOrEqual(child.rect.width, minWidth, childName + " should be visually large enough.");
            Assert.GreaterOrEqual(child.rect.height, minHeight, childName + " should be visually large enough.");
        }

        private static Rect ToScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static int CountButtonsWithPrefix(string prefix)
        {
            var count = 0;
            var buttons = Object.FindObjectsOfType<Button>();
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void ClickButton(string buttonName)
        {
            var button = FindButton(buttonName);
            Assert.IsNotNull(button, "Could not find button: " + buttonName);
            Assert.IsTrue(button.interactable, "Button should be interactable before click: " + buttonName);
            button.onClick.Invoke();
        }

        private static void SubmitGuess(string guess)
        {
            for (var i = 0; i < guess.Length; i++)
            {
                ClickButton("DigitButton_" + guess[i]);
            }
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

        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(MiniGameSaveStore.PlayerPrefsKey);
            PlayerPrefs.DeleteKey(MiniGameRuntimeSettings.PlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }
}
